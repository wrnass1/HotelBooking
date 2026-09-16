"""Lab 06: read-only experiments on the existing Lab 05 dataset.

Run from any directory with Python + psycopg 3 and the HotelBooking stack running.
--with-failure also briefly stops shard 2, then starts it in a finally block.
No schema changes, seeding, or booking writes. Results go to lab-06/results.
"""
import argparse
import base64
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
from datetime import date, datetime, timezone
from decimal import Decimal
import hashlib
import hmac
import json
import os
from pathlib import Path
import random
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
RESULTS = Path(__file__).resolve().parent / "results"
runtime = Path(sys.executable).resolve().parent.parent / "runtime"
DLL = os.add_dll_directory(str(runtime)) if os.name == "nt" and (runtime / "libpq.dll").exists() else None
import psycopg
from psycopg.rows import dict_row

SETTINGS = json.loads((ROOT / "HotelBooking/appsettings.json").read_text(encoding="utf-8"))
SHARDS = sorted(SETTINGS["Sharding"]["Connections"])
PREDICATE = '''"RoomId" = ANY(%s) AND "CheckInDate" >= %s AND "CheckInDate" <= %s'''
PROJECTION = '''"Id", "RoomId", "TotalPrice", "Status", "CheckInDate"'''
GROUP_SQL = f'''SELECT "Status", count(*) AS n, sum("TotalPrice") AS total,
    avg("TotalPrice") AS average FROM public."Bookings" WHERE {PREDICATE} GROUP BY "Status"'''
MONTH_SQL = f'''SELECT to_char("CheckInDate", 'YYYY-MM') AS month, sum("TotalPrice") AS total
    FROM public."Bookings" WHERE {PREDICATE} AND "Status" <> 'Cancelled' GROUP BY 1'''


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def json_value(value):
    if isinstance(value, Decimal):
        return str(value)  # Keep exact monetary values, including scale.
    if isinstance(value, (date, datetime)):
        return value.isoformat()
    raise TypeError(type(value).__name__)


def save(name, value):
    RESULTS.mkdir(parents=True, exist_ok=True)
    (RESULTS / name).write_text(json.dumps(value, ensure_ascii=False, indent=2, default=json_value) + "\n", encoding="utf-8")


def connect(shard=None):
    source = (SETTINGS["ConnectionStrings"]["DefaultConnection"] if shard is None
              else SETTINGS["Sharding"]["Connections"][shard])
    parts = dict(item.split("=", 1) for item in source.split(";") if item)
    return psycopg.connect(host=parts["Host"], port=parts.get("Port", "5432"),
                           dbname=parts["Database"], user=parts["Username"], password=parts["Password"],
                           connect_timeout=3, autocommit=True, row_factory=dict_row,
                           application_name="hotelbooking-lab06",
                           options="-c default_transaction_read_only=on -c statement_timeout=15000")


def query(shard, sql, params=()):
    with connect(shard) as db:
        return db.execute(sql, params).fetchall()


def fanout(sql, params=(), targets=None):
    targets = SHARDS if targets is None else targets
    with ThreadPoolExecutor(max_workers=len(targets)) as pool:
        return list(pool.map(lambda shard: query(shard, sql, params), targets))


def route(booking_id):
    return SHARDS[int.from_bytes(hashlib.sha256(str(booking_id).encode()).digest()[:8], "big") % len(SHARDS)]


