-- ЛР №3. Выполнять в pgAdmin по порядку после миграций 014 и 015.
-- Повторный запуск пересоздаёт только синтетические данные стенда lab03.
-- Части 10–11: docs/lab-03-pg-cron.sql и partition_jobs.py demo (см. отчёт).
SET search_path = lab03, public;
SET timezone = 'UTC';
SET datestyle = 'ISO, YMD';
SET enable_partition_pruning = on;

SELECT version(), current_setting('shared_buffers') AS shared_buffers,
       current_setting('max_parallel_workers_per_gather') AS parallel_workers;

-- 1. RANGE по дате: ровно 100 000 строк на каждый из трёх дней.
SELECT 'PART 1: RANGE dates' AS section;
TRUNCATE lab03.events;
DROP INDEX IF EXISTS lab03.idx_events_user_id;
DROP INDEX IF EXISTS lab03.idx_events_event_type;
INSERT INTO lab03.events (id, user_id, event_type, payload, created_at)
SELECT g, (g - 1) % 100000 + 1,
       CASE WHEN g % 10 = 0 THEN 'click' ELSE 'view' END,
       repeat('x', 100),
       TIMESTAMP '2026-09-09' + ((g - 1) / 100000) * INTERVAL '1 day'
           + ((g - 1) % 86400) * INTERVAL '1 second'
FROM generate_series(1, 300000) AS s(g);
ANALYZE lab03.events;
SELECT tableoid::regclass AS partition_name, count(*) FROM lab03.events
GROUP BY tableoid ORDER BY partition_name;

-- Пробные строки откатываются. На первом запуске для 12 сентября нет партиции.
-- После части 10 она уже есть: это ожидаемое изменение состояния стенда.
BEGIN;
INSERT INTO lab03.events VALUES
    (-1, 1, 'boundary', NULL, '2026-09-10 12:00:00'),
    (-2, 1, 'boundary', NULL, '2026-09-11 00:00:00')
RETURNING id, created_at, tableoid::regclass;
DO $$
BEGIN
    INSERT INTO lab03.events VALUES (-3, 1, 'boundary', NULL, '2026-09-12');
    RAISE NOTICE '2026-09-12 accepted: future partition has already been created by the job';
EXCEPTION WHEN check_violation THEN
    RAISE NOTICE 'Expected SQLSTATE 23514: %', SQLERRM;
END;
$$;
ROLLBACK;

-- 2. Partition pruning: одна партиция против всех.
SELECT 'PART 2: date filter / event_type filter' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) FROM lab03.events
WHERE created_at >= TIMESTAMP '2026-09-10' AND created_at < TIMESTAMP '2026-09-11';
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) FROM lab03.events WHERE event_type = 'click';

-- 3. RANGE по числу, включая граничные цены.
SELECT 'PART 3: numeric RANGE' AS section;
TRUNCATE lab03.products;
INSERT INTO lab03.products VALUES
    (1, 'Free', 0), (2, 'Cheap', 99.99), (3, 'Boundary 100', 100),
    (4, 'Medium', 499.99), (5, 'Medium upper', 999.99),
    (6, 'Boundary 1000', 1000), (7, 'Expensive', 5000);
ANALYZE lab03.products;
SELECT *, tableoid::regclass AS partition_name FROM lab03.products ORDER BY price;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.products WHERE price >= 100 AND price < 500;

-- 4–5. LIST и неизвестная категория. Удаляется только учебная DEFAULT-партиция.
SELECT 'PART 4-5: LIST and DEFAULT' AS section;
TRUNCATE lab03.customers;
DROP TABLE IF EXISTS lab03.customers_default;
INSERT INTO lab03.customers VALUES
    (1, 'Customer A', 'B2C'), (2, 'Customer B', 'B2B'), (3, 'Customer C', 'Enterprise');
ANALYZE lab03.customers;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.customers WHERE customer_type = 'B2B';
DO $$
BEGIN
    INSERT INTO lab03.customers VALUES (100, 'Test User', 'VIP');
    RAISE EXCEPTION 'Expected missing-partition error';
