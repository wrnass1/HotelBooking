"""Проверка реальных фоновых запусков pg_cron; запуск через partition_jobs.py demo."""

import json
import threading
import time

from partition_jobs import create, dispatch, log, receiver


def literal(value):
    return "'" + value.replace("'", "''") + "'"


def wait_for(db, query, description, timeout=40):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        result = json.loads(db.sql(query))
        if result:
            return result
        time.sleep(0.5)
    raise TimeoutError(f"pg_cron: timed out waiting for {description}")


def run_demo(db, today):
    jobs = json.loads(db.sql("""
        SELECT coalesce(jsonb_agg(to_jsonb(j) ORDER BY jobid), '[]'::jsonb)
        FROM (SELECT jobid, jobname, schedule, active, command FROM cron.job
              WHERE jobname IN ('lab03-create-events', 'lab03-create-bookings',
                                'lab03-check-events', 'lab03-check-bookings')) j;
    """))
    if len(jobs) != 4:
        raise RuntimeError("Apply migration 015 first: four pg_cron jobs are required")
    job_ids = ",".join(str(int(job["jobid"])) for job in jobs)
    by_name = {job["jobname"]: job["jobid"] for job in jobs}
    run_start = int(db.sql("SELECT coalesce(max(runid), 0) FROM cron.job_run_details;"))

    def alter(job_id, schedule=None, active=True, command=None):
        options = [f"active := {'true' if active else 'false'}"]
        if schedule is not None:
            options.append("schedule := " + literal(schedule))
        if command is not None:
            options.append("command := " + literal(command))
        db.sql(f"SELECT cron.alter_job({int(job_id)}, {', '.join(options)});")

    def pause_all():
        for job in jobs:
            alter(job["jobid"], active=False)
        wait_for(db, "SELECT to_jsonb(NOT EXISTS (SELECT 1 FROM cron.job_run_details "
                 f"WHERE jobid IN ({job_ids}) AND status IN ('starting','running')));", "jobs to stop")

    def wait_result(task, target, after, predicate):
        return wait_for(db, "SELECT coalesce((SELECT to_jsonb(r) FROM lab03.partition_job_runs r "
                        f"WHERE id > {int(after)} AND task={literal(task)} AND target={literal(target)} "
                        f"AND {predicate} ORDER BY id LIMIT 1), 'null'::jsonb);", f"{target}: {task}")

    server, received = receiver(0, failures=1)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    webhook = f"http://127.0.0.1:{server.server_port}/alerts"
    log("cron_demo_started", reference_date=today.isoformat(), jobs=jobs, webhook=webhook)
    try:
        pause_all()
        create(db, ["events", "Bookings"], today)
        for target in ("events", "Bookings"):
            db.call("partition_health_check", target, today)
        for target in ("events", "Bookings"):
            create_id = by_name["lab03-create-" + target.lower()]
            check_id = by_name["lab03-check-" + target.lower()]
            start_id = int(db.sql("SELECT coalesce(max(id), 0) FROM lab03.partition_alert_outbox;"))
            task_start = int(db.sql("SELECT coalesce(max(id), 0) FROM lab03.partition_job_runs;"))
            part = json.loads(db.sql("SELECT row_to_json(p) FROM (SELECT * FROM "
                                    f"lab03.expected_partitions({literal(target)}, DATE '{today}', 3) "
                                    "ORDER BY range_start DESC LIMIT 1) p;"))
            name = part["partition_name"]
            if not all(char.isalnum() or char == "_" for char in name):
                raise RuntimeError("Unexpected partition name")
            db.sql(f"""BEGIN;
                LOCK TABLE lab03."{name}" IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM lab03."{name}" LIMIT 1) THEN
                        RAISE EXCEPTION 'Demo refuses to remove a nonempty partition';
                    END IF;
                END $$;
                DROP TABLE lab03."{name}";
                COMMIT;""")
            log("failure_injected", target=target, missing_partition=name, creation_job_active=False)
            alter(check_id, schedule="2 seconds")
            critical = wait_result("check", target, task_start, "result->>'status'='CRITICAL'")
            log("cron_detected_failure", **critical)
            repeated = wait_result("check", target, critical["id"], "result->>'status'='CRITICAL'")
            count = int(db.sql("SELECT count(*) FROM lab03.partition_alert_outbox "
                               f"WHERE id > {start_id};"))
            if count != 1:
                raise AssertionError("Repeated cron checks must queue only one CRITICAL")
            if target == "events":
                try:
                    dispatch(db, webhook, start_id)
                except Exception as exc:
                    if "503" not in str(exc):
                        raise
                    log("expected_delivery_failure", error=str(exc))
                else:
                    raise AssertionError("Expected HTTP 503")
                count = int(db.sql("SELECT count(*) FROM lab03.partition_alert_outbox "
                                   f"WHERE id > {start_id} AND delivered_at IS NULL;"))
                if count != 1 or received:
                    raise AssertionError("Failed delivery must remain queued")
            dispatch(db, webhook, start_id)
            alter(create_id, schedule="2 seconds")
            created = wait_result("create", target, repeated["id"],
                                  f"result->'created' @> {literal(json.dumps([name]))}::jsonb")
            log("cron_restored_partition", **created)
            alter(create_id, active=False)
            recovered = wait_result("check", target, created["id"], "result->>'status'='OK'")
            log("cron_detected_recovery", **recovered)
            dispatch(db, webhook, start_id)
            wait_result("check", target, recovered["id"], "result->>'status'='OK'")
            dispatch(db, webhook, start_id)
            alter(check_id, active=False)
            events = [event for event in received if event["payload"]["table"] == "lab03." + target]
            if [event["payload"]["status"] for event in events] != ["CRITICAL", "OK"]:
                raise AssertionError(f"Unexpected delivered events: {events}")
            log("cron_demo_passed", target=target, delivered=2, duplicate_alerts=0)
        pause_all()
        history = json.loads(db.sql("SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY runid), '[]'::jsonb) "
                                    "FROM (SELECT jobid, runid, status, return_message, start_time, end_time "
                                    f"FROM cron.job_run_details WHERE runid > {run_start} "
                                    f"AND jobid IN ({job_ids})) r;"))
        if not history or any(run["status"] != "succeeded" for run in history):
            raise AssertionError(f"Unexpected cron execution history: {history}")
        log("cron_history", runs=history)
    finally:
        try:
            pause_all()
            create(db, ["events", "Bookings"], today)
        finally:
            try:
                for job in jobs:
                    alter(job["jobid"], schedule=job["schedule"], active=job["active"], command=job["command"])
                log("cron_schedules_restored", jobs=jobs)
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=5)
