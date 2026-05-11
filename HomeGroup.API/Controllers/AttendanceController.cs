using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Attendance;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class AttendanceController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AttendanceResponse>>> GetByGroup(
        [FromQuery] long groupId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var query = db.Attendances
            .Include(a => a.Person)
            .Where(a => a.HomeGroupId == groupId);

        if (from.HasValue) query = query.Where(a => a.MeetingDate >= from.Value);
        if (to.HasValue) query = query.Where(a => a.MeetingDate <= to.Value);

        var records = await query
            .OrderByDescending(a => a.MeetingDate)
            .Select(a => new AttendanceResponse(a.Id, a.PersonId, a.Person.Name, a.HomeGroupId, a.MeetingDate, a.WasPresent, a.Notes))
            .ToListAsync();

        return Ok(records);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<List<AttendanceSummary>>> GetSummary([FromQuery] long groupId)
    {
        var summary = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .GroupBy(a => a.MeetingDate)
            .Select(g => new AttendanceSummary(
                g.Key,
                g.Count(),
                g.Count(a => a.WasPresent),
                g.Count() == 0 ? 0 : Math.Round((double)g.Count(a => a.WasPresent) / g.Count() * 100, 1)
            ))
            .OrderByDescending(s => s.MeetingDate)
            .ToListAsync();

        return Ok(summary);
    }

    [HttpPost]
    public async Task<IActionResult> Record(RecordAttendanceRequest request)
    {
        if (!await db.HomeGroups.AnyAsync(g => g.Id == request.HomeGroupId))
            return NotFound(new { message = "Група не знайдена" });

        foreach (var entry in request.Entries)
        {
            var existing = await db.Attendances.FirstOrDefaultAsync(a =>
                a.HomeGroupId == request.HomeGroupId &&
                a.PersonId == entry.PersonId &&
                a.MeetingDate == request.MeetingDate);

            if (existing is not null)
            {
                existing.WasPresent = entry.WasPresent;
                existing.Notes = entry.Notes;
            }
            else
            {
                db.Attendances.Add(new Attendance
                {
                    HomeGroupId = request.HomeGroupId,
                    PersonId = entry.PersonId,
                    MeetingDate = request.MeetingDate,
                    WasPresent = entry.WasPresent,
                    Notes = entry.Notes,
                });
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }
}
