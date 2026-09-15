-- ЛР №3: расписание и контроль через pg_cron. Выполнять после миграции 015.
-- SQL-команды управления можно показывать в pgAdmin.
SELECT extname, extversion FROM pg_extension WHERE extname = 'pg_cron';
SHOW shared_preload_libraries;
SHOW cron.database_name;
SHOW cron.timezone;
SHOW cron.use_background_workers;

-- Повторная регистрация по стабильным именам не создаёт дубликаты.
SELECT lab03.schedule_partition_jobs();
SELECT jobid, jobname, schedule, active, database, username, command
FROM cron.job WHERE jobname LIKE 'lab03-%' ORDER BY jobid;

-- Технический результат SQL: succeeded/failed, время, текст ошибки.
SELECT j.jobname, d.runid, d.status, d.return_message, d.start_time, d.end_time
FROM cron.job_run_details d JOIN cron.job j USING (jobid)
WHERE j.jobname LIKE 'lab03-%'
ORDER BY d.runid DESC LIMIT 30;

-- Предметный результат проверки: OK/CRITICAL и отсутствующие партиции.
-- succeeded в pg_cron означает, что SQL выполнился, а не что все партиции есть!
SELECT id, task, target, result, finished_at
FROM lab03.partition_job_runs ORDER BY id DESC LIMIT 30;
SELECT target, fingerprint, checked_at FROM lab03.partition_health_state ORDER BY target;
SELECT id, target, payload, delivered_at FROM lab03.partition_alert_outbox ORDER BY id DESC LIMIT 10;

-- Ниже только примеры ручного управления; раскомментировать при необходимости.
-- Остановить создание events, оставив отдельную проверку работающей:
-- SELECT cron.alter_job(jobid, active := false) FROM cron.job WHERE jobname='lab03-create-events';
-- Возобновить:
-- SELECT cron.alter_job(jobid, active := true) FROM cron.job WHERE jobname='lab03-create-events';
-- Временно ускорить проверку для защиты:
-- SELECT cron.alter_job(jobid, schedule := '5 seconds') FROM cron.job WHERE jobname='lab03-check-events';
-- Вернуть штатные расписания:
-- SELECT lab03.schedule_partition_jobs();

-- Полный сбой -> alert -> восстановление выполняется реальными заданиями pg_cron:
-- python docs/lab-03/partition_jobs.py demo
-- Эта команда временно ускоряет задания до 2 секунд и восстанавливает их настройки.
