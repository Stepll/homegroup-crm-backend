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
    AuthController.cs            — /api/v1/auth (login)
    GroupsController.cs          — /api/v1/groups (CRUD + members + custom fields +
                                    cabinet + events + plans + stats + next-meeting)
    PeopleController.cs          — /api/v1/people (CRUD + custom field values)
    RolesController.cs           — /api/v1/roles (CRUD, system role protection)
    AttendanceController.cs      — /api/v1/attendance (records + meta)
    ChurchEventsController.cs    — /api/v1/church-events (global church calendar)
    PlanTemplatesController.cs   — /api/v1/plan-templates (global meeting templates)
  Data/
    AppDbContext.cs               — EF Core context, OnModelCreating, role seeds
    Migrations/                  — EF Core migrations
  Models/
    Entities/
      Role.cs                    — Id, Name, Color, PermissionsJson, IsSystem, IsDefault
      User.cs                    — Id, Email, PasswordHash, Name, LastName,
                                   PrimaryGroupId, UserRoles[], UserHomeGroups[]
      Person.cs                  — Id, Name, LastName, Phone, Email, Notes, PersonStatusId?,
                                   OversightInfo, OversightUserId, DateOfBirth,
                                   PrimaryGroupId, CreatedAt
      PersonStatus.cs            — Id, Name, Color, CreatedAt
      HomeGroupEntity.cs         — Id, Name, Description, Color, MeetingDay/Time,
                                   Location, LeaderId, TelegramGroupId,
                                   NextMeetingOverrideDate, IsActive
      HomeGroupMember.cs         — HomeGroupId, PersonId, Role (join table)
      UserHomeGroup.cs           — UserId, HomeGroupId (join table)
      UserRole.cs                — UserId, RoleId (join table)
      Attendance.cs              — PersonId, HomeGroupId, MeetingDate, WasPresent, Notes
      AttendanceMeta.cs          — Id, HomeGroupId, MeetingDate, GuestCount, GuestInfo?
      HomeGroupCustomField.cs    — Id, HomeGroupId, Name, CreatedAt
      PersonCustomFieldValue.cs  — Id, PersonId, FieldId, Value
      GroupEvent.cs              — Id, HomeGroupId, Name, Month, Day, Year?, CreatedAt
      ChurchEvent.cs             — Id, Name, Month, Day, CreatedAt
      PlanTemplate.cs            — Id, Name, Blocks[], CreatedAt
      PlanTemplateBlock.cs       — Id, TemplateId, Order, Time, Title, Info?, Responsible?
      HomeMeetingPlan.cs         — Id, HomeGroupId, MeetingDate, AppliedTemplateName?, Blocks[], UpdatedAt
      MeetingPlanBlock.cs        — Id, PlanId, Order, Time, Title, Info?, Responsible?
    DTOs/
      Auth/AuthDtos.cs
      Groups/GroupDtos.cs        — GroupResponse (+TelegramGroupId), CreateGroupRequest,
                                   UpdateGroupRequest (+TelegramGroupId),
                                   SetNextMeetingRequest(Date, OldDate?),
                                   GroupCustomFieldDto, CreateGroupCustomFieldRequest
      Groups/GroupCabinetDto.cs  — GroupCabinetResponse (+HasPlanForNextMeeting),
                                   CabinetGroupInfo (+TelegramGroupId),
                                   CabinetOrgMember (+CabinetRoleTag?),
                                   CabinetRoleTag(Name, Color),
                                   CabinetAttendanceSummary, CabinetUpcomingEvent,
                                   CabinetOverseePerson, CabinetStats,
                                   GroupEventDto, CreateGroupEventRequest
      Groups/GroupStatsDto.cs    — GroupStatsResponse, StatsSummary, MeetingStatsItem,
                                   PersonAttendanceStat
      People/PersonDtos.cs
      Roles/RoleDtos.cs
      Attendance/AttendanceDtos.cs — RecordAttendanceRequest, AttendanceEntry,
                                     AttendanceResponse, AttendanceSummary,
                                     AttendanceMetaResponse, SaveAttendanceMetaRequest
      Planning/PlanningDtos.cs   — PlanBlockDto, MeetingPlanDto, MeetingPlanSummaryDto,
                                   SavePlanRequest, SavePlanBlockRequest,
                                   PlanTemplateDto, CreatePlanTemplateRequest
  Program.cs                     — DI, JWT, CORS, EF, seeding superuser via raw SQL
