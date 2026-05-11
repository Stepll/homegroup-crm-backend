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
            query = query.Where(p => p.Name.Contains(search) || (p.Phone != null && p.Phone.Contains(search)));

        var people = await query
            .OrderBy(p => p.Name)
            .Select(p => new PersonResponse(p.Id, p.Name, p.Phone, p.Email, p.Notes, p.Status, p.CreatedAt))
            .ToListAsync();

        return Ok(people);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PersonResponse>> GetById(int id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        return Ok(new PersonResponse(person.Id, person.Name, person.Phone, person.Email, person.Notes, person.Status, person.CreatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<PersonResponse>> Create(CreatePersonRequest request)
    {
        var person = new Person
        {
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Notes = request.Notes,
        };

        db.People.Add(person);
        await db.SaveChangesAsync();

        var response = new PersonResponse(person.Id, person.Name, person.Phone, person.Email, person.Notes, person.Status, person.CreatedAt);
        return CreatedAtAction(nameof(GetById), new { id = person.Id }, response);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PersonResponse>> Update(int id, UpdatePersonRequest request)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        person.Name = request.Name;
        person.Phone = request.Phone;
        person.Email = request.Email;
        person.Notes = request.Notes;
        person.Status = request.Status;

        await db.SaveChangesAsync();
        return Ok(new PersonResponse(person.Id, person.Name, person.Phone, person.Email, person.Notes, person.Status, person.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        db.People.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
