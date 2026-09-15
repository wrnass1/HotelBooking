--liquibase formatted sql

--changeset student:014-create-lab-partitioning
-- Изолированный стенд ЛР №3. public.events и public."Bookings" не изменяются.
CREATE SCHEMA lab03;

CREATE TABLE lab03.events (
    id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload TEXT,
    created_at TIMESTAMP NOT NULL
) PARTITION BY RANGE (created_at);

CREATE TABLE lab03.events_2026_09_09 PARTITION OF lab03.events
    FOR VALUES FROM ('2026-09-09') TO ('2026-09-10');
CREATE TABLE lab03.events_2026_09_10 PARTITION OF lab03.events
    FOR VALUES FROM ('2026-09-10') TO ('2026-09-11');
CREATE TABLE lab03.events_2026_09_11 PARTITION OF lab03.events
    FOR VALUES FROM ('2026-09-11') TO ('2026-09-12');

CREATE TABLE lab03.products (
    id BIGINT NOT NULL, name TEXT NOT NULL, price NUMERIC NOT NULL
) PARTITION BY RANGE (price);
CREATE TABLE lab03.products_cheap PARTITION OF lab03.products
    FOR VALUES FROM (0) TO (100);
CREATE TABLE lab03.products_medium PARTITION OF lab03.products
    FOR VALUES FROM (100) TO (1000);
CREATE TABLE lab03.products_expensive PARTITION OF lab03.products
    FOR VALUES FROM (1000) TO (MAXVALUE);

CREATE TABLE lab03.customers (
    id BIGINT NOT NULL, name TEXT NOT NULL, customer_type VARCHAR(30) NOT NULL
) PARTITION BY LIST (customer_type);
CREATE TABLE lab03.customers_b2c PARTITION OF lab03.customers FOR VALUES IN ('B2C');
CREATE TABLE lab03.customers_b2b PARTITION OF lab03.customers FOR VALUES IN ('B2B');
CREATE TABLE lab03.customers_enterprise PARTITION OF lab03.customers FOR VALUES IN ('Enterprise');
-- DEFAULT создаётся в части 5 сценария после демонстрации ошибки.

CREATE TABLE lab03.user_events (
    id BIGINT NOT NULL, user_id BIGINT NOT NULL,
    event_type VARCHAR(50), created_at TIMESTAMP NOT NULL
) PARTITION BY HASH (user_id);
CREATE TABLE lab03.user_events_0 PARTITION OF lab03.user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 0);
CREATE TABLE lab03.user_events_1 PARTITION OF lab03.user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 1);
CREATE TABLE lab03.user_events_2 PARTITION OF lab03.user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 2);
CREATE TABLE lab03.user_events_3 PARTITION OF lab03.user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 3);

-- Копия структуры таблиц сервиса с синтетическими данными.
-- Не копируем DEFAULT nextval: последовательности public не должны расходоваться.
CREATE TABLE lab03."Rooms" (LIKE public."Rooms" INCLUDING CONSTRAINTS);
ALTER TABLE lab03."Rooms" ADD PRIMARY KEY ("Id");
CREATE TABLE lab03."Bookings" (LIKE public."Bookings" INCLUDING CONSTRAINTS)
    PARTITION BY RANGE ("CheckInDate");
-- В PostgreSQL 16 PRIMARY KEY партиционированной таблицы включает partition key.
ALTER TABLE lab03."Bookings" ADD PRIMARY KEY ("Id", "CheckInDate");
ALTER TABLE lab03."Bookings" ADD FOREIGN KEY ("RoomId") REFERENCES lab03."Rooms" ("Id");
CREATE TABLE lab03.bookings_2026_09 PARTITION OF lab03."Bookings"
    FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE lab03.bookings_2026_10 PARTITION OF lab03."Bookings"
    FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE lab03.bookings_2026_11 PARTITION OF lab03."Bookings"
    FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE INDEX "IX_LabBookings_RoomId" ON lab03."Bookings" ("RoomId");
