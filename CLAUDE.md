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
                                    cabinet + events + plans + stats + stats/all + next-meeting + needs)
    PeopleController.cs          — /api/v1/people (CRUD + custom field values + activity + convert-to-admin)
    AdminsController.cs          — /api/v1/admins (CRUD + profile + me/set-password (no perm) +
                                    :id/set-password (settings.admins) + me/dashboard GET/PUT +
                                    me/tasks GET + me/tasks/:id/toggle PATCH (no perm) +
                                    activity GET/POST/DELETE + custom-fields POST/PUT/DELETE)
    DashboardController.cs       — /api/v1/dashboard (inactive-members, status-distribution,
                                    groups-comparison, groups-attendance-summary —
                                    усе фільтрується по UserHomeGroups)
    PersonStatusesController.cs  — /api/v1/person-statuses (CRUD)
    RolesController.cs           — /api/v1/roles (CRUD, system role protection)
    AttendanceController.cs      — /api/v1/attendance (records + meta + dates + dots)
    CalendarController.cs        — /api/v1/calendar (occurrences GET + events CRUD)
    ScheduleController.cs        — /api/v1/groups/:id/schedule (per-week overrides:
                                    GET weeks, cancel/uncancel, move (with plan + attendance),
                                    reset-week)
    GoogleCalendarController.cs  — /api/v1/google-calendar/sync (manual Google sync)
    RoomsController.cs           — /api/v1/rooms (CRUD)
    PlanTemplatesController.cs   — /api/v1/plan-templates (global meeting templates)
  Data/
    AppDbContext.cs               — EF Core context, OnModelCreating, role seeds
    Migrations/                  — EF Core migrations
  Models/
    Entities/
      Role.cs                    — Id, Name, Color, PermissionsJson, IsSystem, IsDefault
      User.cs                    — Id, Email, PasswordHash, Name, LastName,
                                   Phone?, Telegram?, Gender?, MaritalStatus?, Address?,
                                   DateOfBirth?, IsBaptized, Church?, Ministry?,
                                   IsBaptizedWithSpirit, PersonStatusId?,
                                   DashboardConfigJson? (text, JSON array of WidgetConfig),
                                   PrimaryGroupId, UserRoles[], UserHomeGroups[]
      Person.cs                  — Id, Name, LastName, Phone, Email, Telegram?,
                                   Notes, Gender?, MaritalStatus?, Address?,
                                   DateOfBirth?, IsBaptized, Church?, Ministry?,
                                   IsBaptizedWithSpirit, PersonStatusId?,
                                   OversightInfo?, OversightUserId?,
                                   PrimaryGroupId?, CreatedAt
      PersonStatus.cs            — Id, Name, Color, CreatedAt
      HomeGroupEntity.cs         — Id, Name, Description, Color, MeetingDay/Time,
                                   Location, LeaderId, TelegramGroupId,
                                   NextMeetingOverrideDate, NotifSettingsJson?, IsActive
      HomeGroupMember.cs         — HomeGroupId, PersonId, Role (join table)
      UserHomeGroup.cs           — UserId, HomeGroupId (join table)
      UserRole.cs                — UserId, RoleId (join table)
      Attendance.cs              — PersonId? (nullable), UserId? (nullable), HomeGroupId,
                                   MeetingDate, WasPresent, Notes
                                   — filtered unique indexes: (PersonId, HomeGroupId, MeetingDate)
                                     WHERE PersonId IS NOT NULL, і аналогічно для UserId
      AttendanceMeta.cs          — Id, HomeGroupId, MeetingDate, GuestCount, GuestInfo?
      HomeGroupCustomField.cs    — Id, HomeGroupId, Name, CreatedAt
      PersonCustomFieldValue.cs  — Id, PersonId, FieldId, Value
      UserCustomFieldValue.cs    — Id, UserId, FieldId, Value (admin counterpart)
      UserActivity.cs            — Id, UserId, Type ("comment"|"status_change"|
                                   "oversight_change"|"person_converted"), Content?,
                                   AuthorId?, status/value inline fields, CreatedAt
      GroupEvent.cs              — Id, HomeGroupId, Name, Month, Day, Year?, CreatedAt
      GroupNeed.cs               — Id, HomeGroupId, SubjectName, Description,
                                   Status (active|answered|irrelevant),
                                   PersonId? (FK → Person), UserId? (FK → User), CreatedAt
      Room.cs                    — Id, Name
      CalendarEvent.cs           — Id, Title, Description?, Location?, RoomId?,
                                   Type (Recurring|Global|HomeGroup|Google), HomeGroupId?,
                                   IsRecurring, RecurringDayOfWeek? (int, 0=Sun..6=Sat),
                                   StartTime?, EndTime?, Date?, GoogleEventId?,
                                   IsHomeGroupMeeting?, MovedFromDate?, MovedToDate?, CreatedAt
                                   — MovedFromDate/MovedToDate = bidirectional links between
                                     weeks for rescheduled meetings (see Schedule overrides)
      PlanTemplate.cs            — Id, Name, Blocks[], CreatedAt
      PlanTemplateBlock.cs       — Id, TemplateId, Order, Time, Title, Info?, Responsible?
      HomeMeetingPlan.cs         — Id, HomeGroupId, MeetingDate, OriginalMeetingDate?,
                                   AppliedTemplateName?, Blocks[], UpdatedAt
                                   — OriginalMeetingDate set when plan was moved with a
                                     rescheduled meeting; used to restore on Schedule reset
      MeetingPlanBlock.cs        — Id, PlanId, Order, Time, Title, Info?, Responsible?
    DTOs/
      Auth/AuthDtos.cs
      Groups/GroupDtos.cs        — GroupResponse (+TelegramGroupId), CreateGroupRequest,
                                   UpdateGroupRequest (+TelegramGroupId),
                                   SetNextMeetingRequest(Date, OldDate?),
                                   GroupCustomFieldDto, CreateGroupCustomFieldRequest,
                                   NotifSettingsDto, UpdateNotifSettingsRequest,
                                   GroupNeedDto(Id, SubjectName, Description, Status, CreatedAt, PersonId?, UserId?),
                                   CreateGroupNeedRequest(SubjectName, Description, PersonId?, UserId?),
                                   UpdateGroupNeedRequest(SubjectName, Description, Status, PersonId?, UserId?)
      Groups/GroupCabinetDto.cs  — GroupCabinetResponse (+HasPlanForNextMeeting),
                                   CabinetGroupInfo (+TelegramGroupId),
                                   CabinetOrgMember (+CabinetRoleTag?),
                                   CabinetRoleTag(Name, Color),
                                   CabinetAttendanceSummary, CabinetUpcomingEvent,
                                   CabinetOverseePerson,
                                   CabinetStats(AvgAttendanceRate, PrevAvgAttendanceRate,
                                     NewMembers, PrevNewMembers, TotalMembers, PrevTotalMembers),
                                   GroupEventDto, CreateGroupEventRequest,
                                   AllNeedsDto(Id, HomeGroupId, GroupName, GroupColor,
                                     SubjectName, Description, Status, CreatedAt, PersonId?, UserId?)
      Groups/GroupStatsDto.cs    — GroupStatsResponse,
                                   StatsSummary(AvgAttendanceRate, PrevAvgAttendanceRate,
                                     MeetingCount, TotalGuests,
                                     NewMembers, PrevNewMembers, TotalMembers, PrevTotalMembers),
                                   MeetingStatsItem, PersonAttendanceStat
      People/PersonDtos.cs       — CreatePersonRequest, UpdatePersonRequest (всі поля),
                                   PersonResponse, PersonDetailResponse (з розширеними полями),
                                   CustomFieldDto
      PersonStatuses/PersonStatusDtos.cs — PersonStatusDto(Id,Name,Color),
                                   CreatePersonStatusRequest, UpdatePersonStatusRequest
      Admins/AdminDtos.cs        — AdminResponse (всі поля профілю + roles + groups),
                                   CreateAdminRequest, UpdateAdminRequest,
                                   UpdateAdminProfileRequest, SetPasswordRequest
      Roles/RoleDtos.cs
      Attendance/AttendanceDtos.cs — RecordAttendanceRequest, AttendanceEntry (personId? | userId?),
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

