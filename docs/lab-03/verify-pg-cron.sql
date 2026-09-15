-- Проверять после миграции 015. Изменения внутри проверки откатываются.
BEGIN;
DO $$
DECLARE
    v_ids BIGINT[];
    v_after BIGINT[];
    v_result JSONB;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_cron') THEN
        RAISE EXCEPTION 'pg_cron extension is not installed';
    END IF;
    IF current_setting('cron.database_name') <> current_database()
       OR current_setting('cron.timezone') <> 'UTC'
       OR current_setting('cron.use_background_workers') <> 'on' THEN
        RAISE EXCEPTION 'Unexpected pg_cron configuration';
    END IF;
    SELECT array_agg(jobid ORDER BY jobid) INTO v_ids FROM cron.job WHERE jobname LIKE 'lab03-%';
    PERFORM lab03.schedule_partition_jobs();
    PERFORM lab03.schedule_partition_jobs();
    SELECT array_agg(jobid ORDER BY jobid) INTO v_after FROM cron.job WHERE jobname LIKE 'lab03-%';
    IF cardinality(v_after) <> 4 OR v_ids IS DISTINCT FROM v_after THEN
        RAISE EXCEPTION 'Registration must preserve exactly four job IDs';
    END IF;
    IF (SELECT count(*) FROM cron.job WHERE jobname IN ('lab03-create-events', 'lab03-create-bookings')
        AND schedule = '0 1 * * *') <> 2
       OR (SELECT count(*) FROM cron.job WHERE jobname IN ('lab03-check-events', 'lab03-check-bookings')
        AND schedule = '*/5 * * * *') <> 2 THEN
        RAISE EXCEPTION 'Unexpected schedules';
    END IF;
    -- Обёртка не зависит от DateStyle и timezone клиентской сессии.
    PERFORM set_config('DateStyle', 'SQL, DMY', true);
    PERFORM set_config('TimeZone', 'Pacific/Kiritimati', true);
    v_result := lab03.run_partition_task('create', 'events');
    v_result := lab03.run_partition_task('check', 'events');
    IF v_result->>'status' <> 'OK'
       OR (v_result->>'reference_date')::date <> (clock_timestamp() AT TIME ZONE 'UTC')::date THEN
        RAISE EXCEPTION 'Cron wrapper failed to use UTC/ISO dates: %', v_result;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM lab03.partition_job_runs WHERE task='check' AND result=v_result) THEN
        RAISE EXCEPTION 'Domain result not logged';
    END IF;
    RAISE NOTICE 'PASS: pg_cron settings, idempotent registration, four schedules, UTC wrapper and result log';
END;
$$;
ROLLBACK;
