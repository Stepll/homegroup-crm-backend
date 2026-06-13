using System.Security.Claims;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/anon-polls")]
[Authorize]
public class AnonymousPollsController(AppDbContext db) : ControllerBase
{
    public record AnonymousPollDto(
        long Id,
        long HomeGroupId,
        long? StartedByUserId,
        long DestinationChatId,
        DateTime StartedAt,
        DateTime ExpiresAt);

    public record CreateAnonymousPollRequest(long HomeGroupId, long DestinationChatId);

    private static AnonymousPollDto ToDto(AnonymousPoll p) =>
        new(p.Id, p.HomeGroupId, p.StartedByUserId, p.DestinationChatId, p.StartedAt, p.ExpiresAt);

    private static DateTime EndOfKyivDayUtc()
    {
        // ExpiresAt = end of today in Kyiv (i.e. midnight of next day Kyiv → in UTC)
        var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        var nowKyiv = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kyiv);
        var nextDayMidnightKyiv = DateTime.SpecifyKind(
            new DateTime(nowKyiv.Year, nowKyiv.Month, nowKyiv.Day).AddDays(1),
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(nextDayMidnightKyiv, kyiv);
    }

    [HttpPost]
    [RequirePermission("groups.events.manage")]
    public async Task<ActionResult<AnonymousPollDto>> Create(CreateAnonymousPollRequest request)
    {
        var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == request.HomeGroupId);
        if (group is null) return NotFound(new { message = "Домашку не знайдено" });

        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var currentUserId);

        var now = DateTime.UtcNow;

        // Close any existing active poll for this group (overwrite)
        var existing = await db.AnonymousPolls
            .Where(p => p.HomeGroupId == request.HomeGroupId && p.ExpiresAt > now)
            .ToListAsync();
        foreach (var e in existing) e.ExpiresAt = now;

        var poll = new AnonymousPoll
        {
            HomeGroupId = request.HomeGroupId,
            StartedByUserId = currentUserId == 0 ? null : currentUserId,
            DestinationChatId = request.DestinationChatId,
            StartedAt = now,
            ExpiresAt = EndOfKyivDayUtc(),
        };
        db.AnonymousPolls.Add(poll);
        await db.SaveChangesAsync();

        return Ok(ToDto(poll));
    }

    [HttpGet("active")]
    public async Task<ActionResult<AnonymousPollDto?>> GetActive([FromQuery] long homeGroupId)
    {
        var now = DateTime.UtcNow;
        var poll = await db.AnonymousPolls
            .Where(p => p.HomeGroupId == homeGroupId && p.ExpiresAt > now)
            .OrderByDescending(p => p.StartedAt)
            .FirstOrDefaultAsync();
        return poll is null ? Ok((AnonymousPollDto?)null) : Ok(ToDto(poll));
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("groups.events.manage")]
    public async Task<IActionResult> Close(long id)
    {
        var poll = await db.AnonymousPolls.FindAsync(id);
        if (poll is null) return NotFound();
        poll.ExpiresAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