CREATE INDEX "IX_LabBookings_CheckInDate" ON lab03."Bookings" ("CheckInDate");
CREATE INDEX "IX_LabBookings_CheckOutDate" ON lab03."Bookings" ("CheckOutDate");
CREATE INDEX "IX_LabBookings_GuestEmail" ON lab03."Bookings" ("GuestEmail");
CREATE TABLE lab03.bookings_baseline (LIKE public."Bookings" INCLUDING CONSTRAINTS);
ALTER TABLE lab03.bookings_baseline ADD PRIMARY KEY ("Id");
ALTER TABLE lab03.bookings_baseline ADD FOREIGN KEY ("RoomId") REFERENCES lab03."Rooms" ("Id");
CREATE INDEX ON lab03.bookings_baseline ("RoomId");
CREATE INDEX ON lab03.bookings_baseline ("CheckInDate");
CREATE INDEX ON lab03.bookings_baseline ("CheckOutDate");
CREATE INDEX ON lab03.bookings_baseline ("GuestEmail");

CREATE TABLE lab03.partition_health_state (
    target TEXT PRIMARY KEY,
    fingerprint JSONB NOT NULL,
    checked_at TIMESTAMPTZ NOT NULL
);
CREATE TABLE lab03.partition_alert_outbox (
    id BIGSERIAL PRIMARY KEY,
    target TEXT NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    delivered_at TIMESTAMPTZ
);

--changeset student:014-partition-functions splitStatements:false
-- Имена, родитель и границы проверяются по каталогу, а не только по имени таблицы.
CREATE FUNCTION lab03.expected_partitions(
    p_target TEXT, p_today DATE DEFAULT CURRENT_DATE, p_horizon INTEGER DEFAULT 3
) RETURNS TABLE (partition_name TEXT, range_start DATE, range_end DATE, is_valid BOOLEAN)
LANGUAGE plpgsql STABLE SET search_path = pg_catalog, lab03 AS $$
DECLARE
    v_parent REGCLASS;
    v_child REGCLASS;
    v_expected_bound TEXT;
    v_i INTEGER;
BEGIN
    IF p_today IS NULL OR p_horizon IS NULL OR p_horizon < 0 OR p_horizon > 36 THEN
        RAISE EXCEPTION 'today is required; horizon must be between 0 and 36';
    END IF;
    IF p_target = 'events' THEN
        v_parent := 'lab03.events'::regclass;
    ELSIF p_target = 'Bookings' THEN
        v_parent := 'lab03."Bookings"'::regclass;
    ELSE
        RAISE EXCEPTION 'Unsupported target: %', p_target;
    END IF;
    FOR v_i IN 0..p_horizon LOOP
        IF p_target = 'events' THEN
            range_start := p_today + v_i;
            range_end := range_start + 1;
            partition_name := 'events_' || to_char(range_start, 'YYYY_MM_DD');
            v_expected_bound := format('FOR VALUES FROM (%L) TO (%L)',
                range_start::timestamp::text, range_end::timestamp::text);
        ELSE
            range_start := (date_trunc('month', p_today::timestamp) + make_interval(months => v_i))::date;
            range_end := (range_start + interval '1 month')::date;
            partition_name := 'bookings_' || to_char(range_start, 'YYYY_MM');
            v_expected_bound := format('FOR VALUES FROM (%L) TO (%L)', range_start::text, range_end::text);
        END IF;
        v_child := to_regclass(format('lab03.%I', partition_name));
        SELECT EXISTS (
            SELECT 1 FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid
            WHERE i.inhparent = v_parent AND i.inhrelid = v_child
              AND c.relispartition AND NOT i.inhdetachpending
              AND pg_get_expr(c.relpartbound, c.oid) = v_expected_bound
        ) INTO is_valid;
        RETURN NEXT;
    END LOOP;
END;
$$;

