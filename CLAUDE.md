# HomeGroup CRM — Backend

CRM-система для управління церковними домашніми групами. REST API на .NET 9.

## Tech Stack

- **Runtime**: .NET 9
- **Framework**: ASP.NET Core Web API (Controllers)
- **ORM**: Entity Framework Core
- **Database**: PostgreSQL
- **Auth**: JWT Bearer tokens (ASP.NET Core Identity або custom)
- **Deployment**: Docker + VPS (DigitalOcean/Hetzner)

## Project Structure

```
src/
  HomeGroup.Api/          # ASP.NET Core Web API проект
    Controllers/          # API контролери
    Middleware/           # Auth, error handling middleware
    Program.cs
  HomeGroup.Application/  # Бізнес-логіка (services, DTOs, interfaces)
  HomeGroup.Domain/       # Сутності та доменна логіка
  HomeGroup.Infrastructure/ # EF Core, репозиторії, зовнішні сервіси
    Data/
      AppDbContext.cs
      Migrations/
    Repositories/
tests/
  HomeGroup.Tests/        # Unit та integration тести
docker-compose.yml
Dockerfile
```

## Core Domain Entities

- **Person** — учасник / людина (ім'я, контакти, статус, нотатки)
- **HomeGroup** — домашня група (назва, лідер, учасники, розклад)
- **Attendance** — відвідуваність (person, group, дата, статус)

## API Conventions

- REST, JSON
- Versioning: `/api/v1/...`
- Auth: `Authorization: Bearer <token>`
- Помилки: RFC 7807 ProblemDetails
- Пагінація: `?page=1&pageSize=20`

## Development Commands

```bash
# Запустити з docker-compose (API + PostgreSQL)
docker-compose up --build

# Застосувати міграції
dotnet ef database update --project src/HomeGroup.Infrastructure --startup-project src/HomeGroup.Api

# Додати міграцію
dotnet ef migrations add <MigrationName> --project src/HomeGroup.Infrastructure --startup-project src/HomeGroup.Api

# Запустити тести
dotnet test
```

## Environment Variables

```
DATABASE_URL=Host=localhost;Database=homegroup;Username=postgres;Password=...
JWT_SECRET=<мінімум 32 символи>
JWT_ISSUER=homegroup-crm
JWT_AUDIENCE=homegroup-crm-client
ASPNETCORE_ENVIRONMENT=Development
```

## TODO

### Ініціалізація
- [ ] Створити solution та проекти (`HomeGroup.Api`, `Application`, `Domain`, `Infrastructure`)
- [ ] Налаштувати `docker-compose.yml` (API + PostgreSQL)
- [ ] Налаштувати `Dockerfile` для .NET 9
- [ ] Підключити EF Core + Npgsql провайдер
- [ ] Налаштувати `AppDbContext`

### Auth
- [ ] Реалізувати реєстрацію та логін користувачів
- [ ] JWT генерація та валідація (access + refresh token)
- [ ] Middleware для авторизації
- [ ] Ролі: Admin, Leader, Member

### Сутності та API
- [ ] `Person` — CRUD ендпоінти (`/api/v1/people`)
- [ ] `HomeGroup` — CRUD ендпоінти (`/api/v1/groups`)
- [ ] Призначення учасників до груп
- [ ] `Attendance` — записи відвідуваності (`/api/v1/attendance`)
- [ ] Фільтрація відвідуваності по групі та датовому діапазону
- [ ] Статистика відвідуваності (% по групі, по людині)

### Інфраструктура
- [ ] Глобальна обробка помилок (ProblemDetails)
- [ ] Логування (Serilog або вбудований ILogger)
- [ ] Health check endpoint (`/health`)
- [ ] Swagger / OpenAPI документація
- [ ] CORS налаштування для фронтенд домену
- [ ] EF Core міграції через CI/CD

### Deployment
- [ ] `Dockerfile` та `docker-compose.yml` для production
- [ ] Nginx reverse proxy конфіг
- [ ] Скрипт деплою на VPS
- [ ] SSL через Let's Encrypt
