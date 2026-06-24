using System.Security.Claims;
using ClosedXML.Excel;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/church-services")]
[Authorize]
public class ChurchServicesController(AppDbContext db) : ControllerBase
{
    public static readonly string[] ValidTypes =
        ["sunday_1", "sunday_2", "vpb", "youth", "night_prayer"];

    private const string XlsxMime =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public record ChurchServiceRecordDto(
        long Id,
        string ServiceType,
        string Date,
        int AttendanceCount,
        int? CommunionCount,
        string? Notes,
        long? CreatedByUserId,
        DateTime CreatedAt);

    public record CreateChurchServiceRecordRequest(
        string ServiceType,
        DateOnly Date,
        int AttendanceCount,
        int? CommunionCount,
        string? Notes);

    public record UpdateChurchServiceRecordRequest(
        int AttendanceCount,
        int? CommunionCount,
        string? Notes);

    public record MonthlyStatPoint(string Month, int TotalAttendance, int? TotalCommunion, int RecordCount);
    public record YearOverYearPoint(int Year, int Month, int TotalAttendance, int? TotalCommunion);
    public record ChurchServiceStatsDto(
        List<MonthlyStatPoint> Monthly,
        List<YearOverYearPoint> YearOverYear);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ChurchServiceRecordDto ToDto(ChurchServiceRecord r) =>
        new(r.Id, r.ServiceType, r.Date.ToString("yyyy-MM-dd"),
            r.AttendanceCount, r.CommunionCount, r.Notes, r.CreatedByUserId, r.CreatedAt);

    private long CurrentUserId() =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private static string LabelFor(string type) => type switch
    {
        "sunday_1"    => "Недільне (1-е)",
        "sunday_2"    => "Недільне (2-е)",
        "vpb"         => "ВПБ",
        "youth"       => "Молодіжка",
        "night_prayer" => "Нічна молитва",
        _              => type,
    };

    // ── GET /api/v1/church-services ───────────────────────────────────────────

    [HttpGet]
    [RequirePermission("church.attendance.view")]
    public async Task<ActionResult<List<ChurchServiceRecordDto>>> GetAll(
        [FromQuery] string? type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var q = db.ChurchServiceRecords.AsQueryable();

        if (!string.IsNullOrEmpty(type)) q = q.Where(r => r.ServiceType == type);
        if (from.HasValue) q = q.Where(r => r.Date >= from.Value);
        if (to.HasValue) q = q.Where(r => r.Date <= to.Value);

        var records = await q.OrderByDescending(r => r.Date).ToListAsync();
        return Ok(records.Select(ToDto).ToList());
    }

    // ── POST /api/v1/church-services ──────────────────────────────────────────

    [HttpPost]
    [RequirePermission("church.attendance.record")]
    public async Task<ActionResult<ChurchServiceRecordDto>> Create(CreateChurchServiceRecordRequest request)
    {
        if (!ValidTypes.Contains(request.ServiceType))
            return BadRequest(new { message = "Невірний тип події" });

        var existing = await db.ChurchServiceRecords
            .FirstOrDefaultAsync(r => r.ServiceType == request.ServiceType && r.Date == request.Date);
        if (existing is not null)
            return Conflict(new { message = "Запис на цю дату вже існує" });

        var record = new ChurchServiceRecord
        {
            ServiceType = request.ServiceType,
            Date = request.Date,
            AttendanceCount = request.AttendanceCount,
            CommunionCount = request.CommunionCount,
            Notes = request.Notes,
            CreatedByUserId = CurrentUserId() == 0 ? null : CurrentUserId(),
        };
        db.ChurchServiceRecords.Add(record);
        await db.SaveChangesAsync();
        return Ok(ToDto(record));
    }

    // ── PUT /api/v1/church-services/:id ──────────────────────────────────────

    [HttpPut("{id:long}")]
    [RequirePermission("church.attendance.record")]
    public async Task<ActionResult<ChurchServiceRecordDto>> Update(long id, UpdateChurchServiceRecordRequest request)
    {
        var record = await db.ChurchServiceRecords.FindAsync(id);
        if (record is null) return NotFound(new { message = "Запис не знайдено" });

        record.AttendanceCount = request.AttendanceCount;
        record.CommunionCount = request.CommunionCount;
        record.Notes = request.Notes;
        await db.SaveChangesAsync();
        return Ok(ToDto(record));
    }

