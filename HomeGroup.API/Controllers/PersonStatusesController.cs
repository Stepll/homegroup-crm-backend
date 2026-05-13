using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.PersonStatuses;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/person-statuses")]
[Authorize]
public class PersonStatusesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PersonStatusDto>>> GetAll()
    {
        var statuses = await db.PersonStatuses
            .OrderBy(s => s.CreatedAt)
            .Select(s => new PersonStatusDto(s.Id, s.Name, s.Color))
            .ToListAsync();

        return Ok(statuses);
    }

    [HttpPost]
    public async Task<ActionResult<PersonStatusDto>> Create(CreatePersonStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Назва обов'язкова" });

        var status = new PersonStatus
        {
            Name = request.Name.Trim(),
            Color = request.Color.Trim(),
        };

        db.PersonStatuses.Add(status);
        await db.SaveChangesAsync();

        return Ok(new PersonStatusDto(status.Id, status.Name, status.Color));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PersonStatusDto>> Update(long id, UpdatePersonStatusRequest request)
    {
        var status = await db.PersonStatuses.FindAsync(id);
        if (status is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Назва обов'язкова" });

        status.Name = request.Name.Trim();
        status.Color = request.Color.Trim();

        await db.SaveChangesAsync();

        return Ok(new PersonStatusDto(status.Id, status.Name, status.Color));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var status = await db.PersonStatuses.FindAsync(id);
        if (status is null) return NotFound();

        db.PersonStatuses.Remove(status);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
