-- ЛР №4. Выполнять по блокам в ДВУХ подключениях pgAdmin/psql.
-- Primary: localhost:5432, Replica: localhost:5433, БД hotelbooking.
-- Администратор стенда: hotelbooking_user / hotelbooking_password.
-- Подготовка: docker compose up -d --build (Liquibase применяет миграцию 016).
-- Автоматический прогон всех частей с HTTP-проверкой: docs/lab-04/verify.py.

-- Часть 1. Выполнить на КАЖДОМ экземпляре: Primary=f/off, Replica=t/on.
SELECT version(), inet_server_addr(), inet_server_port(), pg_is_in_recovery();
SHOW transaction_read_only;

-- Часть 2. PRIMARY: подключённая Replica и настройки WAL.
SHOW wal_level;
SHOW max_wal_senders;
SHOW max_replication_slots;
SHOW synchronous_standby_names;
SELECT * FROM pg_stat_replication;
SELECT slot_name, slot_type, active, restart_lsn, wal_status,
       pg_wal_lsn_diff(pg_current_wal_lsn(), restart_lsn) AS retained_wal_bytes
FROM pg_replication_slots;

-- REPLICA: получение и применение WAL.
SELECT status, sender_host, sender_port, slot_name, written_lsn, flushed_lsn
FROM pg_stat_wal_receiver;
SELECT pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn(), pg_last_xact_replay_timestamp();

-- Часть 3. PRIMARY: автокоммит включён. Сохранить возвращённый Id.
INSERT INTO public."Hotels" ("Name", "Address", "City", "Country", "StarRating")
VALUES ('Lab04 manual v1', 'Laboratory address', 'Lab04-manual', 'Lab04', 3)
RETURNING "Id", "Name", "City";

-- REPLICA: после получения WAL видна строка Primary.
SELECT "Id", "Name", "City" FROM public."Hotels" WHERE "City" = 'Lab04-manual';

-- Часть 4. REPLICA: выполнить отдельно, ожидается SQLSTATE 25006.
-- ERROR: cannot execute UPDATE in a read-only transaction
UPDATE public."Hotels" SET "Name" = 'Replica write attempt' WHERE "City" = 'Lab04-manual';

-- Часть 5. HTTP GET /api/Hotels?City=Lab04-manual&PageSize=21
-- Запрос каталога использует HotelBookingReadDbContext и ReadConnection.
-- REPLICA: после HTTP виден клиент backend и последний SQL каталога.
SELECT usename, application_name, client_addr, state, query
FROM pg_stat_activity WHERE application_name = 'hotelbooking-catalog-replica';
-- Этот же запрос на PRIMARY не должен находить соединений каталога.

-- Часть 6а. PRIMARY, затем СРАЗУ SELECT на REPLICA.
UPDATE public."Hotels" SET "Name" = 'Lab04 manual v2' WHERE "City" = 'Lab04-manual';
-- REPLICA. Если уже v2 — это нормальный результат асинхронной репликации.
SELECT "Id", "Name" FROM public."Hotels" WHERE "City" = 'Lab04-manual';

-- Часть 6б. Управляемая задержка. REPLICA: временно остановить ТОЛЬКО replay.
SELECT pg_wal_replay_pause();
-- Дождаться 'paused', прежде чем выполнять UPDATE на Primary.
SELECT pg_get_wal_replay_pause_state();
-- PRIMARY:
UPDATE public."Hotels" SET "Name" = 'Lab04 manual v3' WHERE "City" = 'Lab04-manual';
SELECT "Id", "Name" FROM public."Hotels" WHERE "City" = 'Lab04-manual';
-- REPLICA: старое значение; через HTTP с новым PageSize=22 также старое.
SELECT "Id", "Name" FROM public."Hotels" WHERE "City" = 'Lab04-manual';
SELECT pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn(),
       pg_wal_lsn_diff(pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn()) AS pending_replay_bytes;
-- REPLICA: ОБЯЗАТЕЛЬНО возобновить replay после демонстрации.
SELECT pg_wal_replay_resume();
SELECT pg_get_wal_replay_pause_state();
SELECT "Id", "Name" FROM public."Hotels" WHERE "City" = 'Lab04-manual';
-- HTTP с новым PageSize=23: после replay возвращается v3.
-- Разные PageSize создают разные ключи Redis, чтобы кэш не скрывал состояние БД.
-- При повторном ручном прогоне используйте новый City-маркер или дождитесь истечения кэша.

-- PRIMARY: очистка только строк ручной демонстрации (при необходимости).
-- DELETE FROM public."Hotels" WHERE "City" = 'Lab04-manual' AND "Country" = 'Lab04';
