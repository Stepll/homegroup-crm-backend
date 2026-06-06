using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Attendance;
using Entities = HomeGroup.API.Models.Entities;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class AttendanceController(AppDbContext db) : ControllerBase
{
    private static readonly Dictionary<string, DayOfWeek> UkrDays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Понеділок"] = DayOfWeek.Monday,
        ["Вівторок"] = DayOfWeek.Tuesday,
        ["Середа"] = DayOfWeek.Wednesday,
        ["Четвер"] = DayOfWeek.Thursday,
        ["Пʼятниця"] = DayOfWeek.Friday,
        ["П'ятниця"] = DayOfWeek.Friday,
        ["Субота"] = DayOfWeek.Saturday,
        ["Неділя"] = DayOfWeek.Sunday,
    };

    [HttpGet]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<List<AttendanceResponse>>> GetByGroup(
        [FromQuery] long groupId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var query = db.Attendances
            .Include(a => a.Person)
            .Include(a => a.User)
            .Where(a => a.HomeGroupId == groupId);

        if (from.HasValue) query = query.Where(a => a.MeetingDate >= from.Value);
        if (to.HasValue) query = query.Where(a => a.MeetingDate <= to.Value);

        var records = await query
            .OrderByDescending(a => a.MeetingDate)
            .ToListAsync();

        return Ok(records.Select(a => new AttendanceResponse(
            a.Id, a.PersonId, a.UserId,
            MemberName(a),
            a.HomeGroupId, a.MeetingDate, a.WasPresent, a.Notes)));
    }

    [HttpGet("summary")]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<List<AttendanceSummary>>> GetSummary([FromQuery] long groupId)
    {
        var summary = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .GroupBy(a => a.MeetingDate)
            .Select(g => new AttendanceSummary(
                g.Key,
                g.Count(),
                g.Count(a => a.WasPresent),
                g.Count() == 0 ? 0 : Math.Round((double)g.Count(a => a.WasPresent) / g.Count() * 100, 1)
            ))
            .OrderByDescending(s => s.MeetingDate)
            .ToListAsync();

        return Ok(summary);
    }

    [HttpGet("dates")]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<List<string>>> GetMeetingDates([FromQuery] long groupId)
    {
        var fromAttendance = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .Select(a => a.MeetingDate)
            .Distinct()
            .ToListAsync();

        var fromMeta = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == groupId)
            .Select(m => m.MeetingDate)
            .ToListAsync();

        // Include rescheduled meetings (real overrides) — but skip pure cancellation markers
        var fromCalendar = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.IsHomeGroupMeeting == true
                && e.Date != null)
            .Select(e => e.Date!.Value)
            .ToListAsync();

        var allDates = fromAttendance
            .Union(fromMeta)
            .Union(fromCalendar)
            .OrderByDescending(d => d)
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList();

        return Ok(allDates);
    }

    [HttpPost]
    [RequirePermission("attendance.record")]
    public async Task<IActionResult> Record(RecordAttendanceRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == request.HomeGroupId))
            return NotFound(new { message = "Група не знайдена" });

        foreach (var entry in request.Entries)
        {
            if (entry.PersonId is null && entry.UserId is null) continue;

            var existing = await db.Attendances.FirstOrDefaultAsync(a =>
                a.HomeGroupId == request.HomeGroupId &&
                a.PersonId == entry.PersonId &&
                a.UserId == entry.UserId &&
                a.MeetingDate == request.MeetingDate);

            if (existing is not null)
            {
                existing.WasPresent = entry.WasPresent;
                existing.Notes = entry.Notes;
            }
            else
            {
                db.Attendances.Add(new Attendance
                {
                    HomeGroupId = request.HomeGroupId,
                    PersonId = entry.PersonId,
                    UserId = entry.UserId,
                    MeetingDate = request.MeetingDate,
                    WasPresent = entry.WasPresent,
                    Notes = entry.Notes,
                });
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("bulk")]
    [RequirePermission("attendance.record")]
    public async Task<IActionResult> RecordBulk(BulkRecordAttendanceRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == request.HomeGroupId))
            return NotFound(new { message = "Група не знайдена" });

        var dates = request.Entries
            .Select(e => e.Date)
            .Distinct()
            .Select(d => DateOnly.Parse(d))
            .ToList();

        var existing = await db.Attendances
            .Where(a => a.HomeGroupId == request.HomeGroupId && dates.Contains(a.MeetingDate))
            .ToListAsync();

        foreach (var entry in request.Entries)
        {
            if (entry.PersonId is null && entry.UserId is null) continue;
            if (!DateOnly.TryParse(entry.Date, out var date)) continue;

            var record = existing.FirstOrDefault(a =>
                a.MeetingDate == date &&
                a.PersonId == entry.PersonId &&
                a.UserId == entry.UserId);

            if (record is not null)
            {
                record.WasPresent = entry.WasPresent;
            }
            else
            {
                var newRecord = new Attendance
                {
                    HomeGroupId = request.HomeGroupId,
                    PersonId = entry.PersonId,
                    UserId = entry.UserId,
                    MeetingDate = date,
                    WasPresent = entry.WasPresent,
                };
                db.Attendances.Add(newRecord);
                existing.Add(newRecord);
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("meta")]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<AttendanceMetaResponse>> GetMeta(
        [FromQuery] long groupId, [FromQuery] DateOnly date)
    {
        var meta = await db.AttendanceMetas
            .FirstOrDefaultAsync(m => m.HomeGroupId == groupId && m.MeetingDate == date);
        return Ok(new AttendanceMetaResponse(
            meta?.GuestCount ?? 0, meta?.GuestInfo, meta?.Notes, meta?.IsCancelled ?? false));
    }

    [HttpPost("meta")]
    [RequirePermission("attendance.record")]
    public async Task<IActionResult> SaveMeta(SaveAttendanceMetaRequest request)
    {
        var meta = await db.AttendanceMetas
            .FirstOrDefaultAsync(m => m.HomeGroupId == request.HomeGroupId && m.MeetingDate == request.MeetingDate);

        var wasCancelled = meta?.IsCancelled ?? false;

        if (meta is null)
        {
            meta = new Entities.AttendanceMeta
            {
                HomeGroupId = request.HomeGroupId,
                MeetingDate = request.MeetingDate,
                GuestCount = request.GuestCount,
                GuestInfo = request.GuestInfo?.Trim(),
                Notes = request.Notes?.Trim(),
                IsCancelled = request.IsCancelled,
            };
            db.AttendanceMetas.Add(meta);
        }
        else
        {
            meta.GuestCount = request.GuestCount;
            meta.GuestInfo = request.GuestInfo?.Trim();
            meta.Notes = request.Notes?.Trim();
            meta.IsCancelled = request.IsCancelled;
        }

        // Sync cancellation to CalendarEvent (IsHomeGroupMeeting flag)
        if (wasCancelled != request.IsCancelled)
            await SyncCancellationToCalendar(request.HomeGroupId, request.MeetingDate, request.IsCancelled);

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("dots")]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<AttendanceDotsResponse>> GetDots(
        [FromQuery] long groupId, [FromQuery] int limit = 5)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dates = await db.Attendances
            .Where(a => a.HomeGroupId == groupId && a.MeetingDate <= today)
            .Select(a => a.MeetingDate)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(limit)
            .ToListAsync();

        var cancelledDates = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == groupId && m.IsCancelled && dates.Contains(m.MeetingDate))
            .Select(m => m.MeetingDate)
            .ToListAsync();

        var records = await db.Attendances
            .Where(a => a.HomeGroupId == groupId && dates.Contains(a.MeetingDate))
            .Select(a => new AttendanceDotRecord(a.PersonId, a.UserId, a.MeetingDate.ToString("yyyy-MM-dd"), a.WasPresent))
            .ToListAsync();

        return Ok(new AttendanceDotsResponse(
            dates.Select(d => d.ToString("yyyy-MM-dd")).ToArray(),
            cancelledDates.Select(d => d.ToString("yyyy-MM-dd")).ToArray(),
            records.ToArray()));
    }

    [HttpDelete("meeting")]
    [RequirePermission("attendance.record")]
    public async Task<IActionResult> DeleteMeeting([FromQuery] long groupId, [FromQuery] DateOnly date)
    {
        if (date <= DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Можна видаляти тільки майбутні зустрічі" });

        var attendances = await db.Attendances
            .Where(a => a.HomeGroupId == groupId && a.MeetingDate == date)
            .ToListAsync();
        db.Attendances.RemoveRange(attendances);

        var meta = await db.AttendanceMetas
            .FirstOrDefaultAsync(m => m.HomeGroupId == groupId && m.MeetingDate == date);
        if (meta is not null)
            db.AttendanceMetas.Remove(meta);

        // Remove override CalendarEvent for this date if present
        var calEvent = await db.CalendarEvents
            .FirstOrDefaultAsync(e =>
                e.HomeGroupId == groupId &&
                e.Type == CalendarEventType.HomeGroup &&
                !e.IsRecurring &&
                e.Date == date);
        if (calEvent is not null)
            db.CalendarEvents.Remove(calEvent);

        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/v1/groups/:id/attendance-table
    // Registered on GroupsController path via AttendanceController route override below
    [HttpGet("/api/v1/groups/{groupId}/attendance-table")]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<AttendanceTableResponse>> GetTable(long groupId)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) return NotFound(new { message = "Група не знайдена" });

        const int tableSize = 8;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Collect all dates that have any data in DB
        var dbDatesFromAttendance = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .Select(a => a.MeetingDate)
            .Distinct()
            .ToListAsync();

        var dbDatesFromMeta = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == groupId)
            .Select(m => m.MeetingDate)
            .ToListAsync();

        // Schedule overrides — CalendarEvent dates for HomeGroup (real meetings + manual cancellations).
        // EXCLUDE move shadows (IsHomeGroupMeeting=false WITH MovedToDate) — those just say
        // "the meeting was moved away from this date", they aren't a real cancelled meeting.
        var dbDatesFromCalendar = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.Date != null
                && !(e.IsHomeGroupMeeting == false && e.MovedToDate != null))
            .Select(e => e.Date!.Value)
            .ToListAsync();

        // Dates where the meeting was moved AWAY from — exclude from table entirely.
        // Even if attendance/meta records still exist on the moved-out date (e.g., move done
        // before moveAttendance feature existed), don't show that column — the meeting
        // semantically happened on the destination date for that week.
        var movedOutDates = (await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.IsHomeGroupMeeting == false
                && e.MovedToDate != null
                && e.Date != null)
            .Select(e => e.Date!.Value)
            .ToListAsync()).ToHashSet();

        var dbDates = dbDatesFromAttendance
            .Union(dbDatesFromMeta)
            .Union(dbDatesFromCalendar)
            .Where(d => !movedOutDates.Contains(d))
            .OrderByDescending(d => d)
            .ToList();

        // Generate virtual dates based on group schedule to fill up to tableSize
        var allDates = new HashSet<DateOnly>(dbDates);

        if (UkrDays.TryGetValue(group.MeetingDay ?? "", out var meetingDow) && allDates.Count < tableSize)
        {
            // Start from the earliest date we have, or from today if none
            var anchor = dbDates.Count > 0 ? dbDates[^1] : today;
            // Walk backwards from anchor to find meeting days
            var candidate = anchor;
            var daysBack = ((int)candidate.DayOfWeek - (int)meetingDow + 7) % 7;
            candidate = candidate.AddDays(-daysBack);
            // candidate is now on the correct day-of-week at or before anchor

            int safety = 0;
            while (allDates.Count < tableSize && safety < tableSize * 4)
            {
                if (!movedOutDates.Contains(candidate))
                    allDates.Add(candidate);
                candidate = candidate.AddDays(-7);
                safety++;
            }
        }

        var dates = allDates
            .OrderBy(d => d)
            .TakeLast(tableSize)
            .ToList();

        var dbDateSet = new HashSet<DateOnly>(dbDates);

        // Load meta for these dates
        var metas = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == groupId && dates.Contains(m.MeetingDate))
            .ToListAsync();

        var metaByDate = metas.ToDictionary(m => m.MeetingDate);

        // Load schedule overrides (cancellation markers) for these dates
        var scheduleEvents = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.Date != null
                && dates.Contains(e.Date!.Value))
            .ToListAsync();

        // Manual cancellations only — exclude move-out shadows
        var cancelledFromSchedule = scheduleEvents
            .Where(e => e.IsHomeGroupMeeting == false && e.MovedToDate == null)
            .Select(e => e.Date!.Value)
            .ToHashSet();

        // Build meetings list — cancelled if EITHER AttendanceMeta.IsCancelled OR CalendarEvent.IsHomeGroupMeeting=false
        var meetings = dates.Select(d =>
        {
            metaByDate.TryGetValue(d, out var m);
            var isCancelled = (m?.IsCancelled ?? false) || cancelledFromSchedule.Contains(d);
            return new AttendanceTableMeeting(
                d.ToString("yyyy-MM-dd"),
                m?.GuestCount ?? 0,
                m?.GuestInfo,
                m?.Notes,
                isCancelled,
                dbDateSet.Contains(d));
        }).ToList();

        // Load all members: persons + admins
        var personMembers = await db.HomeGroupMembers
            .Where(m => m.HomeGroupId == groupId)
            .Include(m => m.Person)
            .ToListAsync();

        var userMembers = await db.UserHomeGroups
            .Where(u => u.HomeGroupId == groupId)
            .Include(u => u.User)
            .Where(u => u.UserId != 0)
            .ToListAsync();

        // Load all attendance records for these dates
        var attendanceRecords = await db.Attendances
            .Where(a => a.HomeGroupId == groupId && dates.Contains(a.MeetingDate))
            .ToListAsync();

        // For attendance rate: load ALL cancellation markers across history (both AttendanceMeta and CalendarEvent overrides)
        var allMetaCancelled = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == groupId && m.IsCancelled)
            .Select(m => m.MeetingDate)
            .ToListAsync();
        var allCalendarCancelled = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.IsHomeGroupMeeting == false
                && e.MovedToDate == null
                && e.Date != null)
            .Select(e => e.Date!.Value)
            .ToListAsync();
        var cancelledDates = allMetaCancelled.Union(allCalendarCancelled).ToHashSet();

        var allAttendance = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .ToListAsync();

        var members = new List<AttendanceTableMember>();

        var currentPersonIds = personMembers.Select(p => p.PersonId).ToHashSet();
        var currentUserIds = userMembers.Select(u => u.UserId).ToHashSet();

        foreach (var pm in personMembers)
        {
            var attendanceMap = BuildAttendanceMap(
                dates,
                attendanceRecords.Where(a => a.PersonId == pm.PersonId).ToList());

            var rate = CalcRate(
                allAttendance.Where(a => a.PersonId == pm.PersonId).ToList(),
                cancelledDates);

            members.Add(new AttendanceTableMember(
                pm.PersonId, null,
                pm.Person.Name, pm.Person.LastName,
                DateOnly.FromDateTime(pm.JoinedAt).ToString("yyyy-MM-dd"),
                null,
                rate, attendanceMap));
        }

        foreach (var um in userMembers)
        {
            var attendanceMap = BuildAttendanceMap(
                dates,
                attendanceRecords.Where(a => a.UserId == um.UserId).ToList());

            var rate = CalcRate(
                allAttendance.Where(a => a.UserId == um.UserId).ToList(),
                cancelledDates);

            members.Add(new AttendanceTableMember(
                null, um.UserId,
                um.User.Name, um.User.LastName,
                DateOnly.FromDateTime(um.AssignedAt).ToString("yyyy-MM-dd"),
                null,
                rate, attendanceMap));
        }

        // Include past members (from history) who have attendance records in this group
        var histories = await db.GroupMemberHistories
            .Where(h => h.HomeGroupId == groupId && h.LeftAt != null)
            .Include(h => h.Person)
            .Include(h => h.User)
            .ToListAsync();

        var pastMembers = histories
            .Where(h => (h.PersonId == null || !currentPersonIds.Contains(h.PersonId.Value))
                     && (h.UserId == null || !currentUserIds.Contains(h.UserId.Value)))
            .GroupBy(h => h.PersonId.HasValue ? $"p_{h.PersonId}" : $"u_{h.UserId}")
            .Select(g => g.OrderByDescending(h => h.JoinedAt).First())
            .ToList();

        foreach (var hist in pastMembers)
        {
            if (hist.PersonId.HasValue && hist.Person != null)
            {
                var attendanceMap = BuildAttendanceMap(
                    dates,
                    attendanceRecords.Where(a => a.PersonId == hist.PersonId).ToList());
                var rate = CalcRate(
                    allAttendance.Where(a => a.PersonId == hist.PersonId).ToList(),
                    cancelledDates);
                members.Add(new AttendanceTableMember(
                    hist.PersonId, null,
                    hist.Person.Name, hist.Person.LastName,
                    DateOnly.FromDateTime(hist.JoinedAt).ToString("yyyy-MM-dd"),
                    DateOnly.FromDateTime(hist.LeftAt!.Value).ToString("yyyy-MM-dd"),
                    rate, attendanceMap));
            }
            else if (hist.UserId.HasValue && hist.User != null)
            {
                var attendanceMap = BuildAttendanceMap(
                    dates,
                    attendanceRecords.Where(a => a.UserId == hist.UserId).ToList());
                var rate = CalcRate(
                    allAttendance.Where(a => a.UserId == hist.UserId).ToList(),
                    cancelledDates);
                members.Add(new AttendanceTableMember(
                    null, hist.UserId,
                    hist.User.Name, hist.User.LastName,
                    DateOnly.FromDateTime(hist.JoinedAt).ToString("yyyy-MM-dd"),
                    DateOnly.FromDateTime(hist.LeftAt!.Value).ToString("yyyy-MM-dd"),
                    rate, attendanceMap));
            }
        }

        members = members.OrderBy(m => m.Name).ThenBy(m => m.LastName).ToList();

        var dateStrings = dates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
        return Ok(new AttendanceTableResponse(dateStrings, meetings, members));
    }

    private static Dictionary<string, bool> BuildAttendanceMap(
        List<DateOnly> dates,
        List<Attendance> memberRecords)
    {
        var recordByDate = memberRecords.ToDictionary(a => a.MeetingDate, a => a.WasPresent);
        var map = new Dictionary<string, bool>();

        foreach (var date in dates)
        {
            if (recordByDate.TryGetValue(date, out var val))
                map[date.ToString("yyyy-MM-dd")] = val;
            // Dates with no record are omitted — frontend treats missing keys as "no data" (gray)
        }

        return map;
    }

    private static double CalcRate(
        List<Attendance> allRecords,
        HashSet<DateOnly> cancelledDates)
    {
        var eligible = allRecords
            .Where(a => !cancelledDates.Contains(a.MeetingDate))
            .ToList();

        if (eligible.Count == 0) return 0;
        return Math.Round((double)eligible.Count(a => a.WasPresent) / eligible.Count * 100, 1);
    }

    private async Task SyncCancellationToCalendar(long groupId, DateOnly date, bool isCancelled)
    {
        var calEvent = await db.CalendarEvents
            .FirstOrDefaultAsync(e =>
                e.HomeGroupId == groupId &&
                e.Type == CalendarEventType.HomeGroup &&
                !e.IsRecurring &&
                e.Date == date);

        if (isCancelled)
        {
            if (calEvent is null)
            {
                // Get group info for the event title
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
            else
            {
                calEvent.IsHomeGroupMeeting = false;
            }
        }
        else
        {
            // Un-cancel: remove the override event so the recurring ghost shows again
            if (calEvent is not null && calEvent.IsHomeGroupMeeting == false)
                db.CalendarEvents.Remove(calEvent);
        }
    }

    private static string MemberName(Attendance a)
    {
        if (a.Person is not null)
            return $"{a.Person.Name}{(a.Person.LastName is null ? "" : " " + a.Person.LastName)}";
        if (a.User is not null)
            return $"{a.User.Name}{(a.User.LastName is null ? "" : " " + a.User.LastName)}";
        return "?";
    }
}