docker-compose.yml
Dockerfile
```

## Domain Model

### Custom Fields Architecture
Поля прив'язані до **HomeGroup**, а не до людини:
- `HomeGroupCustomField` — визначення поля (назва) для конкретної групи
- `PersonCustomFieldValue` — значення поля для конкретної людини

### Bidirectional Group Sync
- `Person.PrimaryGroupId` ↔ `HomeGroupMembers` синхронізуються автоматично:
  - `PUT /people/:id` → при зміні `PrimaryGroupId` оновлює `HomeGroupMembers`
  - `PUT /groups/:id/members/sync` → при додаванні/видаленні членів оновлює `PrimaryGroupId` людей

### Next Meeting Override
`HomeGroupEntity.NextMeetingOverrideDate` (string?, "yyyy-MM-dd") — одноразове перевизначення дати наступної зустрічі:
- Якщо встановлено і дата >= today → `GetCabinet` повертає її замість розрахованої по розкладу
- Автоматично ігнорується якщо дата в минулому (не видаляється, просто не використовується)
- `PUT /groups/:id/next-meeting` — встановлює override і опційно переміщає план зі старої дати на нову
- `PUT /groups/:id/skip-meeting` — бекенд сам обчислює наступний коректний день тижня після поточної next meeting

### AttendanceMeta
`AttendanceMeta` — метаінформація про зустріч (окремо від per-person записів):
- Унікальний ключ: (HomeGroupId, MeetingDate)
- `GuestCount` — кількість гостей на зустрічі
- `GuestInfo` — довільний текст про гостей

### Plans & Templates
- `PlanTemplate` — глобальний шаблон плану (не прив'язаний до групи)
- `HomeMeetingPlan` — план конкретної зустрічі (HomeGroupId + MeetingDate = унікальний)
- Унікальний індекс на `(HomeGroupId, MeetingDate)` для HomeMeetingPlan

## API Endpoints

### Auth
```
POST /api/v1/auth/login
```

### People
```
GET    /api/v1/people                          — ?search=&noGroup=true
GET    /api/v1/people/:id                      → PersonDetailResponse (з customFields)
POST   /api/v1/people
PUT    /api/v1/people/:id                      — синхронізує HomeGroupMembers
DELETE /api/v1/people/:id

POST   /api/v1/people/:id/custom-fields
PUT    /api/v1/people/:id/custom-fields/:fieldId
DELETE /api/v1/people/:id/custom-fields/:fieldId
```

### Groups
```
GET    /api/v1/groups
GET    /api/v1/groups/:id
POST   /api/v1/groups
PUT    /api/v1/groups/:id                      — включає TelegramGroupId
DELETE /api/v1/groups/:id

GET    /api/v1/groups/:id/members
POST   /api/v1/groups/:id/members
PUT    /api/v1/groups/:id/members/sync
DELETE /api/v1/groups/:id/members/:personId

GET    /api/v1/groups/:id/custom-fields
POST   /api/v1/groups/:id/custom-fields
DELETE /api/v1/groups/:id/custom-fields/:fieldId

GET    /api/v1/groups/:id/cabinet              → GroupCabinetResponse (включає HasPlanForNextMeeting,
                                                 TelegramGroupId, CabinetRoleTag для orgTeam)
GET    /api/v1/groups/:id/stats?period=1m|3m|6m → GroupStatsResponse

GET    /api/v1/groups/:id/events
POST   /api/v1/groups/:id/events               — { name, month, day, year? }
DELETE /api/v1/groups/:id/events/:eventId

GET    /api/v1/groups/:id/plans
GET    /api/v1/groups/:id/plans/date/:date
POST   /api/v1/groups/:id/plans                — upsert (видаляє старі блоки, додає нові)
DELETE /api/v1/groups/:id/plans/date/:date

PUT    /api/v1/groups/:id/next-meeting         — { date?, oldDate? } → override + опційно переміщає план
PUT    /api/v1/groups/:id/skip-meeting         → обчислює наступний день тижня після поточного next-meeting
```

### Roles
```
GET    /api/v1/roles
GET    /api/v1/roles/:id
POST   /api/v1/roles
PUT    /api/v1/roles/:id
DELETE /api/v1/roles/:id   — заборонено для IsSystem=true
```

### Attendance
```
GET  /api/v1/attendance             — ?groupId=&from=&to=
GET  /api/v1/attendance/summary     — ?groupId=
POST /api/v1/attendance             — { homeGroupId, meetingDate, entries: [{personId, wasPresent}] }
GET  /api/v1/attendance/meta        — ?groupId=&date= → { guestCount, guestInfo }
POST /api/v1/attendance/meta        — { homeGroupId, meetingDate, guestCount, guestInfo? }
```

### Church Events
```
GET    /api/v1/church-events
POST   /api/v1/church-events        — { name, month, day }
DELETE /api/v1/church-events/:id
```

### Plan Templates
```
GET    /api/v1/plan-templates
POST   /api/v1/plan-templates       — { name, blocks: [{order, time, title, info?, responsible?}] }
DELETE /api/v1/plan-templates/:id
```

### Person Statuses
```
GET    /api/v1/person-statuses
POST   /api/v1/person-statuses      — { name, color }
PUT    /api/v1/person-statuses/:id  — { name, color }
DELETE /api/v1/person-statuses/:id
```

## Key Patterns

### Superuser (id = 0)
Створюється через raw SQL в `Program.cs` щоб обійти EF Core auto-increment. Параметри з env: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`.
- Виключається з `GetCabinet` org team запиту: `&& u.Id != 0`
- Обходить фільтр видимих груп у `PeopleController`

