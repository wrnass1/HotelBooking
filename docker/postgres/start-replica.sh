#!/bin/sh
set -eu

if [ "$(id -u)" = '0' ]; then
    mkdir -p "$PGDATA"
    chown postgres:postgres "$PGDATA"
    chmod 700 "$PGDATA"
    exec su-exec postgres /bin/sh "$0"
fi

if [ ! -s "$PGDATA/PG_VERSION" ]; then
    # Refuse to overwrite a partial backup or an unexpected data directory.
    if [ -n "$(ls -A "$PGDATA")" ]; then
        echo 'Replica data directory is nonempty but has no PG_VERSION; inspect it before reinitializing.' >&2
        exit 1
    fi
    pg_basebackup \
        --dbname='host=postgres port=5432 user=hotelbooking_replicator application_name=hotelbooking-replica' \
        --pgdata="$PGDATA" --wal-method=stream --checkpoint=fast \
        --slot=hotelbooking_replica --write-recovery-conf --progress --no-password
fi

if [ ! -f "$PGDATA/standby.signal" ]; then
    echo 'standby.signal is missing; refusing to start this replica as a writable primary.' >&2
    exit 1
fi

# pg_cron is not scheduled on the standby. Match primary worker settings required by recovery.
exec postgres -c hot_standby=on -c max_worker_processes=16 \
    -c shared_preload_libraries= -c timezone=UTC
