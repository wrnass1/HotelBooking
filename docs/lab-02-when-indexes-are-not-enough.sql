-- ЛР №2: Когда индексов недостаточно. Выполнять в pgAdmin по порядку.
-- Таблица events создаётся Liquibase: 013-create-lab-events.sql.
-- Методичка предлагает сравнить 10k, 100k, 1m, 5m строк; 10m опционально.

-- A1. Проверка таблицы.
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = 'events';

-- A2. Наполнение таблицы. Меняй целевой размер и повторяй блок.
-- Рекомендуемый порядок: 10000, 100000, 1000000, 5000000.
WITH target_rows AS (
    SELECT 10000::bigint AS value
),
current_rows AS (
    SELECT COUNT(*)::bigint AS value
    FROM events
)
INSERT INTO events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE
        WHEN random() < 0.4 THEN 'MESSAGE'
        WHEN random() < 0.7 THEN 'LOGIN'
        WHEN random() < 0.9 THEN 'PURCHASE'
        ELSE 'OTHER'
    END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM target_rows, current_rows,
     generate_series(1, GREATEST(target_rows.value - current_rows.value, 0));

ANALYZE events;

SELECT COUNT(*) AS events_count FROM events;

-- A3. Размер таблицы.
SELECT pg_size_pretty(pg_relation_size('events')) AS table_size;
SELECT pg_size_pretty(pg_total_relation_size('events')) AS total_size_with_indexes;

-- A4. SELECT без индекса.
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM events
WHERE user_id = 123;

-- A5. Индекс по user_id.
CREATE INDEX IF NOT EXISTS idx_events_user_id
ON events(user_id);

ANALYZE events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM events
WHERE user_id = 123;

-- A6. Фильтр по дате.
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM events
WHERE created_at >= NOW() - INTERVAL '1 day';

CREATE INDEX IF NOT EXISTS idx_events_created_at
ON events(created_at);

ANALYZE events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM events
WHERE created_at >= NOW() - INTERVAL '1 day';

-- A7. Сортировка и LIMIT.
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM events
WHERE user_id = 123
ORDER BY created_at DESC
LIMIT 100;

CREATE INDEX IF NOT EXISTS idx_events_user_created
ON events(user_id, created_at DESC);

ANALYZE events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM events
WHERE user_id = 123
ORDER BY created_at DESC
LIMIT 100;

-- A8. Агрегация.
EXPLAIN (ANALYZE, BUFFERS)
SELECT event_type, COUNT(*)
FROM events
WHERE created_at >= NOW() - INTERVAL '30 days'
GROUP BY event_type;

-- A9. INSERT при наличии индексов.
EXPLAIN (ANALYZE, BUFFERS)
INSERT INTO events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE
        WHEN random() < 0.4 THEN 'MESSAGE'
        WHEN random() < 0.7 THEN 'LOGIN'
        WHEN random() < 0.9 THEN 'PURCHASE'
        ELSE 'OTHER'
    END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 10000);

ANALYZE events;

-- A10. Размер индексов.
SELECT
    indexrelname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE relname = 'events'
ORDER BY indexrelname;

-- A11. Когда индекс не спасает.
EXPLAIN (ANALYZE, BUFFERS)
SELECT DATE(created_at), COUNT(*)
FROM events
WHERE created_at >= NOW() - INTERVAL '365 days'
GROUP BY DATE(created_at);

-- B12. Таблица сервиса, которая будет расти: "Bookings".
-- B13. Три реальные SQL-модели для сервиса HotelBooking.

-- Query 1: бронирования конкретной комнаты.
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM "Bookings"
WHERE "RoomId" = 1;

-- Query 2: бронирования в диапазоне дат.
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM "Bookings"
WHERE "CheckInDate" >= DATE '2026-01-01'
  AND "CheckInDate" < DATE '2026-02-01';

-- Query 3: последние бронирования гостя.
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM "Bookings"
WHERE "GuestEmail" = 'guest@example.com'
ORDER BY "CreatedAt" DESC
LIMIT 50;

-- B14-B18. Индекс для оптимизации Query 3.
CREATE INDEX IF NOT EXISTS "IX_Bookings_GuestEmail_CreatedAt_Desc"
ON "Bookings" ("GuestEmail", "CreatedAt" DESC);

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM "Bookings"
WHERE "GuestEmail" = 'guest@example.com'
ORDER BY "CreatedAt" DESC
LIMIT 50;
