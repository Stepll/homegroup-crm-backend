using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Groups;
using HomeGroup.API.Models.DTOs.People;
using HomeGroup.API.Models.DTOs.PersonStatuses;
using HomeGroup.API.Models.DTOs.Planning;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/groups")]
[Authorize]
public class GroupsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GroupResponse>>> GetAll()
    {
        var groups = await db.HomeGroups
            .Include(g => g.Leader)
            .Include(g => g.Members)
            .OrderBy(g => g.Name)
            .Select(g => new GroupResponse(
                g.Id, g.Name, g.Description, g.Color, g.MeetingDay, g.MeetingTime, g.Location,
                g.LeaderId, g.Leader != null ? g.Leader.Name : null,
                g.IsActive, g.Members.Count, g.TelegramGroupId, g.MeetingEndTime))
            .ToListAsync();

        return Ok(groups);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GroupResponse>> GetById(long id)
    {
        var group = await db.HomeGroups
            .Include(g => g.Leader)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null) return NotFound();

        return Ok(new GroupResponse(
            group.Id, group.Name, group.Description, group.Color, group.MeetingDay, group.MeetingTime, group.Location,
            group.LeaderId, group.Leader?.Name, group.IsActive, group.Members.Count, group.TelegramGroupId));
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<GroupMemberResponse>>> GetMembers(long id)
    {
        var personMembers = await db.HomeGroupMembers
            .Where(m => m.HomeGroupId == id)
            .Include(m => m.Person).ThenInclude(p => p.PersonStatus)
            .Include(m => m.Person).ThenInclude(p => p.PrimaryGroup)
            .OrderBy(m => m.Person.Name)
            .Select(m => new GroupMemberResponse(
                m.Person.Id, m.Person.Name, m.Person.LastName, m.Person.Phone, m.Person.Email, m.Person.Notes,
                m.Person.PersonStatus != null ? new PersonStatusDto(m.Person.PersonStatus.Id, m.Person.PersonStatus.Name, m.Person.PersonStatus.Color) : null,
                m.Person.PrimaryGroupId, m.Person.PrimaryGroup != null ? m.Person.PrimaryGroup.Name : null,
                m.Person.PrimaryGroup != null ? m.Person.PrimaryGroup.Color : null,
                m.Person.CreatedAt, false, null, null))
            .ToListAsync();

        var adminMembers = await db.Users
            .Where(u => u.PrimaryGroupId == id && u.Id != 0)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PersonStatus)
            .Include(u => u.PrimaryGroup)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var adminResponses = adminMembers.Select(u =>
        {
            var primaryRole = u.UserRoles.Select(ur => ur.Role).FirstOrDefault();
            var roleTag = primaryRole is null ? null : new MemberRoleTagDto(primaryRole.Name, primaryRole.Color);
            var status = u.PersonStatus is null ? null : new PersonStatusDto(u.PersonStatus.Id, u.PersonStatus.Name, u.PersonStatus.Color);
            return new GroupMemberResponse(
                u.Id, u.Name, u.LastName, u.Phone, u.Email, u.Notes,
                status, u.PrimaryGroupId, u.PrimaryGroup?.Name, u.PrimaryGroup?.Color,
                u.CreatedAt, true, u.Id, roleTag);
        }).ToList();

        var all = personMembers.Concat(adminResponses).OrderBy(m => m.Name).ToList();
        return Ok(all);
    }

    [HttpPost]
    public async Task<ActionResult<GroupResponse>> Create(CreateGroupRequest request)
    {
        var group = new HomeGroupEntity
        {
            Name = request.Name,
            Description = request.Description,
            Color = request.Color,
            MeetingDay = request.MeetingDay,
            MeetingTime = request.MeetingTime,
            MeetingEndTime = request.MeetingEndTime,
            Location = request.Location,
            LeaderId = request.LeaderId,
            TelegramGroupId = request.TelegramGroupId,
        };

        db.HomeGroups.Add(group);
        await db.SaveChangesAsync();
        await SyncGroupCalendarEvent(group, db);

        return CreatedAtAction(nameof(GetById), new { id = group.Id },
            new GroupResponse(group.Id, group.Name, group.Description, group.Color, group.MeetingDay, group.MeetingTime, group.Location, group.LeaderId, null, group.IsActive, 0, group.TelegramGroupId, group.MeetingEndTime));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GroupResponse>> Update(long id, UpdateGroupRequest request)
    {
        var group = await db.HomeGroups.Include(g => g.Leader).Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        group.Name = request.Name;
        group.Description = request.Description;
        group.Color = request.Color;
        group.MeetingDay = request.MeetingDay;
        group.MeetingTime = request.MeetingTime;
        group.MeetingEndTime = request.MeetingEndTime;
        group.Location = request.Location;
        group.LeaderId = request.LeaderId;
        group.IsActive = request.IsActive;
        group.TelegramGroupId = request.TelegramGroupId;

        await db.SaveChangesAsync();
        await SyncGroupCalendarEvent(group, db);
        return Ok(new GroupResponse(group.Id, group.Name, group.Description, group.Color, group.MeetingDay, group.MeetingTime, group.Location, group.LeaderId, group.Leader?.Name, group.IsActive, group.Members.Count, group.TelegramGroupId, group.MeetingEndTime));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        db.HomeGroups.Remove(group);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(long id, AddMemberRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();
        if (!await db.People.AnyAsync(p => p.Id == request.PersonId)) return NotFound();

        if (await db.HomeGroupMembers.AnyAsync(m => m.HomeGroupId == id && m.PersonId == request.PersonId))
            return Conflict(new { message = "Людина вже є учасником цієї групи" });

        db.HomeGroupMembers.Add(new HomeGroupMember { HomeGroupId = id, PersonId = request.PersonId, Role = request.Role });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id}/members/sync")]
    public async Task<IActionResult> SyncMembers(long id, SyncMembersRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var current = await db.HomeGroupMembers.Where(m => m.HomeGroupId == id).ToListAsync();
        var currentIds = current.Select(m => m.PersonId).ToHashSet();
        var newIds = request.PersonIds.ToHashSet();

        var addedIds = newIds.Except(currentIds).ToList();
        var removedIds = currentIds.Except(newIds).ToList();

        db.HomeGroupMembers.RemoveRange(current.Where(m => !newIds.Contains(m.PersonId)));

        foreach (var personId in addedIds)
        {
            if (await db.People.AnyAsync(p => p.Id == personId))
                db.HomeGroupMembers.Add(new HomeGroupMember { HomeGroupId = id, PersonId = personId });
        }

        // Sync PrimaryGroupId for added/removed members
        if (addedIds.Count > 0)
        {
            var addedPeople = await db.People.Where(p => addedIds.Contains(p.Id)).ToListAsync();
            foreach (var person in addedPeople)
                person.PrimaryGroupId = id;
        }

        if (removedIds.Count > 0)
        {
            var removedPeople = await db.People
                .Where(p => removedIds.Contains(p.Id) && p.PrimaryGroupId == id)
                .ToListAsync();
            foreach (var person in removedPeople)
                person.PrimaryGroupId = null;
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}/members/{personId}")]
    public async Task<IActionResult> RemoveMember(long id, long personId)
    {
        var member = await db.HomeGroupMembers.FirstOrDefaultAsync(m => m.HomeGroupId == id && m.PersonId == personId);
        if (member is null) return NotFound();

        db.HomeGroupMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Custom field definitions for a group

    [HttpGet("{id}/custom-fields")]
    public async Task<ActionResult<List<GroupCustomFieldDto>>> GetCustomFields(long id)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var fields = await db.HomeGroupCustomFields
            .Where(f => f.HomeGroupId == id)
            .OrderBy(f => f.CreatedAt)
            .Select(f => new GroupCustomFieldDto(f.Id, f.Name))
            .ToListAsync();

        return Ok(fields);
    }

    [HttpPost("{id}/custom-fields")]
    public async Task<ActionResult<GroupCustomFieldDto>> AddCustomField(long id, CreateGroupCustomFieldRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var field = new HomeGroupCustomField { HomeGroupId = id, Name = request.Name.Trim() };
        db.HomeGroupCustomFields.Add(field);
        await db.SaveChangesAsync();

        return Ok(new GroupCustomFieldDto(field.Id, field.Name));
    }

    [HttpDelete("{id}/custom-fields/{fieldId}")]
    public async Task<IActionResult> DeleteCustomField(long id, long fieldId)
    {
        var field = await db.HomeGroupCustomFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.HomeGroupId == id);
        if (field is null) return NotFound();

        db.HomeGroupCustomFields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Plans ─────────────────────────────────────────────────────────────────

    [HttpGet("{id}/plans")]
    public async Task<ActionResult<List<MeetingPlanSummaryDto>>> GetPlans(long id)
    {
        var plans = await db.MeetingPlans
            .Where(p => p.HomeGroupId == id)
            .OrderByDescending(p => p.MeetingDate)
            .Select(p => new MeetingPlanSummaryDto(p.Id, p.MeetingDate, p.Blocks.Count, p.AppliedTemplateName))
            .ToListAsync();

        return Ok(plans);
    }

    [HttpGet("{id}/plans/date/{date}")]
    public async Task<ActionResult<MeetingPlanDto>> GetPlanByDate(long id, string date)
    {
        var plan = await db.MeetingPlans
            .Include(p => p.Blocks.OrderBy(b => b.Order))
            .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == date);

        if (plan is null) return NotFound();
        return Ok(ToPlanDto(plan));
    }

    [HttpPost("{id}/plans")]
    public async Task<ActionResult<MeetingPlanDto>> SavePlan(long id, SavePlanRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var plan = await db.MeetingPlans
            .Include(p => p.Blocks)
            .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == request.MeetingDate);

        if (plan is null)
        {
            plan = new HomeMeetingPlan { HomeGroupId = id, MeetingDate = request.MeetingDate };
            db.MeetingPlans.Add(plan);
        }
        else
        {
            db.MeetingPlanBlocks.RemoveRange(plan.Blocks);
            plan.UpdatedAt = DateTime.UtcNow;
        }

        plan.AppliedTemplateName = request.AppliedTemplateName;
        plan.Blocks = request.Blocks.Select(b => new MeetingPlanBlock
        {
            Order = b.Order,
            Time = b.Time.Trim(),
            Title = b.Title.Trim(),
            Info = b.Info?.Trim(),
            Responsible = b.Responsible?.Trim(),
        }).ToList();

        await db.SaveChangesAsync();

        await db.Entry(plan).Collection(p => p.Blocks).LoadAsync();
        return Ok(ToPlanDto(plan));
    }

    private static MeetingPlanDto ToPlanDto(HomeMeetingPlan p) => new(
        p.Id, p.HomeGroupId, p.MeetingDate, p.AppliedTemplateName,
        p.Blocks.OrderBy(b => b.Order)
            .Select(b => new PlanBlockDto(b.Id, b.Order, b.Time, b.Title, b.Info, b.Responsible))
            .ToList(),
        p.UpdatedAt);

    [HttpGet("{id}/events")]
    public async Task<ActionResult<List<GroupEventDto>>> GetEvents(long id)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await db.GroupEvents
            .Where(e => e.HomeGroupId == id)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        var result = events
            .Select(e => (e, days: ComputeDaysUntil(e.Month, e.Day, e.Year, today)))
            .Where(x => x.days >= 0)
            .OrderBy(x => x.days)
            .Take(5)
            .Select(x => new GroupEventDto(x.e.Id, x.e.Name, x.e.Month, x.e.Day, x.e.Year, x.days))
            .ToList();

        return Ok(result);
    }

    [HttpPost("{id}/events")]
    public async Task<ActionResult<GroupEventDto>> AddEvent(long id, CreateGroupEventRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var evt = new GroupEvent
        {
            HomeGroupId = id,
            Name = request.Name.Trim(),
            Month = request.Month,
            Day = request.Day,
            Year = request.Year,
        };
        db.GroupEvents.Add(evt);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(new GroupEventDto(evt.Id, evt.Name, evt.Month, evt.Day, evt.Year, ComputeDaysUntil(evt.Month, evt.Day, evt.Year, today)));
    }

    [HttpDelete("{id}/events/{eventId}")]
    public async Task<IActionResult> DeleteEvent(long id, long eventId)
    {
        var evt = await db.GroupEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.HomeGroupId == id);
        if (evt is null) return NotFound();
        db.GroupEvents.Remove(evt);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/cabinet")]
    public async Task<ActionResult<GroupCabinetResponse>> GetCabinet(long id)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nowTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        var nextMeeting = ComputeNextMeeting(group.MeetingDay, group.MeetingTime, today, nowTime);
        var lastMeeting = ComputeLastMeeting(group.MeetingDay, group.MeetingTime, today, nowTime);


        // Last attendance summary
        CabinetAttendanceSummary? lastAttendance = null;
        if (lastMeeting.HasValue)
        {
            var records = await db.Attendances
                .Where(a => a.HomeGroupId == id && a.MeetingDate == lastMeeting.Value)
                .ToListAsync();
            if (records.Count > 0)
                lastAttendance = new CabinetAttendanceSummary(records.Count(r => r.WasPresent), records.Count);
            else
            {
                // No attendance records yet — just show member count as total
                var memberCount = await db.HomeGroupMembers.CountAsync(m => m.HomeGroupId == id);
                if (memberCount > 0) lastAttendance = new CabinetAttendanceSummary(0, memberCount);
            }
        }

        // Upcoming birthdays (next 30 days)
        var members = await db.People
            .Where(p => p.PrimaryGroupId == id && p.DateOfBirth != null)
            .Select(p => new { p.Id, p.Name, p.LastName, p.DateOfBirth })
            .ToListAsync();

        var upcomingEvents = members
            .Select(p =>
            {
                var dob = p.DateOfBirth!.Value;
                var thisYear = new DateOnly(today.Year, dob.Month, dob.Day);
                if (thisYear < today) thisYear = thisYear.AddYears(1);
                var days = thisYear.DayNumber - today.DayNumber;
                return new { p.Id, FullName = $"{p.Name}{(p.LastName is null ? "" : " " + p.LastName)}", dob, days };
            })
            .Where(x => x.days <= 30)
            .OrderBy(x => x.days)
            .Select(x => new CabinetUpcomingEvent(x.Id, x.FullName, x.dob.ToString("yyyy-MM-dd"), x.days))
            .ToList();

        // Org team: users whose primary group is this group, excluding superadmin (id=0)
        var orgAdmins = await db.Users
            .Where(u => u.PrimaryGroupId == id && u.Id != 0)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();

        var adminIds = orgAdmins.Select(a => a.Id).ToList();
        var oversees = await db.People
            .Where(p => p.OversightUserId != null && adminIds.Contains(p.OversightUserId!.Value))
            .Select(p => new { p.Id, p.Name, p.LastName, p.OversightUserId })
            .ToListAsync();

        var orgTeam = orgAdmins.Select(a =>
        {
            var myOversees = oversees
                .Where(p => p.OversightUserId == a.Id)
                .Select(p => new CabinetOverseePerson(p.Id, $"{p.Name}{(p.LastName is null ? "" : " " + p.LastName)}"))
                .ToList();
            var primaryRole = a.UserRoles.Select(ur => ur.Role).FirstOrDefault();
            var roleTag = primaryRole is null ? null : new CabinetRoleTag(primaryRole.Name, primaryRole.Color);
            return new CabinetOrgMember(a.Id, a.Name, a.LastName, a.Email, myOversees.Count, myOversees, roleTag);
        }).ToList();

        // Stats
        var totalMembers = await db.HomeGroupMembers.CountAsync(m => m.HomeGroupId == id);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var newThisMonth = await db.People.CountAsync(p => p.PrimaryGroupId == id && p.CreatedAt >= monthStart);

        var allAttendance = await db.Attendances.Where(a => a.HomeGroupId == id).ToListAsync();
        double avgRate = 0;
        if (allAttendance.Count > 0 && totalMembers > 0)
        {
            var byDate = allAttendance.GroupBy(a => a.MeetingDate);
            avgRate = byDate.Average(g => g.Count(r => r.WasPresent) * 100.0 / totalMembers);
        }

        var stats = new CabinetStats(Math.Round(avgRate, 1), newThisMonth, totalMembers);

        // Use one-time override date if set and not yet expired
        var nextMeetingStr = group.NextMeetingOverrideDate is not null
            && DateOnly.TryParse(group.NextMeetingOverrideDate, out var overrideDate)
            && overrideDate >= today
            ? group.NextMeetingOverrideDate
            : nextMeeting?.ToString("yyyy-MM-dd");

        var hasPlan = nextMeetingStr != null && await db.MeetingPlans
            .AnyAsync(p => p.HomeGroupId == id && p.MeetingDate == nextMeetingStr);

        // Calendar data for next meeting
        long? nextMeetingRoomId = null;
        List<CabinetCalendarEvent> nextMeetingEvents = [];
        List<CabinetCalendarEvent> nextMeetingConflicts = [];

        if (nextMeetingStr != null && DateOnly.TryParse(nextMeetingStr, out var nextDate))
        {
            // Auto-book: ensure booking CalendarEvent exists for next meeting date
            if (group.AutoBookRoomId.HasValue)
            {
                var autoBooking = await db.CalendarEvents
                    .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup
                        && !e.IsRecurring && e.HomeGroupId == id && e.Date == nextDate);

                if (autoBooking is null)
                {
                    autoBooking = new CalendarEvent
                    {
                        Title = group.Name,
                        Type = CalendarEventType.HomeGroup,
                        HomeGroupId = id,
                        IsRecurring = false,
                        Date = nextDate,
                        StartTime = TimeOnly.TryParse(group.MeetingTime, out var ast) ? ast : null,
                        EndTime = TimeOnly.TryParse(group.MeetingEndTime, out var aet) ? aet : null,
                        RoomId = group.AutoBookRoomId,
                    };
                    db.CalendarEvents.Add(autoBooking);
                    await db.SaveChangesAsync();
                }
                else if (autoBooking.RoomId != group.AutoBookRoomId)
                {
                    autoBooking.RoomId = group.AutoBookRoomId;
                    await db.SaveChangesAsync();
                }
                nextMeetingRoomId = group.AutoBookRoomId;
            }
            else
            {
                var manualBooking = await db.CalendarEvents
                    .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup
                        && !e.IsRecurring && e.HomeGroupId == id && e.Date == nextDate);
                nextMeetingRoomId = manualBooking?.RoomId;
            }

            // Load all events on the next meeting date (for mini calendar + conflicts)
            var eventsOnDate = await db.CalendarEvents
                .Include(e => e.Room)
                .Include(e => e.HomeGroup)
                .Where(e =>
                    (!e.IsRecurring && e.Date == nextDate) ||
                    (e.IsRecurring && e.RecurringDayOfWeek == (int)nextDate.DayOfWeek))
                .Where(e => !(e.Type == CalendarEventType.HomeGroup && e.HomeGroupId == id && e.IsRecurring))
                .ToListAsync();

            nextMeetingEvents = eventsOnDate.Select(e => new CabinetCalendarEvent(
                e.Id, e.Title, e.Type.ToString(),
                e.StartTime?.ToString("HH:mm"), e.EndTime?.ToString("HH:mm"),
                e.RoomId, e.Room?.Color, e.HomeGroup?.Color
            )).ToList();

            // Detect conflicts: events overlapping with the group's meeting time
            if (TimeOnly.TryParse(group.MeetingTime, out var mStart))
            {
                var mStartMin = mStart.Hour * 60 + mStart.Minute;
                var mEndMin = TimeOnly.TryParse(group.MeetingEndTime, out var mEnd)
                    ? mEnd.Hour * 60 + mEnd.Minute
                    : mStartMin + 120;

                nextMeetingConflicts = nextMeetingEvents
                    .Where(e =>
                    {
                        if (e.StartTime == null) return false;
                        var sp = e.StartTime.Split(':');
                        var eStart = int.Parse(sp[0]) * 60 + int.Parse(sp[1]);
                        var eEnd = eStart + 120;
                        if (e.EndTime != null)
                        {
                            var ep = e.EndTime.Split(':');
                            eEnd = int.Parse(ep[0]) * 60 + int.Parse(ep[1]);
                        }
                        return eStart < mEndMin && mStartMin < eEnd;
                    })
                    .ToList();
            }
        }

        return Ok(new GroupCabinetResponse(
            new CabinetGroupInfo(group.Id, group.Name, group.Color, group.MeetingDay, group.MeetingTime, group.Location, group.TelegramGroupId, group.MeetingEndTime, group.AutoBookRoomId),
            nextMeetingStr,
            lastMeeting?.ToString("yyyy-MM-dd"),
            lastAttendance,
            upcomingEvents,
            orgTeam,
            stats,
            hasPlan,
            nextMeetingRoomId,
            nextMeetingEvents,
            nextMeetingConflicts,
            group.AutoBookRoomId.HasValue));
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<GroupStatsResponse>> GetStats(long id, [FromQuery] string period = "3m")
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = period switch
        {
            "1m" => today.AddMonths(-1),
            "6m" => today.AddMonths(-6),
            _ => today.AddMonths(-3),
        };
        var periodStartDt = new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 0, 0, 0, DateTimeKind.Utc);

        var attendance = await db.Attendances
            .Include(a => a.Person)
            .Include(a => a.User)
            .Where(a => a.HomeGroupId == id && a.MeetingDate >= periodStart)
            .OrderBy(a => a.MeetingDate)
            .ToListAsync();

        var metaLookup = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == id && m.MeetingDate >= periodStart)
            .ToDictionaryAsync(m => m.MeetingDate, m => m.GuestCount);

        // Per-meeting stats
        var byDate = attendance.GroupBy(a => a.MeetingDate).OrderBy(g => g.Key).ToList();

        var meetings = byDate.Select(g =>
        {
            var presentCount = g.Count(a => a.WasPresent);
            var totalMembers = g.Count();
            var guestCount = metaLookup.GetValueOrDefault(g.Key, 0);
            var absentees = g.Where(a => !a.WasPresent)
                .Select(a => AttendanceMemberName(a))
                .OrderBy(n => n)
                .ToList();
            return new MeetingStatsItem(
                g.Key.ToString("yyyy-MM-dd"),
                presentCount,
                totalMembers,
                totalMembers == 0 ? 0 : Math.Round(presentCount * 100.0 / totalMembers, 1),
                guestCount,
                absentees);
        }).ToList();

        // Per-person stats
        var personStats = attendance
            .GroupBy(a => new {
                Key = a.PersonId.HasValue ? $"p{a.PersonId}" : $"u{a.UserId}",
                PersonId = a.PersonId,
                UserId = a.UserId,
                Name = AttendanceMemberName(a)
            })
            .Select(g =>
            {
                var present = g.Count(a => a.WasPresent);
                var total = g.Count();
                return new PersonAttendanceStat(
                    g.Key.PersonId,
                    g.Key.UserId,
                    g.Key.Name,
                    present,
                    total,
                    total == 0 ? 0 : Math.Round(present * 100.0 / total, 1));
            })
            .OrderByDescending(p => p.AttendanceRate)
            .ThenByDescending(p => p.PresentCount)
            .ToList();

        // Summary
        var avgRate = meetings.Count > 0 && meetings.Any(m => m.TotalMembers > 0)
            ? meetings.Where(m => m.TotalMembers > 0).Average(m => m.AttendanceRate)
            : 0;
        var totalGuests = metaLookup.Values.Sum();
        var newMembers = await db.People.CountAsync(p => p.PrimaryGroupId == id && p.CreatedAt >= periodStartDt);

        var summary = new StatsSummary(Math.Round(avgRate, 1), meetings.Count, totalGuests, newMembers);

        return Ok(new GroupStatsResponse(summary, meetings, personStats));
    }

    [HttpPut("{id}/next-meeting")]
    public async Task<IActionResult> SetNextMeeting(long id, SetNextMeetingRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();
        group.NextMeetingOverrideDate = request.Date;

        // Move plan from old date to new date if both are provided
        if (!string.IsNullOrEmpty(request.OldDate) && !string.IsNullOrEmpty(request.Date))
        {
            var plan = await db.MeetingPlans
                .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == request.OldDate);
            if (plan is not null)
            {
                // Remove any existing plan for the new date to avoid unique conflict
                var existing = await db.MeetingPlans
                    .Include(p => p.Blocks)
                    .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == request.Date);
                if (existing is not null)
                {
                    db.MeetingPlanBlocks.RemoveRange(existing.Blocks);
                    db.MeetingPlans.Remove(existing);
                }
                plan.MeetingDate = request.Date;
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id}/skip-meeting")]
    public async Task<ActionResult<object>> SkipMeeting(long id)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nowTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        // Determine current next meeting (override or computed)
        DateOnly currentNext;
        if (group.NextMeetingOverrideDate is not null
            && DateOnly.TryParse(group.NextMeetingOverrideDate, out var overrideDate)
            && overrideDate >= today)
        {
            currentNext = overrideDate;
        }
        else
        {
            var computed = ComputeNextMeeting(group.MeetingDay, group.MeetingTime, today, nowTime);
            if (computed is null) return BadRequest(new { message = "Не вказано день тижня для домашки" });
            currentNext = computed.Value;
        }

        // Find next occurrence of meeting day AFTER currentNext
        var nextAfter = ComputeNextMeeting(group.MeetingDay, group.MeetingTime, currentNext, TimeOnly.MinValue);
        if (nextAfter is null) return BadRequest(new { message = "Не вказано день тижня для домашки" });

        group.NextMeetingOverrideDate = nextAfter.Value.ToString("yyyy-MM-dd");
        await db.SaveChangesAsync();
        return Ok(new { date = group.NextMeetingOverrideDate });
    }

    [HttpDelete("{id}/plans/date/{date}")]
    public async Task<IActionResult> DeletePlanByDate(long id, string date)
    {
        var plan = await db.MeetingPlans
            .Include(p => p.Blocks)
            .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == date);
        if (plan is null) return NotFound();
        db.MeetingPlanBlocks.RemoveRange(plan.Blocks);
        db.MeetingPlans.Remove(plan);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Attendance helpers ────────────────────────────────────────────────────

    private static string AttendanceMemberName(Attendance a)
    {
        if (a.Person is not null)
            return $"{a.Person.Name}{(a.Person.LastName is null ? "" : " " + a.Person.LastName)}";
        if (a.User is not null)
            return $"{a.User.Name}{(a.User.LastName is null ? "" : " " + a.User.LastName)}";
        return "?";
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private static int ComputeDaysUntil(int month, int day, int? year, DateOnly today)
    {
        if (year.HasValue)
            return new DateOnly(year.Value, month, day).DayNumber - today.DayNumber;

        var thisYear = new DateOnly(today.Year, month, day);
        if (thisYear.DayNumber < today.DayNumber) thisYear = thisYear.AddYears(1);
        return thisYear.DayNumber - today.DayNumber;
    }

    // ── Meeting date helpers ──────────────────────────────────────────────────

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

    private static DateOnly? ComputeNextMeeting(string? meetingDay, string? meetingTime, DateOnly today, TimeOnly nowTime)
    {
        if (string.IsNullOrEmpty(meetingDay) || !UkrDays.TryGetValue(meetingDay, out var target)) return null;

        var daysUntil = ((int)target - (int)today.DayOfWeek + 7) % 7;

        if (daysUntil == 0)
        {
            // Today is the day — check if time has passed
            if (TimeOnly.TryParse(meetingTime, out var mt) && nowTime >= mt)
                daysUntil = 7;
        }

        return today.AddDays(daysUntil == 0 ? 7 : daysUntil);
    }

    private static DateOnly? ComputeLastMeeting(string? meetingDay, string? meetingTime, DateOnly today, TimeOnly nowTime)
    {
        if (string.IsNullOrEmpty(meetingDay) || !UkrDays.TryGetValue(meetingDay, out var target)) return null;

        var daysAgo = ((int)today.DayOfWeek - (int)target + 7) % 7;

        if (daysAgo == 0)
        {
            // Today is the day — the meeting is today only if time has already passed
            if (!TimeOnly.TryParse(meetingTime, out var mt) || nowTime < mt)
                daysAgo = 7;
        }

        return today.AddDays(-daysAgo == 0 ? -7 : -daysAgo);
    }

    [HttpPut("{id}/book-room")]
    public async Task<IActionResult> BookRoom(long id, BookRoomRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest("Invalid date");

        group.AutoBookRoomId = request.AutoBook ? request.RoomId : null;

        var booking = await db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring && e.HomeGroupId == id && e.Date == date);

        if (request.RoomId.HasValue)
        {
            if (booking is null)
            {
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Title = group.Name,
                    Type = CalendarEventType.HomeGroup,
                    HomeGroupId = id,
                    IsRecurring = false,
                    Date = date,
                    StartTime = TimeOnly.TryParse(group.MeetingTime, out var st) ? st : null,
                    EndTime = TimeOnly.TryParse(group.MeetingEndTime, out var et) ? et : null,
                    RoomId = request.RoomId,
                });
            }
            else
            {
                booking.RoomId = request.RoomId;
            }
        }
        else if (booking is not null)
        {
            db.CalendarEvents.Remove(booking);
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    private static async Task SyncGroupCalendarEvent(HomeGroupEntity group, AppDbContext db)
    {
        var existing = await db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup && e.HomeGroupId == group.Id && e.IsRecurring);

        var hasMeeting = !string.IsNullOrEmpty(group.MeetingDay) &&
                         UkrDays.TryGetValue(group.MeetingDay, out var dow);

        if (hasMeeting)
        {
            UkrDays.TryGetValue(group.MeetingDay!, out var dayOfWeek);
            TimeOnly.TryParse(group.MeetingTime, out var startTime);
            TimeOnly? endTime = TimeOnly.TryParse(group.MeetingEndTime, out var et) ? et : null;

            if (existing is null)
            {
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Title = group.Name,
                    Location = group.Location,
                    Type = CalendarEventType.HomeGroup,
                    HomeGroupId = group.Id,
                    IsRecurring = true,
                    RecurringDayOfWeek = (int)dayOfWeek,
                    StartTime = startTime == default ? null : startTime,
                    EndTime = endTime,
                });
            }
            else
            {
                existing.Title = group.Name;
                existing.Location = group.Location;
                existing.RecurringDayOfWeek = (int)dayOfWeek;
                existing.StartTime = startTime == default ? null : startTime;
                existing.EndTime = endTime;
            }
        }
        else if (existing is not null)
        {
            db.CalendarEvents.Remove(existing);
        }

        await db.SaveChangesAsync();
    }
}
