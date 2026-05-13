using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Attendance;
using Entities = HomeGroup.API.Models.Entities;
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
            .Include(a => a.User)
            .Where(a => a.HomeGroupId == groupId);

        if (from.HasValue) query = query.Where(a => a.MeetingDate >= from.Value);
        if (to.HasValue) query = query.Where(a => a.MeetingDate <= to.Value);

        var records = await query
            .OrderByDescending(a => a.MeetingDate)
            .ToListAsync();

        return Ok(records.Select(a => new AttendanceResponse(
            a.Id, a.PersonId, a.UserId,
            MemberName(a),
            a.HomeGroupId, a.MeetingDate, a.WasPresent, a.Notes)));
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
            if (entry.PersonId is null && entry.UserId is null) continue;

            var existing = await db.Attendances.FirstOrDefaultAsync(a =>
                a.HomeGroupId == request.HomeGroupId &&
                a.PersonId == entry.PersonId &&
                a.UserId == entry.UserId &&
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
                    UserId = entry.UserId,
                    MeetingDate = request.MeetingDate,
                    WasPresent = entry.WasPresent,
                    Notes = entry.Notes,
                });
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("meta")]
    public async Task<ActionResult<AttendanceMetaResponse>> GetMeta(
        [FromQuery] long groupId, [FromQuery] DateOnly date)
    {
        var meta = await db.AttendanceMetas
            .FirstOrDefaultAsync(m => m.HomeGroupId == groupId && m.MeetingDate == date);
        return Ok(new AttendanceMetaResponse(meta?.GuestCount ?? 0, meta?.GuestInfo));
    }

    [HttpPost("meta")]
    public async Task<IActionResult> SaveMeta(SaveAttendanceMetaRequest request)
    {
        var meta = await db.AttendanceMetas
            .FirstOrDefaultAsync(m => m.HomeGroupId == request.HomeGroupId && m.MeetingDate == request.MeetingDate);

        if (meta is null)
        {
            db.AttendanceMetas.Add(new Entities.AttendanceMeta
            {
                HomeGroupId = request.HomeGroupId,
                MeetingDate = request.MeetingDate,
                GuestCount = request.GuestCount,
                GuestInfo = request.GuestInfo?.Trim(),
            });
        }
        else
        {
            meta.GuestCount = request.GuestCount;
            meta.GuestInfo = request.GuestInfo?.Trim();
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    private static string MemberName(Attendance a)
    {
        if (a.Person is not null)
            return $"{a.Person.Name}{(a.Person.LastName is null ? "" : " " + a.Person.LastName)}";
        if (a.User is not null)
            return $"{a.User.Name}{(a.User.LastName is null ? "" : " " + a.User.LastName)}";
        return "?";
    }
}
