FROM postgres:16-alpine

# Build against the same PostgreSQL installation that runs the database.
ARG PG_CRON_VERSION=1.6.7
ARG ALPINE_MIRROR=https://dl-cdn.alpinelinux.org/alpine
RUN sed -i "s|https://dl-cdn.alpinelinux.org/alpine|${ALPINE_MIRROR}|g" /etc/apk/repositories \
    && apk add --no-cache --virtual .cron-build gcc make musl-dev curl \
    && curl -fsSL "https://codeload.github.com/citusdata/pg_cron/tar.gz/refs/tags/v${PG_CRON_VERSION}" -o /tmp/pg_cron.tar.gz \
    && tar -xzf /tmp/pg_cron.tar.gz -C /tmp \
    && make -C "/tmp/pg_cron-${PG_CRON_VERSION}" USE_PGXS=1 with_llvm=no \
    && make -C "/tmp/pg_cron-${PG_CRON_VERSION}" USE_PGXS=1 with_llvm=no install \
    && rm -rf "/tmp/pg_cron-${PG_CRON_VERSION}" /tmp/pg_cron.tar.gz \
    && apk del .cron-build
