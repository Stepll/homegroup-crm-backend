# HomeGroup CRM — Backend

CRM-система для управління церковними домашніми групами. REST API на .NET 9.

## Tech Stack

- **Runtime**: .NET 9
- **Framework**: ASP.NET Core Web API (Controllers, minimal middleware)
- **ORM**: Entity Framework Core + Npgsql
- **Database**: PostgreSQL
- **Auth**: JWT Bearer tokens (custom, BCrypt password hashing)
- **Deployment**: Docker + docker-compose v2, Nginx reverse proxy, DuckDNS + Let's Encrypt SSL

## Actual Project Structure

```
HomeGroup.API/
  Controllers/
    AuthController.cs         — /api/v1/auth (login)
    GroupsController.cs       — /api/v1/groups (CRUD + members + custom fields)
    PeopleController.cs       — /api/v1/people (CRUD + custom field values)
    RolesController.cs        — /api/v1/roles (CRUD, system role protection)
    AttendanceController.cs   — /api/v1/attendance
  Data/
    AppDbContext.cs            — EF Core context, OnModelCreating, role seeds
    Migrations/               — EF Core migrations
  Models/
    Entities/
      Role.cs                 — Id, Name, Color, PermissionsJson, IsSystem, IsDefault
      User.cs                 — Id, Email, PasswordHash, RoleId, Name
      Person.cs               — Id, Name, LastName, Phone, Email, Notes, Status,
                                OversightInfo, DateOfBirth, PrimaryGroupId, CreatedAt
      HomeGroupEntity.cs      — Id, Name, Description, Color, MeetingDay/Time,
                                Location, LeaderId, IsActive
      HomeGroupMember.cs      — HomeGroupId, PersonId, Role (join table)
      UserHomeGroup.cs        — UserId, HomeGroupId (join table)
      Attendance.cs           — PersonId, HomeGroupId, MeetingDate, WasPresent
      HomeGroupCustomField.cs — Id, HomeGroupId, Name, CreatedAt (field DEFINITION)
      PersonCustomFieldValue.cs — Id, PersonId, FieldId, Value (per-person VALUE)
    DTOs/
      Auth/AuthDtos.cs
      Groups/GroupDtos.cs     — GroupResponse, CreateGroupRequest, UpdateGroupRequest,
                                AddMemberRequest, SyncMembersRequest,
                                GroupCustomFieldDto, CreateGroupCustomFieldRequest
      People/PersonDtos.cs    — PersonResponse, PersonDetailResponse, CreatePersonRequest,
                                UpdatePersonRequest, CustomFieldDto,
                                CreateCustomFieldRequest, UpdateCustomFieldRequest
      Roles/RoleDtos.cs
      Attendance/AttendanceDtos.cs
  Program.cs                  — DI, JWT, CORS, EF, seeding superuser via raw SQL
docker-compose.yml
Dockerfile
```

## Domain Model

### Custom Fields Architecture
Поля прив'язані до **HomeGroup**, а не до людини:
- `HomeGroupCustomField` — визначення поля (назва) для конкретної групи
- `PersonCustomFieldValue` — значення поля для конкретної людини

Якщо адмін додає поле через сторінку будь-якого учасника групи — поле з'являється у всіх учасників цієї групи.

### Bidirectional Group Sync
- `Person.PrimaryGroupId` ↔ `HomeGroupMembers` синхронізуються автоматично:
  - `PUT /people/:id` → при зміні `PrimaryGroupId` оновлює `HomeGroupMembers`
  - `PUT /groups/:id/members/sync` → при додаванні/видаленні членів оновлює `PrimaryGroupId` людей

## API Endpoints

### Auth
```
POST /api/v1/auth/login       — { email, password } → { token, name, email, role }
```

### People
```
GET    /api/v1/people                     — ?search=&noGroup=true
GET    /api/v1/people/:id                 → PersonDetailResponse (з customFields)
POST   /api/v1/people                     — { name, lastName?, primaryGroupId? }
PUT    /api/v1/people/:id                 — повний update (синхронізує HomeGroupMembers)
DELETE /api/v1/people/:id

POST   /api/v1/people/:id/custom-fields         — { name } → створює HomeGroupCustomField для групи
PUT    /api/v1/people/:id/custom-fields/:fieldId — { value? } → upsert PersonCustomFieldValue
DELETE /api/v1/people/:id/custom-fields/:fieldId — видаляє HomeGroupCustomField (для всіх в групі)
```