def docker(*args):
    result = subprocess.run(["docker", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", timeout=90)
    if result.returncode:
        raise RuntimeError(result.stderr or result.stdout)
    return result.stdout


def token():
    settings = SETTINGS["JwtSettings"]
    encode = lambda value: base64.urlsafe_b64encode(json.dumps(value).encode()).rstrip(b"=")
    header = encode({"alg": "HS256", "typ": "JWT"})
    body = encode({"sub": "lab06-read-only", "role": "Admin", "iss": settings["Issuer"],
                   "aud": settings["Audience"], "exp": int(time.time()) + 600})
    signature = base64.urlsafe_b64encode(hmac.new(settings["SecretKey"].encode(), header + b"." + body, hashlib.sha256).digest()).rstrip(b"=")
    return b".".join([header, body, signature]).decode()


def http(path):
    request = urllib.request.Request("http://127.0.0.1:8080" + path,
                                     headers={"Authorization": "Bearer " + token()})
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            status, body = response.status, json.load(response)
    except urllib.error.HTTPError as error:
        status, body = error.code, json.loads(error.read())
    elapsed = round((time.perf_counter() - started) * 1000, 3)
    if isinstance(body, list):
        summary = {"rows": len(body)}
    elif isinstance(body, dict):
        summary = {key: body[key] for key in ("id", "hotelName", "total", "status", "checks", "error", "message") if key in body}
    else:
        summary = body
    return {"path": path, "status": status, "elapsedMs": elapsed, "summary": summary}


def benchmark(params):
    measurements = []
    for size in range(1, len(SHARDS) + 1):
        targets = SHARDS[:size]
        for mode in ("sequential", "parallel"):
            samples = []
            for repetition in range(6):
                started = time.perf_counter()
                batches = (fanout(GROUP_SQL, params, targets) if mode == "parallel"
                           else [query(shard, GROUP_SQL, params) for shard in targets])
                count = sum(row["n"] for batch in batches for row in batch)
                elapsed = (time.perf_counter() - started) * 1000
                if repetition:  # One warm-up per case; five recorded runs.
                    samples.append(round(elapsed, 3))
            measurements.append({"shards": size, "mode": mode, "rowsCovered": count,
                                 "samplesMs": samples, "medianMs": round(statistics.median(samples), 3)})
    return {"method": "Five warm runs; includes connect, SQL, transfer, Python reduction and thread pool creation. Same three local containers; subsets contain different amounts of data, not a resharding scalability benchmark.",
            "measurements": measurements}


def hot_shard(samples):
    schedule = [SHARDS[0]] * 100 + [SHARDS[1]] * 150 + [SHARDS[2]] * 750
    random.Random(6).shuffle(schedule)
    connections = {}
    elapsed = defaultdict(float)
    try:
        for shard in SHARDS:
            connections[shard] = connect(shard)
        for shard in schedule:
            started = time.perf_counter()
            row = connections[shard].execute('SELECT "Id" FROM public."Bookings" WHERE "Id"=%s', (samples[shard]["Id"],)).fetchone()
            check(row is not None, "Hot-shard point read failed")
            elapsed[shard] += (time.perf_counter() - started) * 1000
    finally:
        for db in connections.values():
            db.close()
    return {"method": "1000 real sequential point reads, persistent connections, deterministic synthetic 10/15/75 workload; not production traffic or a saturation test.",
            "requests": dict(Counter(schedule)), "sqlElapsedMs": {s: round(t, 3) for s, t in elapsed.items()}}


def failure(samples, room_id, hotel_id, counts):
    container = "hotelbooking-shard-2"
    check(SHARDS == ["0", "1", "2"], "Failure demo expects the original three shards")
    check(docker("inspect", "--format", "{{.State.Running}}", container).strip() == "true", "Shard 2 must already be running")
    paths = [f'/api/Bookings/{samples[s]["Id"]}' for s in SHARDS] + [
        "/api/Bookings", f"/api/Bookings/room/{room_id}",
        "/api/Bookings/guest/lab05.booking.000001%40example.test",
        f"/api/Rooms/available?hotelId={hotel_id}&checkIn=2030-01-01&checkOut=2030-01-02",
        "/api/Hotels?page=1&pageSize=2", "/health"]
    result = {"utc": datetime.now(timezone.utc), "container": container,
              "before": [http(path) for path in paths], "during": [], "after": []}
    check(all(event["status"] == 200 for event in result["before"]), "Baseline HTTP failed; shard was not stopped")
    try:
        docker("stop", "--time", "10", container)
        for path in paths:
            result["during"].append(http(path))
    finally:
        docker("start", container)
        deadline = time.monotonic() + 60
        while True:
            try:
                query("2", "SELECT 1")
                break
            except psycopg.OperationalError:
                if time.monotonic() >= deadline:
                    raise RuntimeError("Shard restart failed; run docker start hotelbooking-shard-2")
                time.sleep(1)
        result["containerRestored"] = True
        # Connections in the backend pool may need to be reopened after restart.
        for path in paths:
            for attempt in range(3):
                event = http(path)
                if event["status"] == 200:
                    break
                time.sleep(1)
            result["after"].append(event)
        result["countsAfter"] = {s: query(s, 'SELECT count(*) AS n FROM public."Bookings"')[0]["n"] for s in SHARDS}
        save("failure.json", result)
    check([e["status"] for e in result["during"]] == [200, 200, 500, 500, 500, 500, 500, 200, 200], "Outage behavior differs from the current implementation; inspect failure.json")
    check(all(e["status"] == 200 for e in result["after"]), "HTTP did not recover")
    check(result["countsAfter"] == counts, "Row counts changed during the failure experiment")
    result["passed"] = True
    save("failure.json", result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--with-failure", action="store_true", help="Briefly stop shard 2; restore it in finally")
    args = parser.parse_args()
    check(SHARDS == ["0", "1", "2"] and SETTINGS["Sharding"]["Strategy"] == "Modulo", "Expected the lab05 modulo layout")
    report = {"utc": datetime.now(timezone.utc), "dataset": "Existing lab05 hotel; no writes or seeding"}
    hotel = query(None, '''SELECT "Id" FROM public."Hotels" WHERE "Name"='Lab05 Sharding Hotel' AND "City"='Lab05' ''')
    check(len(hotel) == 1, "Prepare the lab05 dataset first")
    hotel_id = hotel[0]["Id"]
    rooms = query(None, '''SELECT r."Id", r."RoomNumber", h."Name" AS hotel_name FROM public."Rooms" r
                           JOIN public."Hotels" h ON r."HotelId"=h."Id" WHERE h."Id"=%s ORDER BY r."Id"''', (hotel_id,))
    room_ids = [room["Id"] for room in rooms]
    params = (room_ids, date(2030, 1, 1), date(2040, 1, 1))
    layout = "sha256-u64be-v1|Modulo|1|shard-0,shard-1,shard-2"
    check(all(query(s, "SELECT fingerprint FROM public.sharding_layout WHERE singleton")[0]["fingerprint"] == layout for s in SHARDS), "Persisted router layout differs")
    rows_by_shard = fanout(f'SELECT {PROJECTION} FROM public."Bookings" WHERE {PREDICATE}', params)
    all_rows = [row for batch in rows_by_shard for row in batch]
    check(len(all_rows) == 100000 and len({r["Id"] for r in all_rows}) == 100000, "Expected 100000 unique lab05 bookings")
    check(all(route(row["Id"]) == shard for shard, batch in zip(SHARDS, rows_by_shard) for row in batch), "Stored rows violate routing")
    counts = {s: query(s, 'SELECT count(*) AS n FROM public."Bookings"')[0]["n"] for s in SHARDS}
    samples = {s: min(batch, key=lambda r: r["Id"]) for s, batch in zip(SHARDS, rows_by_shard)}
    report["layout"] = {"hotelId": hotel_id, "rooms": len(rooms), "fingerprint": layout, "allCounts": counts,
                        "selectedCounts": dict(zip(SHARDS, map(len, rows_by_shard)))}
    point = samples["2"]
    plan = query("2", 'EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT * FROM public."Bookings" WHERE "Id"=%s', (point["Id"],))
    report["singleShard"] = {"id": point["Id"], "routedShard": route(point["Id"]), "http": http(f'/api/Bookings/{point["Id"]}'), "plan": plan}
    check(report["singleShard"]["http"]["status"] == 200, "Point HTTP read failed")
    groups = fanout(GROUP_SQL, params)
    merged = defaultdict(lambda: {"count": 0, "total": Decimal(0)})
    for batch in groups:
        for row in batch:
            merged[row["Status"]]["count"] += row["n"]
            merged[row["Status"]]["total"] += row["total"]
    months = defaultdict(Decimal)
    for batch in fanout(MONTH_SQL, params):
        for row in batch:
            months[row["month"]] += row["total"]
    total = sum(value["total"] for value in merged.values())
    count = sum(value["count"] for value in merged.values())
    check(count == len(all_rows) and total == sum(row["TotalPrice"] for row in all_rows), "Aggregation differs from full-row reference")
    check({key: value["count"] for key, value in merged.items()} == dict(Counter(r["Status"] for r in all_rows)), "Status grouping mismatch")
    reference_months = defaultdict(Decimal)
    for row in all_rows:
        if row["Status"] != "Cancelled":
            reference_months[row["CheckInDate"].strftime("%Y-%m")] += row["TotalPrice"]
    check(months == reference_months, "Monthly revenue mismatch")
    report["aggregation"] = {"perShard": dict(zip(SHARDS, groups)), "merged": dict(merged),
                             "count": count, "sum": total, "average": total / count,
                             "revenueByMonth": dict(sorted(months.items())),
                             "transferredGroupRows": sum(map(len, groups)), "fullRowReferenceCount": len(all_rows),
                             "averageCounterexample": {"shardCounts": [1, 3], "shardSums": [10, 90], "wrongMeanOfMeans": 20, "correctWeightedMean": 25}}
    room_lookup = {room["Id"]: room for room in rooms}
    check(all(row["RoomId"] in room_lookup for row in all_rows), "Booking has no room on Primary")
    try:
        query("2", '''SELECT b."Id", r."RoomNumber" FROM public."Bookings" b JOIN public."Rooms" r ON r."Id"=b."RoomId" LIMIT 1''')
    except psycopg.errors.UndefinedTable as error:
        join_error = {"sqlstate": error.sqlstate, "message": error.diag.message_primary}
    else:
        raise AssertionError("Rooms unexpectedly exists on shard 2; review JOIN analysis")
    report["join"] = {"localJoinError": join_error, "bookings": len(all_rows), "distinctRooms": len(room_lookup),
                      "example": {"booking": point, "roomOnPrimary": room_lookup[point["RoomId"]]},
                      "primaryBookingsCount": query(None, 'SELECT count(*) AS n FROM public."Bookings"')[0]["n"]}
    top_batches = fanout(f'SELECT {PROJECTION} FROM public."Bookings" WHERE {PREDICATE} ORDER BY "CheckInDate" DESC, "Id" DESC LIMIT 100', params)
    order = lambda row: (row["CheckInDate"], row["Id"])
    merged_top = sorted([r for batch in top_batches for r in batch], key=order, reverse=True)[:100]
    expected_top = sorted(all_rows, key=order, reverse=True)[:100]
    check(merged_top == expected_top, "Distributed top 100 differs from full-row reference")
    expected_ids = {r["Id"] for r in expected_top}
    report["top100"] = {"order": "CheckInDate DESC, Id DESC", "candidates": sum(map(len, top_batches)),
                        "mergedIds": [r["Id"] for r in merged_top],
                        "contribution": dict(Counter(route(r["Id"]) for r in merged_top)),
                        "missingIfOnlyOneShard": {s: len(expected_ids - {r["Id"] for r in batch}) for s, batch in zip(SHARDS, top_batches)},
                        "matchesFullSort": True}
    report["timing"] = benchmark(params)
    report["hotShard"] = hot_shard(samples)
    report["passed"] = True
    save("queries.json", report)
    print(json.dumps({"queriesPassed": True, "counts": counts, "aggregateRows": report["aggregation"]["transferredGroupRows"], "top100": report["top100"]["contribution"]}), flush=True)
    if args.with_failure:
        result = failure(samples, room_ids[0], hotel_id, counts)
        print(json.dumps({"failurePassed": result["passed"], "containerRestored": result["containerRestored"]}), flush=True)


if __name__ == "__main__":
    main()
