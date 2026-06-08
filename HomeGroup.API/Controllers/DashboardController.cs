using System.Security.Claims;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Dashboard;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    // ── Inactive members (давно не ходять) ─────────────────────────────────────

    [HttpGet("inactive-members")]
    [RequirePermission("attendance.view")]
    public async Task<ActionResult<List<InactiveMemberDto>>> GetInactiveMembers(
        [FromQuery] long? groupId,
        [FromQuery] int minMissed = 5)
    {
        var visibleGroupIds = await GetVisibleGroupIds();
        if (visibleGroupIds is not null && groupId.HasValue && !visibleGroupIds.Contains(groupId.Value))
            return Ok(new List<InactiveMemberDto>());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sixMonthsAgo = today.AddMonths(-6);

        var query = db.Attendances
            .Include(a => a.Person).ThenInclude(p => p!.PrimaryGroup)
            .Include(a => a.User).ThenInclude(u => u!.PrimaryGroup)
            .Where(a => a.MeetingDate >= sixMonthsAgo && a.MeetingDate <= today);

        if (groupId.HasValue)
            query = query.Where(a => a.HomeGroupId == groupId.Value);
        else if (visibleGroupIds is not null)
            query = query.Where(a => visibleGroupIds.Contains(a.HomeGroupId));

        var records = await query.ToListAsync();

        // Group by person / user
        var grouped = records
            .GroupBy(a => new { a.PersonId, a.UserId })
            .Where(g => g.Key.PersonId.HasValue || g.Key.UserId.HasValue)
            .Select(g =>
            {
                // Consecutive missed streak from most recent record backwards
                var sortedDesc = g.OrderByDescending(a => a.MeetingDate).ToList();
                var missed = 0;
                foreach (var r in sortedDesc)
                {
                    if (r.WasPresent) break;
                    missed++;
                }
                var lastAttended = sortedDesc
                    .Where(a => a.WasPresent)
                    .Select(a => (DateOnly?)a.MeetingDate)
                    .FirstOrDefault();

                var sample = g.First();
                string fullName;
                long? grpId;
                string? grpName;
                string? grpColor;

                if (sample.Person is not null)
                {
                    fullName = $"{sample.Person.Name}{(sample.Person.LastName is null ? "" : " " + sample.Person.LastName)}";
                    grpId = sample.Person.PrimaryGroupId;
                    grpName = sample.Person.PrimaryGroup?.Name;
                    grpColor = sample.Person.PrimaryGroup?.Color;
                }
                else if (sample.User is not null)
                {
                    fullName = $"{sample.User.Name}{(sample.User.LastName is null ? "" : " " + sample.User.LastName)}";
                    grpId = sample.User.PrimaryGroupId;
                    grpName = sample.User.PrimaryGroup?.Name;
                    grpColor = sample.User.PrimaryGroup?.Color;
                }
                else
                {
                    return null!;
                }

                return new InactiveMemberDto(
                    g.Key.PersonId, g.Key.UserId, fullName,
                    grpId, grpName, grpColor,
                    missed,
                    lastAttended?.ToString("yyyy-MM-dd"));
            })
            .Where(x => x is not null && x.MissedCount >= minMissed)
            .OrderByDescending(x => x.MissedCount)
            .ThenBy(x => x.FullName)
            .ToList();

        return Ok(grouped);
    }

    // ── Status distribution (співвідношення по статусу) ────────────────────────

    [HttpGet("status-distribution")]
    [RequirePermission("people.view")]
    public async Task<ActionResult<StatusDistributionResponse>> GetStatusDistribution(
        [FromQuery] long? groupId)
    {
        var visibleGroupIds = await GetVisibleGroupIds();
        if (visibleGroupIds is not null && groupId.HasValue && !visibleGroupIds.Contains(groupId.Value))
            return Ok(new StatusDistributionResponse(0, new List<StatusDistributionItem>()));

        var personsQuery = db.People.Include(p => p.PersonStatus).AsQueryable();
        if (groupId.HasValue)
            personsQuery = personsQuery.Where(p => p.PrimaryGroupId == groupId.Value);
        else if (visibleGroupIds is not null)
            personsQuery = personsQuery.Where(p => p.PrimaryGroupId != null && visibleGroupIds.Contains(p.PrimaryGroupId.Value));

        var adminsQuery = db.Users.Include(u => u.PersonStatus).Where(u => u.Id != 0).AsQueryable();
        if (groupId.HasValue)
            adminsQuery = adminsQuery.Where(u => u.PrimaryGroupId == groupId.Value);
        else if (visibleGroupIds is not null)
            adminsQuery = adminsQuery.Where(u => u.PrimaryGroupId != null && visibleGroupIds.Contains(u.PrimaryGroupId.Value));

        var persons = await personsQuery.Select(p => new { p.PersonStatusId, Status = p.PersonStatus }).ToListAsync();
        var admins = await adminsQuery.Select(u => new { u.PersonStatusId, Status = u.PersonStatus }).ToListAsync();
        var combined = persons.Concat(admins).ToList();

        var grouped = combined
            .GroupBy(x => new { x.PersonStatusId, Name = x.Status?.Name, Color = x.Status?.Color })
            .Select(g => new StatusDistributionItem(
                g.Key.PersonStatusId,
                g.Key.Name ?? "Без статусу",
                g.Key.Color ?? "#9CA3AF",
                g.Count()))
            .OrderByDescending(i => i.Count)
            .ToList();

        return Ok(new StatusDistributionResponse(combined.Count, grouped));
    }

    // ── Groups comparison (порівняння домашок) ─────────────────────────────────

    [HttpGet("groups-comparison")]
    [RequirePermission("attendance.stats")]
    public async Task<ActionResult<List<GroupComparisonSeries>>> GetGroupsComparison(
        [FromQuery] string groupIds = "",
        [FromQuery] string period = "3m")
    {
        var ids = groupIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var n) ? n : -1)
            .Where(n => n > 0)
            .ToList();

        if (ids.Count == 0) return Ok(new List<GroupComparisonSeries>());

        var visibleGroupIds = await GetVisibleGroupIds();
        if (visibleGroupIds is not null)
            ids = ids.Where(id => visibleGroupIds.Contains(id)).ToList();

        if (ids.Count == 0) return Ok(new List<GroupComparisonSeries>());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = period switch
        {
            "1m" => today.AddMonths(-1),
            "6m" => today.AddMonths(-6),
            _ => today.AddMonths(-3),
        };

        var groups = await db.HomeGroups
            .Where(g => ids.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.Color })
            .ToListAsync();

        var attendance = await db.Attendances
            .Where(a => ids.Contains(a.HomeGroupId)
                && a.MeetingDate >= periodStart
                && a.MeetingDate <= today)
            .Select(a => new { a.HomeGroupId, a.MeetingDate, a.WasPresent })
            .ToListAsync();

        var result = groups.Select(g =>
        {
            var groupRecords = attendance.Where(a => a.HomeGroupId == g.Id);
            var points = groupRecords
                .GroupBy(a => a.MeetingDate)
                .OrderBy(grp => grp.Key)
                .Select(grp =>
                {
                    var total = grp.Count();
                    var present = grp.Count(a => a.WasPresent);
                    var rate = total == 0 ? 0 : Math.Round(present * 100.0 / total, 1);
                    return new GroupComparisonPoint(grp.Key.ToString("yyyy-MM-dd"), rate);
                })
                .ToList();
            return new GroupComparisonSeries(g.Id, g.Name, g.Color, points);
        }).ToList();

        return Ok(result);
    }

    // ── Groups attendance summary (по домашках з усіма періодами) ──────────────

    [HttpGet("groups-attendance-summary")]
    [RequirePermission("attendance.stats")]
    public async Task<ActionResult<GroupsAttendanceSummaryResponse>> GetGroupsAttendanceSummary()
    {
        var visibleGroupIds = await GetVisibleGroupIds();

        var groupsQuery = db.HomeGroups.AsQueryable();
        if (visibleGroupIds is not null)
            groupsQuery = groupsQuery.Where(g => visibleGroupIds.Contains(g.Id));

        var groups = await groupsQuery
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.Color })
            .ToListAsync();

        if (groups.Count == 0)
            return Ok(new GroupsAttendanceSummaryResponse(new(), 0, 0, 0, 0));

        var groupIdsList = groups.Select(g => g.Id).ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sixMonthsAgo = today.AddMonths(-6);

        var attendance = await db.Attendances
            .Where(a => groupIdsList.Contains(a.HomeGroupId)
                && a.MeetingDate >= sixMonthsAgo
                && a.MeetingDate <= today)
            .Select(a => new AttRec(a.HomeGroupId, a.MeetingDate, a.WasPresent))
            .ToListAsync();

        // Member counts per group (persons + admins with primaryGroupId)
        var personCounts = await db.People
            .Where(p => p.PrimaryGroupId != null && groupIdsList.Contains(p.PrimaryGroupId.Value))
            .GroupBy(p => p.PrimaryGroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync();

        var adminCounts = await db.Users
            .Where(u => u.Id != 0 && u.PrimaryGroupId != null && groupIdsList.Contains(u.PrimaryGroupId.Value))
            .GroupBy(u => u.PrimaryGroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync();

        var memberCountByGroup = groupIdsList.ToDictionary(
            id => id,
            id => (personCounts.FirstOrDefault(c => c.GroupId == id)?.Count ?? 0)
                  + (adminCounts.FirstOrDefault(c => c.GroupId == id)?.Count ?? 0));

        var rows = groups.Select(g =>
        {
            var groupRecs = attendance.Where(a => a.HomeGroupId == g.Id).ToList();
            return new GroupAttendanceSummaryRow(
                g.Id, g.Name, g.Color,
                memberCountByGroup.GetValueOrDefault(g.Id, 0),
                AvgRateOverPeriod(groupRecs, today.AddMonths(-1), today),
                AvgRateOverPeriod(groupRecs, today.AddMonths(-3), today),
                AvgRateOverPeriod(groupRecs, today.AddMonths(-6), today));
        }).ToList();

        var totalMembers = memberCountByGroup.Values.Sum();
        var totalAvg1m = AvgRateOverPeriod(attendance, today.AddMonths(-1), today);
        var totalAvg3m = AvgRateOverPeriod(attendance, today.AddMonths(-3), today);
        var totalAvg6m = AvgRateOverPeriod(attendance, today.AddMonths(-6), today);

        return Ok(new GroupsAttendanceSummaryResponse(rows, totalMembers, totalAvg1m, totalAvg3m, totalAvg6m));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private record AttRec(long HomeGroupId, DateOnly MeetingDate, bool WasPresent);

    private static double AvgRateOverPeriod(List<AttRec> records, DateOnly start, DateOnly end)
    {
        var filtered = records.Where(r => r.MeetingDate >= start && r.MeetingDate <= end).ToList();
        if (filtered.Count == 0) return 0;

        var perMeeting = filtered
            .GroupBy(r => new { r.HomeGroupId, r.MeetingDate })
            .Select(g =>
            {
                var total = g.Count();
                var present = g.Count(r => r.WasPresent);
                return total == 0 ? 0 : present * 100.0 / total;
            })
            .ToList();

        return perMeeting.Count == 0 ? 0 : Math.Round(perMeeting.Average(), 1);
    }

    private async Task<HashSet<long>?> GetVisibleGroupIds()
    {
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var currentUserId);
        bool isSuperAdmin = currentUserId == 0;

        if (isSuperAdmin) return null;

        var ids = await db.UserHomeGroups
            .Where(ug => ug.UserId == currentUserId)
            .Select(ug => ug.HomeGroupId)
            .ToListAsync();
        return ids.ToHashSet();
    }
}
