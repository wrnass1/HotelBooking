-- Интеграционные проверки функций на PostgreSQL 16. Все изменения откатываются.
SET timezone = 'UTC';
SET datestyle = 'ISO, YMD';
BEGIN;
DO $$
DECLARE
    v_result JSONB;
    v_before BIGINT;
    v_after BIGINT;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended('lab03.partitions.events', 0));
    DELETE FROM lab03.partition_health_state WHERE target = 'events';
    SELECT count(*) INTO v_before FROM lab03.partition_alert_outbox WHERE target = 'events';
    PERFORM lab03.create_partitions_job('events', DATE '2099-01-31', 3);
    SELECT lab03.create_partitions_job('events', DATE '2099-01-31', 3) INTO v_result;
    IF v_result->'created' <> '[]'::jsonb OR (v_result->>'required')::int <> 4 THEN
        RAISE EXCEPTION 'Idempotency/horizon failed: %', v_result;
    END IF;
    IF (SELECT count(*) FROM lab03.expected_partitions('events', DATE '2099-01-31', 3)
        WHERE is_valid) <> 4 THEN
        RAISE EXCEPTION 'Daily horizon across month boundary failed';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM lab03.expected_partitions('events', DATE '2028-02-28', 3)
        WHERE range_start = DATE '2028-02-29' AND range_end = DATE '2028-03-01') THEN
        RAISE EXCEPTION 'Leap day failed';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM lab03.expected_partitions('Bookings', DATE '2026-12-31', 3)
        WHERE range_start = DATE '2027-01-01' AND range_end = DATE '2027-02-01') THEN
        RAISE EXCEPTION 'Monthly horizon across year boundary failed';
    END IF;

    -- Существующая таблица с нужным именем, но без связи с родителем, не считается OK.
    PERFORM lab03.partition_health_check('events', DATE '2099-01-31', 3);
    ALTER TABLE lab03.events DETACH PARTITION lab03.events_2099_02_03;
    v_result := lab03.partition_health_check('events', DATE '2099-01-31', 3);
    IF v_result->>'status' <> 'CRITICAL'
        OR v_result->'missing_partitions' <> '["events_2099_02_03"]'::jsonb THEN
        RAISE EXCEPTION 'Detached partition was not detected: %', v_result;
    END IF;
    BEGIN
        PERFORM lab03.create_partitions_job('events', DATE '2099-01-31', 3);
        RAISE EXCEPTION 'Expected invalid relation rejection';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE 'Invalid existing relation%' THEN RAISE; END IF;
    END;

    -- Правильные имя и родитель, но неверные границы также должны обнаруживаться.
    ALTER TABLE lab03.events ATTACH PARTITION lab03.events_2099_02_03
        FOR VALUES FROM ('2099-02-05') TO ('2099-02-06');
    IF (SELECT is_valid FROM lab03.expected_partitions('events', DATE '2099-01-31', 3)
        WHERE partition_name = 'events_2099_02_03') THEN
        RAISE EXCEPTION 'Wrong bounds were accepted';
    END IF;
    PERFORM lab03.partition_health_check('events', DATE '2099-01-31', 3);
    SELECT count(*) INTO v_after FROM lab03.partition_alert_outbox WHERE target = 'events';
    IF v_after <> v_before + 1 THEN RAISE EXCEPTION 'Duplicate CRITICAL queued'; END IF;
    DROP TABLE lab03.events_2099_02_03;
    PERFORM lab03.create_partitions_job('events', DATE '2099-01-31', 3);
    v_result := lab03.partition_health_check('events', DATE '2099-01-31', 3);
    IF v_result->>'status' <> 'OK' THEN RAISE EXCEPTION 'Recovery failed'; END IF;
    PERFORM lab03.partition_health_check('events', DATE '2099-01-31', 3);
    SELECT count(*) INTO v_after FROM lab03.partition_alert_outbox WHERE target = 'events';
    IF v_after <> v_before + 2 THEN RAISE EXCEPTION 'Recovery deduplication failed'; END IF;

    -- Неверный ключ таблицы не должен позволять динамический DDL вне стенда.
    BEGIN
        PERFORM lab03.create_partitions_job('public.events', CURRENT_DATE, 3);
        RAISE EXCEPTION 'Expected target rejection';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE 'Unsupported target:%' THEN RAISE; END IF;
    END;
    BEGIN
        PERFORM lab03.create_partitions_job('events', CURRENT_DATE, -1);
        RAISE EXCEPTION 'Expected horizon rejection';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE 'today is required%' THEN RAISE; END IF;
    END;
    RAISE NOTICE 'PASS: idempotency, calendar, detached/wrong-bound partition, CRITICAL/recovery deduplication, arguments';
END;
$$;
ROLLBACK;
