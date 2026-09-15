# Лабораторные работы

| Работа | Отчёт | SQL |
|---|---|---|
| №1. EXPLAIN ANALYZE и индексы | [Отчёт](lab-01-indexes-report.md) | [Сценарий](lab-01-indexes-explain-analyze.sql) |
| №2. Когда индексов недостаточно | [Отчёт](lab-02-when-indexes-are-not-enough-report.md) | [Сценарий](lab-02-when-indexes-are-not-enough.sql) |
| №3. Партиционирование PostgreSQL | [Отчёт и запуск](lab-03-partitioning-report.md) | [Сценарий](lab-03-partitioning.sql) |
| №4. Масштабирование чтения: Primary + Replica | [Отчёт и запуск](lab-04-read-scaling-report.md) | [Сценарий](lab-04-read-scaling.sql) |
| №5. Шардирование: Router и Consistent Hashing | [Отчёт и запуск](lab-05-sharding-report.md) | [Сценарий](lab-05-sharding.sql) |

Для №3 расписание создания и проверок выполняет **pg_cron**. Подготовлены [SQL для управления заданиями](lab-03-pg-cron.sql), [демонстрация и HTTP-доставка alerts](lab-03/partition_jobs.py), [интеграционные проверки](lab-03/verify.sql) и [результаты замеров](lab-03/results/explain-analyze.txt). Перед выполнением собирается PostgreSQL с pg_cron и применяются миграции `014` и `015` через Liquibase. Команды запуска — в отчёте. Эксперименты выполняются в схеме `lab03`, задания и техническая история хранятся в схеме `cron`.
