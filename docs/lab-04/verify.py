"""LR4 integration experiment. Python 3.10+ and psycopg 3 are required.

Run from the repository root after `docker compose up -d --build`.
Only the hotel created by this run is changed; WAL replay is resumed in finally.
"""

import argparse
import json
import os
import sys
import time
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

# pgAdmin bundles psycopg but keeps libpq next to its runtime, outside Python's DLL path.
_pgadmin_runtime = Path(sys.executable).resolve().parent.parent / "runtime"
_dll_directory = (os.add_dll_directory(str(_pgadmin_runtime))
                  if sys.platform == "win32" and (_pgadmin_runtime / "libpq.dll").exists()
                  else None)

import psycopg
from psycopg.rows import dict_row


def wait_for(check, description, timeout=30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        result = check()
        if result:
            return result
        time.sleep(0.1)
    raise TimeoutError(description)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).parent / "results" / "verification.jsonl")
    parser.add_argument("--api", default="http://localhost:8080")
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    primary_dsn = os.environ.get("LAB04_PRIMARY_DSN",
        "host=localhost port=5432 dbname=hotelbooking user=hotelbooking_user password=hotelbooking_password")
    replica_dsn = os.environ.get("LAB04_REPLICA_DSN",
        "host=localhost port=5433 dbname=hotelbooking user=hotelbooking_user password=hotelbooking_password")

    with args.output.open("w", encoding="utf-8") as output, \
            psycopg.connect(primary_dsn, autocommit=True, row_factory=dict_row,
                            connect_timeout=10, application_name="lab04-verification-primary") as primary, \
            psycopg.connect(replica_dsn, autocommit=True, row_factory=dict_row,
                            connect_timeout=10, application_name="lab04-verification-replica") as replica:

        def emit(step, **data):
            line = json.dumps({"time_utc": datetime.now(timezone.utc).isoformat(),
                               "step": step, **data}, ensure_ascii=False, default=str)
            print(line, flush=True)
            output.write(line + "\n")
            output.flush()

        def rows(connection, sql, params=None):
            return connection.execute(sql, params).fetchall()

        def caught_up():
            lsn = primary.execute("SELECT pg_current_wal_flush_lsn() AS lsn").fetchone()["lsn"]
            wait_for(lambda: replica.execute(
                "SELECT pg_last_wal_replay_lsn() >= %s::pg_lsn AS ready", (lsn,)
            ).fetchone()["ready"], "Replica did not replay the committed WAL")
            return lsn

        role_sql = """SELECT version(), pg_is_in_recovery() AS in_recovery,
            current_setting('transaction_read_only') AS read_only,
            inet_server_addr() AS server_address, inet_server_port() AS server_port,
            current_user"""
        p_role = rows(primary, role_sql)[0]
        r_role = rows(replica, role_sql)[0]
        assert p_role["in_recovery"] is False and r_role["in_recovery"] is True
        assert r_role["read_only"] == "on"
        assert replica.execute("SELECT pg_get_wal_replay_pause_state() AS state").fetchone()["state"] == "not paused", \
            "Replica was already paused; resume it before running the experiment"
        emit("1.roles", primary=p_role, replica=r_role)

        streaming = wait_for(lambda: rows(primary,
            "SELECT * FROM pg_stat_replication WHERE application_name = 'hotelbooking-replica' AND state = 'streaming'"),
            "Streaming replication is not connected")
        emit("2.streaming", primary_pg_stat_replication=streaming,
             replica_wal_receiver=rows(replica,
                 "SELECT status, slot_name, sender_host, sender_port, written_lsn, flushed_lsn FROM pg_stat_wal_receiver"),
             settings=rows(primary,
                 "SELECT name, setting, unit FROM pg_settings WHERE name IN ('wal_level', 'max_wal_senders',"
                 " 'max_replication_slots', 'wal_keep_size', 'max_slot_wal_keep_size', 'synchronous_standby_names') ORDER BY name"))

        marker = "Lab04-" + uuid.uuid4().hex[:12]
        hotel_id = None
        pause_requested = False
        select_sql = 'SELECT "Id", "Name", "City" FROM public."Hotels" WHERE "Id" = %s'

        def catalog(page_size):
            # Each call uses a fresh Redis key. A cached response must not mask WAL lag.
            query = urllib.parse.urlencode({"City": marker, "Page": 1, "PageSize": page_size})
            url = args.api.rstrip("/") + "/api/Hotels?" + query
            with urllib.request.urlopen(url, timeout=20) as response:
                result = json.load(response)
                assert response.status == 200
            assert result["total"] == 1 and len(result["items"]) == 1, result
            assert result["items"][0]["id"] == hotel_id, result
            return {"url": url, "status": 200, "body": result}

        try:
            insert_sql = '''INSERT INTO public."Hotels"
                ("Name", "Address", "City", "Country", "Description", "StarRating")
                VALUES (%s, 'Laboratory address', %s, 'Lab04', 'Read scaling fixture', 3)
                RETURNING "Id", "Name", "City"'''
            inserted = primary.execute(insert_sql, (marker + " v1", marker)).fetchone()
            hotel_id = inserted["Id"]
            started = time.perf_counter()
            immediate = rows(replica, select_sql, (hotel_id,))
            elapsed = (time.perf_counter() - started) * 1000
            emit("3.insert_primary_and_immediate_select", sql=insert_sql,
                 parameters=[marker + " v1", marker], primary_returning=inserted,
                 replica_rows=immediate, select_elapsed_ms=round(elapsed, 3))
            replayed_lsn = caught_up()
            replicated = rows(replica, select_sql, (hotel_id,))
            assert replicated == [inserted]
            emit("3.replication_proof", sql=select_sql, parameters=[hotel_id],
                 replica_rows=replicated, replayed_through_lsn=replayed_lsn)

            rejected_sql = 'UPDATE public."Hotels" SET "Name" = \'Replica write attempt\' WHERE "Id" = %s'
            try:
                replica.execute(rejected_sql, (hotel_id,))
            except psycopg.Error as error:
                assert error.sqlstate == "25006", error
                emit("4.replica_write_rejected", sql=rejected_sql, parameters=[hotel_id],
                     sqlstate=error.sqlstate, message=error.diag.message_primary)
            else:
                raise AssertionError("Replica accepted UPDATE")

            initial_http = catalog(21)
            assert initial_http["body"]["items"][0]["name"] == marker + " v1"
            sessions_sql = """SELECT datname, usename, application_name, client_addr, state, query
                FROM pg_stat_activity WHERE application_name = 'hotelbooking-catalog-replica'"""
            replica_sessions = rows(replica, sessions_sql)
            primary_sessions = rows(primary, sessions_sql)
            assert replica_sessions and not primary_sessions
            assert all(s["usename"] == "hotelbooking_reader" for s in replica_sessions)
            emit("5.backend_reads_replica", http=initial_http,
                 replica_sessions=replica_sessions, primary_sessions=primary_sessions)

            pause_requested = True
            replica.execute("SELECT pg_wal_replay_pause()")
            wait_for(lambda: replica.execute(
                "SELECT pg_get_wal_replay_pause_state() = 'paused' AS paused"
            ).fetchone()["paused"], "WAL replay did not pause")
            update_sql = 'UPDATE public."Hotels" SET "Name" = %s WHERE "Id" = %s RETURNING "Id", "Name", "City"'
            updated = primary.execute(update_sql, (marker + " v2", hotel_id)).fetchone()
            stale = rows(replica, select_sql, (hotel_id,))
            assert stale[0]["Name"] == marker + " v1"
            delayed_http = catalog(22)
            assert delayed_http["body"]["items"][0]["name"] == marker + " v1"
            emit("6.controlled_lag", note="WAL replay deliberately paused; this is not a natural lag measurement",
                 sql=update_sql, parameters=[marker + " v2", hotel_id],
                 primary_returning=updated, replica_rows=stale, http=delayed_http,
                 replica_lsn=rows(replica, """SELECT pg_last_wal_receive_lsn() AS receive_lsn,
                     pg_last_wal_replay_lsn() AS replay_lsn,
                     pg_wal_lsn_diff(pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn()) AS received_not_replayed_bytes,
                     pg_get_wal_replay_pause_state() AS pause_state"""))

            replica.execute("SELECT pg_wal_replay_resume()")
            pause_requested = False
            replayed_lsn = caught_up()
            fresh = rows(replica, select_sql, (hotel_id,))
            assert fresh == [updated]
            recovered_http = catalog(23)
            assert recovered_http["body"]["items"][0]["name"] == marker + " v2"
            emit("6.recovered", replica_rows=fresh, http=recovered_http,
                 replayed_through_lsn=replayed_lsn)
        finally:
            if pause_requested:
                replica.execute("SELECT pg_wal_replay_resume()")
                emit("cleanup.replay_resumed")
            if hotel_id is not None:
                # Only this run's fixture; sequence values are intentionally not reset.
                primary.execute('DELETE FROM public."Hotels" WHERE "Id" = %s AND "City" = %s',
                                (hotel_id, marker))
                caught_up()
                assert not rows(replica, select_sql, (hotel_id,))
                emit("cleanup.fixture_deleted", hotel_id=hotel_id)

        with urllib.request.urlopen(args.api.rstrip("/") + "/health", timeout=20) as response:
            health = json.load(response)
        assert health["status"] == "Healthy", health
        emit("verification_passed", health=health,
             replay_state=rows(replica, "SELECT pg_get_wal_replay_pause_state() AS state"),
             replication=rows(primary, "SELECT application_name, state, sync_state FROM pg_stat_replication"))


if __name__ == "__main__":
    main()
