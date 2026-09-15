--liquibase formatted sql

--changeset student:015-enable-pg-cron
-- Requires the Docker image and shared_preload_libraries from docker-compose.yml.
CREATE EXTENSION IF NOT EXISTS pg_cron;
CREATE TABLE lab03.partition_job_runs (
    id BIGSERIAL PRIMARY KEY,
    task TEXT NOT NULL CHECK (task IN ('create', 'check')),
    target TEXT NOT NULL,
    result JSONB NOT NULL,
    finished_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
);

--changeset student:015-pg-cron-functions splitStatements:false
CREATE FUNCTION lab03.run_partition_task(p_task TEXT, p_target TEXT)
RETURNS JSONB LANGUAGE plpgsql
SET search_path = pg_catalog, lab03
SET timezone = 'UTC'
SET datestyle = 'ISO, YMD'
SET lock_timeout = '5s'
SET statement_timeout = '30s'
AS $$
DECLARE
    v_result JSONB;
BEGIN
    IF p_task = 'create' THEN
        v_result := lab03.create_partitions_job(p_target, CURRENT_DATE, 3);
    ELSIF p_task = 'check' THEN
        v_result := lab03.partition_health_check(p_target, CURRENT_DATE, 3);
    ELSE
        RAISE EXCEPTION 'Unsupported partition task: %', p_task;
    END IF;
    INSERT INTO lab03.partition_job_runs(task, target, result) VALUES (p_task, p_target, v_result);
    RETURN v_result;
END;
$$;

CREATE FUNCTION lab03.schedule_partition_jobs() RETURNS VOID
LANGUAGE plpgsql SET search_path = pg_catalog, lab03 AS $$
BEGIN
    -- Stable names: repeated registration updates the existing jobs.
    PERFORM cron.schedule('lab03-create-events', '0 1 * * *',
        $cmd$SELECT lab03.run_partition_task('create', 'events');$cmd$);
    PERFORM cron.schedule('lab03-create-bookings', '0 1 * * *',
        $cmd$SELECT lab03.run_partition_task('create', 'Bookings');$cmd$);
    PERFORM cron.schedule('lab03-check-events', '*/5 * * * *',
        $cmd$SELECT lab03.run_partition_task('check', 'events');$cmd$);
    PERFORM cron.schedule('lab03-check-bookings', '*/5 * * * *',
        $cmd$SELECT lab03.run_partition_task('check', 'Bookings');$cmd$);
END;
$$;

--changeset student:015-register-partition-jobs
-- Bootstrap the current horizon once; subsequent calls are scheduled by pg_cron.
SELECT lab03.run_partition_task('create', 'events');
SELECT lab03.run_partition_task('create', 'Bookings');
SELECT lab03.run_partition_task('check', 'events');
SELECT lab03.run_partition_task('check', 'Bookings');
SELECT lab03.schedule_partition_jobs();
