using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.ChurchEvents;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/church-events")]
[Authorize]
public class ChurchEventsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ChurchEventDto>>> GetAll()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await db.ChurchEvents.OrderBy(e => e.CreatedAt).ToListAsync();

        var result = events
            .Select(e => (e, days: NextOccurrence(e.Month, e.Day, today)))
            .OrderBy(x => x.days)
            .Take(5)
            .Select(x => new ChurchEventDto(x.e.Id, x.e.Name, x.e.Month, x.e.Day, x.days))
            .ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ChurchEventDto>> Create(CreateChurchEventRequest request)
    {
        var evt = new ChurchEvent { Name = request.Name.Trim(), Month = request.Month, Day = request.Day };
        db.ChurchEvents.Add(evt);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(new ChurchEventDto(evt.Id, evt.Name, evt.Month, evt.Day, NextOccurrence(evt.Month, evt.Day, today)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var evt = await db.ChurchEvents.FindAsync(id);
        if (evt is null) return NotFound();
        db.ChurchEvents.Remove(evt);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static int NextOccurrence(int month, int day, DateOnly today)
    {
        var thisYear = new DateOnly(today.Year, month, day);
        if (thisYear.DayNumber < today.DayNumber) thisYear = thisYear.AddYears(1);
        return thisYear.DayNumber - today.DayNumber;
    }
}
