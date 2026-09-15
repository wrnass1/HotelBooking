-- Лабораторная №5. Диагностика шардирования реальной сущности Bookings.
-- Shard 0: localhost:5434; Shard 1: localhost:5435; Shard 2: localhost:5436.
-- База на каждом: hotelbooking_shard.
-- Пользователь: hotelbooking_shard_user; пароль: hotelbooking_shard_password.
-- Primary прежних лабораторных: localhost:5432, hotelbooking, hotelbooking_user.

-- Часть 2. На КАЖДОМ шарде: независимый экземпляр, recovery=false, read_only=off.
SELECT version(), current_database(), inet_server_addr(), inet_server_port(), pg_is_in_recovery();
SHOW transaction_read_only;
SELECT system_identifier FROM pg_control_system();
SELECT fingerprint FROM public.sharding_layout;

-- Части 3–4. На КАЖДОМ шарде: данные и индексы существующей сущности сервиса.
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'Bookings'
ORDER BY ordinal_position;

SELECT COUNT(*) AS all_bookings,
       COUNT(*) FILTER (WHERE "GuestEmail" LIKE 'lab05.booking.%@example.test') AS lab05_bookings
FROM public."Bookings";
-- Фактически для 100 000 записей: Shard 0 = 33 219; Shard 1 = 33 353; Shard 2 = 33 428.

SELECT "Id", "RoomId", "GuestName", "GuestEmail", "CheckInDate", "CheckOutDate", "TotalPrice", "Status"
FROM public."Bookings"
WHERE "GuestEmail" LIKE 'lab05.booking.%@example.test'
ORDER BY "Id" LIMIT 5;

SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'Bookings';

-- Часть 3. Поиск по Id выполняется только на рассчитанном Router шарде.
-- Для загруженного набора: Id=6 -> Shard 0, Id=1 -> Shard 1, Id=5 -> Shard 2.
-- На двух остальных экземплярах та же запись отсутствует.
SELECT "Id", "RoomId", "GuestEmail" FROM public."Bookings" WHERE "Id" IN (1, 5, 6);
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM public."Bookings" WHERE "Id" = 6;

-- После HTTP-запроса: соединение backend с этим шардом.
SELECT application_name, client_addr, state, query FROM pg_stat_activity
WHERE application_name LIKE 'hotelbooking-bookings-shard-%';

-- PRIMARY: справочники и глобальная последовательность Id остались здесь.
SELECT "Id", "Name", "City" FROM public."Hotels" WHERE "Name" = 'Lab05 Sharding Hotel' AND "City" = 'Lab05';
SELECT COUNT(*) FROM public."Rooms" r JOIN public."Hotels" h ON h."Id" = r."HotelId"
WHERE h."Name" = 'Lab05 Sharding Hotel' AND h."City" = 'Lab05';
SELECT pg_get_serial_sequence('public."Bookings"', 'Id');
-- Исходная таблица сохранена как снимок до перехода на шарды; в данном стенде была пустой.
SELECT COUNT(*) FROM public."Bookings";

-- Части 5–7. Сравнение выполняет C# Router внутри самого backend по реальным Id из шардов.
-- В терминале из корня проекта:
-- docker exec hotelbooking-api dotnet HotelBooking.dll --lab05 compare
-- Результат: Modulo 74.969%; базовый ConsistentHash 47.324%; 128 virtual nodes 23.669%.
-- Это число записей, которым ПОТРЕБОВАЛСЯ БЫ перенос при 3 -> 4.
-- Команда не меняет физическое размещение и конфигурацию живого API.
-- Полная проверка с HTTP CRUD: python docs/lab-05/run.py
