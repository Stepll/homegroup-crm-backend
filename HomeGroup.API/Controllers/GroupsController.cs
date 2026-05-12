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
            .Select(m => new PersonResponse(m.Person.Id, m.Person.Name, m.Person.Phone, m.Person.Email, m.Person.Notes, m.Person.Status, m.Person.CreatedAt))
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

        db.HomeGroupMembers.RemoveRange(current.Where(m => !newIds.Contains(m.PersonId)));

        foreach (var personId in newIds.Except(currentIds))
        {
            if (await db.People.AnyAsync(p => p.Id == personId))
                db.HomeGroupMembers.Add(new HomeGroupMember { HomeGroupId = id, PersonId = personId });
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
}
