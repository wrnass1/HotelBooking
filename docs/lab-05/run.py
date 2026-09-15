"""Run the existing backend's sharding experiment and HTTP checks.

--prepare: stop API, import existing Bookings, seed 100000 domain records, start API.
Default: compare routers against stored IDs, verify backend queries, verify HTTP CRUD.
Requires Docker CLI; HTTP verification also requires psycopg 3 (bundled with pgAdmin).
"""
import argparse
import base64
import hashlib
import hmac
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import date, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESULTS = Path(__file__).resolve().parent / "results"


def command(args):
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", timeout=300)
    with (RESULTS / "backend-output.txt").open("a", encoding="utf-8") as output:
        output.write("$ " + " ".join(args) + "\n" + result.stdout + result.stderr + "\n")
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return result.stdout


def backend(action, compose=False):
    args = (["docker", "compose", "run", "--rm", "--no-deps", "api"] if compose
            else ["docker", "exec", "hotelbooking-api", "dotnet", "HotelBooking.dll"])
    output = command(args + ["--lab05", action])
    record = json.loads(next(line.removeprefix("LAB05_RESULT:") for line in output.splitlines()
                             if line.startswith("LAB05_RESULT:")))
    (RESULTS / (action + ".json")).write_text(json.dumps(record, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(record, ensure_ascii=False), flush=True)
    return record


def http_verify():
    runtime = Path(sys.executable).resolve().parent.parent / "runtime"
    dll = os.add_dll_directory(str(runtime)) if os.name == "nt" and (runtime / "libpq.dll").exists() else None
    import psycopg
    from psycopg.rows import dict_row

    # A short-lived test JWT signed with the project's local development settings.
    # It is never written to results or printed. Existing authorization remains enabled.
    settings = json.loads((ROOT / "HotelBooking/appsettings.json").read_text(encoding="utf-8"))["JwtSettings"]
    encode = lambda data: base64.urlsafe_b64encode(data).rstrip(b"=")
    head = encode(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    body = encode(json.dumps({"sub": "lab05-test", "role": "Admin", "iss": settings["Issuer"],
                              "aud": settings["Audience"], "exp": int(time.time()) + 300}).encode())
    signature = encode(hmac.new(settings["SecretKey"].encode(), head + b"." + body, hashlib.sha256).digest())
    token = b".".join([head, body, signature]).decode()
    events = []

    def request(method, path, body=None, expected=200):
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request("http://127.0.0.1:8080" + path, data=data, method=method,
                                     headers={"Content-Type": "application/json", "Authorization": "Bearer " + token})
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                status = response.status
                raw = response.read()
                result = json.loads(raw) if raw else None
        except urllib.error.HTTPError as error:
            status, result = error.code, error.read().decode()
        if status != expected:
            raise AssertionError(f"{method} {path}: expected {expected}, got {status}: {result}")
        events.append({"method": method, "path": path, "status": status, "body": result})
        return result

    primary = psycopg.connect("host=127.0.0.1 port=5432 dbname=hotelbooking user=hotelbooking_user password=hotelbooking_password",
                              autocommit=True, row_factory=dict_row)
    shards = [psycopg.connect(f"host=127.0.0.1 port={5434+i} dbname=hotelbooking_shard user=hotelbooking_shard_user password=hotelbooking_shard_password",
                             autocommit=True, row_factory=dict_row) for i in range(3)]
    created = []
    placements = []
    try:
        room = primary.execute('''SELECT r."Id", r."HotelId" FROM public."Rooms" r
            JOIN public."Hotels" h ON h."Id"=r."HotelId"
            WHERE h."Name"='Lab05 Sharding Hotel' AND h."City"='Lab05' ORDER BY r."Id" LIMIT 1''').fetchone()
        assert room, "Run --prepare first"
        covered = set()
        for i in range(30):
            check_in = date(2050, 1, 1) + timedelta(days=i * 2)
            booking = request("POST", "/api/Bookings", {
                "roomId": room["Id"], "guestName": "Lab05 HTTP fixture", "guestEmail": "lab05.http@example.test",
                "checkInDate": check_in.isoformat(), "checkOutDate": (check_in + timedelta(days=1)).isoformat(),
                "numberOfGuests": 1}, expected=201)
            booking_id = booking["id"]
            created.append(booking_id)
            expected_shard = int.from_bytes(hashlib.sha256(str(booking_id).encode()).digest()[:8], "big") % 3
            found = [i for i, db in enumerate(shards) if db.execute(
                'SELECT "Id" FROM public."Bookings" WHERE "Id"=%s', (booking_id,)).fetchone()]
            assert found == [expected_shard], (booking_id, expected_shard, found)
            assert not primary.execute('SELECT "Id" FROM public."Bookings" WHERE "Id"=%s', (booking_id,)).fetchone()
            fetched = request("GET", f"/api/Bookings/{booking_id}")
            assert fetched["id"] == booking_id and fetched["hotelName"] == "Lab05 Sharding Hotel"
            placements.append({"id": booking_id, "expectedShard": expected_shard, "foundOnShards": found, "onPrimary": False})
            covered.add(expected_shard)
            if len(covered) == 3:
                break
        assert len(covered) == 3
        first = created[0]
        updated = request("PUT", f"/api/Bookings/{first}", {"guestName": "Lab05 HTTP updated"})
        assert updated["guestName"] == "Lab05 HTTP updated"
        assert request("GET", f"/api/Bookings/{first}")["guestName"] == "Lab05 HTTP updated"
        by_email = request("GET", "/api/Bookings/guest/lab05.http%40example.test")
        assert set(b["id"] for b in by_email) == set(created)
        occupied = request("GET", f'/api/Rooms/available?hotelId={room["HotelId"]}&checkIn=2050-01-01&checkOut=2050-01-02')
        assert room["Id"] not in [r["id"] for r in occupied]
        request("POST", f"/api/Bookings/{first}/cancel")
        assert request("GET", f"/api/Bookings/{first}")["status"] == "Cancelled"
    finally:
        try:
            for booking_id in created:
                request("DELETE", f"/api/Bookings/{booking_id}", expected=204)
                request("GET", f"/api/Bookings/{booking_id}", expected=404)
        finally:
            for db in shards:
                db.close()
            primary.close()
    health = request("GET", "/health")
    assert health["status"] == "Healthy"
    result = {"passed": True, "placements": placements, "events": events, "fixturesDeleted": created}
    (RESULTS / "http-verification.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"httpPassed": True, "coveredShards": sorted(covered), "requests": len(events), "fixturesDeleted": created}), flush=True)


def inspect_cluster():
    sql = '''SELECT json_build_object(
        'database', current_database(), 'version', version(),
        'systemIdentifier', (SELECT system_identifier::text FROM pg_control_system()),
        'inRecovery', pg_is_in_recovery(), 'readOnly', current_setting('transaction_read_only'),
        'layout', (SELECT fingerprint FROM public.sharding_layout),
        'datasetRows', (SELECT count(*) FROM public."Bookings" WHERE "GuestEmail" LIKE 'lab05.booking.%@example.test'),
        'totalRows', (SELECT count(*) FROM public."Bookings"))'''
    nodes = []
    for i in range(3):
        node = json.loads(command(["docker", "exec", f"hotelbooking-shard-{i}", "psql", "-XAt",
            "-v", "ON_ERROR_STOP=1", "-U", "hotelbooking_shard_user", "-d", "hotelbooking_shard", "-c", sql]))
        node["shard"] = i
        assert node["inRecovery"] is False and node["readOnly"] == "off"
        nodes.append(node)
    assert len({node["systemIdentifier"] for node in nodes}) == 3
    assert sum(node["datasetRows"] for node in nodes) == 100000
    with urllib.request.urlopen("http://127.0.0.1:8080/health", timeout=20) as response:
        health = json.load(response)
    assert health["status"] == "Healthy"
    result = {"capturedUnixTime": time.time(), "passed": True, "nodes": nodes, "apiHealth": health}
    (RESULTS / "cluster-state.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare", action="store_true")
    parser.add_argument("--inspect", action="store_true", help="Read current node identities, counts and API health")
    args = parser.parse_args()
    RESULTS.mkdir(parents=True, exist_ok=True)
    if args.inspect:
        inspect_cluster()
    elif args.prepare:
        command(["docker", "compose", "stop", "api"])
        backend("import", compose=True)
        backend("seed", compose=True)
        command(["docker", "compose", "up", "-d", "--no-deps", "api"])
        print("Prepared. Run this script without --prepare to verify.")
    else:
        backend("compare")
        backend("verify")
        http_verify()
        inspect_cluster()


if __name__ == "__main__":
    main()
