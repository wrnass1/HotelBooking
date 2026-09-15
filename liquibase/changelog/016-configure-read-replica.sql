--liquibase formatted sql

--changeset system:016-configure-read-replica splitStatements:false
-- Local laboratory credentials, matching docker-compose.yml.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'hotelbooking_replicator') THEN
        CREATE ROLE hotelbooking_replicator WITH LOGIN REPLICATION PASSWORD 'lab04_replication_password';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'hotelbooking_reader') THEN
        CREATE ROLE hotelbooking_reader WITH LOGIN PASSWORD 'lab04_reader_password';
    END IF;
END;
$$;
GRANT CONNECT ON DATABASE hotelbooking TO hotelbooking_reader;
GRANT USAGE ON SCHEMA public TO hotelbooking_reader;
GRANT SELECT ON TABLE public."Hotels", public."Rooms" TO hotelbooking_reader;
ALTER ROLE hotelbooking_reader SET default_transaction_read_only = on;

--changeset system:016-create-physical-replication-slot runInTransaction:false
SELECT pg_create_physical_replication_slot('hotelbooking_replica')
WHERE NOT EXISTS (SELECT FROM pg_replication_slots WHERE slot_name = 'hotelbooking_replica');