EXCEPTION WHEN check_violation THEN
    RAISE NOTICE 'Expected SQLSTATE 23514: %', SQLERRM;
END;
$$;
CREATE TABLE lab03.customers_default PARTITION OF lab03.customers DEFAULT;
INSERT INTO lab03.customers VALUES (100, 'Test User', 'VIP')
RETURNING *, tableoid::regclass;

-- 6. HASH: 100 000 различных user_id.
SELECT 'PART 6: HASH distribution' AS section;
TRUNCATE lab03.user_events;
INSERT INTO lab03.user_events
SELECT g, g, 'click', TIMESTAMP '2026-09-10' FROM generate_series(1, 100000) AS s(g);
ANALYZE lab03.user_events;
SELECT tableoid::regclass AS partition_name, count(*),
       round(100.0 * count(*) / sum(count(*)) OVER (), 2) AS percent
FROM lab03.user_events GROUP BY tableoid ORDER BY partition_name;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.user_events WHERE user_id = 12345;

-- 7. Выбор стратегий A–E обоснован в отчёте.

-- 8. Pruning + локальный индекс: сравнить до и после.
SELECT 'PART 8: before user_id index' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.events
WHERE created_at >= TIMESTAMP '2026-09-10' AND created_at < TIMESTAMP '2026-09-11'
  AND user_id = 12345;
CREATE INDEX idx_events_user_id ON lab03.events (user_id);
ANALYZE lab03.events;
SELECT 'PART 8: after user_id index' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.events
WHERE created_at >= TIMESTAMP '2026-09-10' AND created_at < TIMESTAMP '2026-09-11'
  AND user_id = 12345;
SELECT t.relid::regclass AS index_name, t.parentrelid::regclass AS parent, t.isleaf
FROM pg_partition_tree('lab03.idx_events_user_id') t;

-- 9. Без условия по дате pruning нет даже после создания индекса.
SELECT 'PART 9: before event_type index' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) FROM lab03.events WHERE event_type = 'click';
CREATE INDEX idx_events_event_type ON lab03.events (event_type);
-- VACUUM отдельно от BEGIN/COMMIT: обновляет visibility map для Index Only Scan.
VACUUM (ANALYZE) lab03.events;
SELECT 'PART 9: after event_type index and vacuum' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) FROM lab03.events WHERE event_type = 'click';

-- 10. Проверка идемпотентности функции с датой из задания.
-- Регулярно её вызывает pg_cron через lab03.run_partition_task с текущей датой UTC.
SELECT 'PART 10: CreatePartitionsJob, repeat' AS section;
SELECT lab03.create_partitions_job('events', DATE '2026-09-11', 3);
SELECT lab03.create_partitions_job('events', DATE '2026-09-11', 3);
SELECT * FROM lab03.expected_partitions('events', DATE '2026-09-11', 3);

-- 11. Ручная проверка функции; фоновую проверку запускает отдельное задание pg_cron.
-- HTTP-alert, повторная проверка и recovery через pg_cron — команда demo.
SELECT 'PART 11: PartitionHealthCheck' AS section;
SELECT lab03.partition_health_check('events', DATE '2026-09-11', 3);

-- 12. Таблица сервиса: RANGE по CheckInDate, месячные партиции.
-- Все данные синтетические; обе таблицы содержат одни и те же 300 000 строк.
SELECT 'PART 12: HotelBooking dataset' AS section;
TRUNCATE lab03."Bookings", lab03.bookings_baseline, lab03."Rooms";
INSERT INTO lab03."Rooms"
    ("Id", "HotelId", "RoomNumber", "RoomType", "PricePerNight", "MaxOccupancy", "IsAvailable", "CreatedAt")
SELECT g, (g - 1) / 100 + 1, g::text, 'Standard', 5000, 2, true, TIMESTAMP '2026-09-01'
FROM generate_series(1, 1000) AS s(g);
INSERT INTO lab03."Bookings"
    ("Id", "RoomId", "GuestName", "GuestEmail", "CheckInDate", "CheckOutDate",
     "NumberOfGuests", "TotalPrice", "Status", "CreatedAt")