### Next Meeting Override (legacy)
`HomeGroupEntity.NextMeetingOverrideDate` (string?, "yyyy-MM-dd") — застаріле поле, конвертується
при старті в CalendarEvent override (`MigrateLegacyScheduleData` у `Program.cs`).
- `PUT /groups/:id/next-meeting` (cabinet's "Перенести" button) ще працює, але створює
  CalendarEvent override напряму без використання поля
- `PUT /groups/:id/skip-meeting` — бекенд сам обчислює наступний коректний день тижня

### Schedule Overrides (новий механізм)
Перенос/скасування зустрічей живуть як non-recurring HomeGroup `CalendarEvent`:
- `IsHomeGroupMeeting = true` + `Date = X` — реальна зустріч (override)
- `IsHomeGroupMeeting = false` + `Date = X` — маркер скасування / "тінь" від переносу
- `IsHomeGroupMeeting = null` — звичайне бронювання кімнати (не впливає на schedule)

Bidirectional links через `MovedFromDate` / `MovedToDate`:
- При переносі пт → чт: створюється real event на чт з `MovedFromDate=пт` + shadow event
  на пт з `MovedToDate=чт`
- При `reset-week` лінки використовуються щоб очистити обидва тижні разом

`ScheduleController` (`/api/v1/groups/:id/schedule`):
- `GET ?from=&to=` → `ScheduleWeekDto[]` (один запис на тиждень з status: default |
  cancelled | rescheduled_internal | moved_in | moved_out + effectiveDate + movedFromDate/movedToDate +
  hasPlan + attendanceRecordCount)
- `POST /cancel { date }`, `DELETE /cancel?date=` — manual cancel/uncancel
- `POST /move { fromDate, toDate, movePlan, moveAttendance }` — переміщення зустрічі
  (опціонально з планом і записами відвідуваності)
- `POST /reset-week { weekStart, restorePlan }` — повне очищення тижня (видаляє зв'язаний
  тиждень якщо є; повертає план через `OriginalMeetingDate` якщо restorePlan=true)

Permission: `groups.schedule.manage`

### AttendanceMeta
`AttendanceMeta` — метаінформація про зустріч (окремо від per-person записів):
- Унікальний ключ: (HomeGroupId, MeetingDate)
- `GuestCount` — кількість гостей на зустрічі
- `GuestInfo` — довільний текст про гостей

### Plans & Templates
- `PlanTemplate` — глобальний шаблон плану (не прив'язаний до групи)
- `HomeMeetingPlan` — план конкретної зустрічі (HomeGroupId + MeetingDate = унікальний)
- Унікальний індекс на `(HomeGroupId, MeetingDate)` для HomeMeetingPlan

### Group Needs
`GroupNeed` — потреби/молитовні запити прив'язані до групи:
- `SubjectName` — ім'я людини (заголовок картки, вільний текст або заповнюється з вибраного члена)
- `Description` — текст потреби
- `Status` — `active` (активна) | `answered` (отримана відповідь) | `irrelevant` (не актуальна)
- `PersonId?` — FK на Person (опціонально, при виборі члена групи з People)
- `UserId?` — FK на User (опціонально, при виборі адміна з групи)
- При наявності PersonId/UserId — ім'я на картці стає клікабельним посиланням на профіль

### Notification Settings
`HomeGroupEntity.NotifSettingsJson` (string?, text) — JSON-об'єкт з налаштуваннями сповіщень Telegram:
- Ключі: `event_7days`, `event_day`, `conflict`, `conflict_resolved`, `attendance_ask`
- Дефолт для всіх: `true`
- API повертає camelCase: `eventSevenDays`, `eventDay`, `conflict`, `conflictResolved`, `attendanceAsk`
- Бот читає налаштування через API (не локальний JSON-файл) і перевіряє перед кожним типом сповіщення

## API Endpoints

### Auth
```
POST /api/v1/auth/login
```

### People
```
GET    /api/v1/people                          — ?search=&noGroup=&includeAdmins=&myOversight=
                                                 → GroupMemberResponse[] (persons + optional admins)
GET    /api/v1/people/:id                      → PersonDetailResponse (з customFields)
POST   /api/v1/people
PUT    /api/v1/people/:id                      — синхронізує HomeGroupMembers
DELETE /api/v1/people/:id

GET    /api/v1/people/:id/activity             → PersonActivityDto[] (comments + status_change, desc)
POST   /api/v1/people/:id/comments             — { content } → PersonActivityDto

POST   /api/v1/people/:id/custom-fields
PUT    /api/v1/people/:id/custom-fields/:fieldId
DELETE /api/v1/people/:id/custom-fields/:fieldId

GET    /api/v1/people/:id/convert-to-admin/preview
                                               → ConvertToAdminPreview (counts of what migrates)
                                               [people.convertToAdmin]
POST   /api/v1/people/:id/convert-to-admin     — { email, password, roleIds[], primaryGroupId?, visibleGroupIds[] }
                                               → newAdminId (long) [people.convertToAdmin]
                                               Transaction: creates User з усіх профільних полів,
                                               мігрує Attendance/GroupNeeds/GroupMemberHistory
                                               (PersonId→UserId), копіює PersonCustomFieldValue→
                                               UserCustomFieldValue + PersonActivity→UserActivity,
                                               видаляє HomeGroupMembers + Person. Логує
                                               UserActivity з Type="person_converted".
```

### Admins
```
GET    /api/v1/admins/me                       → AdminResponse (поточний юзер)
POST   /api/v1/admins/me/set-password         — { newPassword } — БЕЗ RequirePermission, будь-який auth юзер
GET    /api/v1/admins/me/dashboard             → WidgetConfig[] (конфіг дашборду поточного юзера)
PUT    /api/v1/admins/me/dashboard             — { config: WidgetConfig[] } → 204
GET    /api/v1/admins/me/tasks                 → AdminTaskDto[] (мої задачі, без permission check)
PATCH  /api/v1/admins/me/tasks/:taskId/toggle  → AdminTaskDto (тогл власної задачі, без permission check)
GET    /api/v1/admins                          → AdminResponse[] [settings.admins]
GET    /api/v1/admins/:id                      → AdminResponse (з customFields)
POST   /api/v1/admins                          — { name, lastName?, email, password, roleIds[], primaryGroupId?, visibleGroupIds[] } [settings.admins]
PUT    /api/v1/admins/:id                      — { name, lastName?, email, roleIds[], primaryGroupId?, visibleGroupIds[] } [settings.admins]
PUT    /api/v1/admins/:id/profile              — { phone?, telegram?, gender?, maritalStatus?, address?,
                                                   dateOfBirth?, isBaptized, church?, ministry?,
                                                   isBaptizedWithSpirit, personStatusId? }
POST   /api/v1/admins/:id/set-password        — { newPassword } [settings.admins]
DELETE /api/v1/admins/:id                      [settings.admins]

GET    /api/v1/admins/:id/activity             → PersonActivityDto[] [admins.viewProfiles]
POST   /api/v1/admins/:id/comments             — { content } → PersonActivityDto [admins.viewProfiles]
DELETE /api/v1/admins/:id/activity/:entryId    [admins.viewProfiles]

POST   /api/v1/admins/:id/custom-fields        — { name } [people.customFields]
                                                 створює HomeGroupCustomField в адміновій PrimaryGroup
PUT    /api/v1/admins/:id/custom-fields/:fieldId — { value } → upsert UserCustomFieldValue [people.customFields]
DELETE /api/v1/admins/:id/custom-fields/:fieldId [people.customFields]
```

**Dashboard config**: `WidgetConfig[] = [{id: string, enabled: bool}]` — зберігається в `User.DashboardConfigJson` як text.
Порожній масив → фронт показує дефолтні віджети.


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
GET    /api/v1/groups/stats/all?period=1m|3m|6m → GroupStatsResponse (всі групи агреговано)
GET    /api/v1/groups/:id/stats?period=1m|3m|6m → GroupStatsResponse
GET    /api/v1/groups/all-needs?groupId=&status=active
                                               → AllNeedsDto[] (активні потреби з усіх видимих
                                                 груп або конкретної групи, з GroupName/Color)
                                                 [page.cabinet]

GET    /api/v1/groups/:id/events
POST   /api/v1/groups/:id/events               — { name, month, day, year? }
PUT    /api/v1/groups/:id/events/:eventId      — { name, month, day, year? }
DELETE /api/v1/groups/:id/events/:eventId

GET    /api/v1/groups/:id/plans
GET    /api/v1/groups/:id/plans/date/:date
POST   /api/v1/groups/:id/plans                — upsert (видаляє старі блоки, додає нові)
DELETE /api/v1/groups/:id/plans/date/:date

PUT    /api/v1/groups/:id/next-meeting         — { date?, oldDate? } → override + опційно переміщає план
PUT    /api/v1/groups/:id/skip-meeting         → обчислює наступний день тижня після поточного next-meeting

GET    /api/v1/groups/:id/notif-settings       → NotifSettingsDto [page.cabinet]
PUT    /api/v1/groups/:id/notif-settings       — { eventSevenDays, eventDay, conflict,
                                                   conflictResolved, attendanceAsk } [page.cabinet]

GET    /api/v1/groups/:id/needs                → GroupNeedDto[] [page.cabinet]
POST   /api/v1/groups/:id/needs                — { subjectName, description, personId?, userId? } [groups.events.manage]
PUT    /api/v1/groups/:id/needs/:needId        — { subjectName, description, status, personId?, userId? } [groups.events.manage]
DELETE /api/v1/groups/:id/needs/:needId        [groups.events.manage]
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
GET  /api/v1/attendance/summary     — ?groupId= → AttendanceSummary[] (per date)
GET  /api/v1/attendance/dates       — ?groupId= → string[] (union of all known meeting
                                      dates: Attendances + AttendanceMeta + CalendarEvent
                                      real-meeting overrides, sorted desc)
POST /api/v1/attendance             — { homeGroupId, meetingDate,
                                        entries: [{personId?, userId?, wasPresent}] }
POST /api/v1/attendance/bulk        — { homeGroupId, entries: [{date, personId?, userId?, wasPresent}] }
GET  /api/v1/attendance/meta        — ?groupId=&date= → { guestCount, guestInfo, notes, isCancelled }
POST /api/v1/attendance/meta        — { homeGroupId, meetingDate, guestCount, guestInfo?, notes?, isCancelled }
GET  /api/v1/attendance/dots        — ?groupId=&limit=5 → AttendanceDotsResponse
                                      фільтрує MeetingDate <= today
DELETE /api/v1/attendance/meeting   — ?groupId=&date= (тільки майбутні зустрічі)
```

### Attendance Import/Export (Excel)
```
GET  /api/v1/attendance/export        — ?groupIds=1,2,3&from=&to= → .xlsx (multi-sheet)
GET  /api/v1/attendance/template      — ?groupIds=1,2,3&from=&to= → .xlsx
                                         (з періодом + MeetingDay → генерує порожні колонки
                                          по днях зустрічей; без періоду → лише люди)
POST /api/v1/attendance/import/preview — multipart file → ImportPreviewResponse
                                          [attendance.record]
POST /api/v1/attendance/import/apply   — { importId, sheets: [...] } → ImportApplyResponse
                                          [attendance.record]
```

Excel структура (поточний експорт):
- Cols: `% | ID(hidden) | ПІБ | Статус | Опіка | Дата приєднання | dates →`
- Rows (в порядку): `1`=group name + dates | `2`=Загалом | `5`=Нові/невіруючі/гості |
  `6`=Нотатки | `8`=header (% / ID / ПІБ / Статус / Опіка / Дата приєднання) | `9+`=люди
- Cell values: `1`=присутній, `0`=відсутній, `-`=скасована зустріч, порожньо=ще не приєднався/пішов
- Past members: червоний фон, LeftAt з `GroupMemberHistory`
- ID hint: `p:123`(Person) / `u:5`(User) — для round-trip matching

Парсер дополнительно знаходить рядок «З інших домашок» зі **старих** Google Sheets файлів
(label-based dynamic search) і додає його значення до `GuestCount`. У наших нових експортах
цього рядка немає.

Template + period: якщо `from` і `to` задано і у групи є `MeetingDay`, `MergeRecurringDates`
додає колонку для кожного дня тижня в діапазоні (скіпає `MovedToDate` shadows).

Import logic:
- Sheet → group: exact name → contains → leader name fuzzy
- Person → match: ID hint → exact name in group → past members → unknown (з suggestions)
- "Нові/гості" + "З інших домашок" (legacy) суммуются в `AttendanceMeta.GuestCount`
- Конфлікти: attendance value / cancellation / guests count / notes
  Key: `(type, date, personRowIndex?)`. Default рішення = useFile.
- LeftAt detection: остання непуста дата + ≥2 порожніх після неї → пропонується кандидат
- Status / Oversight / JoinedAt / LeftAt — окремі тогли в `ImportSheetDecision`, default OFF
- Pending preview зберігається в `AttendanceImports` (PayloadJson, ExpiresAt = +2h)
- `ParsedSheet` records використовують string-keyed Dictionary (`yyyy-MM-dd`) щоб обійти
  System.Text.Json не-підтримку `DateOnly` як ключа dict

### Schedule (per-group)
```
GET    /api/v1/groups/:id/schedule        — ?from=&to= → ScheduleWeekDto[]
POST   /api/v1/groups/:id/schedule/cancel — { date } [groups.schedule.manage]
DELETE /api/v1/groups/:id/schedule/cancel — ?date= [groups.schedule.manage]
POST   /api/v1/groups/:id/schedule/move   — { fromDate, toDate, movePlan, moveAttendance }
                                            [groups.schedule.manage]
POST   /api/v1/groups/:id/schedule/reset-week — { weekStart, restorePlan } [groups.schedule.manage]
```

### Calendar
```
GET    /api/v1/calendar             — ?from=yyyy-MM-dd&to=yyyy-MM-dd&types=Recurring,Global,HomeGroup&groupIds=1,2
                                      → CalendarOccurrenceDto[] (recurring events expanded per day)
GET    /api/v1/calendar/events      → CalendarEventDto[] (all event definitions)
GET    /api/v1/calendar/events/:id  → CalendarEventDto
POST   /api/v1/calendar/events      — { title, description?, location?, roomId?, type, homeGroupId?,
                                        isRecurring, recurringDayOfWeek?, startTime?, endTime?, date? }
PUT    /api/v1/calendar/events/:id
DELETE /api/v1/calendar/events/:id
```

### Google Calendar
```
POST   /api/v1/google-calendar/sync — fetches events from Google Calendar, upserts as Type=Google
                                      preserves existing RoomId, deletes removed events
                                      env: Google:CalendarId, Google:ServiceAccountJson
```

### Rooms
```
GET    /api/v1/rooms
POST   /api/v1/rooms                — { name }
PUT    /api/v1/rooms/:id            — { name }
DELETE /api/v1/rooms/:id
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

### Dashboard (analytics)
```
GET    /api/v1/dashboard/inactive-members         — ?groupId=&minMissed=5 → InactiveMemberDto[]
                                                    люди (Person + User) з consecutiveMissed >= minMissed
                                                    за останні 6 міс (streak: рахуються пропуски
                                                    підряд з останнього запису, зупиняється на
                                                    першому WasPresent=true), сортовані desc
                                                    [attendance.view]
GET    /api/v1/dashboard/status-distribution      — ?groupId= → StatusDistributionResponse
                                                    count людей по PersonStatusId — включає
                                                    як Person так і User (без superadmin id=0);
                                                    "Без статусу" група для null [people.view]
GET    /api/v1/dashboard/groups-comparison        — ?groupIds=1,2,3&period=1m|3m|6m
                                                    → GroupComparisonSeries[]
                                                    для кожної групи: точки (date, attendanceRate)
                                                    [attendance.stats]
GET    /api/v1/dashboard/groups-attendance-summary → GroupsAttendanceSummaryResponse
                                                    список груп з totalMembers (Persons + Admins)
                                                    і avg1m/avg3m/avg6m + total row [attendance.stats]
```
Всі ендпоінти фільтруються по `UserHomeGroups` для не-superadmin (визначені у `GetVisibleGroupIds()`).

## Key Patterns

### Superuser (id = 0)
Створюється через raw SQL в `Program.cs` щоб обійти EF Core auto-increment. Параметри з env: `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`.
- Виключається з `GetCabinet` org team запиту: `&& u.Id != 0`
- Обходить фільтр видимих груп у `PeopleController`

### Permissions enforcement
`RequirePermissionAttribute` (`HomeGroup.API/Authorization/RequirePermissionAttribute.cs`) — `IAuthorizationFilter`.
- Перевіряє JWT claim `"permission"` (один claim на кожен ключ)
- Wildcard `"*"` = superadmin (повний доступ)
- Повертає 403 з `{ message: "Недостатньо прав доступу" }`

Permissions baked into JWT at login: `JwtService.GetMergedPermissions(user)` — об'єднує permissions з усіх ролей.
`AuthResponse` повертає `List<string> Permissions` — фронт зберігає в localStorage і перевіряє локально.

**Важливо**: `POST /admins/me/set-password` — без `[RequirePermission]`, будь-який авторизований юзер може змінити свій пароль. `POST /admins/:id/set-password` — потребує `settings.admins`.

### Role.PermissionsJson
Зберігається як text (не JSONB), серіалізується через extension methods `GetPermissions()` / `SetPermissions()`.

### People Visibility Filter
У `PeopleController.GetAll`: якщо користувач має `UserHomeGroups` (не superadmin) — показуються тільки люди з цих груп.
- `includeAdmins=true` → додає Users (isAdmin=true) з PrimaryGroupId у видимих групах як `GroupMemberResponse`
- `myOversight=true` → фільтрує лише Person де `OversightUserId == currentUserId` (admins не включаються)

### Admin Profile
`User` тепер має ті ж особисті поля що і `Person` (Phone, Telegram, Gender, тощо).
- `PUT /admins/:id/profile` — оновлює особисті поля (не стосується ролей/груп)
- `GET /admins/me` — поточний авторизований користувач
- `PUT /admins/:id` — оновлює name, email, roleIds, primaryGroupId, visibleGroupIds

### Groups Members — Admins
`GET /groups/:id/members` повертає як `Person` так і `User` з `PrimaryGroupId == groupId`.
Admins в результаті мають `IsAdmin=true` і `UserId` (id юзера), persons мають `IsAdmin=false`.

### Cabinet org team — roles
`GetCabinet` включає `UserRoles.ThenInclude(Role)` для orgTeam і повертає першу роль як `CabinetRoleTag(Name, Color)`.

### ComputeNextMeeting / ComputeLastMeeting
Симетричні: в день зустрічі повертають today якщо meeting time ще не настав
(або last=today якщо вже настав).
- `ComputeNextMeeting`: повертає `today.AddDays(daysUntil)`. `daysUntil=0` означає сьогодні.
  Якщо today=meeting day і `nowTime >= mt` → `daysUntil=7` (наступний тиждень).
- `ComputeLastMeeting`: дзеркально — `daysAgo=0` сьогодні, `daysAgo=7` тиждень тому.

### Cabinet stale-booking cleanup
`GetCabinet` чистить ТІЛЬКИ minулі чисті кімнатні бронювання — `IsHomeGroupMeeting IS NULL`
**і** немає `MovedFromDate`/`MovedToDate`. Schedule overrides і cancellation markers
зберігаються вічно (без цього фікса cabinet був видаляв override-и при кожному відкритті).

### Cancellation source of truth
Скасування зустрічі живе в **двох** місцях: `AttendanceMeta.IsCancelled` (legacy) і
CalendarEvent з `IsHomeGroupMeeting=false` (новий). Синхронізовані:
- `SaveMeta` викликає `SyncCancellationToCalendar` при зміні `IsCancelled`
- Schedule UI cancel → створює CalendarEvent напряму
- `MigrateLegacyScheduleData` (Program.cs) одноразово створює CalendarEvent для всіх
  існуючих `AttendanceMeta.IsCancelled=true` (idempotent — пропускає якщо вже є)

GetSchedule / GetTable читають обидва джерела і об'єднують.

### Move-out shadow vs manual cancellation
CalendarEvent з `IsHomeGroupMeeting=false`:
- Без `MovedToDate` → ручне скасування (відображається як жовтий стовпчик в таблиці)
- З `MovedToDate` → "тінь" від переносу: зустріч відбулась в інший день того ж тижня

В `GetTable` move-out shadows виключаються з усіх джерел (Attendances, Meta, Calendar)
і з генерації віртуальних стовпчиків, щоб не показувати "примарний" пт коли зустріч була в чт.

### Ghost event suppression (CalendarController)
Recurring HomeGroup events є "ghost" — прозорі події-шаблони. Ghost пригнічується якщо:
- Існує non-recurring HomeGroup event з `IsHomeGroupMeeting IS NOT NULL` (true=реальна зустріч або false=маркер скасування) для того ж тижня (Mon–Sun)

Реалізація: окремий DB-запит `suppressionQuery` на повний тижневий діапазон (Mon fromDate's week — Sun toDate's week), **незалежний від `types` фільтра**. Будує `HashSet<(HomeGroupId, WeekMonday)>` — перевіряє кожен ghost.

`IsHomeGroupMeeting` значення:
- `null` — звичайне бронювання кімнати (не впливає на ghost)
- `true` — реальна зустріч (ghost suppressed)
- `false` — маркер скасування (ghost suppressed, сама подія НЕ відображається в результатах)

### Conflict detection (GetCabinet)
`nextMeetingConflicts` — події що накладаються на час зустрічі домашки по кімнаті.
Конфлікти тільки для **не-HomeGroup** типів (`e.Type != "HomeGroup"`): Recurring, Global, Google.
Дві HomeGroup в одній кімнаті — не є конфліктом.

### lastMeetingDate (GetCabinet)
Визначається з реальних записів відвідуваності (остання дата в `Attendances` таблиці для групи),
а не з розкладу. Якщо записів немає — повертає `null` (кнопка "Відмітити" в кабінеті не показується).

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
10. `AddPersonExtendedFields` — Person: Telegram, Gender, MaritalStatus, Address,
    IsBaptized, Church, Ministry, IsBaptizedWithSpirit
11. `AddAdminProfileFields` — User: Phone, Telegram, Gender, MaritalStatus, Address, DateOfBirth,
    IsBaptized, Church, Ministry, IsBaptizedWithSpirit, PersonStatusId (FK)
    + Attendance: PersonId → nullable, нове поле UserId (nullable)
    + filtered unique indexes на Attendance (PersonId WHERE NOT NULL, UserId WHERE NOT NULL)
12. `AddCalendarAndRooms` — Drop ChurchEvents, create Rooms table, create CalendarEvents table
    (Type: Recurring|Global|HomeGroup, IsRecurring, RecurringDayOfWeek, StartTime, EndTime, Date)
13. `AddRoomFields` — Room: Building (default "Church"), Floor (default 1), Color (default "#6B7280")
14. `AddGoogleCalendarSync` — CalendarEvent: GoogleEventId (nullable string)
15. `AddDashboardConfig` — User: DashboardConfigJson (text nullable)
16. `AddPersonActivity` — PersonActivities table (Id, PersonId FK, Type, Content?, AuthorId? FK,
    OldStatus*/NewStatus* inline fields, CreatedAt)
17. `AddGroupNotifSettings` — HomeGroupEntity.NotifSettingsJson (text nullable)
18. `AddGroupNeeds` — GroupNeeds table (Id, HomeGroupId FK, SubjectName, Description, Status, CreatedAt)
19. `AddGroupNeedPersonLink` — GroupNeed: PersonId? (FK → People), UserId? (FK → Users)
20. `AddScheduleOverrides` — CalendarEvent: MovedFromDate?, MovedToDate? (DateOnly nullable)
    + HomeMeetingPlan: OriginalMeetingDate? (text nullable)
21. `AddAttendanceImports` — AttendanceImports table (Id, CreatedByUserId? FK, PayloadJson text,
    CreatedAt, ExpiresAt) — pending stage для двоетапного імпорту Excel
22. `AddUserActivityFieldsAndUserCustomFieldValues` —
    + UserActivities: Content, OldValue, NewValue (nullable text)
    + UserCustomFieldValues table (Id, UserId FK, FieldId FK, Value?) для адмінських значень
    кастомних полів. Unique (UserId, FieldId).

## Startup Data Migrations (Program.cs `MigrateLegacyScheduleData`)
Запускається при старті після `Database.Migrate()`:
- Конвертує застарілі `HomeGroupEntity.NextMeetingOverrideDate` (майбутні) в CalendarEvent
  з `IsHomeGroupMeeting=true`, після чого зануляє поле
- Створює CalendarEvent з `IsHomeGroupMeeting=false` для всіх існуючих
  `AttendanceMeta.IsCancelled=true` що ще не мають парного маркера
Обидві операції idempotent — пропускають якщо CalendarEvent вже існує.

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
GOOGLE_CALENDAR_ID=your-calendar-id@group.calendar.google.com
GOOGLE_SERVICE_ACCOUNT_JSON={"type":"service_account",...}  # inline JSON або mount файлу
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
- [x] Extended Person fields (Telegram, Gender, MaritalStatus, Address,
      IsBaptized, Church, Ministry, IsBaptizedWithSpirit)
- [x] Admins CRUD (AdminsController: getAll, getById, getMe, create, update, updateProfile,
      setPassword, delete)
- [x] Admin profile fields on User entity (same personal fields as Person)
- [x] Mixed attendance — Attendance.PersonId nullable + UserId nullable, filtered unique indexes
- [x] GET /people includeAdmins + myOversight params → GroupMemberResponse[]
- [x] GET /groups/:id/members includes admins with PrimaryGroupId == groupId
- [x] Calendar — unified CalendarEvent (Recurring/Global/HomeGroup/Google types, recurring expansion in GET)
- [x] Rooms CRUD (Id, Name, Building, Floor, Color — conflict detection in frontend)
- [x] Auto-sync CalendarEvent (Type=HomeGroup) on HomeGroup create/update from MeetingDay/MeetingTime
- [x] Google Calendar sync — POST /api/v1/google-calendar/sync via Service Account JSON
      CalendarEventType.Google=3, GoogleEventId tracks source, RoomId preserved on re-sync
- [x] Ghost suppression — окремий suppressionQuery на повний тижневий діапазон (Mon–Sun),
      незалежний від types фільтра; IsHomeGroupMeeting (null/true/false) керує ghost visibility
- [x] Conflict detection — тільки Recurring/Global/Google, не HomeGroup vs HomeGroup
- [x] lastMeetingDate — з реальних Attendances записів, не з ComputeLastMeeting по розкладу
- [x] Dashboard config per user — User.DashboardConfigJson + GET/PUT /admins/me/dashboard
- [x] GET /groups/stats/all — агрегована статистика по всіх групах для дашборду
- [x] POST /groups/:id/plans/date/:date/send-to-telegram — надсилає план в Telegram групу через Bot API
      Формат: plain text, блоки без часу → футер, відповідальний резолвиться до @telegram через People/Users lookup
      Потребує BOT_TOKEN env var в api сервісі
- [x] GET /groups/:id/events — повертає всі події без ліміту (прибрано Take(5))
- [x] PUT /groups/:id/events/:eventId — редагування події (UpdateGroupEventRequest: name, month, day, year?)
- [x] docker-compose: WEBSITE_URL env var передається в bot сервіс
- [x] RequirePermissionAttribute — IAuthorizationFilter, перевіряє JWT claim "permission", wildcard "*"
- [x] JwtService.GetMergedPermissions — об'єднує permissions з усіх ролей, додає до JWT + AuthResponse
- [x] Всі ендпоінти захищені відповідними [RequirePermission("...")] атрибутами
- [x] POST /admins/me/set-password — без RequirePermission, будь-який юзер може змінити свій пароль
- [x] PersonActivity feed — GET /people/:id/activity, POST /people/:id/comments
      Type "comment" = ручний коментар адміна; "status_change" = системна подія при зміні статусу
      Статус зберігається inline (name+color) щоб не залежати від видалених статусів
- [x] GET /attendance/dots фільтрує MeetingDate <= today — майбутні зустрічі не потрапляють в точки
- [x] GET/PUT /groups/:id/notif-settings — налаштування Telegram-сповіщень per group [page.cabinet]
      Зберігається в HomeGroupEntity.NotifSettingsJson (text, JSON)
      Ключі: event_7days, event_day, conflict, conflict_resolved, attendance_ask (дефолт: всі true)
      Бот читає через API (не локальний файл), планувальник перевіряє перед кожним сповіщенням
- [x] GET/POST/PUT/DELETE /groups/:id/needs — потреби групи [page.cabinet / groups.events.manage]
      GroupNeed: SubjectName, Description, Status (active|answered|irrelevant), PersonId?, UserId?
      Блок у кабінеті: статус-тег з dropdown, олівчик і урна
      PersonId/UserId — опціональне прив'язання до Person або User:
        Mobile: кнопка "З групи" → picker-popup зі списком членів + пошук
        Desktop: antd Select з showSearch, lazy-load при відкритті модалки
        Якщо прив'язано — ім'я на картці стає посиланням → /people/:id або /admins/:id
- [x] ScheduleController — `/api/v1/groups/:id/schedule` (GET weeks, cancel, move, reset-week)
      [groups.schedule.manage] — новий механізм для переносу/скасування зустрічей за тижнями
- [x] CalendarEvent.MovedFromDate + MovedToDate — bidirectional links між тижнями
- [x] HomeMeetingPlan.OriginalMeetingDate — план повертається на оригінальну дату при reset-week
- [x] Move endpoint опційно переносить Attendance + AttendanceMeta records разом з зустріччю
- [x] Startup data migration — конвертує NextMeetingOverrideDate + AttendanceMeta.IsCancelled
      в CalendarEvent overrides одноразово
- [x] GetTable виключає move-out shadows з усіх джерел (attendance/meta/calendar) і з генерації
      віртуальних стовпчиків — переїхані дні не показуються як примарні стовпчики
- [x] GetCabinet stale-booking cleanup НЕ видаляє schedule overrides (IsHomeGroupMeeting != null
      або MovedFromDate/MovedToDate set)
- [x] ComputeNextMeeting fix — в день зустрічі повертає today якщо meeting time ще не настав
      (раніше через ламаний тернарник завжди стрибав на наступний тиждень)
- [x] GET /attendance/dates — union усіх відомих дат (Attendance + Meta + Calendar real-meetings)
- [x] Attendance Excel import/export — `/attendance/export`, `/template`, `/import/preview`, `/import/apply`
      ClosedXML, multi-sheet (sheet per group), hidden ID column для round-trip matching.
      Двоетапний імпорт: preview (зберігається в `AttendanceImports` table, ExpiresAt=+2h) →
      apply з рішеннями по конфліктах (`(type, date, personRowIndex?)` key).
      Конфлікти: attendance value / cancellation / guests / notes.
      Парсер додатково знаходить «З інших домашок» зі старих файлів і додає до `GuestCount`
      (у нашому експорті цього рядка немає).
      LeftAt detection — остання непуста дата + ≥2 порожніх після.
      Status/Oversight/JoinedAt/LeftAt — окремі тогли в decision (default OFF).
      Past members з `GroupMemberHistory.LeftAt` → червоний рядок в експорті.
- [x] Template + period → генерує колонку на кожний `MeetingDay` в діапазоні через
      `MergeRecurringDates` (move-out shadows виключаються). Дозволяє юзеру одразу мати
      бланк на backfill-період.
- [x] Convert Person → Admin — `POST /people/:id/convert-to-admin` [people.convertToAdmin].
      Транзакція: створює User з усіх профільних полів Person, мігрує Attendance/
      GroupNeeds/GroupMemberHistory (PersonId→UserId), копіює PersonCustomFieldValue→
      UserCustomFieldValue + PersonActivity→UserActivity, видаляє HomeGroupMembers +
      Person. Логує `UserActivity` з Type="person_converted".
      Preview endpoint `GET /people/:id/convert-to-admin/preview` для UI попередження
      (counts + email availability).
      Втрачається: Person.OversightUserId (адмін не має оверсайта).
- [x] Admin activity feed — `GET/POST/DELETE /admins/:id/activity`, `POST /admins/:id/comments`
      [admins.viewProfiles]. UserActivity entity розширено Content/OldValue/NewValue.
- [x] Admin custom fields — `POST/PUT/DELETE /admins/:id/custom-fields[/:fieldId]`
      [people.customFields]. Definitions reuse `HomeGroupCustomField` (per-group), values
      live в новій `UserCustomFieldValue` table (UserId, FieldId, Value).
      `AdminResponse` тепер містить `customFields`.
- [x] Dashboard analytics — `DashboardController` з ендпоінтами для нових віджетів:
      `inactive-members` (consecutive streak 5+ підряд за 6 міс, рахує Person + User),
      `status-distribution` (pie chart по статусам, Person + User),
      `groups-comparison` (line chart порівняння домашок), `groups-attendance-summary`
      (таблиця домашок з 1м/3м/6м + total). Все фільтрується по `UserHomeGroups`.
- [x] My tasks dashboard widget — `GET /admins/me/tasks` + `PATCH /admins/me/tasks/:id/toggle`
      без RequirePermission (своя дата). Решта tasks CRUD залишилась на `/admins/:id/tasks/...`
      з `admins.viewProfiles`.
- [x] Frontend dashboard widgets (Vite/React/antd-mobile):
      MyTasks, MyOversight (peopleApi + dots reused), InactiveMembers (group filter),
      GroupsComparison (SVG line chart, multi-select chips, period toggle),
      StatusDistribution (SVG donut з center stat), GroupsAttendanceSummary (table з total row),
      RandomNeed (випадкова активна потреба, group filter, ↺ Нова, статус-зміна).
      Registered у `widgetRegistry.ts` + desktop/mobile WIDGET_COMPONENTS maps.
      FULL_WIDTH_WIDGETS desktop: groupStats, groupsComparison, groupsAttendanceSummary, inactiveMembers.
- [x] CabinetStats оновлено: фіксований 3-місячний вікон, `NewMembers` через
      `GroupMemberHistory.JoinedAt` (не `Person.CreatedAt`), `TotalMembers` включає
      адмінів (`Users.PrimaryGroupId`). Кожна стата має `Prev*` поле для порівняння
      з попереднім 3-місячним вікном. `CalcAvgAttendanceRate` хелпер — per-meeting avg rate.
- [x] StatsSummary (GetStats/GetStatsAll) аналогічно оновлено: prev period, totalMembers,
      newMembers через GroupMemberHistory.JoinedAt. Prev period = той самий проміжок до current.
- [x] `GET /groups/all-needs` — нові активні потреби з усіх видимих груп або конкретної,
      з вбудованим GroupName/Color. `GetVisibleGroupIds()` хелпер додано в GroupsController.
- [x] Stats page (desktop/mobile) — додано картки "Під ризиком" (3+ пропуски, незалежно
      від вибраного періоду) та "Розподіл за статусом" (SVG donut для цієї групи).
      Фікс: прогрес-бари в "Рейтинг присутності" на десктопі більше не вилізають за картку.

## TODO

- [ ] Опіка (Oversight) — configurable list
- [ ] Refresh tokens
- [ ] Pagination для великих списків
- [ ] Swagger / OpenAPI
- [ ] Логування (Serilog)
- [ ] Health check `/health`
