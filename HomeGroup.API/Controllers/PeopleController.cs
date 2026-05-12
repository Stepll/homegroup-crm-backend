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
    public async Task<ActionResult<List<PersonResponse>>> GetAll([FromQuery] string? search)
    {
        var query = db.People.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                (p.LastName != null && p.LastName.Contains(search)) ||
                (p.Phone != null && p.Phone.Contains(search)));

        var people = await query
            .OrderBy(p => p.Name)
            .Select(p => new PersonResponse(p.Id, p.Name, p.LastName, p.Phone, p.Email, p.Notes, p.Status, p.CreatedAt))
            .ToListAsync();

        return Ok(people);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PersonDetailResponse>> GetById(long id)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.CustomFields.OrderBy(f => f.CreatedAt))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        return Ok(ToDetail(person));
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

        return CreatedAtAction(nameof(GetById), new { id = person.Id }, ToDetail(person));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PersonDetailResponse>> Update(long id, UpdatePersonRequest request)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.CustomFields.OrderBy(f => f.CreatedAt))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        person.Name = request.Name.Trim();
        person.LastName = request.LastName?.Trim();
        person.Phone = request.Phone?.Trim();
        person.Email = request.Email?.Trim();
        person.Notes = request.Notes?.Trim();
        person.Status = request.Status;
        person.OversightInfo = request.OversightInfo?.Trim();
        person.DateOfBirth = request.DateOfBirth;
        person.PrimaryGroupId = request.PrimaryGroupId;

        await db.SaveChangesAsync();
        await db.Entry(person).Reference(p => p.PrimaryGroup).LoadAsync();

        return Ok(ToDetail(person));
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

    // Custom fields

    [HttpPost("{id}/custom-fields")]
    public async Task<ActionResult<CustomFieldDto>> AddCustomField(long id, CreateCustomFieldRequest request)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var field = new PersonCustomField { PersonId = id, Name = request.Name.Trim() };
        db.PersonCustomFields.Add(field);
        await db.SaveChangesAsync();

        return Ok(new CustomFieldDto(field.Id, field.Name, field.Value));
    }

    [HttpPut("{id}/custom-fields/{fieldId}")]
    public async Task<ActionResult<CustomFieldDto>> UpdateCustomField(long id, long fieldId, UpdateCustomFieldRequest request)
    {
        var field = await db.PersonCustomFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.PersonId == id);
        if (field is null) return NotFound();

        field.Value = request.Value?.Trim();
        await db.SaveChangesAsync();

        return Ok(new CustomFieldDto(field.Id, field.Name, field.Value));
    }

    [HttpDelete("{id}/custom-fields/{fieldId}")]
    public async Task<IActionResult> DeleteCustomField(long id, long fieldId)
    {
        var field = await db.PersonCustomFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.PersonId == id);
        if (field is null) return NotFound();

        db.PersonCustomFields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static PersonDetailResponse ToDetail(Person p) => new(
        p.Id, p.Name, p.LastName, p.Phone, p.Email, p.Notes,
        p.Status, p.OversightInfo, p.DateOfBirth,
        p.PrimaryGroupId, p.PrimaryGroup?.Name,
        p.CreatedAt,
        p.CustomFields.Select(f => new CustomFieldDto(f.Id, f.Name, f.Value)).ToList());
}
