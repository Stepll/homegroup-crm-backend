using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Groups;
using HomeGroup.API.Models.DTOs.People;
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
                g.IsActive, g.Members.Count))
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
            group.LeaderId, group.Leader?.Name, group.IsActive, group.Members.Count));
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<PersonResponse>>> GetMembers(long id)
    {
        var members = await db.HomeGroupMembers
            .Where(m => m.HomeGroupId == id)
            .Include(m => m.Person)
            .OrderBy(m => m.Person.Name)
            .Select(m => new PersonResponse(m.Person.Id, m.Person.Name, m.Person.LastName, m.Person.Phone, m.Person.Email, m.Person.Notes, m.Person.Status, m.Person.PrimaryGroupId, null, null, m.Person.CreatedAt))
            .ToListAsync();

        return Ok(members);
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
            Location = request.Location,
            LeaderId = request.LeaderId,
        };

        db.HomeGroups.Add(group);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = group.Id },
            new GroupResponse(group.Id, group.Name, group.Description, group.Color, group.MeetingDay, group.MeetingTime, group.Location, group.LeaderId, null, group.IsActive, 0));
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
        group.Location = request.Location;
        group.LeaderId = request.LeaderId;
        group.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        return Ok(new GroupResponse(group.Id, group.Name, group.Description, group.Color, group.MeetingDay, group.MeetingTime, group.Location, group.LeaderId, group.Leader?.Name, group.IsActive, group.Members.Count));
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

        // Org team (admins with PrimaryGroupId = this group)
        var orgAdmins = await db.Users
            .Where(u => u.PrimaryGroupId == id)
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email })
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
            return new CabinetOrgMember(a.Id, a.Name, a.LastName, a.Email, myOversees.Count, myOversees);
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

        return Ok(new GroupCabinetResponse(
            new CabinetGroupInfo(group.Id, group.Name, group.Color, group.MeetingDay, group.MeetingTime, group.Location),
            nextMeeting?.ToString("yyyy-MM-dd"),
            lastMeeting?.ToString("yyyy-MM-dd"),
            lastAttendance,
            upcomingEvents,
            orgTeam,
            stats));
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
}
