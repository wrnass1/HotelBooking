-- ЛР №6. Запросы и архитектура после шардирования.
-- Выполняйте блоки отдельно в указанном подключении pgAdmin.
-- Только чтение; подготовка данных описана в ЛР №5.
-- Здесь выборка ограничена учебными email ЛР №5. run.py использует RoomId
-- выбранного отеля и диапазон дат, как BookingReportRepository.

-- 1. PRIMARY: localhost:5432 / hotelbooking.
-- Узнать отель и комнаты, которые остаются вне шардов.
SELECT h."Id" AS hotel_id, h."Name", array_agg(r."Id" ORDER BY r."Id") AS room_ids
FROM public."Hotels" h
JOIN public."Rooms" r ON r."HotelId" = h."Id"
WHERE h."Name" = 'Lab05 Sharding Hotel' AND h."City" = 'Lab05'
GROUP BY h."Id", h."Name";

-- Старая таблица не является объединением шардов. На проверенном стенде: 0.
SELECT count(*) AS bookings_on_primary FROM public."Bookings";

-- Такой JOIN существует в нешардированной ветке BookingReportRepository.
-- На Primary он видит только старую локальную таблицу Bookings.
SELECT h."Name", count(b."Id") AS local_bookings
FROM public."Hotels" h
JOIN public."Rooms" r ON r."HotelId" = h."Id"
LEFT JOIN public."Bookings" b ON b."RoomId" = r."Id"
WHERE h."Name" = 'Lab05 Sharding Hotel' AND h."City" = 'Lab05'
GROUP BY h."Id", h."Name";

-- 2. SHARD 2: localhost:5436 / hotelbooking_shard.
-- Для текущего набора SHA-256("5")[0:8] % 3 = 2.
-- На другом наборе выберите существующий Id через run.py.
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM public."Bookings" WHERE "Id" = 5;

-- Вспомогательные таблицы на шарде отсутствуют.
SELECT to_regclass('public."Bookings"') AS bookings,
       to_regclass('public."Rooms"') AS rooms,
       to_regclass('public."Hotels"') AS hotels;

-- 3. КАЖДЫЙ SHARD: порты 5434, 5435, 5436 / hotelbooking_shard.
-- Это частичные результаты. Их нужно объединить в приложении.
SELECT "Status", count(*) AS bookings, sum("TotalPrice") AS revenue,
       avg("TotalPrice") AS local_average
FROM public."Bookings"
WHERE "GuestEmail" LIKE 'lab05.booking.%@example.test'
  AND "CheckInDate" BETWEEN DATE '2030-01-01' AND DATE '2040-01-01'
GROUP BY "Status"
ORDER BY "Status";

SELECT to_char("CheckInDate", 'YYYY-MM') AS month, sum("TotalPrice") AS revenue
FROM public."Bookings"
WHERE "GuestEmail" LIKE 'lab05.booking.%@example.test'
  AND "CheckInDate" BETWEEN DATE '2030-01-01' AND DATE '2040-01-01'
  AND "Status" <> 'Cancelled'
GROUP BY 1
ORDER BY 1;

-- 4. КАЖДЫЙ SHARD: локальные кандидаты для глобального top-100.
-- run.py объединяет максимум 300 кандидатов и снова берёт первые 100.
SELECT "Id", "RoomId", "CheckInDate", "TotalPrice"
FROM public."Bookings"
WHERE "GuestEmail" LIKE 'lab05.booking.%@example.test'
  AND "CheckInDate" BETWEEN DATE '2030-01-01' AND DATE '2040-01-01'
ORDER BY "CheckInDate" DESC, "Id" DESC
LIMIT 100;

-- 5. REPLICA: localhost:5433 / hotelbooking.
-- SQL-эквивалент выбора страницы отелей из HotelRepository.GetPagedAsync
-- (без загрузки связанных Rooms). Hotels сейчас не шардированы.
SELECT "Id", "Name", "City", "StarRating"
FROM public."Hotels"
ORDER BY "Name"
LIMIT 10 OFFSET 0;
-- Для устойчивых страниц при одинаковых названиях нужен ORDER BY "Name", "Id".

-- 6. ЛЮБОЙ PostgreSQL: почему нельзя усреднять средние шардов.
-- Учебный контрпример, не реальные суммы бронирований.
WITH partials(n, total) AS (VALUES (1, 10::numeric), (3, 90::numeric))
SELECT avg(total / n) AS wrong_average,       -- 20
       sum(total) / sum(n) AS correct_average -- 25
FROM partials;

-- 7. SHARD 2: запустить отдельно, ожидается SQLSTATE 42P01.
-- Это намеренная демонстрация: обычный JOIN не видит Rooms на Primary.
-- SELECT b."Id", r."RoomNumber"
-- FROM public."Bookings" b
-- JOIN public."Rooms" r ON r."Id" = b."RoomId"
-- LIMIT 1;
-- run.py исполняет этот запрос и проверяет ожидаемую ошибку автоматически.
