using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Calendar;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/calendar")]
[Authorize]
public class CalendarController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CalendarOccurrenceDto>>> GetOccurrences(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string? types,
        [FromQuery] string? groupIds)
    {
        if (!DateOnly.TryParse(from, out var fromDate) || !DateOnly.TryParse(to, out var toDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        var query = db.CalendarEvents
            .Include(e => e.Room)
            .Include(e => e.HomeGroup)
            .AsQueryable();

        if (!string.IsNullOrEmpty(types))
        {
            var typeList = types.Split(',')
                .Select(t => Enum.TryParse<CalendarEventType>(t, true, out var et) ? et : (CalendarEventType?)null)
                .Where(t => t.HasValue).Select(t => t!.Value)
                .ToList();
            if (typeList.Count > 0)
                query = query.Where(e => typeList.Contains(e.Type));
        }

        HashSet<long>? groupIdSet = null;
        if (!string.IsNullOrEmpty(groupIds))
        {
            groupIdSet = groupIds.Split(',').Select(long.Parse).ToHashSet();
            query = query.Where(e =>
                e.Type != CalendarEventType.HomeGroup ||
                (e.HomeGroupId.HasValue && groupIdSet.Contains(e.HomeGroupId.Value)));
        }

        var events = await query.ToListAsync();
        var result = new List<CalendarOccurrenceDto>();

        static DateOnly GetWeekMonday(DateOnly date) {
            int dow = (int)date.DayOfWeek;
            int daysFromMonday = dow == 0 ? 6 : dow - 1;
            return date.AddDays(-daysFromMonday);
        }

        // Load suppression markers from full week range (Mon–Sun), independent of types filter.
        // This ensures a cancellation on any day of the week suppresses the ghost for that week.
        var weekStart = GetWeekMonday(fromDate);
        var weekEnd = GetWeekMonday(toDate).AddDays(6);
        var suppressionQuery = db.CalendarEvents
            .Where(e => !e.IsRecurring
                        && e.Type == CalendarEventType.HomeGroup
                        && e.HomeGroupId.HasValue
                        && e.Date.HasValue
                        && e.IsHomeGroupMeeting.HasValue
                        && e.Date >= weekStart
                        && e.Date <= weekEnd);
        if (groupIdSet != null)
            suppressionQuery = suppressionQuery.Where(e => groupIdSet.Contains(e.HomeGroupId!.Value));

        var suppressedWeeks = (await suppressionQuery
                .Select(e => new { e.HomeGroupId, e.Date })
                .ToListAsync())
            .Select(e => (e.HomeGroupId!.Value, GetWeekMonday(e.Date!.Value)))
            .ToHashSet();

        foreach (var evt in events)
        {
            if (evt.IsRecurring && evt.RecurringDayOfWeek.HasValue)
            {
                for (var d = fromDate; d <= toDate; d = d.AddDays(1))
                {
                    if ((int)d.DayOfWeek == evt.RecurringDayOfWeek.Value)
                    {
                        bool isGhost = evt.Type == CalendarEventType.HomeGroup;
                        // Suppress ghost if there's a suppression marker for that week
                        if (isGhost && evt.HomeGroupId.HasValue &&
                            suppressedWeeks.Contains((evt.HomeGroupId.Value, GetWeekMonday(d))))
                            continue;
                        result.Add(ToOccurrence(evt, d, isGhost));
                    }
                }
            }
            else if (!evt.IsRecurring && evt.Date.HasValue && evt.Date >= fromDate && evt.Date <= toDate)
            {
                // Don't show cancellation markers in calendar (IsHomeGroupMeeting=false)
                if (evt.IsHomeGroupMeeting == false) continue;
                result.Add(ToOccurrence(evt, evt.Date.Value, false));
            }
        }

        return Ok(result.OrderBy(o => o.Date).ThenBy(o => o.StartTime).ToList());
    }

    [HttpGet("events")]
    public async Task<ActionResult<List<CalendarEventDto>>> GetEvents()
    {
        var events = await db.CalendarEvents
            .Include(e => e.Room)
            .Include(e => e.HomeGroup)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return Ok(events.Select(ToDto).ToList());
    }

    [HttpGet("events/{id}")]
    public async Task<ActionResult<CalendarEventDto>> GetEvent(long id)
    {
        var evt = await db.CalendarEvents
            .Include(e => e.Room)
            .Include(e => e.HomeGroup)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (evt is null) return NotFound();
        return Ok(ToDto(evt));
    }

    [HttpPost("events")]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateCalendarEventRequest request)
    {
        if (!Enum.TryParse<CalendarEventType>(request.Type, true, out var type))
            return BadRequest("Invalid type. Use Recurring, Global or HomeGroup.");

        var evt = new CalendarEvent
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Location = request.Location?.Trim(),
            RoomId = request.RoomId,
            Type = type,
            HomeGroupId = request.HomeGroupId,
            IsRecurring = request.IsRecurring,
            RecurringDayOfWeek = request.IsRecurring ? request.RecurringDayOfWeek : null,
            StartTime = ParseTime(request.StartTime),
            EndTime = ParseTime(request.EndTime),
            Date = !request.IsRecurring && request.Date != null ? DateOnly.Parse(request.Date) : null,
            IsHomeGroupMeeting = request.IsHomeGroupMeeting,
        };

        db.CalendarEvents.Add(evt);
        await db.SaveChangesAsync();

        await db.Entry(evt).Reference(e => e.Room).LoadAsync();
        await db.Entry(evt).Reference(e => e.HomeGroup).LoadAsync();
        return Ok(ToDto(evt));
    }

    [HttpPut("events/{id}")]
    public async Task<ActionResult<CalendarEventDto>> Update(long id, UpdateCalendarEventRequest request)
    {
        var evt = await db.CalendarEvents
            .Include(e => e.Room)
            .Include(e => e.HomeGroup)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (evt is null) return NotFound();

        if (!Enum.TryParse<CalendarEventType>(request.Type, true, out var type))
            return BadRequest("Invalid type. Use Recurring, Global or HomeGroup.");

        evt.Title = request.Title.Trim();
        evt.Description = request.Description?.Trim();
        evt.Location = request.Location?.Trim();
        evt.RoomId = request.RoomId;
        evt.Type = type;
        evt.HomeGroupId = request.HomeGroupId;
        evt.IsRecurring = request.IsRecurring;
        evt.RecurringDayOfWeek = request.IsRecurring ? request.RecurringDayOfWeek : null;
        evt.StartTime = ParseTime(request.StartTime);
        evt.EndTime = ParseTime(request.EndTime);
        evt.Date = !request.IsRecurring && request.Date != null ? DateOnly.Parse(request.Date) : null;
        evt.IsHomeGroupMeeting = request.IsHomeGroupMeeting;

        await db.SaveChangesAsync();

        await db.Entry(evt).Reference(e => e.Room).LoadAsync();
        await db.Entry(evt).Reference(e => e.HomeGroup).LoadAsync();
        return Ok(ToDto(evt));
    }

    [HttpDelete("events/{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var evt = await db.CalendarEvents.FindAsync(id);
        if (evt is null) return NotFound();
        db.CalendarEvents.Remove(evt);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CalendarEventDto ToDto(CalendarEvent e) => new(
        e.Id, e.Title, e.Description, e.Location,
        e.RoomId, e.Room is null ? null : new RoomDto(e.Room.Id, e.Room.Name, e.Room.Building, e.Room.Floor, e.Room.Color),
        e.Type.ToString(),
        e.HomeGroupId, e.HomeGroup?.Name, e.HomeGroup?.Color,
        e.IsRecurring, e.RecurringDayOfWeek,
        e.StartTime?.ToString("HH:mm"), e.EndTime?.ToString("HH:mm"),
        e.Date?.ToString("yyyy-MM-dd"),
        e.IsHomeGroupMeeting);

    private static CalendarOccurrenceDto ToOccurrence(CalendarEvent e, DateOnly date, bool isGhost = false) => new(
        e.Id, e.Title, e.Description, e.Location,
        e.RoomId, e.Room is null ? null : new RoomDto(e.Room.Id, e.Room.Name, e.Room.Building, e.Room.Floor, e.Room.Color),
        e.Type.ToString(),
        e.HomeGroupId, e.HomeGroup?.Name, e.HomeGroup?.Color,
        date.ToString("yyyy-MM-dd"),
        e.StartTime?.ToString("HH:mm"), e.EndTime?.ToString("HH:mm"),
        isGhost);

    private static TimeOnly? ParseTime(string? time) =>
        time is not null && TimeOnly.TryParse(time, out var t) ? t : null;
}