### Groups
```
GET    /api/v1/groups
GET    /api/v1/groups/:id
POST   /api/v1/groups
PUT    /api/v1/groups/:id
DELETE /api/v1/groups/:id

GET    /api/v1/groups/:id/members
POST   /api/v1/groups/:id/members          — { personId, role }
PUT    /api/v1/groups/:id/members/sync     — { personIds } (синхронізує PrimaryGroupId)
DELETE /api/v1/groups/:id/members/:personId

GET    /api/v1/groups/:id/custom-fields
POST   /api/v1/groups/:id/custom-fields    — { name }
DELETE /api/v1/groups/:id/custom-fields/:fieldId
```

### Roles
```
GET    /api/v1/roles
GET    /api/v1/roles/:id
POST   /api/v1/roles
PUT    /api/v1/roles/:id      — захист системних ролей
DELETE /api/v1/roles/:id      — заборонено для IsSystem=true
```

### Attendance
```
GET  /api/v1/attendance   — ?groupId=&date=
POST /api/v1/attendance
```

## Key Patterns

### Superuser (id = 0)
Створюється через raw SQL в `Program.cs` щоб обійти EF Core auto-increment:
```csharp
db.Database.ExecuteSqlAsync($"INSERT INTO \"Users\" (...) VALUES (0, ...) ON CONFLICT (\"Id\") DO UPDATE SET ...");
```
Параметри з env: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`.

### Role.PermissionsJson
Зберігається як text (не JSONB), серіалізується через extension methods `GetPermissions()` / `SetPermissions()`.

### PersonResponse
Містить `PrimaryGroupId`, `PrimaryGroupName`, `PrimaryGroupColor` — щоб список людей міг показувати кольоровий тег групи.

## Migrations (в порядку)
1. `InitialCreate` — базові таблиці (Users, Roles, HomeGroups, etc.)
2. `PersonNewFields` — LastName, DateOfBirth, PrimaryGroupId, OversightInfo
3. `GroupScopedCustomFields` — HomeGroupCustomFields + PersonCustomFieldValues (видалено старий PersonCustomFields)

## Development Commands

```bash
# Запустити локально (API + PostgreSQL)
docker compose up --build

# Додати міграцію
dotnet ef migrations add <Name> --project HomeGroup.API --startup-project HomeGroup.API

# Застосувати міграції
dotnet ef database update --project HomeGroup.API --startup-project HomeGroup.API

# Build
dotnet build HomeGroup.API
```

## Environment Variables

```
DATABASE_URL=Host=...;Database=homegroup;Username=postgres;Password=...
JWT_SECRET=<мінімум 32 символи>
JWT_ISSUER=homegroup-crm
JWT_AUDIENCE=homegroup-crm-client
SUPERADMIN_EMAIL=admin@example.com
SUPERADMIN_PASSWORD=<пароль>
FRONTEND_URL=https://your-frontend.vercel.app    # без trailing slash!
ASPNETCORE_ENVIRONMENT=Development
```

## Deployment (Production)

```bash
# На сервері
git pull && docker compose up --build -d
```

Nginx проксує на контейнер. SSL через Certbot + Let's Encrypt. CORS: `FRONTEND_URL` env var.

> ⚠️ `FRONTEND_URL` не повинен мати trailing slash — інакше CORS не працює.

## What's Done

- [x] Auth (login, JWT, BCrypt)
- [x] Superuser id=0 з env
- [x] Roles CRUD (system role protection, IsDefault handling, PermissionsJson)
- [x] HomeGroups CRUD (color, members sync, custom fields)
- [x] People CRUD (inline editing fields, bidirectional group sync)
- [x] Group-scoped custom fields (HomeGroupCustomField + PersonCustomFieldValue)
- [x] Attendance (базовий CRUD)
- [x] Docker + Nginx + SSL deployment
- [x] CORS

## TODO

- [ ] Статуси (configurable) — зараз хардкод "Active"
- [ ] Опіка (Oversight) — configurable list
- [ ] Реальний enforcement прав доступу на основі Role.PermissionsJson
- [ ] Admins CRUD (Users management)
- [ ] Refresh tokens
- [ ] Pagination для великих списків
- [ ] Swagger / OpenAPI
- [ ] Логування (Serilog)
- [ ] Health check `/health`
