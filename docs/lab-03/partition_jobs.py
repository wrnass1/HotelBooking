"""ЛР №3: pg_cron, HTTP-доставка и демонстрация сбоя.

Python 3.10+, только стандартная библиотека; SQL выполняется psql в Docker.
Расписание создания и проверок выполняет pg_cron (миграция 015).
Python обслуживает HTTP-доставку и демонстрацию. Даты используют UTC.
"""

import argparse
from datetime import date, datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import time
from urllib.request import Request, urlopen


def log(event, **fields):
    print(json.dumps({"at": datetime.now(timezone.utc).isoformat(), "event": event, **fields},
                     ensure_ascii=False), flush=True)


class Database:
    def __init__(self, args):
        self.command = ["docker", "exec", "-i", args.container, "psql", "-X", "-qAt",
                        "-v", "ON_ERROR_STOP=1", "-U", args.user, "-d", args.database]

    def sql(self, statement):
        result = subprocess.run(
            self.command,
            input="SET timezone='UTC'; SET datestyle='ISO, YMD'; "
                  "SET lock_timeout='5s'; SET statement_timeout='30s';\n" + statement,
            text=True, encoding="utf-8", capture_output=True, timeout=40, check=False)
        if result.returncode:
            raise RuntimeError(result.stderr.strip() or "psql failed")
        for line in result.stderr.splitlines():
            log("database_notice", message=line)
        return result.stdout.strip()

    def call(self, function, target, today):
        # function/target — внутренние константы; today — разобранный datetime.date.
        return json.loads(self.sql(
            f"SELECT lab03.{function}('{target}', DATE '{today.isoformat()}', 3);"))


def create(db, targets, today):
    for target in targets:
        log("CreatePartitionsJob", **db.call("create_partitions_job", target, today))


def dispatch(db, webhook, after_id=0):
    pending = json.loads(db.sql(
        "SELECT coalesce(jsonb_agg(jsonb_build_object('event_id', id, 'payload', payload) "
        "ORDER BY id), '[]'::jsonb) FROM lab03.partition_alert_outbox "
        f"WHERE delivered_at IS NULL AND id > {int(after_id)};"))
    if pending and not webhook:
        raise RuntimeError("Alerts are queued. Set LAB03_ALERT_WEBHOOK or --webhook to deliver them.")
    # Один dispatcher; доставка at-least-once. Получатель дедуплицирует по event_id.
    for event in pending:
        request = Request(webhook, json.dumps(event).encode("utf-8"),
                          {"Content-Type": "application/json", "Idempotency-Key": str(event["event_id"])},
                          method="POST")
        with urlopen(request, timeout=10) as response:
            if not 200 <= response.status < 300:
                raise RuntimeError(f"Alert endpoint returned HTTP {response.status}")
        db.sql("UPDATE lab03.partition_alert_outbox SET delivered_at=clock_timestamp() "
               f"WHERE id={int(event['event_id'])} AND delivered_at IS NULL;")
        log("alert_delivered", **event)


def check(db, targets, today, webhook, after_id=0):
    results = []
    for target in targets:
        result = db.call("partition_health_check", target, today)
        results.append(result)
        log("PartitionHealthCheck", **result)
    dispatch(db, webhook, after_id)
    return all(result["status"] == "OK" for result in results)


def receiver(port, output=None, failures=0):
    received = []
    seen = set()
    remaining_failures = [failures]
    sink_lock = threading.Lock()
    if output and output.exists():
        for line in output.read_text(encoding="utf-8").splitlines():
            seen.add(json.loads(line)["event_id"])

    class Handler(BaseHTTPRequestHandler):
        def do_POST(self):
            if self.path != "/alerts":
                self.send_error(404)
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= 65536:
                    raise ValueError("Invalid body length")
                event = json.loads(self.rfile.read(length))
                event_id = int(event["event_id"])
                with sink_lock:
                    if remaining_failures[0]:
                        remaining_failures[0] -= 1
                        self.send_error(503, "Injected delivery failure")
                        return
                    if event_id not in seen:
                        if output:
                            with output.open("a", encoding="utf-8") as stream:
                                stream.write(json.dumps(event, ensure_ascii=False) + "\n")
                                stream.flush()
                                os.fsync(stream.fileno())
                        received.append(event)
                        seen.add(event_id)
                        log("alert_received", **event)
                self.send_response(204)
                self.end_headers()
            except (ValueError, KeyError, TypeError):
                self.send_error(400)

        def log_message(self, *_args):
            pass

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    return server, received


def run_dispatch(db, args):
    if not args.webhook:
        raise ValueError("run-dispatch requires --webhook or LAB03_ALERT_WEBHOOK")
    while True:
        try:
            dispatch(db, args.webhook)
        except Exception as exc:
            log("delivery_failed", error=str(exc))
        time.sleep(args.interval)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["create", "check", "dispatch", "run-dispatch", "demo", "serve"])
    parser.add_argument("--container", default="hotelbooking-postgres")
    parser.add_argument("--database", default="hotelbooking")
    parser.add_argument("--user", default="hotelbooking_user")
    parser.add_argument("--target", choices=["all", "events", "Bookings"], default="all")
    parser.add_argument("--today", type=date.fromisoformat)
    parser.add_argument("--webhook", default=os.environ.get("LAB03_ALERT_WEBHOOK"))
    parser.add_argument("--interval", type=int, default=5)
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--output", type=Path, help="JSONL receipt log for the local serve command")
    args = parser.parse_args()
    if args.interval < 1:
        parser.error("--interval must be positive")
    targets = ["events", "Bookings"] if args.target == "all" else [args.target]
    today = args.today or datetime.now(timezone.utc).date()
    if args.mode == "serve":
        server, _ = receiver(args.port, args.output)
        log("receiver_started", url=f"http://127.0.0.1:{server.server_port}/alerts")
        try:
            server.serve_forever()
        finally:
            server.server_close()
        return 0
    db = Database(args)
    if args.mode == "demo":
        if args.today:
            parser.error("pg_cron demo uses the real UTC date; omit --today")
        # Embedded Python from pgAdmin omits the script directory from sys.path.
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        sys.modules.setdefault("partition_jobs", sys.modules[__name__])
        from cron_demo import run_demo
        run_demo(db, today)
    elif args.mode == "create":
        create(db, targets, today)
    elif args.mode == "check":
        return 0 if check(db, targets, today, args.webhook) else 2
    elif args.mode == "dispatch":
        dispatch(db, args.webhook)
    else:
        run_dispatch(db, args)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(130)
    except Exception as error:
        log("failed", error=str(error))
        sys.exit(1)