SELECT g, (g - 1) % 1000 + 1, 'Guest ' || g, 'guest' || (g % 10000) || '@example.test',
       DATE '2026-09-01' + (g * 37 % 91), DATE '2026-09-01' + (g * 37 % 91) + 3,
       2, 15000, CASE WHEN g % 10 = 0 THEN 'Cancelled' ELSE 'Confirmed' END,
       TIMESTAMP '2026-08-01' + (g % 28) * INTERVAL '1 day'
FROM generate_series(1, 300000) AS s(g);
INSERT INTO lab03.bookings_baseline SELECT * FROM lab03."Bookings" ORDER BY "Id";
ANALYZE lab03."Rooms";
ANALYZE lab03."Bookings";
ANALYZE lab03.bookings_baseline;
SELECT tableoid::regclass AS partition_name, count(*) FROM lab03."Bookings"
GROUP BY tableoid ORDER BY partition_name;

-- SQL-ядро BookingRepository.GetByIdAsync: WHERE по Id без даты.
SELECT 'PART 12 Q1 baseline: GetByIdAsync' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.bookings_baseline WHERE "Id" = 12345;
SELECT 'PART 12 Q1 partitioned: GetByIdAsync' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03."Bookings" WHERE "Id" = 12345;

-- SQL-ядро BookingRepository.GetByRoomIdAsync: без даты также нет pruning.
SELECT 'PART 12 Q2 baseline: GetByRoomIdAsync' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03.bookings_baseline WHERE "RoomId" = 1;
SELECT 'PART 12 Q2 partitioned: GetByRoomIdAsync' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab03."Bookings" WHERE "RoomId" = 1;

-- BookingReportRepository.GetBookingStatisticsAsync: даты и JOIN по отелю.
-- Имена колонок взяты в кавычки в соответствии с реальной схемой PostgreSQL.
SELECT 'PART 12 Q3 baseline: GetBookingStatisticsAsync' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) AS "TotalBookings", coalesce(sum(b."TotalPrice"), 0) AS "TotalRevenue",
       coalesce(avg(b."TotalPrice"), 0) AS "AverageBookingValue",
       count(CASE WHEN b."Status" = 'Confirmed' THEN 1 END) AS "ConfirmedBookings",
       count(CASE WHEN b."Status" = 'Cancelled' THEN 1 END) AS "CancelledBookings"
FROM lab03.bookings_baseline b JOIN lab03."Rooms" r ON b."RoomId" = r."Id"
WHERE r."HotelId" = 1 AND b."CheckInDate" >= DATE '2026-10-01' AND b."CheckInDate" <= DATE '2026-10-31';
SELECT 'PART 12 Q3 partitioned: GetBookingStatisticsAsync' AS section;
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) AS "TotalBookings", coalesce(sum(b."TotalPrice"), 0) AS "TotalRevenue",
       coalesce(avg(b."TotalPrice"), 0) AS "AverageBookingValue",
       count(CASE WHEN b."Status" = 'Confirmed' THEN 1 END) AS "ConfirmedBookings",
       count(CASE WHEN b."Status" = 'Cancelled' THEN 1 END) AS "CancelledBookings"
FROM lab03."Bookings" b JOIN lab03."Rooms" r ON b."RoomId" = r."Id"
WHERE r."HotelId" = 1 AND b."CheckInDate" >= DATE '2026-10-01' AND b."CheckInDate" <= DATE '2026-10-31';

-- Размер родителя равен 0: суммируем физические партиции, включая индексы.
SELECT pg_size_pretty(sum(pg_total_relation_size(relid))) AS partitioned_total_size
FROM pg_partition_tree('lab03."Bookings"') WHERE isleaf;
SELECT pg_size_pretty(pg_total_relation_size('lab03.bookings_baseline')) AS baseline_total_size;
SELECT lab03.create_partitions_job('Bookings', DATE '2026-09-11', 3);
SELECT lab03.partition_health_check('Bookings', DATE '2026-09-11', 3);
-- Четыре задания зарегистрированы миграцией 015; история — в lab-03-pg-cron.sql.
SELECT jobname, schedule, active FROM cron.job WHERE jobname LIKE 'lab03-%' ORDER BY jobname;
RESET search_path;