    // ── DELETE /api/v1/church-services/:id ───────────────────────────────────

    [HttpDelete("{id:long}")]
    [RequirePermission("church.attendance.record")]
    public async Task<IActionResult> Delete(long id)
    {
        var record = await db.ChurchServiceRecords.FindAsync(id);
        if (record is null) return NotFound(new { message = "Запис не знайдено" });
        db.ChurchServiceRecords.Remove(record);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/v1/church-services/stats ────────────────────────────────────

    [HttpGet("stats")]
    [RequirePermission("church.attendance.view")]
    public async Task<ActionResult<ChurchServiceStatsDto>> GetStats(
        [FromQuery] string? type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var q = db.ChurchServiceRecords.AsQueryable();
        if (!string.IsNullOrEmpty(type)) q = q.Where(r => r.ServiceType == type);
        if (from.HasValue) q = q.Where(r => r.Date >= from.Value);
        if (to.HasValue) q = q.Where(r => r.Date <= to.Value);

        var records = await q.OrderBy(r => r.Date).ToListAsync();

        var monthly = records
            .GroupBy(r => new { r.Date.Year, r.Date.Month })
            .Select(g =>
            {
                var hasCommunion = g.Any(r => r.CommunionCount.HasValue);
                return new MonthlyStatPoint(
                    $"{g.Key.Year}-{g.Key.Month:D2}",
                    g.Sum(r => r.AttendanceCount),
                    hasCommunion ? g.Sum(r => r.CommunionCount ?? 0) : null,
                    g.Count());
            })
            .OrderBy(p => p.Month)
            .ToList();

        var yearOverYear = records
            .GroupBy(r => new { r.Date.Year, r.Date.Month })
            .Select(g =>
            {
                var hasCommunion = g.Any(r => r.CommunionCount.HasValue);
                return new YearOverYearPoint(
                    g.Key.Year,
                    g.Key.Month,
                    g.Sum(r => r.AttendanceCount),
                    hasCommunion ? g.Sum(r => r.CommunionCount ?? 0) : null);
            })
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToList();

        return Ok(new ChurchServiceStatsDto(monthly, yearOverYear));
    }

    // ── GET /api/v1/church-services/export ───────────────────────────────────

    [HttpGet("export")]
    [RequirePermission("church.attendance.view")]
    public async Task<IActionResult> Export(
        [FromQuery] string? type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var typesToExport = string.IsNullOrEmpty(type)
            ? ValidTypes
            : [type];

        var q = db.ChurchServiceRecords.AsQueryable();
        if (!string.IsNullOrEmpty(type)) q = q.Where(r => r.ServiceType == type);
        if (from.HasValue) q = q.Where(r => r.Date >= from.Value);
        if (to.HasValue) q = q.Where(r => r.Date <= to.Value);

        var all = await q.OrderBy(r => r.ServiceType).ThenBy(r => r.Date).ToListAsync();

        using var wb = new XLWorkbook();

        foreach (var stype in typesToExport)
        {
            var rows = all.Where(r => r.ServiceType == stype).ToList();
            if (rows.Count == 0) continue;

            var ws = wb.Worksheets.Add(LabelFor(stype));
            var hasCommunion = stype is "sunday_1" or "sunday_2";

            // Header
            ws.Cell(1, 1).Value = "Дата";
            ws.Cell(1, 2).Value = "Присутніх";
            var col = 3;
            if (hasCommunion) { ws.Cell(1, col).Value = "Причастя"; col++; }
            ws.Cell(1, col).Value = "Нотатки";

            var headerRow = ws.Range(1, 1, 1, col).Style;
            headerRow.Font.Bold = true;
            headerRow.Fill.BackgroundColor = XLColor.FromHtml("#E8F4FD");

            // Data
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = i + 2;
                ws.Cell(row, 1).Value = r.Date.ToString("dd.MM.yyyy");
                ws.Cell(row, 2).Value = r.AttendanceCount;
                var c = 3;
                if (hasCommunion) { if (r.CommunionCount.HasValue) ws.Cell(row, c).Value = r.CommunionCount.Value; c++; }
                ws.Cell(row, c).Value = r.Notes ?? "";
            }

            ws.Columns().AdjustToContents();
        }

        if (!wb.Worksheets.Any())
        {
            wb.Worksheets.Add("Немає даних");
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var filename = $"church-services-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
        return File(ms.ToArray(), XlsxMime, filename);
    }
}
