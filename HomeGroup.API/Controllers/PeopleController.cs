using System.Security.Claims;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Groups;
using HomeGroup.API.Models.DTOs.People;
using HomeGroup.API.Models.DTOs.PersonStatuses;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonActivityEntity = HomeGroup.API.Models.Entities.PersonActivity;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/people")]
[Authorize]
public class PeopleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("people.view")]
    public async Task<ActionResult<List<GroupMemberResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool noGroup = false,
        [FromQuery] bool includeAdmins = false,
        [FromQuery] bool myOversight = false)
    {
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var currentUserId);
        bool isSuperAdmin = currentUserId == 0;

        List<long>? visibleGroupIds = null;
        if (!isSuperAdmin)
        {
            visibleGroupIds = await db.UserHomeGroups
                .Where(ug => ug.UserId == currentUserId)
                .Select(ug => ug.HomeGroupId)
                .ToListAsync();
        }

        // ── Persons ───────────────────────────────────────────────────────────
        var query = db.People.Include(p => p.PrimaryGroup).Include(p => p.PersonStatus).Include(p => p.OversightUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                (p.LastName != null && p.LastName.Contains(search)) ||
                (p.Phone != null && p.Phone.Contains(search)));

        if (noGroup)
            query = query.Where(p => p.PrimaryGroupId == null);

        if (myOversight)
            query = query.Where(p => p.OversightUserId == currentUserId);

        if (!isSuperAdmin && visibleGroupIds is { Count: > 0 })
            query = query.Where(p => p.PrimaryGroupId != null && visibleGroupIds.Contains(p.PrimaryGroupId.Value));

        var persons = await query.OrderBy(p => p.Name).ToListAsync();

        var result = persons.Select(p => new GroupMemberResponse(
            p.Id, p.Name, p.LastName, p.Phone, p.Email, p.Notes,
            p.PersonStatus != null ? new PersonStatusDto(p.PersonStatus.Id, p.PersonStatus.Name, p.PersonStatus.Color) : null,
            p.PrimaryGroupId, p.PrimaryGroup?.Name, p.PrimaryGroup?.Color,
            p.CreatedAt, false, null, null,
            p.OversightUser is null ? null : $"{p.OversightUser.Name}{(p.OversightUser.LastName is null ? "" : " " + p.OversightUser.LastName)}")).ToList();

        // ── Admins ────────────────────────────────────────────────────────────
        if (includeAdmins && !myOversight && !noGroup)
        {
            var adminQuery = db.Users
                .Where(u => u.Id != 0 && u.PrimaryGroupId != null)
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.PersonStatus)
                .Include(u => u.PrimaryGroup)
                .AsQueryable();

            if (!isSuperAdmin && visibleGroupIds is not null)
                adminQuery = adminQuery.Where(u => visibleGroupIds.Contains(u.PrimaryGroupId!.Value));

            if (!string.IsNullOrWhiteSpace(search))
                adminQuery = adminQuery.Where(u =>
                    u.Name.Contains(search) ||
                    (u.LastName != null && u.LastName.Contains(search)) ||
                    (u.Phone != null && u.Phone.Contains(search)));

            var admins = await adminQuery.OrderBy(u => u.Name).ToListAsync();

            foreach (var a in admins)
            {
                var primaryRole = a.UserRoles.Select(ur => ur.Role).FirstOrDefault();
                var roleTag = primaryRole is null ? null : new MemberRoleTagDto(primaryRole.Name, primaryRole.Color);
                var status = a.PersonStatus is null ? null : new PersonStatusDto(a.PersonStatus.Id, a.PersonStatus.Name, a.PersonStatus.Color);
                result.Add(new GroupMemberResponse(
                    a.Id, a.Name, a.LastName, a.Phone, a.Email, a.Notes,
                    status, a.PrimaryGroupId, a.PrimaryGroup?.Name, a.PrimaryGroup?.Color,
                    a.CreatedAt, true, a.Id, roleTag));
            }

            result = result.OrderBy(r => r.Name).ToList();
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequirePermission("people.view")]
    public async Task<ActionResult<PersonDetailResponse>> GetById(long id)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .Include(p => p.PersonStatus)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        var statusDto = person.PersonStatus is null ? null : new PersonStatusDto(person.PersonStatus.Id, person.PersonStatus.Name, person.PersonStatus.Color);

        return Ok(new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Telegram, person.Notes,
            person.Gender, person.MaritalStatus, person.Address, person.DateOfBirth,
            person.IsBaptized, person.Church, person.Ministry, person.IsBaptizedWithSpirit,
            statusDto, person.OversightInfo, person.OversightUserId,
            person.OversightUser is null ? null : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}",
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt,
            await GetCustomFields(id, person.PrimaryGroupId)));
    }

    [HttpPost]
    [RequirePermission("people.create")]
    public async Task<ActionResult<PersonDetailResponse>> Create(CreatePersonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Ім'я обов'язкове" });

        var person = new Person
        {
            Name = request.Name.Trim(),
            LastName = request.LastName?.Trim(),
            PrimaryGroupId = request.PrimaryGroupId,
        };

        db.People.Add(person);
        await db.SaveChangesAsync();

        if (person.PrimaryGroupId.HasValue)
            db.HomeGroupMembers.Add(new HomeGroupMember { HomeGroupId = person.PrimaryGroupId.Value, PersonId = person.Id });
        await db.SaveChangesAsync();

        await db.Entry(person).Reference(p => p.PrimaryGroup).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = person.Id }, new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Telegram, person.Notes,
            person.Gender, person.MaritalStatus, person.Address, person.DateOfBirth,
            person.IsBaptized, person.Church, person.Ministry, person.IsBaptizedWithSpirit,
            null, person.OversightInfo, null, null,
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt, []));
    }

    [HttpPut("{id}")]
    [RequirePermission("people.edit")]
    public async Task<ActionResult<PersonDetailResponse>> Update(long id, UpdatePersonRequest request)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .Include(p => p.PersonStatus)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        var oldGroupId = person.PrimaryGroupId;
        var oldStatusId = person.PersonStatusId;
        var oldStatusName = person.PersonStatus?.Name;
        var oldStatusColor = person.PersonStatus?.Color;

        person.Name = request.Name.Trim();
        person.LastName = request.LastName?.Trim();
        person.Phone = request.Phone?.Trim();
        person.Email = request.Email?.Trim();
        person.Telegram = request.Telegram?.Trim();
        person.Notes = request.Notes?.Trim();
        person.Gender = request.Gender;
        person.MaritalStatus = request.MaritalStatus;
        person.Address = request.Address?.Trim();
        person.DateOfBirth = request.DateOfBirth;
        person.IsBaptized = request.IsBaptized;
        person.Church = request.Church?.Trim();
        person.Ministry = request.Ministry?.Trim();
        person.IsBaptizedWithSpirit = request.IsBaptizedWithSpirit;
        person.PersonStatusId = request.PersonStatusId;
        person.OversightInfo = request.OversightInfo?.Trim();
        person.OversightUserId = request.OversightUserId;
        person.PrimaryGroupId = request.PrimaryGroupId;

        // Sync HomeGroupMembers when primary group changes
        if (oldGroupId != request.PrimaryGroupId)
        {
            if (oldGroupId.HasValue)
            {
                var oldMembership = await db.HomeGroupMembers
                    .FirstOrDefaultAsync(m => m.PersonId == id && m.HomeGroupId == oldGroupId.Value);
                if (oldMembership != null) db.HomeGroupMembers.Remove(oldMembership);
            }
            if (request.PrimaryGroupId.HasValue &&
                !await db.HomeGroupMembers.AnyAsync(m => m.PersonId == id && m.HomeGroupId == request.PrimaryGroupId.Value))
            {
                db.HomeGroupMembers.Add(new HomeGroupMember { PersonId = id, HomeGroupId = request.PrimaryGroupId.Value });
            }
        }

        await db.SaveChangesAsync();
        await db.Entry(person).Reference(p => p.PrimaryGroup).LoadAsync();
        await db.Entry(person).Reference(p => p.OversightUser).LoadAsync();
        await db.Entry(person).Reference(p => p.PersonStatus).LoadAsync();

        if (oldStatusId != request.PersonStatusId)
        {
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var authorId);
            db.PersonActivities.Add(new PersonActivityEntity
            {
                PersonId = id,
                Type = "status_change",
                AuthorId = authorId == 0 ? null : authorId,
                OldStatusId = oldStatusId,
                OldStatusName = oldStatusName,
                OldStatusColor = oldStatusColor,
                NewStatusId = person.PersonStatus?.Id,
                NewStatusName = person.PersonStatus?.Name,
                NewStatusColor = person.PersonStatus?.Color,
            });
            await db.SaveChangesAsync();
        }

        var updatedStatusDto = person.PersonStatus is null ? null : new PersonStatusDto(person.PersonStatus.Id, person.PersonStatus.Name, person.PersonStatus.Color);

        return Ok(new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Telegram, person.Notes,
            person.Gender, person.MaritalStatus, person.Address, person.DateOfBirth,
            person.IsBaptized, person.Church, person.Ministry, person.IsBaptizedWithSpirit,
            updatedStatusDto, person.OversightInfo, person.OversightUserId,
            person.OversightUser is null ? null : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}",
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt,
            await GetCustomFields(id, person.PrimaryGroupId)));
    }

    [HttpDelete("{id}")]
    [RequirePermission("people.delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        db.People.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/activity")]
    [RequirePermission("people.view")]
    public async Task<ActionResult<List<PersonActivityDto>>> GetActivity(long id)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var entries = await db.PersonActivities
            .Include(a => a.Author)
            .Where(a => a.PersonId == id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(entries.Select(a => new PersonActivityDto(
            a.Id,
            a.Type,
            a.Content,
            a.AuthorId,
            a.Author is null ? null : $"{a.Author.Name}{(a.Author.LastName is null ? "" : " " + a.Author.LastName)}",
            a.OldStatusId is null && a.OldStatusName is null ? null : new PersonStatusDto(a.OldStatusId ?? 0, a.OldStatusName ?? "", a.OldStatusColor ?? "#888"),
            a.NewStatusId is null && a.NewStatusName is null ? null : new PersonStatusDto(a.NewStatusId ?? 0, a.NewStatusName ?? "", a.NewStatusColor ?? "#888"),
            a.CreatedAt)).ToList());
    }

    [HttpPost("{id}/comments")]
    [RequirePermission("people.edit")]
    public async Task<ActionResult<PersonActivityDto>> AddComment(long id, AddPersonCommentRequest request)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Коментар не може бути порожнім" });

        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var authorId);

        var entry = new PersonActivityEntity
        {
            PersonId = id,
            Type = "comment",
            Content = request.Content.Trim(),
            AuthorId = authorId == 0 ? null : authorId,
        };
        db.PersonActivities.Add(entry);
        await db.SaveChangesAsync();
        await db.Entry(entry).Reference(a => a.Author).LoadAsync();

        return Ok(new PersonActivityDto(
            entry.Id,
            entry.Type,
            entry.Content,
            entry.AuthorId,
            entry.Author is null ? null : $"{entry.Author.Name}{(entry.Author.LastName is null ? "" : " " + entry.Author.LastName)}",
            null, null,
            entry.CreatedAt));
    }

    // Custom fields — definitions live on the HomeGroup, values are per-person

    [HttpPost("{id}/custom-fields")]
    [RequirePermission("people.customFields")]
    public async Task<ActionResult<CustomFieldDto>> AddCustomField(long id, CreateCustomFieldRequest request)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();
        if (!person.PrimaryGroupId.HasValue)
            return BadRequest(new { message = "Людина не прив'язана до домашньої групи" });

        var field = new HomeGroupCustomField
        {
            HomeGroupId = person.PrimaryGroupId.Value,
            Name = request.Name.Trim(),
        };
        db.HomeGroupCustomFields.Add(field);
        await db.SaveChangesAsync();

        return Ok(new CustomFieldDto(field.Id, field.Name, null));
    }

    [HttpPut("{id}/custom-fields/{fieldId}")]
    [RequirePermission("people.customFields")]
    public async Task<ActionResult<CustomFieldDto>> UpdateCustomField(long id, long fieldId, UpdateCustomFieldRequest request)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var field = await db.HomeGroupCustomFields.FindAsync(fieldId);
        if (field is null) return NotFound();

        var value = await db.PersonCustomFieldValues
            .FirstOrDefaultAsync(v => v.PersonId == id && v.FieldId == fieldId);

        if (value is null)
        {
            value = new PersonCustomFieldValue { PersonId = id, FieldId = fieldId };
            db.PersonCustomFieldValues.Add(value);
        }
        value.Value = request.Value?.Trim();

        await db.SaveChangesAsync();
        return Ok(new CustomFieldDto(field.Id, field.Name, value.Value));
    }

    [HttpDelete("{id}/custom-fields/{fieldId}")]
    [RequirePermission("people.customFields")]
    public async Task<IActionResult> DeleteCustomField(long id, long fieldId)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var field = await db.HomeGroupCustomFields.FindAsync(fieldId);
        if (field is null) return NotFound();

        db.HomeGroupCustomFields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<CustomFieldDto>> GetCustomFields(long personId, long? primaryGroupId)
    {
        if (!primaryGroupId.HasValue) return [];

        var fields = await db.HomeGroupCustomFields
            .Where(f => f.HomeGroupId == primaryGroupId.Value)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();

        var fieldIds = fields.Select(f => f.Id).ToList();
        var values = await db.PersonCustomFieldValues
            .Where(v => v.PersonId == personId && fieldIds.Contains(v.FieldId))
            .ToListAsync();

        return fields.Select(f => new CustomFieldDto(
            f.Id, f.Name,
            values.FirstOrDefault(v => v.FieldId == f.Id)?.Value)).ToList();
    }
}
