using System.Security.Claims;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Groups;
using HomeGroup.API.Models.DTOs.People;
using HomeGroup.API.Models.DTOs.PersonStatuses;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/people")]
[Authorize]
public class PeopleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
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
        var query = db.People.Include(p => p.PrimaryGroup).Include(p => p.PersonStatus).AsQueryable();

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
            p.CreatedAt, false, null, null)).ToList();

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
    public async Task<ActionResult<PersonDetailResponse>> Update(long id, UpdatePersonRequest request)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .Include(p => p.PersonStatus)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        var oldGroupId = person.PrimaryGroupId;

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
    public async Task<IActionResult> Delete(long id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        db.People.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Custom fields — definitions live on the HomeGroup, values are per-person

    [HttpPost("{id}/custom-fields")]
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