### Role.PermissionsJson
Зберігається як text (не JSONB), серіалізується через extension methods `GetPermissions()` / `SetPermissions()`.

### People Visibility Filter
У `PeopleController.GetAll`: якщо користувач має `UserHomeGroups` (не superadmin) — показуються тільки люди з цих груп.

### Cabinet org team — roles
`GetCabinet` включає `UserRoles.ThenInclude(Role)` для orgTeam і повертає першу роль як `CabinetRoleTag(Name, Color)`.

### skip-meeting logic
`ComputeNextMeeting` викликається з `currentNextDate` як `today` і `TimeOnly.MinValue` як `nowTime` — якщо поточна дата є днем зустрічі (daysUntil=0), функція повертає `today + 7`. Таким чином завжди повертається наступний коректний день тижня.

## Migrations (в порядку)
1. `InitialCreate` — базові таблиці
2. `PersonNewFields` — LastName, DateOfBirth, PrimaryGroupId, OversightInfo
3. `GroupScopedCustomFields` — HomeGroupCustomFields + PersonCustomFieldValues
4. `AddGroupEvents` — GroupEvent entity
5. `AddChurchEvents` — ChurchEvent entity
6. `AddPlanning` — PlanTemplate, PlanTemplateBlock, HomeMeetingPlan, MeetingPlanBlock
7. `AddTelegramGroupId` — HomeGroupEntity.TelegramGroupId
8. `AddNextMeetingOverrideAndMeetingMeta` — HomeGroupEntity.NextMeetingOverrideDate + AttendanceMeta table
9. `AddPersonStatuses` — PersonStatuses table + Person.PersonStatusId FK (replaces string Status)

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
git pull && docker compose up --build -d
```

Nginx проксує на контейнер. SSL через Certbot + Let's Encrypt. CORS: `FRONTEND_URL` env var.

> ⚠️ `FRONTEND_URL` не повинен мати trailing slash — інакше CORS не працює.

## What's Done

- [x] Auth (login, JWT, BCrypt)
- [x] Superuser id=0 з env
- [x] Roles CRUD (system role protection, IsDefault, PermissionsJson)
- [x] HomeGroups CRUD (color, members sync, custom fields, TelegramGroupId)
- [x] People CRUD (inline editing, bidirectional group sync)
- [x] Group-scoped custom fields (HomeGroupCustomField + PersonCustomFieldValue)
- [x] People visibility filter (по UserHomeGroups, superadmin бачить всіх)
- [x] Attendance CRUD + AttendanceMeta (GuestCount + GuestInfo per meeting)
- [x] Group Cabinet endpoint (next/last meeting dates, org team з ролями,
      birthdays, stats, HasPlanForNextMeeting, TelegramGroupId)
- [x] Next meeting override (one-time date override + skip-meeting endpoint)
- [x] Group Events (custom events per group з ComputeDaysUntil)
- [x] Church Events (глобальний календар)
- [x] Meeting Plans (upsert per group+date, move/delete by date)
- [x] Plan Templates (глобальні шаблони)
- [x] Group Stats endpoint (per-period: summary, per-meeting, per-person)
- [x] Docker + Nginx + SSL deployment
- [x] Person Statuses CRUD (configurable, color + name, FK on Person)

## TODO

- [ ] Опіка (Oversight) — configurable list
- [ ] Реальний enforcement прав доступу на основі Role.PermissionsJson
- [ ] Admins CRUD (Users management)
- [ ] Refresh tokens
- [ ] Pagination для великих списків
- [ ] Swagger / OpenAPI
- [ ] Логування (Serilog)
- [ ] Health check `/health`
- [ ] Telegram notify endpoint (повідомити про план в групу)
