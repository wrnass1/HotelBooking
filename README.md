# Hotel Booking API

Production-ready REST API сервис для системы бронирования отелей на ASP.NET Core.

## Технологический стек

- **.NET 8.0** - ASP.NET Core Web API
- **PostgreSQL** - основная база данных
- **Redis** - кэширование
- **Docker & Docker Compose** - контейнеризация
- **Liquibase** - управление миграциями БД
- **Entity Framework Core** - ORM
- **Dapper** - для сложных запросов и отчетов
- **FluentValidation** - валидация данных
- **JWT Bearer** - аутентификация пользователей
- **API Key** - аутентификация системных клиентов
- **Swagger/OpenAPI** - документация API
- **Serilog** - логирование

## Архитектура

Проект следует принципам Clean Architecture:

```
/Controllers      - API endpoints
/Auth             - JWT и API Key аутентификация
/Services         - Бизнес-логика
  /Interfaces     - Интерфейсы сервисов
/Repositories     - Доступ к данным
  /Interfaces     - Интерфейсы репозиториев
/Data             - DbContext
/Models
  /Entities       - Сущности БД
  /DTO            - Data Transfer Objects
/Middleware       - Глобальная обработка ошибок
/Validators       - FluentValidation валидаторы
```

## Запуск проекта

### Требования

- Docker и Docker Compose
- .NET 10.0 SDK (для локальной разработки)

### Запуск через Docker Compose

```bash
docker-compose up -d
```

Это запустит:
- PostgreSQL на порту 5432
- Redis на порту 6379
- Liquibase для создания схемы БД
- API на порту 8080

### Доступ к API

- **Swagger UI**: http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/health
- **Health Check UI**: http://localhost:8080/health-ui

## API Endpoints

### Аутентификация

- `POST /api/auth/login` - Вход пользователя
- `POST /api/auth/register` - Регистрация нового пользователя

### Отели

- `GET /api/hotels` - Список отелей (с пагинацией и фильтрацией)
- `GET /api/hotels/{id}` - Получить отель по ID
- `POST /api/hotels` - Создать отель (требуется роль Admin или Manager)
- `PUT /api/hotels/{id}` - Обновить отель (требуется роль Admin или Manager)
- `DELETE /api/hotels/{id}` - Удалить отель (требуется роль Admin)

### Номера

- `GET /api/rooms` - Список номеров
- `GET /api/rooms/{id}` - Получить номер по ID
- `POST /api/rooms` - Создать номер
- `PUT /api/rooms/{id}` - Обновить номер
- `DELETE /api/rooms/{id}` - Удалить номер

### Бронирования

- `GET /api/bookings` - Список бронирований
- `GET /api/bookings/{id}` - Получить бронирование по ID
- `POST /api/bookings` - Создать бронирование
- `PUT /api/bookings/{id}` - Обновить бронирование
- `DELETE /api/bookings/{id}` - Удалить бронирование

## Аутентификация

### JWT Bearer Token

Для пользователей системы:

```bash
POST /api/auth/login
{
  "username": "admin",
  "password": "password"
}
```

Ответ содержит JWT токен, который нужно передавать в заголовке:
```
Authorization: Bearer <token>
```

### API Key

Для системных клиентов:

```
X-API-KEY: <your-api-key>
```

## Роли и Permissions

- **Admin** - полный доступ ко всем операциям
- **Manager** - может читать, создавать и обновлять (не может удалять)
- **User** - только чтение и создание бронирований

## Пагинация

Пример запроса с пагинацией:

```
GET /api/hotels?page=1&pageSize=10&search=hotel&city=Moscow&minStarRating=4
```

Ответ:

```json
{
  "items": [...],
  "total": 100,
  "page": 1,
  "pageSize": 10,
  "totalPages": 10
}
```

## Health Checks

Проверка состояния сервиса:

```
GET /health
```

Проверяет доступность:
- API
- PostgreSQL
- Redis

## Миграции БД

Миграции управляются через Liquibase. Все изменения схемы БД должны быть добавлены в файлы в папке `liquibase/changelog/`.

EF Core **НЕ** должен создавать или изменять схему БД - это делает только Liquibase.

## Кэширование

Redis используется для кэширования:
- Список отелей (с пагинацией)
- Детали отеля по ID

Кэш автоматически инвалидируется при создании/обновлении/удалении отелей.

## Логирование

Используется Serilog для структурированного логирования. Логи выводятся в консоль.

## Обработка ошибок

Глобальный middleware обрабатывает все исключения и возвращает единый формат:

```json
{
  "error": "NotFound",
  "message": "Hotel not found",
  "traceId": "..."
}
```

## Тестирование

```bash
dotnet test
```

## Разработка

Для локальной разработки:

1. Запустите PostgreSQL и Redis через Docker Compose
2. Обновите connection string в `appsettings.Development.json`
3. Запустите проект: `dotnet run`

## Лицензия

MIT