CREATE FUNCTION lab03.create_partitions_job(
    p_target TEXT, p_today DATE DEFAULT CURRENT_DATE, p_horizon INTEGER DEFAULT 3
) RETURNS JSONB LANGUAGE plpgsql SET search_path = pg_catalog, lab03 AS $$
DECLARE
    v_part RECORD;
    v_created TEXT[] := ARRAY[]::text[];
    v_existing INTEGER := 0;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended('lab03.partitions.' || p_target, 0));
    RAISE NOTICE 'CreatePartitionsJob started: %, today=%, horizon=%', p_target, p_today, p_horizon;
    FOR v_part IN SELECT * FROM lab03.expected_partitions(p_target, p_today, p_horizon) LOOP
        IF v_part.is_valid THEN
            v_existing := v_existing + 1;
        ELSE
            IF to_regclass(format('lab03.%I', v_part.partition_name)) IS NOT NULL THEN
                RAISE EXCEPTION 'Invalid existing relation lab03.%: check parent and bounds', v_part.partition_name;
            END IF;
            EXECUTE format('CREATE TABLE lab03.%I PARTITION OF lab03.%I FOR VALUES FROM (%L) TO (%L)',
                v_part.partition_name, p_target, v_part.range_start, v_part.range_end);
            v_created := array_append(v_created, v_part.partition_name);
            RAISE NOTICE 'Partition created: %', v_part.partition_name;
        END IF;
    END LOOP;
    RAISE NOTICE 'CreatePartitionsJob finished: existing=%, required=%, created=%',
        v_existing, p_horizon + 1, cardinality(v_created);
    RETURN jsonb_build_object('target', p_target, 'existing', v_existing,
        'required', p_horizon + 1, 'created', v_created);
END;
$$;

CREATE FUNCTION lab03.partition_health_check(
    p_target TEXT, p_today DATE DEFAULT CURRENT_DATE, p_horizon INTEGER DEFAULT 3
) RETURNS JSONB LANGUAGE plpgsql SET search_path = pg_catalog, lab03 AS $$
DECLARE
    v_missing TEXT[];
    v_status TEXT;
    v_fingerprint JSONB;
    v_previous JSONB;
    v_payload JSONB;
    v_checked TIMESTAMPTZ := clock_timestamp();
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended('lab03.partitions.' || p_target, 0));
    SELECT coalesce(array_agg(partition_name ORDER BY partition_name), ARRAY[]::text[])
      INTO v_missing FROM lab03.expected_partitions(p_target, p_today, p_horizon) WHERE NOT is_valid;
    v_status := CASE WHEN cardinality(v_missing) = 0 THEN 'OK' ELSE 'CRITICAL' END;
    v_fingerprint := jsonb_build_object('status', v_status, 'missing_partitions', v_missing);
    SELECT fingerprint INTO v_previous FROM lab03.partition_health_state WHERE target = p_target;
    v_payload := v_fingerprint || jsonb_build_object(
        'table', 'lab03.' || p_target, 'expected_horizon', p_horizon,
        'horizon_unit', CASE WHEN p_target = 'events' THEN 'days' ELSE 'months' END,
        'reference_date', p_today, 'checked_at', v_checked,
        'message', CASE WHEN v_status = 'CRITICAL' THEN 'Partition alert' ELSE 'Partition check OK (recovery)' END);
    IF (v_previous IS NULL AND v_status = 'CRITICAL')
       OR (v_previous IS NOT NULL AND v_previous IS DISTINCT FROM v_fingerprint) THEN
        INSERT INTO lab03.partition_alert_outbox(target, payload) VALUES (p_target, v_payload);
    END IF;
    INSERT INTO lab03.partition_health_state(target, fingerprint, checked_at)
        VALUES (p_target, v_fingerprint, v_checked)
        ON CONFLICT (target) DO UPDATE SET fingerprint = EXCLUDED.fingerprint, checked_at = EXCLUDED.checked_at;
    RETURN v_payload;
END;
$$;
