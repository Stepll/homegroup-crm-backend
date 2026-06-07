using System.Security.Claims;
using System.Text.Json;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Attendance;
using HomeGroup.API.Models.Entities;
using HomeGroup.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class AttendanceImportExportController(AppDbContext db, AttendanceExcelService excel) : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // ============================================================
    // EXPORT
    // ============================================================

    [HttpGet("export")]
    [RequirePermission("attendance.view")]
    public async Task<IActionResult> Export(
        [FromQuery] string groupIds,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var ids = ParseIds(groupIds);
        if (ids.Length == 0) return BadRequest(new { message = "Не вказано групи" });

        var bytes = await excel.Export(ids, from, to, isTemplate: false);
        var filename = $"attendance-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
        return File(bytes, XlsxMime, filename);
    }

    [HttpGet("template")]
    [RequirePermission("attendance.view")]
    public async Task<IActionResult> Template([FromQuery] string groupIds)
    {
        var ids = ParseIds(groupIds);
        if (ids.Length == 0) return BadRequest(new { message = "Не вказано групи" });

        var bytes = await excel.Export(ids, from: null, to: null, isTemplate: true);
        var filename = $"attendance-template-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
        return File(bytes, XlsxMime, filename);
    }

    // ============================================================
    // IMPORT PREVIEW
    // ============================================================

    [HttpPost("import/preview")]
    [RequirePermission("attendance.record")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ImportPreviewResponse>> ImportPreview(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Файл не передано" });

        List<AttendanceExcelService.ParsedSheet> sheets;
        try
        {
            await using var stream = file.OpenReadStream();
            sheets = excel.Parse(stream);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Помилка читання файлу: {ex.Message}" });
        }

        if (sheets.Count == 0)
            return BadRequest(new { message = "У файлі не знайдено жодної таблиці з відомою структурою" });

        var allGroups = await db.HomeGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new GroupOption(g.Id, g.Name))
            .ToListAsync();

        var sheetPreviews = new List<ImportSheetPreview>();
        foreach (var sheet in sheets)
        {
            var matchedGroup = await FindGroupBySheetName(sheet.Name, allGroups);
            var preview = await BuildSheetPreview(sheet, matchedGroup);
            sheetPreviews.Add(preview);
        }

        var importId = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(sheets);
        var record = new AttendanceImport
        {
            CreatedByUserId = GetUserId(),
            PayloadJson = payload,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
        };
        db.AttendanceImports.Add(record);
        await db.SaveChangesAsync();

        // CleanExpired
        var stale = await db.AttendanceImports
            .Where(i => i.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        if (stale.Count > 0)
        {
            db.AttendanceImports.RemoveRange(stale);
            await db.SaveChangesAsync();
        }

        return Ok(new ImportPreviewResponse(
            record.Id.ToString(),
            record.ExpiresAt,
            sheetPreviews,
            allGroups));
    }

    private async Task<GroupOption?> FindGroupBySheetName(string sheetName, List<GroupOption> all)
    {
        var trimmed = sheetName.Trim();
        var exact = all.FirstOrDefault(g => string.Equals(g.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var contains = all.FirstOrDefault(g =>
            trimmed.Contains(g.Name, StringComparison.OrdinalIgnoreCase) ||
            g.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        if (contains is not null) return contains;

        // Match by leader name in sheet name
        var groups = await db.HomeGroups
            .Where(g => g.IsActive && g.LeaderId != null)
            .Include(g => g.Leader)
            .ToListAsync();
        var byLeader = groups.FirstOrDefault(g => g.Leader != null &&
            (trimmed.Contains(g.Leader.Name, StringComparison.OrdinalIgnoreCase) ||
             (g.Leader.LastName != null && trimmed.Contains(g.Leader.LastName, StringComparison.OrdinalIgnoreCase))));
        return byLeader is null ? null : new GroupOption(byLeader.Id, byLeader.Name);
    }

    private async Task<ImportSheetPreview> BuildSheetPreview(
        AttendanceExcelService.ParsedSheet sheet,
        GroupOption? matchedGroup)
    {
        var conflicts = new List<ImportConflict>();
        int newAtt = 0, updAtt = 0, newMeet = 0, cancMeet = 0, newMeta = 0, updMeta = 0;

        // Date previews
        var datePreviews = new List<ImportDatePreview>();
        if (matchedGroup is not null)
        {
            var groupId = matchedGroup.Id;
            var validDates = sheet.Dates.Where(d => d != null).Select(d => DateOnly.Parse(d!)).ToList();

            var existingMetas = await db.AttendanceMetas
                .Where(m => m.HomeGroupId == groupId && validDates.Contains(m.MeetingDate))
                .ToListAsync();
            var metaByDate = existingMetas.ToDictionary(m => m.MeetingDate);

            var existingCancellations = (await db.CalendarEvents
                .Where(e => e.HomeGroupId == groupId
                    && e.Type == CalendarEventType.HomeGroup
                    && !e.IsRecurring
                    && e.IsHomeGroupMeeting == false
                    && e.MovedToDate == null
                    && e.Date != null
                    && validDates.Contains(e.Date!.Value))
                .Select(e => e.Date!.Value)
                .ToListAsync()).ToHashSet();

            var dbDatesAny = new HashSet<DateOnly>();
            dbDatesAny.UnionWith(metaByDate.Keys);
            dbDatesAny.UnionWith(existingCancellations);
            dbDatesAny.UnionWith(await db.Attendances
                .Where(a => a.HomeGroupId == groupId && validDates.Contains(a.MeetingDate))
                .Select(a => a.MeetingDate).Distinct().ToListAsync());

            for (int i = 0; i < sheet.Dates.Count; i++)
            {
                var iso = sheet.Dates[i];
                if (iso is null) continue;
                var date = DateOnly.Parse(iso);

                var fileCancelled = sheet.CancelledByDate.GetValueOrDefault(iso);
                bool? dbCancelled = metaByDate.TryGetValue(date, out var m) ? m.IsCancelled : (bool?)null;
                if (existingCancellations.Contains(date)) dbCancelled = true;

                var fileGuests = sheet.GuestsByDate.GetValueOrDefault(iso)
                                 + sheet.OtherGroupsByDate.GetValueOrDefault(iso);
                int? dbGuests = metaByDate.TryGetValue(date, out var m2) ? m2.GuestCount : (int?)null;

                var fileNotes = sheet.NotesByDate.GetValueOrDefault(iso);
                string? dbNotes = null;
                if (metaByDate.TryGetValue(date, out var m3))
                    dbNotes = string.Join("\n", new[] { m3.GuestInfo, m3.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(dbNotes)) dbNotes = null;

                var existedInDb = dbDatesAny.Contains(date);
                if (!existedInDb) newMeet++;
                if (fileCancelled && !(dbCancelled ?? false)) cancMeet++;

                if (dbCancelled.HasValue && dbCancelled.Value != fileCancelled)
                    conflicts.Add(new ImportConflict(conflicts.Count, "cancellation",
                        iso, null, null,
                        fileCancelled ? "скасована" : "проведена",
                        dbCancelled.Value ? "скасована" : "проведена"));

                if (dbGuests.HasValue && dbGuests.Value != fileGuests && (fileGuests > 0 || dbGuests > 0))
                    conflicts.Add(new ImportConflict(conflicts.Count, "guests",
                        iso, null, null,
                        fileGuests.ToString(), dbGuests.Value.ToString()));

                if (!string.IsNullOrWhiteSpace(fileNotes) && !string.IsNullOrWhiteSpace(dbNotes)
                    && !string.Equals(fileNotes.Trim(), dbNotes.Trim(), StringComparison.Ordinal))
                    conflicts.Add(new ImportConflict(conflicts.Count, "notes",
                        iso, null, null,
                        Truncate(fileNotes, 100), Truncate(dbNotes, 100)));

                if (metaByDate.ContainsKey(date)) updMeta++;
                else if (fileGuests > 0 || !string.IsNullOrWhiteSpace(fileNotes) || fileCancelled) newMeta++;

                datePreviews.Add(new ImportDatePreview(
                    sheet.DateColumns[i], iso,
                    existedInDb, fileCancelled, dbCancelled,
                    fileGuests, dbGuests, fileNotes, dbNotes));
            }
        }
        else
        {
            for (int i = 0; i < sheet.Dates.Count; i++)
            {
                var iso = sheet.Dates[i];
                if (iso is null) continue;
                var fileGuests = sheet.GuestsByDate.GetValueOrDefault(iso)
                                 + sheet.OtherGroupsByDate.GetValueOrDefault(iso);
                datePreviews.Add(new ImportDatePreview(
                    sheet.DateColumns[i], iso,
                    false, sheet.CancelledByDate.GetValueOrDefault(iso),
                    null, fileGuests, null,
                    sheet.NotesByDate.GetValueOrDefault(iso), null));
            }
        }

        // People previews
        var peoplePreviews = new List<ImportPersonPreview>();
        if (matchedGroup is not null)
        {
            var groupId = matchedGroup.Id;
            foreach (var person in sheet.People)
            {
                var preview = await BuildPersonPreview(person, sheet, groupId, conflicts,
                    () => newAtt++, () => updAtt++);
                peoplePreviews.Add(preview);
            }
        }
        else
        {
            foreach (var person in sheet.People)
            {
                var counts = CountFileValues(person);
                peoplePreviews.Add(new ImportPersonPreview(
                    person.RowIndex, person.Name, person.LastName, person.IdHint,
                    null, null, "unmatched", [],
                    person.Status, person.Oversight,
                    person.JoinedAt, null,
                    counts.Present, counts.Absent));
            }
        }

        var changes = new ImportChangesSummary(newAtt, updAtt, newMeet, cancMeet, newMeta, updMeta);
        return new ImportSheetPreview(
            sheet.Index, sheet.Name,
            matchedGroup?.Id, matchedGroup?.Name,
            datePreviews, peoplePreviews, conflicts, changes);
    }


    private async Task<ImportPersonPreview> BuildPersonPreview(
        AttendanceExcelService.ParsedPerson person,
        AttendanceExcelService.ParsedSheet sheet,
        long groupId,
        List<ImportConflict> conflicts,
        Action onNewAtt,
        Action onUpdAtt)
    {
        long? matchedPersonId = null;
        long? matchedUserId = null;
        var matchType = "unmatched";

        // Try ID hint first
        if (!string.IsNullOrWhiteSpace(person.IdHint))
        {
            if (person.IdHint.StartsWith("p:", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(person.IdHint.AsSpan(2), out var pid))
            {
                if (await db.People.AnyAsync(p => p.Id == pid))
                {
                    matchedPersonId = pid;
                    matchType = "by_id";
                }
            }
            else if (person.IdHint.StartsWith("u:", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(person.IdHint.AsSpan(2), out var uid))
            {
                if (await db.Users.AnyAsync(u => u.Id == uid))
                {
                    matchedUserId = uid;
                    matchType = "by_id";
                }
            }
        }

        // Try name match within group
        if (matchedPersonId is null && matchedUserId is null)
        {
            var nameTrim = person.Name.Trim();
            var lastTrim = person.LastName?.Trim();

            var personMatches = await db.HomeGroupMembers
                .Where(m => m.HomeGroupId == groupId)
                .Include(m => m.Person)
                .Where(m => m.Person.Name == nameTrim
                    && (lastTrim == null || m.Person.LastName == lastTrim))
                .Select(m => m.Person)
                .ToListAsync();
            var userMatches = await db.UserHomeGroups
                .Where(u => u.HomeGroupId == groupId)
                .Include(u => u.User)
                .Where(u => u.User.Name == nameTrim
                    && (lastTrim == null || u.User.LastName == lastTrim))
                .Select(u => u.User)
                .ToListAsync();

            // Also check past members
            var pastMatches = await db.GroupMemberHistories
                .Where(h => h.HomeGroupId == groupId && h.LeftAt != null)
                .Include(h => h.Person)
                .Include(h => h.User)
                .Where(h => (h.Person != null && h.Person.Name == nameTrim
                            && (lastTrim == null || h.Person.LastName == lastTrim))
                         || (h.User != null && h.User.Name == nameTrim
                            && (lastTrim == null || h.User.LastName == lastTrim)))
                .ToListAsync();

            foreach (var p in pastMatches)
            {
                if (p.PersonId.HasValue && p.Person != null
                    && !personMatches.Any(x => x.Id == p.PersonId))
                    personMatches.Add(p.Person);
                if (p.UserId.HasValue && p.User != null
                    && !userMatches.Any(x => x.Id == p.UserId))
                    userMatches.Add(p.User);
            }

            if (personMatches.Count == 1 && userMatches.Count == 0)
            {
                matchedPersonId = personMatches[0].Id;
                matchType = "by_name";
            }
            else if (userMatches.Count == 1 && personMatches.Count == 0)
            {
                matchedUserId = userMatches[0].Id;
                matchType = "by_name";
            }
        }

        // Suggestions (if unmatched)
        var suggestions = new List<PersonMatchSuggestion>();
        if (matchType == "unmatched")
        {
            var nameTrim = person.Name.Trim();
            var lastTrim = person.LastName?.Trim();

            var people = await db.People
                .Include(p => p.PrimaryGroup)
                .Where(p => p.Name == nameTrim || (lastTrim != null && p.LastName == lastTrim))
                .Take(10)
                .ToListAsync();

            var users = await db.Users
                .Include(u => u.PrimaryGroup)
                .Where(u => u.Id != 0 && (u.Name == nameTrim || (lastTrim != null && u.LastName == lastTrim)))
                .Take(10)
                .ToListAsync();

            foreach (var p in people)
                suggestions.Add(new PersonMatchSuggestion(p.Id, null, p.Name, p.LastName, p.PrimaryGroup?.Name, false));
            foreach (var u in users)
                suggestions.Add(new PersonMatchSuggestion(null, u.Id, u.Name, u.LastName, u.PrimaryGroup?.Name, true));
        }

        // Compute file present/absent counts + detect attendance conflicts vs DB
        var counts = CountFileValues(person);
        string? detectedLeftAt = null;

        if (matchedPersonId.HasValue || matchedUserId.HasValue)
        {
            var datesInSheet = sheet.Dates.Where(d => d != null).Select(d => DateOnly.Parse(d!)).ToList();
            var existing = await db.Attendances
                .Where(a => a.HomeGroupId == groupId && datesInSheet.Contains(a.MeetingDate)
                    && (matchedPersonId.HasValue ? a.PersonId == matchedPersonId : a.UserId == matchedUserId))
                .ToListAsync();
            var existingByDate = existing.ToDictionary(a => a.MeetingDate);

            foreach (var (iso, cell) in person.Cells)
            {
                if (cell.IsCancelMark) continue;
                if (cell.AsBool is null) continue;

                var date = DateOnly.Parse(iso);
                if (existingByDate.TryGetValue(date, out var rec))
                {
                    if (rec.WasPresent != cell.AsBool.Value)
                    {
                        conflicts.Add(new ImportConflict(conflicts.Count, "attendance",
                            iso, person.RowIndex,
                            $"{person.Name} {person.LastName}".Trim(),
                            cell.AsBool.Value ? "присутній" : "відсутній",
                            rec.WasPresent ? "присутній" : "відсутній"));
                        onUpdAtt();
                    }
                }
                else
                {
                    onNewAtt();
                }
            }

            // Detect "left the group": last present-or-absent date < latest sheet date
            var valueDates = person.Cells
                .Where(kv => kv.Value.AsBool.HasValue)
                .Select(kv => DateOnly.Parse(kv.Key))
                .ToList();
            if (valueDates.Count > 0 && datesInSheet.Count > 0)
            {
                var lastValueDate = valueDates.Max();
                var maxSheetDate = datesInSheet.Max();
                if (lastValueDate < maxSheetDate)
                {
                    var afterBlank = datesInSheet
                        .Where(d => d > lastValueDate)
                        .Count(d => !person.Cells.ContainsKey(d.ToString("yyyy-MM-dd")));
                    if (afterBlank >= 2)
                        detectedLeftAt = lastValueDate.ToString("yyyy-MM-dd");
                }
            }
        }

        return new ImportPersonPreview(
            person.RowIndex, person.Name, person.LastName, person.IdHint,
            matchedPersonId, matchedUserId, matchType, suggestions,
            person.Status, person.Oversight,
            person.JoinedAt, detectedLeftAt,
            counts.Present, counts.Absent);
    }

    private static (int Present, int Absent) CountFileValues(AttendanceExcelService.ParsedPerson p)
    {
        int present = 0, absent = 0;
        foreach (var c in p.Cells.Values)
        {
            if (c.AsBool == true) present++;
            else if (c.AsBool == false) absent++;
        }
        return (present, absent);
    }

    private static string? Truncate(string? s, int n)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= n ? s : s[..n] + "…";
    }

    // ============================================================
    // IMPORT APPLY
    // ============================================================

    [HttpPost("import/apply")]
    [RequirePermission("attendance.record")]
    public async Task<ActionResult<ImportApplyResponse>> ImportApply([FromBody] ImportApplyRequest request)
    {
        if (!long.TryParse(request.ImportId, out var importId))
            return BadRequest(new { message = "Невалідний importId" });

        var rec = await db.AttendanceImports.FirstOrDefaultAsync(i => i.Id == importId);
        if (rec is null) return NotFound(new { message = "Імпорт не знайдено або застарів" });
        if (rec.ExpiresAt < DateTime.UtcNow)
        {
            db.AttendanceImports.Remove(rec);
            await db.SaveChangesAsync();
            return NotFound(new { message = "Імпорт застарів. Завантажте файл ще раз" });
        }

        List<AttendanceExcelService.ParsedSheet>? sheets;
        try
        {
            sheets = JsonSerializer.Deserialize<List<AttendanceExcelService.ParsedSheet>>(rec.PayloadJson);
        }
        catch
        {
            return BadRequest(new { message = "Не вдалось прочитати дані імпорту" });
        }
        if (sheets is null) return BadRequest(new { message = "Порожній імпорт" });

        int attCreated = 0, attUpdated = 0, metaCreated = 0, metaUpdated = 0;
        int peopleCreated = 0, membershipsCreated = 0, membershipsLeft = 0;

        foreach (var decision in request.Sheets)
        {
            var sheet = sheets.FirstOrDefault(s => s.Index == decision.SheetIndex);
            if (sheet is null) continue;
            if (decision.GroupId is null) continue;

            var groupId = decision.GroupId.Value;
            if (!await db.HomeGroups.AnyAsync(g => g.Id == groupId)) continue;

            var resolutions = decision.ConflictResolutions.ToDictionary(
                c => (c.Type, c.Date, c.PersonRowIndex),
                c => c.UseFile);
            bool ResolveConflict(string type, DateOnly date, int? personRow) =>
                resolutions.GetValueOrDefault((type, date.ToString("yyyy-MM-dd"), personRow), true);

            var personDecisions = decision.PersonDecisions.ToDictionary(p => p.RowIndex);

            // Resolve all person rows to (personId? userId?)
            var resolvedPeople = new Dictionary<int, (long? PersonId, long? UserId)>();
            foreach (var person in sheet.People)
            {
                if (!personDecisions.TryGetValue(person.RowIndex, out var pd))
                {
                    // Default: skip if not in decisions
                    continue;
                }

                var joinedAtDate = !string.IsNullOrWhiteSpace(person.JoinedAt) && DateOnly.TryParse(person.JoinedAt, out var ja)
                    ? (DateOnly?)ja : null;

                switch (pd.Action)
                {
                    case "skip":
                        continue;
                    case "use":
                    case "link":
                        resolvedPeople[person.RowIndex] = (pd.TargetPersonId, pd.TargetUserId);
                        if (pd.TargetPersonId.HasValue)
                            await EnsurePersonMembership(groupId, pd.TargetPersonId.Value, joinedAtDate, b => { if (b) membershipsCreated++; });
                        else if (pd.TargetUserId.HasValue)
                            await EnsureUserMembership(groupId, pd.TargetUserId.Value, joinedAtDate, b => { if (b) membershipsCreated++; });
                        break;
                    case "create":
                        var newPerson = new Person
                        {
                            Name = person.Name,
                            LastName = person.LastName,
                            PrimaryGroupId = groupId,
                            CreatedAt = DateTime.UtcNow,
                        };
                        if (decision.ImportStatus && !string.IsNullOrWhiteSpace(person.Status))
                        {
                            var status = await GetOrCreateStatus(person.Status!);
                            newPerson.PersonStatusId = status.Id;
                        }
                        if (decision.ImportOversight && !string.IsNullOrWhiteSpace(person.Oversight))
                            newPerson.OversightInfo = person.Oversight;

                        db.People.Add(newPerson);
                        await db.SaveChangesAsync();
                        peopleCreated++;

                        await EnsurePersonMembership(groupId, newPerson.Id, joinedAtDate, b => { if (b) membershipsCreated++; });
                        resolvedPeople[person.RowIndex] = (newPerson.Id, null);
                        break;
                }
            }

            // Apply per-date meta (guests, notes, cancellation)
            var validDates = sheet.Dates.Where(d => d != null).Select(d => DateOnly.Parse(d!)).Distinct().ToList();
            var existingMetas = await db.AttendanceMetas
                .Where(m => m.HomeGroupId == groupId && validDates.Contains(m.MeetingDate))
                .ToListAsync();
            var metaByDate = existingMetas.ToDictionary(m => m.MeetingDate);

            for (int i = 0; i < sheet.Dates.Count; i++)
            {
                var iso = sheet.Dates[i];
                if (iso is null) continue;
                var date = DateOnly.Parse(iso);

                var fileCancelled = sheet.CancelledByDate.GetValueOrDefault(iso);
                var fileGuests = sheet.GuestsByDate.GetValueOrDefault(iso)
                                 + sheet.OtherGroupsByDate.GetValueOrDefault(iso);
                var fileNotes = sheet.NotesByDate.GetValueOrDefault(iso);

                metaByDate.TryGetValue(date, out var meta);
                var newMeta = meta is null;

                bool useFileCancel = ResolveConflict("cancellation", date, null);
                bool useFileGuests = ResolveConflict("guests", date, null);
                bool useFileNotes = ResolveConflict("notes", date, null);

                if (meta is null && (fileCancelled || fileGuests > 0 || !string.IsNullOrWhiteSpace(fileNotes)))
                {
                    meta = new AttendanceMeta
                    {
                        HomeGroupId = groupId,
                        MeetingDate = date,
                    };
                    db.AttendanceMetas.Add(meta);
                    metaByDate[date] = meta;
                }

                if (meta is not null)
                {
                    if (useFileCancel) meta.IsCancelled = fileCancelled;
                    if (useFileGuests) meta.GuestCount = fileGuests;
                    if (useFileNotes && !string.IsNullOrWhiteSpace(fileNotes))
                    {
                        meta.GuestInfo = null;
                        meta.Notes = fileNotes;
                    }

                    if (newMeta) metaCreated++;
                    else metaUpdated++;

                    await SyncCancellation(groupId, date, meta.IsCancelled);
                }
            }

            // Apply per-person attendance
            var sheetDates = validDates;
            var allExisting = await db.Attendances
                .Where(a => a.HomeGroupId == groupId && sheetDates.Contains(a.MeetingDate))
                .ToListAsync();

            foreach (var person in sheet.People)
            {
                if (!resolvedPeople.TryGetValue(person.RowIndex, out var resolved)) continue;

                foreach (var (iso, cell) in person.Cells)
                {
                    if (cell.IsCancelMark || cell.AsBool is null) continue;
                    var date = DateOnly.Parse(iso);

                    var existing = allExisting.FirstOrDefault(a =>
                        a.MeetingDate == date
                        && a.PersonId == resolved.PersonId
                        && a.UserId == resolved.UserId);

                    if (existing is null)
                    {
                        db.Attendances.Add(new Attendance
                        {
                            HomeGroupId = groupId,
                            PersonId = resolved.PersonId,
                            UserId = resolved.UserId,
                            MeetingDate = date,
                            WasPresent = cell.AsBool.Value,
                        });
                        attCreated++;
                    }
                    else if (existing.WasPresent != cell.AsBool.Value)
                    {
                        var useFile = ResolveConflict("attendance", date, person.RowIndex);
                        if (useFile)
                        {
                            existing.WasPresent = cell.AsBool.Value;
                            attUpdated++;
                        }
                    }
                }

                // Status / Oversight imports for existing people
                if (decision.ImportStatus && !string.IsNullOrWhiteSpace(person.Status))
                {
                    var status = await GetOrCreateStatus(person.Status!);
                    if (resolved.PersonId.HasValue)
                    {
                        var p = await db.People.FindAsync(resolved.PersonId.Value);
                        if (p is not null) p.PersonStatusId = status.Id;
                    }
                    else if (resolved.UserId.HasValue)
                    {
                        var u = await db.Users.FindAsync(resolved.UserId.Value);
                        if (u is not null) u.PersonStatusId = status.Id;
                    }
                }
                if (decision.ImportOversight && !string.IsNullOrWhiteSpace(person.Oversight) && resolved.PersonId.HasValue)
                {
                    var p = await db.People.FindAsync(resolved.PersonId.Value);
                    if (p is not null) p.OversightInfo = person.Oversight;
                }

                // JoinedAt import
                if (decision.ImportJoinedAt
                    && !string.IsNullOrWhiteSpace(person.JoinedAt)
                    && DateOnly.TryParse(person.JoinedAt, out var joinedDate))
                {
                    var joined = joinedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    if (resolved.PersonId.HasValue)
                    {
                        var member = await db.HomeGroupMembers.FirstOrDefaultAsync(m =>
                            m.HomeGroupId == groupId && m.PersonId == resolved.PersonId.Value);
                        if (member is not null) member.JoinedAt = joined;
                        var hist = await db.GroupMemberHistories.FirstOrDefaultAsync(h =>
                            h.HomeGroupId == groupId && h.PersonId == resolved.PersonId.Value && h.LeftAt == null);
                        if (hist is not null) hist.JoinedAt = joined;
                    }
                    else if (resolved.UserId.HasValue)
                    {
                        var member = await db.UserHomeGroups.FirstOrDefaultAsync(m =>
                            m.HomeGroupId == groupId && m.UserId == resolved.UserId.Value);
                        if (member is not null) member.AssignedAt = joined;
                        var hist = await db.GroupMemberHistories.FirstOrDefaultAsync(h =>
                            h.HomeGroupId == groupId && h.UserId == resolved.UserId.Value && h.LeftAt == null);
                        if (hist is not null) hist.JoinedAt = joined;
                    }
                }

                // LeftAt detection: last present-or-absent date < latest sheet date, with ≥2 blanks after
                if (decision.ImportLeftAt)
                {
                    var valueDates = person.Cells
                        .Where(kv => kv.Value.AsBool.HasValue)
                        .Select(kv => DateOnly.Parse(kv.Key))
                        .ToList();
                    if (valueDates.Count > 0 && sheetDates.Count > 0)
                    {
                        var lastValueDate = valueDates.Max();
                        var maxSheetDate = sheetDates.Max();
                        if (lastValueDate < maxSheetDate)
                        {
                            var afterBlank = sheetDates
                                .Where(d => d > lastValueDate)
                                .Count(d => !person.Cells.ContainsKey(d.ToString("yyyy-MM-dd")));
                            if (afterBlank >= 2)
                                await MarkLeft(groupId, resolved, lastValueDate, b => { if (b) membershipsLeft++; });
                        }
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        db.AttendanceImports.Remove(rec);
        await db.SaveChangesAsync();

        return Ok(new ImportApplyResponse(
            request.Sheets.Count, attCreated, attUpdated, metaCreated, metaUpdated,
            peopleCreated, membershipsCreated, membershipsLeft));
    }

    private async Task EnsurePersonMembership(long groupId, long personId, DateOnly? joinedAt, Action<bool> onCreated)
    {
        var existing = await db.HomeGroupMembers.FirstOrDefaultAsync(m =>
            m.HomeGroupId == groupId && m.PersonId == personId);
        var joined = joinedAt.HasValue
            ? joinedAt.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            : DateTime.UtcNow;

        if (existing is null)
        {
            db.HomeGroupMembers.Add(new HomeGroupMember
            {
                HomeGroupId = groupId,
                PersonId = personId,
                JoinedAt = joined,
            });

            var person = await db.People.FindAsync(personId);
            if (person is not null && person.PrimaryGroupId is null)
                person.PrimaryGroupId = groupId;

            var openHistory = await db.GroupMemberHistories.FirstOrDefaultAsync(h =>
                h.HomeGroupId == groupId && h.PersonId == personId && h.LeftAt == null);
            if (openHistory is null)
            {
                db.GroupMemberHistories.Add(new GroupMemberHistory
                {
                    HomeGroupId = groupId,
                    PersonId = personId,
                    JoinedAt = joined,
                });
            }
            onCreated(true);
        }
    }

    private async Task EnsureUserMembership(long groupId, long userId, DateOnly? joinedAt, Action<bool> onCreated)
    {
        var existing = await db.UserHomeGroups.FirstOrDefaultAsync(m =>
            m.HomeGroupId == groupId && m.UserId == userId);
        var joined = joinedAt.HasValue
            ? joinedAt.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            : DateTime.UtcNow;

        if (existing is null)
        {
            db.UserHomeGroups.Add(new UserHomeGroup
            {
                HomeGroupId = groupId,
                UserId = userId,
                AssignedAt = joined,
            });

            var openHistory = await db.GroupMemberHistories.FirstOrDefaultAsync(h =>
                h.HomeGroupId == groupId && h.UserId == userId && h.LeftAt == null);
            if (openHistory is null)
            {
                db.GroupMemberHistories.Add(new GroupMemberHistory
                {
                    HomeGroupId = groupId,
                    UserId = userId,
                    JoinedAt = joined,
                });
            }
            onCreated(true);
        }
    }

    private async Task MarkLeft(long groupId, (long? PersonId, long? UserId) resolved,
        DateOnly lastDate, Action<bool> onLeft)
    {
        var leftAt = lastDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        if (resolved.PersonId.HasValue)
        {
            var member = await db.HomeGroupMembers.FirstOrDefaultAsync(m =>
                m.HomeGroupId == groupId && m.PersonId == resolved.PersonId.Value);
            if (member is not null)
            {
                db.HomeGroupMembers.Remove(member);
                var person = await db.People.FindAsync(resolved.PersonId.Value);
                if (person is not null && person.PrimaryGroupId == groupId)
                    person.PrimaryGroupId = null;
            }
            var hist = await db.GroupMemberHistories.FirstOrDefaultAsync(h =>
                h.HomeGroupId == groupId && h.PersonId == resolved.PersonId.Value && h.LeftAt == null);
            if (hist is not null) hist.LeftAt = leftAt;
            else
            {
                db.GroupMemberHistories.Add(new GroupMemberHistory
                {
                    HomeGroupId = groupId,
                    PersonId = resolved.PersonId.Value,
                    JoinedAt = leftAt,
                    LeftAt = leftAt,
                });
            }
            onLeft(true);
        }
        else if (resolved.UserId.HasValue)
        {
            var member = await db.UserHomeGroups.FirstOrDefaultAsync(m =>
                m.HomeGroupId == groupId && m.UserId == resolved.UserId.Value);
            if (member is not null) db.UserHomeGroups.Remove(member);
            var hist = await db.GroupMemberHistories.FirstOrDefaultAsync(h =>
                h.HomeGroupId == groupId && h.UserId == resolved.UserId.Value && h.LeftAt == null);
            if (hist is not null) hist.LeftAt = leftAt;
            else
            {
                db.GroupMemberHistories.Add(new GroupMemberHistory
                {
                    HomeGroupId = groupId,
                    UserId = resolved.UserId.Value,
                    JoinedAt = leftAt,
                    LeftAt = leftAt,
                });
            }
            onLeft(true);
        }
    }

    private async Task<PersonStatus> GetOrCreateStatus(string name)
    {
        var trimmed = name.Trim();
        var existing = await db.PersonStatuses
            .FirstOrDefaultAsync(s => s.Name == trimmed);
        if (existing is not null) return existing;

        var created = new PersonStatus { Name = trimmed, Color = "#6366F1" };
        db.PersonStatuses.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    private async Task SyncCancellation(long groupId, DateOnly date, bool isCancelled)
    {
        var calEvent = await db.CalendarEvents.FirstOrDefaultAsync(e =>
            e.HomeGroupId == groupId &&
            e.Type == CalendarEventType.HomeGroup &&
            !e.IsRecurring &&
            e.Date == date);

        if (isCancelled)
        {
            if (calEvent is null)
            {
                var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Title = group?.Name ?? "Домашка",
                    Type = CalendarEventType.HomeGroup,
                    HomeGroupId = groupId,
                    IsRecurring = false,
                    Date = date,
                    IsHomeGroupMeeting = false,
                });
            }
            else calEvent.IsHomeGroupMeeting = false;
        }
        else if (calEvent is not null && calEvent.IsHomeGroupMeeting == false)
        {
            db.CalendarEvents.Remove(calEvent);
        }
    }

    private static long[] ParseIds(string s) =>
        (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => long.TryParse(x, out var v) ? v : 0L)
            .Where(v => v > 0)
            .ToArray();

    private long? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var id) ? id : null;
    }
}
