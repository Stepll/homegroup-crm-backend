using System.Security.Claims;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.People;
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
    public async Task<ActionResult<List<PersonResponse>>> GetAll([FromQuery] string? search, [FromQuery] bool noGroup = false)
    {
        var query = db.People.Include(p => p.PrimaryGroup).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                (p.LastName != null && p.LastName.Contains(search)) ||
                (p.Phone != null && p.Phone.Contains(search)));

        if (noGroup)
            query = query.Where(p => p.PrimaryGroupId == null);

        // Non-superadmin users with visible groups can only see people from those groups
        if (long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) && userId != 0)
        {
            var visibleGroupIds = await db.UserHomeGroups
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.HomeGroupId)
                .ToListAsync();

            if (visibleGroupIds.Count > 0)
                query = query.Where(p => p.PrimaryGroupId != null && visibleGroupIds.Contains(p.PrimaryGroupId.Value));
        }

        var people = await query
            .OrderBy(p => p.Name)
            .Select(p => new PersonResponse(p.Id, p.Name, p.LastName, p.Phone, p.Email, p.Notes, p.Status,
                p.PrimaryGroupId, p.PrimaryGroup != null ? p.PrimaryGroup.Name : null,
                p.PrimaryGroup != null ? p.PrimaryGroup.Color : null, p.CreatedAt))
            .ToListAsync();

        return Ok(people);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PersonDetailResponse>> GetById(long id)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        return Ok(new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Notes,
            person.Status, person.OversightInfo, person.OversightUserId,
            person.OversightUser is null ? null : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}",
            person.DateOfBirth,
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
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Notes,
            person.Status, person.OversightInfo, null, null, person.DateOfBirth,
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt, []));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PersonDetailResponse>> Update(long id, UpdatePersonRequest request)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        var oldGroupId = person.PrimaryGroupId;

        person.Name = request.Name.Trim();
        person.LastName = request.LastName?.Trim();
        person.Phone = request.Phone?.Trim();
        person.Email = request.Email?.Trim();
        person.Notes = request.Notes?.Trim();
        person.Status = request.Status;
        person.OversightInfo = request.OversightInfo?.Trim();
        person.OversightUserId = request.OversightUserId;
        person.DateOfBirth = request.DateOfBirth;
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

        return Ok(new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Notes,
            person.Status, person.OversightInfo, person.OversightUserId,
            person.OversightUser is null ? null : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}",
            person.DateOfBirth,
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
