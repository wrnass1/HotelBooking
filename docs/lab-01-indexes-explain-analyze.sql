-- ЛР №1: Индексы и EXPLAIN ANALYZE PostgreSQL. Выполнять в pgAdmin по порядку.
-- Таблицы orders/users создаются Liquibase: 011 и 012.

-- 1–2. Проверка таблиц, 1 000 000 строк и статистика.
SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('orders','users');
INSERT INTO orders(user_id,product_id,status,amount,created_at,updated_at)
SELECT (random()*100000)::BIGINT,(random()*10000)::BIGINT,
       (ARRAY['NEW','PAID','DELIVERED','CANCELLED'])[floor(random()*4+1)],
       (random()*10000)::NUMERIC(10,2),NOW()-(random()*INTERVAL '2 years'),NOW()
FROM generate_series(1,1000000) WHERE NOT EXISTS (SELECT 1 FROM orders);
ANALYZE orders;

-- 3–5. EXPLAIN, EXPLAIN ANALYZE, Sequential Scan.
EXPLAIN SELECT * FROM orders WHERE user_id=123;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123;
EXPLAIN ANALYZE SELECT * FROM orders;
EXPLAIN ANALYZE SELECT * FROM orders WHERE amount>0;

-- 6. B-tree.
CREATE INDEX IF NOT EXISTS idx_orders_user_id ON orders(user_id);
ANALYZE orders;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123;

-- 7–8. Селективность status.
CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status);
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='PAID';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='NEW';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='DELIVERED';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='CANCELLED';
SELECT status,COUNT(*) FROM orders GROUP BY status ORDER BY status;

-- 9. Range Query.
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at>NOW()-INTERVAL '7 days';
CREATE INDEX IF NOT EXISTS idx_orders_created_at ON orders(created_at);
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at>NOW()-INTERVAL '1 day';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at>NOW()-INTERVAL '1 month';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at>NOW()-INTERVAL '1 year';

-- 10. Bitmap Scan.
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='NEW';
CREATE INDEX IF NOT EXISTS idx_orders_amount ON orders(amount);
EXPLAIN ANALYZE SELECT * FROM orders WHERE amount BETWEEN 1000 AND 3000;

-- 11–12. Несколько одиночных и составной индекс.
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 AND status='PAID';
CREATE INDEX IF NOT EXISTS idx_orders_user_status ON orders(user_id,status);
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 AND status='PAID';

-- 13. Порядок столбцов составного индекса.
CREATE INDEX IF NOT EXISTS idx_orders_user_created_at ON orders(user_id,created_at);
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 AND created_at>NOW()-INTERVAL '30 days';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at>NOW()-INTERVAL '30 days';
CREATE INDEX IF NOT EXISTS idx_orders_created_at_user ON orders(created_at,user_id);

-- 14–15. WHERE + ORDER BY и пагинация API.
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 ORDER BY created_at DESC;
CREATE INDEX IF NOT EXISTS idx_orders_user_created_at_desc ON orders(user_id,created_at DESC);
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 ORDER BY created_at DESC;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 ORDER BY created_at DESC LIMIT 20;

-- 16. Index Only Scan и INCLUDE.
EXPLAIN ANALYZE SELECT id,user_id FROM orders WHERE user_id=123;
CREATE INDEX IF NOT EXISTS idx_orders_user_id_include ON orders(user_id) INCLUDE(id,status,created_at);
VACUUM ANALYZE orders;
EXPLAIN ANALYZE SELECT id,user_id FROM orders WHERE user_id=123;

-- 17. Partial Index.
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='NEW' ORDER BY created_at;
CREATE INDEX IF NOT EXISTS idx_orders_new ON orders(created_at) WHERE status='NEW';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status='NEW' ORDER BY created_at;

-- 18. Expression Index.
INSERT INTO users(email) SELECT 'user'||value||'@example.com' FROM generate_series(1,100000) value
WHERE NOT EXISTS(SELECT 1 FROM users);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
EXPLAIN ANALYZE SELECT * FROM users WHERE LOWER(email)='test@example.com';
CREATE INDEX IF NOT EXISTS idx_users_lower_email ON users(LOWER(email));
EXPLAIN ANALYZE SELECT * FROM users WHERE LOWER(email)='test@example.com';

-- 19. INSERT при наличии индексов.
EXPLAIN (ANALYZE,BUFFERS) INSERT INTO orders(user_id,product_id,status,amount,created_at,updated_at)
VALUES(123,1,'NEW',100.00,NOW(),NOW());

-- 20. Комплексный запрос: до и после целевого частичного покрывающего индекса.
EXPLAIN ANALYZE SELECT id,amount,status,created_at FROM orders
WHERE user_id=123 AND status='PAID' AND created_at>=NOW()-INTERVAL '30 days'
ORDER BY created_at DESC LIMIT 50;
CREATE INDEX IF NOT EXISTS idx_orders_paid_user_created_at_covering
ON orders(user_id,created_at DESC) INCLUDE(id,amount,status) WHERE status='PAID';
EXPLAIN ANALYZE SELECT id,amount,status,created_at FROM orders
WHERE user_id=123 AND status='PAID' AND created_at>=NOW()-INTERVAL '30 days'
ORDER BY created_at DESC LIMIT 50;

-- 21. Scaling Entity: orders; покрыты user_id/status/date/pagination.
-- 22–23. Три запроса для сравнительной таблицы отчёта.
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 AND status='PAID';
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id=123 ORDER BY created_at DESC LIMIT 20;

-- 24–25. Статистика использования индексов.
SELECT schemaname,relname,indexrelname,idx_scan FROM pg_stat_user_indexes
WHERE relname IN ('orders','users') ORDER BY relname,idx_scan;
-- 26. Контрольные вопросы и итоговая таблица — в lab-01-indexes-report.md.
