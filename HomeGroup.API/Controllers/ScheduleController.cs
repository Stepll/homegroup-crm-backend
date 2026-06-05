using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Schedule;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/schedule")]
[Authorize]
public class ScheduleController(AppDbContext db) : ControllerBase
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
    [RequirePermission("groups.schedule.manage")]
    public async Task<ActionResult<List<ScheduleWeekDto>>> GetSchedule(
        long groupId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) return NotFound();
        if (!UkrDays.TryGetValue(group.MeetingDay ?? "", out var meetingDow))
            return BadRequest(new { message = "У групи не вказано день зустрічі" });

        var events = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.Date != null
                && e.Date >= from
                && e.Date <= to)
            .ToListAsync();

        var planDates = await db.MeetingPlans
            .Where(p => p.HomeGroupId == groupId)
            .Select(p => p.MeetingDate)
            .ToListAsync();
        var planSet = new HashSet<string>(planDates);

        var attCounts = await db.Attendances
            .Where(a => a.HomeGroupId == groupId && a.MeetingDate >= from && a.MeetingDate <= to)
            .GroupBy(a => a.MeetingDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();
        var attCountByDate = attCounts.ToDictionary(x => x.Date, x => x.Count);

        var firstMonday = SnapToMonday(from);
        var weeks = new List<ScheduleWeekDto>();

        for (var weekStart = firstMonday; weekStart <= to; weekStart = weekStart.AddDays(7))
        {
            var weekEnd = weekStart.AddDays(6);
            var dowOffset = ((int)meetingDow - (int)DayOfWeek.Monday + 7) % 7;
            var defaultDate = weekStart.AddDays(dowOffset);

            var weekEvents = events.Where(e => e.Date >= weekStart && e.Date <= weekEnd).ToList();
            var realEvent = weekEvents.FirstOrDefault(e => e.IsHomeGroupMeeting == true);
            var cancelEvent = weekEvents.FirstOrDefault(e => e.IsHomeGroupMeeting == false);

            string status = "default";
            string? effectiveDate = defaultDate.ToString("yyyy-MM-dd");
            string? movedFromDate = null;
            string? movedToDate = null;

            if (realEvent is not null)
            {
                effectiveDate = realEvent.Date!.Value.ToString("yyyy-MM-dd");
                if (realEvent.MovedFromDate.HasValue)
                {
                    status = "moved_in";
                    movedFromDate = realEvent.MovedFromDate.Value.ToString("yyyy-MM-dd");
                }
                else if (realEvent.Date != defaultDate)
                {
                    status = "rescheduled_internal";
                }
            }
            else if (cancelEvent is not null)
            {
                effectiveDate = null;
                if (cancelEvent.MovedToDate.HasValue)
                {
                    status = "moved_out";
                    movedToDate = cancelEvent.MovedToDate.Value.ToString("yyyy-MM-dd");
                }
                else
                {
                    status = "cancelled";
                }
            }

            var hasPlan = effectiveDate is not null && planSet.Contains(effectiveDate);
            var attCount = 0;
            if (effectiveDate is not null && DateOnly.TryParse(effectiveDate, out var effDateOnly))
                attCountByDate.TryGetValue(effDateOnly, out attCount);

            weeks.Add(new ScheduleWeekDto(
                weekStart.ToString("yyyy-MM-dd"),
                defaultDate.ToString("yyyy-MM-dd"),
                effectiveDate,
                status,
                movedFromDate,
                movedToDate,
                hasPlan,
                attCount
            ));
        }

        return Ok(weeks);
    }

    [HttpPost("cancel")]
    [RequirePermission("groups.schedule.manage")]
    public async Task<IActionResult> Cancel(long groupId, ScheduleCancelRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) return NotFound();
        if (!DateOnly.TryParse(request.Date, out var date)) return BadRequest(new { message = "Некоректна дата" });

        var existing = await db.CalendarEvents.FirstOrDefaultAsync(e =>
            e.HomeGroupId == groupId
            && e.Type == CalendarEventType.HomeGroup
            && !e.IsRecurring
            && e.Date == date);

        if (existing is not null)
            existing.IsHomeGroupMeeting = false;
        else
            db.CalendarEvents.Add(new CalendarEvent
            {
                Type = CalendarEventType.HomeGroup,
                HomeGroupId = groupId,
                IsRecurring = false,
                Date = date,
                IsHomeGroupMeeting = false,
                Title = group.Name,
            });

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("cancel")]
    [RequirePermission("groups.schedule.manage")]
    public async Task<IActionResult> Uncancel(long groupId, [FromQuery] DateOnly date)
    {
        var existing = await db.CalendarEvents.FirstOrDefaultAsync(e =>
            e.HomeGroupId == groupId
            && e.Type == CalendarEventType.HomeGroup
            && !e.IsRecurring
            && e.Date == date
            && e.IsHomeGroupMeeting == false);

        if (existing is null) return NoContent();

        // If this was a moved-in event that got cancelled, restore it to active
        if (existing.MovedFromDate.HasValue)
            existing.IsHomeGroupMeeting = true;
        // If this was a shadow cancellation (move-out marker), keep the link but flip back
        else if (existing.MovedToDate.HasValue)
            existing.IsHomeGroupMeeting = true;
        else
            db.CalendarEvents.Remove(existing);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("move")]
    [RequirePermission("groups.schedule.manage")]
    public async Task<IActionResult> Move(long groupId, ScheduleMoveRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) return NotFound();
        if (!DateOnly.TryParse(request.FromDate, out var fromDate) || !DateOnly.TryParse(request.ToDate, out var toDate))
            return BadRequest(new { message = "Некоректна дата" });
        if (fromDate == toDate) return BadRequest(new { message = "Дати збігаються" });

        // 1. Upsert real event at toDate
        var toEvent = await db.CalendarEvents.FirstOrDefaultAsync(e =>
            e.HomeGroupId == groupId && e.Type == CalendarEventType.HomeGroup
            && !e.IsRecurring && e.Date == toDate);

        // Determine time
        TimeOnly? startTime = null;
        TimeOnly? endTime = null;
        if (!string.IsNullOrEmpty(group.MeetingTime) && TimeOnly.TryParse(group.MeetingTime, out var grpStart))
        {
            startTime = grpStart;
            if (!string.IsNullOrEmpty(group.MeetingEndTime) && TimeOnly.TryParse(group.MeetingEndTime, out var grpEnd))
                endTime = grpEnd;
        }

        // Preserve room from source if present
        long? transferredRoomId = null;
        var fromEvent = await db.CalendarEvents.FirstOrDefaultAsync(e =>
            e.HomeGroupId == groupId && e.Type == CalendarEventType.HomeGroup
            && !e.IsRecurring && e.Date == fromDate);
        if (fromEvent is not null && fromEvent.IsHomeGroupMeeting != false)
            transferredRoomId = fromEvent.RoomId;

        if (toEvent is not null)
        {
            toEvent.IsHomeGroupMeeting = true;
            toEvent.MovedFromDate = fromDate;
            toEvent.MovedToDate = null;
            if (startTime.HasValue) toEvent.StartTime = startTime;
            if (endTime.HasValue) toEvent.EndTime = endTime;
            if (transferredRoomId.HasValue && !toEvent.RoomId.HasValue) toEvent.RoomId = transferredRoomId;
        }
        else
        {
            db.CalendarEvents.Add(new CalendarEvent
            {
                Type = CalendarEventType.HomeGroup,
                HomeGroupId = groupId,
                IsRecurring = false,
                Date = toDate,
                IsHomeGroupMeeting = true,
                MovedFromDate = fromDate,
                Title = group.Name,
                StartTime = startTime,
                EndTime = endTime,
                RoomId = transferredRoomId,
            });
        }

        // 2. Upsert shadow cancellation at fromDate
        if (fromEvent is not null)
        {
            fromEvent.IsHomeGroupMeeting = false;
            fromEvent.MovedToDate = toDate;
            fromEvent.MovedFromDate = null;
            fromEvent.RoomId = null;
        }
        else
        {
            db.CalendarEvents.Add(new CalendarEvent
            {
                Type = CalendarEventType.HomeGroup,
                HomeGroupId = groupId,
                IsRecurring = false,
                Date = fromDate,
                IsHomeGroupMeeting = false,
                MovedToDate = toDate,
                Title = group.Name,
            });
        }

        // 3. Move plan if requested
        if (request.MovePlan)
        {
            var fromDateStr = fromDate.ToString("yyyy-MM-dd");
            var toDateStr = toDate.ToString("yyyy-MM-dd");
            var plan = await db.MeetingPlans
                .FirstOrDefaultAsync(p => p.HomeGroupId == groupId && p.MeetingDate == fromDateStr);
            if (plan is not null)
            {
                var existing = await db.MeetingPlans
                    .Include(p => p.Blocks)
                    .FirstOrDefaultAsync(p => p.HomeGroupId == groupId && p.MeetingDate == toDateStr);
                if (existing is not null)
                {
                    db.MeetingPlanBlocks.RemoveRange(existing.Blocks);
                    db.MeetingPlans.Remove(existing);
                }
                plan.MeetingDate = toDateStr;
                plan.OriginalMeetingDate ??= fromDateStr;
                plan.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("reset-week")]
    [RequirePermission("groups.schedule.manage")]
    public async Task<IActionResult> ResetWeek(long groupId, ScheduleResetRequest request)
    {
        if (!DateOnly.TryParse(request.WeekStart, out var weekStart))
            return BadRequest(new { message = "Некоректний weekStart" });

        var weekEnd = weekStart.AddDays(6);

        var weekEvents = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.Date != null
                && e.Date >= weekStart
                && e.Date <= weekEnd)
            .ToListAsync();

        // Collect linked dates from MovedFromDate/MovedToDate
        var linkedDates = new HashSet<DateOnly>();
        foreach (var ev in weekEvents)
        {
            if (ev.MovedFromDate.HasValue) linkedDates.Add(ev.MovedFromDate.Value);
            if (ev.MovedToDate.HasValue) linkedDates.Add(ev.MovedToDate.Value);
        }

        // Load and remove linked events in OTHER weeks
        if (linkedDates.Count > 0)
        {
            var linkedEvents = await db.CalendarEvents
                .Where(e => e.HomeGroupId == groupId
                    && e.Type == CalendarEventType.HomeGroup
                    && !e.IsRecurring
                    && e.Date != null
                    && linkedDates.Contains(e.Date.Value)
                    && (e.Date < weekStart || e.Date > weekEnd))
                .ToListAsync();
            db.CalendarEvents.RemoveRange(linkedEvents);
        }

        db.CalendarEvents.RemoveRange(weekEvents);

        // Restore plan(s) if requested
        if (request.RestorePlan)
        {
            var weekStartStr = weekStart.ToString("yyyy-MM-dd");
            var weekEndStr = weekEnd.ToString("yyyy-MM-dd");

            var plansInWeek = await db.MeetingPlans
                .Include(p => p.Blocks)
                .Where(p => p.HomeGroupId == groupId
                    && string.Compare(p.MeetingDate, weekStartStr) >= 0
                    && string.Compare(p.MeetingDate, weekEndStr) <= 0
                    && p.OriginalMeetingDate != null)
                .ToListAsync();

            foreach (var plan in plansInWeek)
            {
                var existing = await db.MeetingPlans
                    .Include(p => p.Blocks)
                    .FirstOrDefaultAsync(p => p.HomeGroupId == groupId && p.MeetingDate == plan.OriginalMeetingDate);
                if (existing is not null && existing.Id != plan.Id)
                {
                    db.MeetingPlanBlocks.RemoveRange(existing.Blocks);
                    db.MeetingPlans.Remove(existing);
                }
                plan.MeetingDate = plan.OriginalMeetingDate!;
                plan.OriginalMeetingDate = null;
                plan.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    private static DateOnly SnapToMonday(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }
}
