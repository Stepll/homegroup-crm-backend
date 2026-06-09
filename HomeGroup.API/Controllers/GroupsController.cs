using HomeGroup.API.Authorization;
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
        var raw = await db.HomeGroups
            .Include(g => g.Leader)
            .Include(g => g.Members)
            .OrderBy(g => g.Name)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nowTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        var groups = raw.Select(g =>
        {
            var computed = ComputeNextMeeting(g.MeetingDay, g.MeetingTime, today, nowTime);
            var nextMeetingDate = g.NextMeetingOverrideDate is not null
                && DateOnly.TryParse(g.NextMeetingOverrideDate, out var ov) && ov >= today
                ? g.NextMeetingOverrideDate
                : computed?.ToString("yyyy-MM-dd");
            return new GroupResponse(
                g.Id, g.Name, g.Description, g.Color, g.MeetingDay, g.MeetingTime, g.Location,
                g.LeaderId, g.Leader?.Name, g.IsActive, g.Members.Count, g.TelegramGroupId, g.MeetingEndTime,
                nextMeetingDate);
        }).ToList();

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
        // Active person members
        var personMemberEntities = await db.HomeGroupMembers
            .Where(m => m.HomeGroupId == id)
            .Include(m => m.Person).ThenInclude(p => p.PersonStatus)
            .Include(m => m.Person).ThenInclude(p => p.PrimaryGroup)
            .Include(m => m.Person).ThenInclude(p => p.OversightUser)
            .OrderBy(m => m.Person.Name)
            .ToListAsync();

        var personMembers = personMemberEntities.Select(m => new GroupMemberResponse(
            m.Person.Id, m.Person.Name, m.Person.LastName, m.Person.Phone, m.Person.Email, m.Person.Notes,
            m.Person.PersonStatus != null ? new PersonStatusDto(m.Person.PersonStatus.Id, m.Person.PersonStatus.Name, m.Person.PersonStatus.Color) : null,
            m.Person.PrimaryGroupId, m.Person.PrimaryGroup?.Name, m.Person.PrimaryGroup?.Color,
            m.Person.CreatedAt, false, null, null,
            m.Person.OversightUser != null ? m.Person.OversightUser.Name + (m.Person.OversightUser.LastName != null ? " " + m.Person.OversightUser.LastName : "") : null,
            m.JoinedAt)).ToList();

        // Active admin members
        var adminMembers = await db.Users
            .Where(u => u.PrimaryGroupId == id && u.Id != 0)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PersonStatus)
            .Include(u => u.PrimaryGroup)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var adminUserIds = adminMembers.Select(u => u.Id).ToList();
        var adminJoinDates = await db.UserHomeGroups
            .Where(uhg => uhg.HomeGroupId == id && adminUserIds.Contains(uhg.UserId))
            .ToDictionaryAsync(uhg => uhg.UserId, uhg => (DateTime?)uhg.AssignedAt);

        var adminResponses = adminMembers.Select(u =>
        {
            var primaryRole = u.UserRoles.Select(ur => ur.Role).FirstOrDefault();
            var roleTag = primaryRole is null ? null : new MemberRoleTagDto(primaryRole.Name, primaryRole.Color);
            var status = u.PersonStatus is null ? null : new PersonStatusDto(u.PersonStatus.Id, u.PersonStatus.Name, u.PersonStatus.Color);
            adminJoinDates.TryGetValue(u.Id, out var joinedAt);
            return new GroupMemberResponse(
                u.Id, u.Name, u.LastName, u.Phone, u.Email, u.Notes,
                status, u.PrimaryGroupId, u.PrimaryGroup?.Name, u.PrimaryGroup?.Color,
                u.CreatedAt, true, u.Id, roleTag, null, joinedAt);
        }).ToList();

        // Past members from history
        var activePersonIds = personMembers.Select(m => (long?)m.Id).ToHashSet();
        var activeUserIds = adminResponses.Select(m => m.UserId).ToHashSet();

        var pastHistories = await db.GroupMemberHistories
            .Where(h => h.HomeGroupId == id && h.LeftAt != null)
            .OrderByDescending(h => h.LeftAt)
            .ToListAsync();

        // Deduplicate: latest LeftAt per person/user, skip if currently active
        var seenPersonIds = new HashSet<long>();
        var seenUserIds = new HashSet<long>();
        var formerPersonIds = new List<(long PersonId, DateTime JoinedAt, DateTime LeftAt)>();
        var formerUserIds = new List<(long UserId, DateTime JoinedAt, DateTime LeftAt)>();

        foreach (var h in pastHistories)
        {
            if (h.PersonId.HasValue && !activePersonIds.Contains(h.PersonId) && seenPersonIds.Add(h.PersonId.Value))
                formerPersonIds.Add((h.PersonId.Value, h.JoinedAt, h.LeftAt!.Value));
            if (h.UserId.HasValue && !activeUserIds.Contains(h.UserId) && seenUserIds.Add(h.UserId.Value))
                formerUserIds.Add((h.UserId.Value, h.JoinedAt, h.LeftAt!.Value));
        }

        var formerPersonMembers = new List<GroupMemberResponse>();
        if (formerPersonIds.Count > 0)
        {
            var pids = formerPersonIds.Select(x => x.PersonId).ToList();
            var persons = await db.People.Where(p => pids.Contains(p.Id))
                .Include(p => p.PersonStatus).Include(p => p.PrimaryGroup).Include(p => p.OversightUser)
                .ToListAsync();
            foreach (var (pid, joinedAt, leftAt) in formerPersonIds)
            {
                var p = persons.FirstOrDefault(x => x.Id == pid);
                if (p is null) continue;
                var status = p.PersonStatus is null ? null : new PersonStatusDto(p.PersonStatus.Id, p.PersonStatus.Name, p.PersonStatus.Color);
                formerPersonMembers.Add(new GroupMemberResponse(
                    p.Id, p.Name, p.LastName, p.Phone, p.Email, p.Notes,
                    status, p.PrimaryGroupId, p.PrimaryGroup?.Name, p.PrimaryGroup?.Color,
                    p.CreatedAt, false, null, null,
                    p.OversightUser != null ? p.OversightUser.Name + (p.OversightUser.LastName != null ? " " + p.OversightUser.LastName : "") : null,
                    joinedAt, true, leftAt));
            }
        }

        var formerAdminMembers = new List<GroupMemberResponse>();
        if (formerUserIds.Count > 0)
        {
            var uids = formerUserIds.Select(x => x.UserId).ToList();
            var users = await db.Users.Where(u => uids.Contains(u.Id))
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.PersonStatus).Include(u => u.PrimaryGroup)
                .ToListAsync();
            foreach (var (uid, joinedAt, leftAt) in formerUserIds)
            {
                var u = users.FirstOrDefault(x => x.Id == uid);
                if (u is null) continue;
                var primaryRole = u.UserRoles.Select(ur => ur.Role).FirstOrDefault();
                var roleTag = primaryRole is null ? null : new MemberRoleTagDto(primaryRole.Name, primaryRole.Color);
                var status = u.PersonStatus is null ? null : new PersonStatusDto(u.PersonStatus.Id, u.PersonStatus.Name, u.PersonStatus.Color);
                formerAdminMembers.Add(new GroupMemberResponse(
                    u.Id, u.Name, u.LastName, u.Phone, u.Email, u.Notes,
                    status, u.PrimaryGroupId, u.PrimaryGroup?.Name, u.PrimaryGroup?.Color,
                    u.CreatedAt, true, u.Id, roleTag, null, joinedAt, true, leftAt));
            }
        }

        var all = personMembers.Concat(adminResponses).OrderBy(m => m.Name)
            .Concat(formerPersonMembers.Concat(formerAdminMembers).OrderBy(m => m.Name))
            .ToList();
        return Ok(all);
    }

    [HttpPost]
    [RequirePermission("groups.create")]
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
    [RequirePermission("groups.edit")]
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
    [RequirePermission("groups.delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        db.HomeGroups.Remove(group);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/members")]
    [RequirePermission("groups.members.manage")]
    public async Task<IActionResult> AddMember(long id, AddMemberRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();
        if (!await db.People.AnyAsync(p => p.Id == request.PersonId)) return NotFound();

        if (await db.HomeGroupMembers.AnyAsync(m => m.HomeGroupId == id && m.PersonId == request.PersonId))
            return Conflict(new { message = "Людина вже є учасником цієї групи" });

        var member = new HomeGroupMember { HomeGroupId = id, PersonId = request.PersonId, Role = request.Role };
        db.HomeGroupMembers.Add(member);

        // Close any stale open history record first, then create fresh entry
        var stale = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == request.PersonId && h.LeftAt == null);
        if (stale is not null) stale.LeftAt = DateTime.UtcNow;
        db.GroupMemberHistories.Add(new GroupMemberHistory { HomeGroupId = id, PersonId = request.PersonId, JoinedAt = member.JoinedAt });

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id}/members/sync")]
    [RequirePermission("groups.members.manage")]
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
            {
                var m = new HomeGroupMember { HomeGroupId = id, PersonId = personId };
                db.HomeGroupMembers.Add(m);
                var stale = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == personId && h.LeftAt == null);
                if (stale is not null) stale.LeftAt = DateTime.UtcNow;
                db.GroupMemberHistories.Add(new GroupMemberHistory { HomeGroupId = id, PersonId = personId, JoinedAt = m.JoinedAt });
            }
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

            foreach (var personId in removedIds)
            {
                var history = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == personId && h.LeftAt == null);
                if (history is not null)
                    history.LeftAt = DateTime.UtcNow;
                else
                {
                    var removed = current.FirstOrDefault(m => m.PersonId == personId);
                    db.GroupMemberHistories.Add(new GroupMemberHistory
                    {
                        HomeGroupId = id, PersonId = personId,
                        JoinedAt = removed?.JoinedAt ?? DateTime.UtcNow,
                        LeftAt = DateTime.UtcNow,
                    });
                }
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}/members/{personId}")]
    [RequirePermission("groups.members.manage")]
    public async Task<IActionResult> RemoveMember(long id, long personId)
    {
        var member = await db.HomeGroupMembers.FirstOrDefaultAsync(m => m.HomeGroupId == id && m.PersonId == personId);
        if (member is null) return NotFound();

        db.HomeGroupMembers.Remove(member);

        var person = await db.People.FirstOrDefaultAsync(p => p.Id == personId);
        if (person is not null && person.PrimaryGroupId == id) person.PrimaryGroupId = null;

        var history = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == personId && h.LeftAt == null);
        if (history is not null)
            history.LeftAt = DateTime.UtcNow;
        else
            db.GroupMemberHistories.Add(new GroupMemberHistory
            {
                HomeGroupId = id, PersonId = personId,
                JoinedAt = member.JoinedAt,
                LeftAt = DateTime.UtcNow,
            });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/members/joined-at")]
    [RequirePermission("groups.members.manage")]
    public async Task<IActionResult> SetMemberJoinedAt(long id, SetMemberJoinedAtRequest request)
    {
        var joinedAt = DateTime.SpecifyKind(request.JoinedAt, DateTimeKind.Utc);
        if (request.PersonId.HasValue)
        {
            var member = await db.HomeGroupMembers.FirstOrDefaultAsync(m => m.HomeGroupId == id && m.PersonId == request.PersonId);
            if (member is not null)
            {
                member.JoinedAt = joinedAt;
                var openHistory = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == request.PersonId && h.LeftAt == null);
                if (openHistory is not null) openHistory.JoinedAt = joinedAt;
            }
            else
            {
                // Former member — update most recent closed history entry
                var history = await db.GroupMemberHistories
                    .Where(h => h.HomeGroupId == id && h.PersonId == request.PersonId && h.LeftAt != null)
                    .OrderByDescending(h => h.LeftAt)
                    .FirstOrDefaultAsync();
                if (history is null) return NotFound();
                history.JoinedAt = joinedAt;
            }
        }
        else if (request.UserId.HasValue)
        {
            var uhg = await db.UserHomeGroups.FirstOrDefaultAsync(u => u.HomeGroupId == id && u.UserId == request.UserId);
            if (uhg is not null)
            {
                uhg.AssignedAt = joinedAt;
                var openHistory = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.UserId == request.UserId && h.LeftAt == null);
                if (openHistory is not null) openHistory.JoinedAt = joinedAt;
            }
            else
            {
                // Former admin — update most recent closed history entry
                var history = await db.GroupMemberHistories
                    .Where(h => h.HomeGroupId == id && h.UserId == request.UserId && h.LeftAt != null)
                    .OrderByDescending(h => h.LeftAt)
                    .FirstOrDefaultAsync();
                if (history is null) return NotFound();
                history.JoinedAt = joinedAt;
            }
        }
        else
        {
            return BadRequest(new { message = "PersonId або UserId обов'язковий" });
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/members/left-at")]
    [RequirePermission("groups.members.manage")]
    public async Task<IActionResult> SetMemberLeftAt(long id, SetMemberLeftAtRequest request)
    {
        var leftAt = DateTime.SpecifyKind(request.LeftAt, DateTimeKind.Utc);
        if (request.PersonId.HasValue)
        {
            var history = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == request.PersonId && h.LeftAt != null);
            if (history is null) return NotFound();
            history.LeftAt = leftAt;
        }
        else if (request.UserId.HasValue)
        {
            var history = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.UserId == request.UserId && h.LeftAt != null);
            if (history is null) return NotFound();
            history.LeftAt = leftAt;
        }
        else return BadRequest();

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/members/transfer")]
    [RequirePermission("groups.members.manage")]
    public async Task<IActionResult> TransferMember(long id, TransferMemberRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == request.ToGroupId)) return NotFound();
        var now = DateTime.UtcNow;

        if (request.PersonId.HasValue)
        {
            var member = await db.HomeGroupMembers.FirstOrDefaultAsync(m => m.HomeGroupId == id && m.PersonId == request.PersonId);
            if (member is null) return NotFound();

            db.HomeGroupMembers.Remove(member);

            // Add to new group
            if (!await db.HomeGroupMembers.AnyAsync(m => m.HomeGroupId == request.ToGroupId && m.PersonId == request.PersonId))
                db.HomeGroupMembers.Add(new HomeGroupMember { HomeGroupId = request.ToGroupId, PersonId = request.PersonId.Value, JoinedAt = now });

            var person = await db.People.FirstOrDefaultAsync(p => p.Id == request.PersonId);
            if (person is not null) person.PrimaryGroupId = request.ToGroupId;

            var oldHistory = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.PersonId == request.PersonId && h.LeftAt == null);
            if (oldHistory is not null)
                oldHistory.LeftAt = now;
            else
                db.GroupMemberHistories.Add(new GroupMemberHistory
                {
                    HomeGroupId = id, PersonId = request.PersonId.Value,
                    JoinedAt = member.JoinedAt, LeftAt = now,
                });

            db.GroupMemberHistories.Add(new GroupMemberHistory { HomeGroupId = request.ToGroupId, PersonId = request.PersonId.Value, JoinedAt = now });
        }
        else if (request.UserId.HasValue)
        {
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.PrimaryGroupId == id);
            if (admin is null) return NotFound();

            var adminGroup = await db.UserHomeGroups.FirstOrDefaultAsync(uhg => uhg.HomeGroupId == id && uhg.UserId == request.UserId);
            admin.PrimaryGroupId = request.ToGroupId;

            // Ensure UserHomeGroup exists for new group
            if (!await db.UserHomeGroups.AnyAsync(uhg => uhg.HomeGroupId == request.ToGroupId && uhg.UserId == request.UserId))
                db.UserHomeGroups.Add(new UserHomeGroup { UserId = request.UserId.Value, HomeGroupId = request.ToGroupId, AssignedAt = now });

            var oldHistory = await db.GroupMemberHistories.FirstOrDefaultAsync(h => h.HomeGroupId == id && h.UserId == request.UserId && h.LeftAt == null);
            if (oldHistory is not null)
                oldHistory.LeftAt = now;
            else
                db.GroupMemberHistories.Add(new GroupMemberHistory
                {
                    HomeGroupId = id, UserId = request.UserId.Value,
                    JoinedAt = adminGroup?.AssignedAt ?? now, LeftAt = now,
                });

            db.GroupMemberHistories.Add(new GroupMemberHistory { HomeGroupId = request.ToGroupId, UserId = request.UserId.Value, JoinedAt = now });
        }
        else return BadRequest();

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/members/timeline/{personId}")]
    public async Task<ActionResult<List<TimelineEventDto>>> GetPersonTimeline(long id, long personId)
    {
        var history = await db.GroupMemberHistories
            .Where(h => h.PersonId == personId)
            .Include(h => h.HomeGroup)
            .OrderBy(h => h.JoinedAt)
            .ToListAsync();

        var activity = await db.PersonActivities
            .Where(a => a.PersonId == personId && (a.Type == "status_change" || a.Type == "oversight_change"))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var events = new List<TimelineEventDto>();

        foreach (var h in history)
        {
            events.Add(new TimelineEventDto("group_joined", h.JoinedAt, h.HomeGroupId, h.HomeGroup.Name, h.HomeGroup.Color));
            if (h.LeftAt.HasValue)
                events.Add(new TimelineEventDto("group_left", h.LeftAt.Value, h.HomeGroupId, h.HomeGroup.Name, h.HomeGroup.Color));
        }

        foreach (var a in activity)
        {
            if (a.Type == "status_change")
                events.Add(new TimelineEventDto("status_change", a.CreatedAt, StatusName: a.NewStatusName, StatusColor: a.NewStatusColor, OldStatusName: a.OldStatusName, OldStatusColor: a.OldStatusColor));
            else if (a.Type == "oversight_change")
                events.Add(new TimelineEventDto("oversight_change", a.CreatedAt, OversightName: a.NewValue, OldOversightName: a.OldValue));
        }

        return Ok(events.OrderBy(e => e.Date).ToList());
    }

    [HttpGet("{id}/members/admin-timeline/{userId}")]
    public async Task<ActionResult<List<TimelineEventDto>>> GetAdminTimeline(long id, long userId)
    {
        var history = await db.GroupMemberHistories
            .Where(h => h.UserId == userId)
            .Include(h => h.HomeGroup)
            .OrderBy(h => h.JoinedAt)
            .ToListAsync();

        var activity = await db.UserActivities
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var events = new List<TimelineEventDto>();

        foreach (var h in history)
        {
            events.Add(new TimelineEventDto("group_joined", h.JoinedAt, h.HomeGroupId, h.HomeGroup.Name, h.HomeGroup.Color));
            if (h.LeftAt.HasValue)
                events.Add(new TimelineEventDto("group_left", h.LeftAt.Value, h.HomeGroupId, h.HomeGroup.Name, h.HomeGroup.Color));
        }

        foreach (var a in activity)
            events.Add(new TimelineEventDto("status_change", a.CreatedAt, StatusName: a.NewStatusName, StatusColor: a.NewStatusColor, OldStatusName: a.OldStatusName, OldStatusColor: a.OldStatusColor));

        return Ok(events.OrderBy(e => e.Date).ToList());
    }

    [HttpGet("{id}/members/history")]
    [RequirePermission("groups.members.manage")]
    public async Task<ActionResult<List<GroupMemberHistoryDto>>> GetMemberHistory(long id)
    {
        var history = await db.GroupMemberHistories
            .Where(h => h.HomeGroupId == id)
            .Include(h => h.Person)
            .Include(h => h.User)
            .Include(h => h.HomeGroup)
            .OrderByDescending(h => h.JoinedAt)
            .Select(h => new GroupMemberHistoryDto(
                h.Id,
                h.PersonId,
                h.Person != null ? h.Person.Name + (h.Person.LastName != null ? " " + h.Person.LastName : "") : null,
                h.UserId,
                h.User != null ? h.User.Name + (h.User.LastName != null ? " " + h.User.LastName : "") : null,
                h.HomeGroupId,
                h.HomeGroup.Name,
                h.JoinedAt,
                h.LeftAt))
            .ToListAsync();

        return Ok(history);
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
    [RequirePermission("settings.groups")]
    public async Task<ActionResult<GroupCustomFieldDto>> AddCustomField(long id, CreateGroupCustomFieldRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var field = new HomeGroupCustomField { HomeGroupId = id, Name = request.Name.Trim() };
        db.HomeGroupCustomFields.Add(field);
        await db.SaveChangesAsync();

        return Ok(new GroupCustomFieldDto(field.Id, field.Name));
    }

    [HttpDelete("{id}/custom-fields/{fieldId}")]
    [RequirePermission("settings.groups")]
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
    [RequirePermission("planning.view")]
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
    [RequirePermission("planning.view")]
    public async Task<ActionResult<MeetingPlanDto>> GetPlanByDate(long id, string date)
    {
        var plan = await db.MeetingPlans
            .Include(p => p.Blocks.OrderBy(b => b.Order))
            .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == date);

        if (plan is null) return NotFound();
        return Ok(ToPlanDto(plan));
    }

    [HttpPost("{id}/plans")]
    [RequirePermission("planning.edit")]
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

    [HttpPost("{id}/plans/date/{date}/send-to-telegram")]
    [RequirePermission("planning.sendToTelegram")]
    public async Task<IActionResult> SendPlanToTelegram(
        long id, string date,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IConfiguration config)
    {
        var group = await db.HomeGroups.FindAsync(id);
        if (group is null) return NotFound();
        if (string.IsNullOrEmpty(group.TelegramGroupId))
            return BadRequest("Group has no Telegram group configured");

        var plan = await db.MeetingPlans
            .Include(p => p.Blocks.OrderBy(b => b.Order))
            .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == date);
        if (plan is null) return NotFound("No plan for this date");

        var botToken = config["BOT_TOKEN"];
        if (string.IsNullOrEmpty(botToken))
            return StatusCode(500, "BOT_TOKEN is not configured");

        var tgMap = await BuildTelegramMap();
        var text = FormatPlanMessage(plan, date, tgMap);
        var client = httpClientFactory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{botToken}/sendMessage",
            new { chat_id = group.TelegramGroupId, text }
        );

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            return StatusCode(502, $"Telegram API error: {err}");
        }

        return Ok();
    }

    private async Task<Dictionary<string, string>> BuildTelegramMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var people = await db.People
            .Where(p => p.Telegram != null)
            .Select(p => new { p.Name, p.LastName, p.Telegram })
            .ToListAsync();
        var admins = await db.Users
            .Where(u => u.Telegram != null)
            .Select(u => new { u.Name, u.LastName, u.Telegram })
            .ToListAsync();

        foreach (var p in people.Concat(admins.Select(a => new { a.Name, a.LastName, a.Telegram })))
        {
            var handle = p.Telegram!.TrimStart('@');
            map.TryAdd(p.Name.Trim(), handle);
            if (!string.IsNullOrEmpty(p.LastName))
                map.TryAdd($"{p.Name} {p.LastName}".Trim(), handle);
        }
        return map;
    }

    private static string ResolveResponsible(string? responsible, Dictionary<string, string> map)
    {
        if (string.IsNullOrWhiteSpace(responsible)) return "";
        responsible = responsible.Trim();
        if (responsible.StartsWith('@')) return responsible;
        return map.TryGetValue(responsible, out var handle) ? $"@{handle}" : responsible;
    }

    private static string FormatPlanMessage(HomeMeetingPlan plan, string date, Dictionary<string, string> tgMap)
    {
        var displayDate = DateOnly.TryParse(date, out var d) ? d.ToString("dd.MM.yyyy") : date;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"План на {displayDate}");
        sb.AppendLine("------------------");

        var timeline = plan.Blocks.Where(b => !string.IsNullOrWhiteSpace(b.Time)).OrderBy(b => b.Order);
        var footer = plan.Blocks.Where(b => string.IsNullOrWhiteSpace(b.Time)).OrderBy(b => b.Order).ToList();

        foreach (var block in timeline)
        {
            var responsible = ResolveResponsible(block.Responsible, tgMap);
            var line = $"{block.Time} - {block.Title}";
            if (!string.IsNullOrEmpty(responsible)) line += $": {responsible}";
            sb.AppendLine(line);
            if (!string.IsNullOrEmpty(block.Info))
                foreach (var infoLine in block.Info.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    if (!string.IsNullOrWhiteSpace(infoLine))
                        sb.AppendLine($"   • {infoLine.Trim()}");
        }

        if (footer.Count > 0)
        {
            sb.AppendLine("------------------");
            foreach (var block in footer)
            {
                var responsible = ResolveResponsible(block.Responsible, tgMap);
                sb.AppendLine(string.IsNullOrEmpty(responsible)
                    ? block.Title
                    : $"{responsible} - {block.Title}");
            }
        }

        return sb.ToString().Trim();
    }

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
            .Select(x => new GroupEventDto(x.e.Id, x.e.Name, x.e.Month, x.e.Day, x.e.Year, x.days))
            .ToList();

        return Ok(result);
    }

    [HttpPost("{id}/events")]
    [RequirePermission("groups.events.manage")]
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

    [HttpPut("{id}/events/{eventId}")]
    [RequirePermission("groups.events.manage")]
    public async Task<ActionResult<GroupEventDto>> UpdateEvent(long id, long eventId, UpdateGroupEventRequest request)
    {
        var evt = await db.GroupEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.HomeGroupId == id);
        if (evt is null) return NotFound();
        evt.Name = request.Name.Trim();
        evt.Month = request.Month;
        evt.Day = request.Day;
        evt.Year = request.Year;
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(new GroupEventDto(evt.Id, evt.Name, evt.Month, evt.Day, evt.Year, ComputeDaysUntil(evt.Month, evt.Day, evt.Year, today)));
    }

    [HttpDelete("{id}/events/{eventId}")]
    [RequirePermission("groups.events.manage")]
    public async Task<IActionResult> DeleteEvent(long id, long eventId)
    {
        var evt = await db.GroupEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.HomeGroupId == id);
        if (evt is null) return NotFound();
        db.GroupEvents.Remove(evt);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Needs ────────────────────────────────────────────────────────────────────

    [HttpGet("{id}/needs")]
    [RequirePermission("page.cabinet")]
    public async Task<ActionResult<List<GroupNeedDto>>> GetNeeds(long id)
    {
        var needs = await db.GroupNeeds
            .Where(n => n.HomeGroupId == id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new GroupNeedDto(n.Id, n.SubjectName, n.Description, n.Status, n.CreatedAt, n.PersonId, n.UserId))
            .ToListAsync();
        return Ok(needs);
    }

    [HttpPost("{id}/needs")]
    [RequirePermission("groups.events.manage")]
    public async Task<ActionResult<GroupNeedDto>> AddNeed(long id, CreateGroupNeedRequest request)
    {
        var need = new GroupNeed
        {
            HomeGroupId = id,
            SubjectName = request.SubjectName,
            Description = request.Description,
            Status = "active",
            PersonId = request.PersonId,
            UserId = request.UserId,
        };
        db.GroupNeeds.Add(need);
        await db.SaveChangesAsync();
        return Ok(new GroupNeedDto(need.Id, need.SubjectName, need.Description, need.Status, need.CreatedAt, need.PersonId, need.UserId));
    }

    [HttpPut("{id}/needs/{needId}")]
    [RequirePermission("groups.events.manage")]
    public async Task<ActionResult<GroupNeedDto>> UpdateNeed(long id, long needId, UpdateGroupNeedRequest request)
    {
        var need = await db.GroupNeeds.FirstOrDefaultAsync(n => n.Id == needId && n.HomeGroupId == id);
        if (need is null) return NotFound();
        need.SubjectName = request.SubjectName;
        need.Description = request.Description;
        need.Status = request.Status;
        need.PersonId = request.PersonId;
        need.UserId = request.UserId;
        await db.SaveChangesAsync();
        return Ok(new GroupNeedDto(need.Id, need.SubjectName, need.Description, need.Status, need.CreatedAt, need.PersonId, need.UserId));
    }

    [HttpDelete("{id}/needs/{needId}")]
    [RequirePermission("groups.events.manage")]
    public async Task<IActionResult> DeleteNeed(long id, long needId)
    {
        var need = await db.GroupNeeds.FirstOrDefaultAsync(n => n.Id == needId && n.HomeGroupId == id);
        if (need is null) return NotFound();
        db.GroupNeeds.Remove(need);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/cabinet")]
    [RequirePermission("page.cabinet")]
    public async Task<ActionResult<GroupCabinetResponse>> GetCabinet(long id)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nowTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        var nextMeeting = ComputeNextMeeting(group.MeetingDay, group.MeetingTime, today, nowTime);

        // Last meeting = most recent date with actual attendance records
        var lastMeeting = await db.Attendances
            .Where(a => a.HomeGroupId == id)
            .Select(a => (DateOnly?)a.MeetingDate)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync();

        // Last attendance summary
        CabinetAttendanceSummary? lastAttendance = null;
        if (lastMeeting.HasValue)
        {
            var records = await db.Attendances
                .Where(a => a.HomeGroupId == id && a.MeetingDate == lastMeeting.Value)
                .ToListAsync();
            if (records.Count > 0)
                lastAttendance = new CabinetAttendanceSummary(records.Count(r => r.WasPresent), records.Count);
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

        // Stats: fixed 3-month window (current) vs previous 3 months
        var currStart = today.AddMonths(-3);
        var prevStart = today.AddMonths(-6);
        var currStartDt = new DateTime(currStart.Year, currStart.Month, currStart.Day, 0, 0, 0, DateTimeKind.Utc);
        var prevStartDt = new DateTime(prevStart.Year, prevStart.Month, prevStart.Day, 0, 0, 0, DateTimeKind.Utc);

        var totalMembers = await db.HomeGroupMembers.CountAsync(m => m.HomeGroupId == id)
            + await db.Users.CountAsync(u => u.PrimaryGroupId == id && u.Id != 0);

        var joinedInCurr3m = await db.GroupMemberHistories
            .CountAsync(h => h.HomeGroupId == id && h.JoinedAt >= currStartDt);
        var leftInCurr3m = await db.GroupMemberHistories
            .CountAsync(h => h.HomeGroupId == id && h.LeftAt != null && h.LeftAt >= currStartDt);
        var prevTotalMembers = Math.Max(0, totalMembers - joinedInCurr3m + leftInCurr3m);

        var newMembers = joinedInCurr3m;
        var prevNewMembers = await db.GroupMemberHistories
            .CountAsync(h => h.HomeGroupId == id && h.JoinedAt >= prevStartDt && h.JoinedAt < currStartDt);

        var allAttendance6m = await db.Attendances
            .Where(a => a.HomeGroupId == id && a.MeetingDate >= prevStart && a.MeetingDate <= today)
            .ToListAsync();
        var avgRate = CalcAvgAttendanceRate(allAttendance6m.Where(a => a.MeetingDate >= currStart));
        var prevAvgRate = CalcAvgAttendanceRate(allAttendance6m.Where(a => a.MeetingDate < currStart));

        var stats = new CabinetStats(avgRate, prevAvgRate, newMembers, prevNewMembers, totalMembers, prevTotalMembers);

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
            // Cleanup stale past non-recurring booking events for this group.
            // KEEP schedule overrides (IsHomeGroupMeeting != null = real meeting or cancellation marker)
            // and any event linked via MovedFromDate/MovedToDate.
            var staleBookings = await db.CalendarEvents
                .Where(e => e.Type == CalendarEventType.HomeGroup && !e.IsRecurring
                            && e.HomeGroupId == id && e.Date < today
                            && e.IsHomeGroupMeeting == null
                            && e.MovedFromDate == null
                            && e.MovedToDate == null)
                .ToListAsync();
            if (staleBookings.Count > 0)
            {
                db.CalendarEvents.RemoveRange(staleBookings);
                await db.SaveChangesAsync();
            }

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
                        IsHomeGroupMeeting = true,
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
                .Where(e => !(e.Type == CalendarEventType.HomeGroup && e.HomeGroupId == id))
                .ToListAsync();

            // Determine suppressed recurring events (other groups where a marker exists for nextDate's week)
            var weekMonday = nextDate.AddDays(-(((int)nextDate.DayOfWeek - 1 + 7) % 7));
            var weekSunday = weekMonday.AddDays(6);
            var suppressedGroupIds = (await db.CalendarEvents
                .Where(e => !e.IsRecurring && e.Type == CalendarEventType.HomeGroup
                            && e.HomeGroupId.HasValue && e.IsHomeGroupMeeting.HasValue
                            && e.Date >= weekMonday && e.Date <= weekSunday)
                .Select(e => e.HomeGroupId!.Value)
                .ToListAsync()).ToHashSet();
            eventsOnDate = eventsOnDate
                .Where(e => !e.IsRecurring || !suppressedGroupIds.Contains(e.HomeGroupId ?? -1))
                .ToList();

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
                        // Only warn for non-HomeGroup events (Recurring, Global, Google)
                        if (e.Type == "HomeGroup") return false;
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

        var schedPrev = ComputeLastMeeting(group.MeetingDay, group.MeetingTime, today, nowTime);
        DateOnly? prevScheduled = schedPrev;
        if (group.NextMeetingOverrideDate is not null
            && DateOnly.TryParse(group.NextMeetingOverrideDate, out var overridePrev))
        {
            var overridePassed = overridePrev < today
                || (overridePrev == today && (!TimeOnly.TryParse(group.MeetingTime, out var mt) || nowTime >= mt));
            if (overridePassed && (schedPrev is null || overridePrev >= schedPrev))
                prevScheduled = overridePrev;
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
            group.AutoBookRoomId.HasValue,
            prevScheduled?.ToString("yyyy-MM-dd")));
    }

    [HttpGet("stats/all")]
    [RequirePermission("attendance.stats")]
    public async Task<ActionResult<GroupStatsResponse>> GetAllStats([FromQuery] string period = "3m")
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodMonthsAll = period switch { "1m" => 1, "6m" => 6, _ => 3 };
        var periodStart = period switch
        {
            "1m" => today.AddMonths(-1),
            "6m" => today.AddMonths(-6),
            _ => today.AddMonths(-3),
        };
        var prevPeriodStartAll = periodStart.AddMonths(-periodMonthsAll);
        var periodStartDt = new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 0, 0, 0, DateTimeKind.Utc);
        var prevPeriodStartDtAll = new DateTime(prevPeriodStartAll.Year, prevPeriodStartAll.Month, prevPeriodStartAll.Day, 0, 0, 0, DateTimeKind.Utc);

        var attendance = await db.Attendances
            .Include(a => a.Person)
            .Include(a => a.User)
            .Where(a => a.MeetingDate >= prevPeriodStartAll && a.MeetingDate <= today)
            .OrderBy(a => a.MeetingDate)
            .ToListAsync();

        var metas = await db.AttendanceMetas
            .Where(m => m.MeetingDate >= periodStart)
            .ToListAsync();

        var currAttendanceAll = attendance.Where(a => a.MeetingDate >= periodStart).ToList();
        var prevAttendanceAll = attendance.Where(a => a.MeetingDate >= prevPeriodStartAll && a.MeetingDate < periodStart).ToList();

        var byDate = currAttendanceAll.GroupBy(a => a.MeetingDate).OrderBy(g => g.Key).ToList();

        var meetings = byDate.Select(g =>
        {
            var presentCount = g.Count(a => a.WasPresent);
            var totalMembers = g.Count();
            var guestCount = metas.Where(m => m.MeetingDate == g.Key).Sum(m => m.GuestCount);
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

        var personStats = currAttendanceAll
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
                return new PersonAttendanceStat(g.Key.PersonId, g.Key.UserId, g.Key.Name, present, total,
                    total == 0 ? 0 : Math.Round(present * 100.0 / total, 1));
            })
            .OrderByDescending(p => p.AttendanceRate)
            .ThenByDescending(p => p.PresentCount)
            .ToList();

        var avgRateAll = CalcAvgAttendanceRate(currAttendanceAll);
        var prevAvgRateAll = CalcAvgAttendanceRate(prevAttendanceAll);
        var totalGuests = metas.Sum(m => m.GuestCount);
        var newMembersAll = await db.GroupMemberHistories.CountAsync(h => h.JoinedAt >= periodStartDt);
        var prevNewMembersAll = await db.GroupMemberHistories.CountAsync(h => h.JoinedAt >= prevPeriodStartDtAll && h.JoinedAt < periodStartDt);
        var totalMembersAll = await db.HomeGroupMembers.CountAsync()
            + await db.Users.CountAsync(u => u.Id != 0 && u.PrimaryGroupId != null);
        var leftInPeriodAll = await db.GroupMemberHistories.CountAsync(h => h.LeftAt != null && h.LeftAt >= periodStartDt);
        var prevTotalMembersAll = Math.Max(0, totalMembersAll - newMembersAll + leftInPeriodAll);

        var summary = new StatsSummary(avgRateAll, prevAvgRateAll, meetings.Count, totalGuests, newMembersAll, prevNewMembersAll, totalMembersAll, prevTotalMembersAll);
        return Ok(new GroupStatsResponse(summary, meetings, personStats));
    }

    [HttpGet("{id}/stats")]
    [RequirePermission("attendance.stats")]
    public async Task<ActionResult<GroupStatsResponse>> GetStats(long id, [FromQuery] string period = "3m")
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == id)) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodMonths = period switch { "1m" => 1, "6m" => 6, _ => 3 };
        var periodStart = period switch
        {
            "1m" => today.AddMonths(-1),
            "6m" => today.AddMonths(-6),
            _ => today.AddMonths(-3),
        };
        var prevPeriodStart = periodStart.AddMonths(-periodMonths);
        var periodStartDt = new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 0, 0, 0, DateTimeKind.Utc);
        var prevPeriodStartDt = new DateTime(prevPeriodStart.Year, prevPeriodStart.Month, prevPeriodStart.Day, 0, 0, 0, DateTimeKind.Utc);

        var attendance = await db.Attendances
            .Include(a => a.Person)
            .Include(a => a.User)
            .Where(a => a.HomeGroupId == id && a.MeetingDate >= prevPeriodStart && a.MeetingDate <= today)
            .OrderBy(a => a.MeetingDate)
            .ToListAsync();

        var metaLookup = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == id && m.MeetingDate >= periodStart)
            .ToDictionaryAsync(m => m.MeetingDate, m => m.GuestCount);

        var currAttendance = attendance.Where(a => a.MeetingDate >= periodStart).ToList();
        var prevAttendanceRecs = attendance.Where(a => a.MeetingDate >= prevPeriodStart && a.MeetingDate < periodStart).ToList();

        // Per-meeting stats (current period only)
        var byDate = currAttendance.GroupBy(a => a.MeetingDate).OrderBy(g => g.Key).ToList();

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

        // Per-person stats (current period only)
        var personStats = currAttendance
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
        var avgRate = CalcAvgAttendanceRate(currAttendance);
        var prevAvgRate = CalcAvgAttendanceRate(prevAttendanceRecs);
        var totalGuests = metaLookup.Values.Sum();
        var newMembers = await db.GroupMemberHistories.CountAsync(h => h.HomeGroupId == id && h.JoinedAt >= periodStartDt);
        var prevNewMembers = await db.GroupMemberHistories.CountAsync(h => h.HomeGroupId == id && h.JoinedAt >= prevPeriodStartDt && h.JoinedAt < periodStartDt);
        var totalMembers = await db.HomeGroupMembers.CountAsync(m => m.HomeGroupId == id)
            + await db.Users.CountAsync(u => u.PrimaryGroupId == id && u.Id != 0);
        var leftInPeriod = await db.GroupMemberHistories.CountAsync(h => h.HomeGroupId == id && h.LeftAt != null && h.LeftAt >= periodStartDt);
        var prevTotalMembers = Math.Max(0, totalMembers - newMembers + leftInPeriod);

        var summary = new StatsSummary(avgRate, prevAvgRate, meetings.Count, totalGuests, newMembers, prevNewMembers, totalMembers, prevTotalMembers);

        return Ok(new GroupStatsResponse(summary, meetings, personStats));
    }

    [HttpPut("{id}/next-meeting")]
    [RequirePermission("groups.nextMeeting.manage")]
    public async Task<IActionResult> SetNextMeeting(long id, SetNextMeetingRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();
        group.NextMeetingOverrideDate = request.Date;

        // Move plan and calendar booking from old date to new date if both are provided
        if (!string.IsNullOrEmpty(request.OldDate) && !string.IsNullOrEmpty(request.Date)
            && DateOnly.TryParse(request.OldDate, out var oldDateOnly)
            && DateOnly.TryParse(request.Date, out var newDateOnly))
        {
            // Move plan
            var plan = await db.MeetingPlans
                .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == request.OldDate);
            if (plan is not null)
            {
                var existingPlan = await db.MeetingPlans
                    .Include(p => p.Blocks)
                    .FirstOrDefaultAsync(p => p.HomeGroupId == id && p.MeetingDate == request.Date);
                if (existingPlan is not null)
                {
                    db.MeetingPlanBlocks.RemoveRange(existingPlan.Blocks);
                    db.MeetingPlans.Remove(existingPlan);
                }
                plan.MeetingDate = request.Date;
            }

            // Compute start/end time for the rescheduled meeting
            TimeOnly? newStartTime = null;
            TimeOnly? newEndTime = null;

            if (!string.IsNullOrEmpty(request.Time) && TimeOnly.TryParse(request.Time, out var reqTime))
                newStartTime = reqTime;
            else if (!string.IsNullOrEmpty(group.MeetingTime) && TimeOnly.TryParse(group.MeetingTime, out var grpTime))
                newStartTime = grpTime;

            if (newStartTime.HasValue
                && !string.IsNullOrEmpty(group.MeetingTime) && TimeOnly.TryParse(group.MeetingTime, out var origStart)
                && !string.IsNullOrEmpty(group.MeetingEndTime) && TimeOnly.TryParse(group.MeetingEndTime, out var origEnd))
            {
                newEndTime = newStartTime.Value.Add(origEnd - origStart);
            }

            var oldDateEvent = await db.CalendarEvents
                .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup
                    && !e.IsRecurring && e.HomeGroupId == id && e.Date == oldDateOnly);

            var newDateEvent = await db.CalendarEvents
                .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup
                    && !e.IsRecurring && e.HomeGroupId == id && e.Date == newDateOnly);

            // Transfer room and remove old event (unless it's already a suppression marker)
            long? transferredRoomId = null;
            if (oldDateEvent is not null && oldDateEvent.IsHomeGroupMeeting != false)
            {
                transferredRoomId = oldDateEvent.RoomId;
                db.CalendarEvents.Remove(oldDateEvent);
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Type = CalendarEventType.HomeGroup,
                    HomeGroupId = id,
                    IsRecurring = false,
                    Date = oldDateOnly,
                    IsHomeGroupMeeting = false,
                    Title = group.Name,
                });
            }
            else if (oldDateEvent is null)
            {
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Type = CalendarEventType.HomeGroup,
                    HomeGroupId = id,
                    IsRecurring = false,
                    Date = oldDateOnly,
                    IsHomeGroupMeeting = false,
                    Title = group.Name,
                });
            }

            // Upsert real meeting event on new date
            if (newDateEvent is not null)
            {
                newDateEvent.IsHomeGroupMeeting = true;
                if (newStartTime.HasValue) newDateEvent.StartTime = newStartTime;
                if (newEndTime.HasValue) newDateEvent.EndTime = newEndTime;
                if (transferredRoomId.HasValue && !newDateEvent.RoomId.HasValue)
                    newDateEvent.RoomId = transferredRoomId;
            }
            else
            {
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Type = CalendarEventType.HomeGroup,
                    HomeGroupId = id,
                    IsRecurring = false,
                    Date = newDateOnly,
                    IsHomeGroupMeeting = true,
                    Title = group.Name,
                    StartTime = newStartTime,
                    EndTime = newEndTime,
                    RoomId = transferredRoomId,
                });
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id}/skip-meeting")]
    [RequirePermission("groups.nextMeeting.manage")]
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

        // Create/update cancellation marker to suppress ghost for the cancelled week
        var cancelDate = currentNext;
        var existingMarker = await db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup && !e.IsRecurring
                                       && e.HomeGroupId == id && e.Date == cancelDate);
        if (existingMarker is null)
        {
            db.CalendarEvents.Add(new CalendarEvent
            {
                Title = group.Name,
                Type = CalendarEventType.HomeGroup,
                HomeGroupId = id,
                IsRecurring = false,
                Date = cancelDate,
                IsHomeGroupMeeting = false, // cancellation marker — suppresses ghost, not shown in calendar
            });
        }
        else
        {
            existingMarker.IsHomeGroupMeeting = false;
        }

        group.NextMeetingOverrideDate = nextAfter.Value.ToString("yyyy-MM-dd");
        await db.SaveChangesAsync();
        return Ok(new { date = group.NextMeetingOverrideDate });
    }

    [HttpDelete("{id}/plans/date/{date}")]
    [RequirePermission("planning.edit")]
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

    private static double CalcAvgAttendanceRate(IEnumerable<Attendance> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return 0;
        var rates = list
            .GroupBy(a => a.MeetingDate)
            .Select(g => { var t = g.Count(); var p = g.Count(r => r.WasPresent); return t == 0 ? 0.0 : p * 100.0 / t; })
            .ToList();
        return rates.Count == 0 ? 0 : Math.Round(rates.Average(), 1);
    }

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
            // Today is the meeting day — if the meeting time has already passed, jump to next week.
            // Otherwise, "next meeting" IS today.
            if (TimeOnly.TryParse(meetingTime, out var mt) && nowTime >= mt)
                daysUntil = 7;
        }

        return today.AddDays(daysUntil);
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

        return today.AddDays(-daysAgo);
    }

    [HttpPut("{id}/book-room")]
    public async Task<IActionResult> BookRoom(long id, BookRoomRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();

        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest("Invalid date");

        group.AutoBookRoomId = request.AutoBook ? request.RoomId : null;

        // Sync recurring event's RoomId so other groups can detect conflicts
        var recurringEvent = await db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Type == CalendarEventType.HomeGroup && e.IsRecurring && e.HomeGroupId == id);
        if (recurringEvent != null)
        {
            recurringEvent.RoomId = request.AutoBook ? request.RoomId : null;
        }
        else if (request.AutoBook && !string.IsNullOrEmpty(group.MeetingDay)
                 && UkrDays.TryGetValue(group.MeetingDay, out var recurDow))
        {
            // Recurring event missing (group predates calendar feature) — create it now
            TimeOnly.TryParse(group.MeetingTime, out var rStart);
            TimeOnly? rEnd = TimeOnly.TryParse(group.MeetingEndTime, out var rEt) ? rEt : null;
            db.CalendarEvents.Add(new CalendarEvent
            {
                Title = group.Name,
                Location = group.Location,
                Type = CalendarEventType.HomeGroup,
                HomeGroupId = id,
                IsRecurring = true,
                RecurringDayOfWeek = (int)recurDow,
                StartTime = rStart == default ? null : rStart,
                EndTime = rEnd,
                RoomId = request.RoomId,
            });
        }

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
                    IsHomeGroupMeeting = true,
                });
            }
            else
            {
                booking.RoomId = request.RoomId;
                booking.IsHomeGroupMeeting = true;
            }
        }
        else if (booking is not null)
        {
            db.CalendarEvents.Remove(booking);
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    private static readonly string[] NotifKeys = ["event_7days", "event_day", "conflict", "conflict_resolved", "attendance_ask"];

    private static Dictionary<string, bool> ParseNotifSettings(string? json)
    {
        var defaults = NotifKeys.ToDictionary(k => k, _ => true);
        if (string.IsNullOrEmpty(json)) return defaults;
        try
        {
            var stored = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? [];
            foreach (var k in NotifKeys.Where(k => !stored.ContainsKey(k)))
                stored[k] = true;
            return stored;
        }
        catch { return defaults; }
    }

    [HttpGet("{id}/notif-settings")]
    [RequirePermission("page.cabinet")]
    public async Task<IActionResult> GetNotifSettings(long id)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();
        var s = ParseNotifSettings(group.NotifSettingsJson);
        return Ok(new NotifSettingsDto(s["event_7days"], s["event_day"], s["conflict"], s["conflict_resolved"], s["attendance_ask"]));
    }

    [HttpPut("{id}/notif-settings")]
    [RequirePermission("page.cabinet")]
    public async Task<IActionResult> UpdateNotifSettings(long id, UpdateNotifSettingsRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();
        var s = new Dictionary<string, bool>
        {
            ["event_7days"] = request.EventSevenDays,
            ["event_day"] = request.EventDay,
            ["conflict"] = request.Conflict,
            ["conflict_resolved"] = request.ConflictResolved,
            ["attendance_ask"] = request.AttendanceAsk,
        };
        group.NotifSettingsJson = System.Text.Json.JsonSerializer.Serialize(s);
        await db.SaveChangesAsync();
        return Ok(new NotifSettingsDto(s["event_7days"], s["event_day"], s["conflict"], s["conflict_resolved"], s["attendance_ask"]));
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
