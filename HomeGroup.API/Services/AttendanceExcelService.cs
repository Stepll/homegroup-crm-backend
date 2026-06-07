using ClosedXML.Excel;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Attendance;
using HomeGroup.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Services;

public class AttendanceExcelService(AppDbContext db)
{
    // Column layout for export sheets
    private const int ColPercent = 1;       // A
    private const int ColIdHint = 2;        // B (hidden)
    private const int ColFullName = 3;      // C
    private const int ColStatus = 4;        // D
    private const int ColOversight = 5;     // E
    private const int ColJoinedAt = 6;      // F
    private const int ColFirstDate = 7;     // G

    // Row layout
    private const int RowGroupName = 1;
    private const int RowTotal = 2;
    private const int RowGuests = 5;
    private const int RowNotes = 6;
    private const int RowHeader = 8;
    private const int RowFirstPerson = 9;

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd",
        "dd.MM.yyyy",
        "d.M.yyyy",
        "dd/MM/yyyy",
        "MM/dd/yyyy",
    };

    // ============================================================
    // EXPORT
    // ============================================================

    public async Task<byte[]> Export(
        long[] groupIds,
        DateOnly? from,
        DateOnly? to,
        bool isTemplate)
    {
        using var wb = new XLWorkbook();

        foreach (var groupId in groupIds)
        {
            var group = await db.HomeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null) continue;

            await BuildSheet(wb, group, from, to, isTemplate);
        }

        if (wb.Worksheets.Count == 0)
            wb.Worksheets.Add("Порожньо");

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task BuildSheet(
        XLWorkbook wb,
        HomeGroupEntity group,
        DateOnly? from,
        DateOnly? to,
        bool isTemplate)
    {
        var sheetName = SanitizeSheetName(group.Name, wb);
        var ws = wb.Worksheets.Add(sheetName);

        var dates = await CollectDates(group.Id, from, to);
        var (members, attendanceMap) = await CollectMembers(group.Id);
        var metas = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == group.Id && (from == null || m.MeetingDate >= from) && (to == null || m.MeetingDate <= to))
            .ToListAsync();
        var metaByDate = metas.ToDictionary(m => m.MeetingDate);

        var cancelledFromCal = (await db.CalendarEvents
            .Where(e => e.HomeGroupId == group.Id
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.IsHomeGroupMeeting == false
                && e.MovedToDate == null
                && e.Date != null
                && dates.Contains(e.Date!.Value))
            .Select(e => e.Date!.Value)
            .ToListAsync()).ToHashSet();

        // Row 1: group name + dates
        ws.Cell(RowGroupName, ColFullName).Value = group.Name;
        ws.Cell(RowGroupName, ColFullName).Style.Fill.BackgroundColor = XLColor.FromHtml("#333333");
        ws.Cell(RowGroupName, ColFullName).Style.Font.FontColor = XLColor.White;
        ws.Cell(RowGroupName, ColFullName).Style.Font.Bold = true;

        for (int i = 0; i < dates.Count; i++)
        {
            var col = ColFirstDate + i;
            var date = dates[i];
            ws.Cell(RowGroupName, col).Value = date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(RowGroupName, col).Style.NumberFormat.Format = "dd.MM.yyyy";
            ws.Cell(RowGroupName, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#333333");
            ws.Cell(RowGroupName, col).Style.Font.FontColor = XLColor.White;
            ws.Cell(RowGroupName, col).Style.Font.Bold = true;
        }

        // Row 2: "Загалом"
        ws.Cell(RowTotal, ColFullName).Value = "Загалом";
        ws.Range(RowTotal, ColPercent, RowTotal, ColJoinedAt).Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
        ws.Range(RowTotal, ColPercent, RowTotal, ColJoinedAt).Style.Font.FontColor = XLColor.White;

        // Row 5: guests
        ws.Cell(RowGuests, ColFullName).Value = "Нові / невіруючі / гості";
        ws.Range(RowGuests, ColPercent, RowGuests, ColJoinedAt).Style.Fill.BackgroundColor = XLColor.FromHtml("#93C5FD");

        // Row 6: notes
        ws.Cell(RowNotes, ColFullName).Value = "Нотатки (тема, гості, перенос)";
        ws.Range(RowNotes, ColPercent, RowNotes, ColJoinedAt).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
        ws.Row(RowNotes).Height = 60;
        ws.Row(RowNotes).Style.Alignment.WrapText = true;

        // Row 9: column headers
        ws.Cell(RowHeader, ColPercent).Value = "%";
        ws.Cell(RowHeader, ColIdHint).Value = "ID";
        ws.Cell(RowHeader, ColFullName).Value = "Прізвище, ім'я";
        ws.Cell(RowHeader, ColStatus).Value = "Статус";
        ws.Cell(RowHeader, ColOversight).Value = "Опіка";
        ws.Cell(RowHeader, ColJoinedAt).Value = "Дата приєднання";
        ws.Range(RowHeader, ColPercent, RowHeader, ColJoinedAt).Style.Font.Bold = true;
        ws.Range(RowHeader, ColPercent, RowHeader, ColJoinedAt).Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");

        // Fill per-date meeting data (rows 2, 5, 7)
        for (int i = 0; i < dates.Count; i++)
        {
            var col = ColFirstDate + i;
            var date = dates[i];
            metaByDate.TryGetValue(date, out var meta);
            var isCancelled = (meta?.IsCancelled ?? false) || cancelledFromCal.Contains(date);

            if (isCancelled)
            {
                // Mark column with "-" in row 2 to indicate cancellation
                ws.Cell(RowTotal, col).Value = "-";
                ws.Column(col).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
            }
            else
            {
                int present = 0;
                foreach (var (_, attMap) in attendanceMap)
                {
                    if (attMap.TryGetValue(date, out var was) && was) present++;
                }
                var guests = meta?.GuestCount ?? 0;
                ws.Cell(RowTotal, col).Value = present + guests;
                ws.Cell(RowGuests, col).Value = guests;
            }

            // Notes: combine GuestInfo + Notes
            var notesParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(meta?.GuestInfo)) notesParts.Add(meta.GuestInfo);
            if (!string.IsNullOrWhiteSpace(meta?.Notes)) notesParts.Add(meta.Notes);
            if (notesParts.Count > 0)
                ws.Cell(RowNotes, col).Value = string.Join("\n", notesParts);
        }

        // People rows (10+)
        // Order: current members first (sorted by name), then past members (with LeftAt)
        var currentMembers = members.Where(m => m.LeftAt is null).OrderBy(m => m.LastName).ThenBy(m => m.Name).ToList();
        var pastMembers = members.Where(m => m.LeftAt is not null).OrderBy(m => m.LastName).ThenBy(m => m.Name).ToList();
        var ordered = currentMembers.Concat(pastMembers).ToList();

        for (int rowIdx = 0; rowIdx < ordered.Count; rowIdx++)
        {
            var row = RowFirstPerson + rowIdx;
            var m = ordered[rowIdx];

            // ID hint: "p:123" or "u:5"
            ws.Cell(row, ColIdHint).Value = m.PersonId.HasValue ? $"p:{m.PersonId}" : $"u:{m.UserId}";

            var fullName = string.IsNullOrWhiteSpace(m.LastName) ? m.Name : $"{m.LastName} {m.Name}";
            ws.Cell(row, ColFullName).Value = fullName;
            ws.Cell(row, ColStatus).Value = m.Status ?? "";
            ws.Cell(row, ColOversight).Value = m.Oversight ?? "";

            ws.Cell(row, ColJoinedAt).Value = m.JoinedAt.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, ColJoinedAt).Style.NumberFormat.Format = "dd.MM.yyyy";

            // Past members: highlight row red
            if (m.LeftAt is not null)
                ws.Range(row, ColPercent, row, ColJoinedAt).Style.Fill.BackgroundColor = XLColor.FromHtml("#FECACA");

            // Attendance percentage
            if (!isTemplate)
            {
                attendanceMap.TryGetValue(m.Key, out var attMap);
                attMap ??= new Dictionary<DateOnly, bool>();

                var eligible = attMap.Where(kv => !cancelledFromCal.Contains(kv.Key)).ToList();
                if (eligible.Count > 0)
                {
                    var rate = (double)eligible.Count(kv => kv.Value) / eligible.Count;
                    ws.Cell(row, ColPercent).Value = rate;
                    ws.Cell(row, ColPercent).Style.NumberFormat.Format = "0%";
                }

                for (int i = 0; i < dates.Count; i++)
                {
                    var col = ColFirstDate + i;
                    var date = dates[i];

                    // Don't fill cells before person's JoinedAt or after LeftAt
                    if (date < m.JoinedAt) continue;
                    if (m.LeftAt is not null && date > m.LeftAt.Value) continue;

                    if (cancelledFromCal.Contains(date) || (metaByDate.TryGetValue(date, out var mt) && mt.IsCancelled))
                    {
                        ws.Cell(row, col).Value = "-";
                        continue;
                    }

                    if (attMap.TryGetValue(date, out var was))
                    {
                        ws.Cell(row, col).Value = was ? 1 : 0;
                        ws.Cell(row, col).Style.Fill.BackgroundColor =
                            was ? XLColor.FromHtml("#BBF7D0") : XLColor.FromHtml("#FECACA");
                    }
                }
            }
        }

        // Column sizing + hide ID
        ws.Column(ColPercent).Width = 6;
        ws.Column(ColIdHint).Hide();
        ws.Column(ColFullName).Width = 25;
        ws.Column(ColStatus).Width = 30;
        ws.Column(ColOversight).Width = 12;
        ws.Column(ColJoinedAt).Width = 14;
        for (int i = 0; i < dates.Count; i++)
            ws.Column(ColFirstDate + i).Width = 11;

        ws.SheetView.FreezeRows(RowHeader);
        ws.SheetView.FreezeColumns(ColFullName);
    }

    private async Task<List<DateOnly>> CollectDates(long groupId, DateOnly? from, DateOnly? to)
    {
        var fromAttendance = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .Select(a => a.MeetingDate)
            .Distinct()
            .ToListAsync();

        var fromMeta = await db.AttendanceMetas
            .Where(m => m.HomeGroupId == groupId)
            .Select(m => m.MeetingDate)
            .ToListAsync();

        var fromCalendar = await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.Date != null
                && !(e.IsHomeGroupMeeting == false && e.MovedToDate != null))
            .Select(e => e.Date!.Value)
            .ToListAsync();

        var movedOut = (await db.CalendarEvents
            .Where(e => e.HomeGroupId == groupId
                && e.Type == CalendarEventType.HomeGroup
                && !e.IsRecurring
                && e.IsHomeGroupMeeting == false
                && e.MovedToDate != null
                && e.Date != null)
            .Select(e => e.Date!.Value)
            .ToListAsync()).ToHashSet();

        return fromAttendance
            .Union(fromMeta)
            .Union(fromCalendar)
            .Where(d => !movedOut.Contains(d))
            .Where(d => (from == null || d >= from) && (to == null || d <= to))
            .OrderBy(d => d)
            .ToList();
    }

    private record MemberInfo(
        string Key,
        long? PersonId,
        long? UserId,
        string Name,
        string? LastName,
        string? Status,
        string? Oversight,
        DateOnly JoinedAt,
        DateOnly? LeftAt);

    private async Task<(List<MemberInfo> Members, Dictionary<string, Dictionary<DateOnly, bool>> AttendanceMap)> CollectMembers(long groupId)
    {
        var personMembers = await db.HomeGroupMembers
            .Where(m => m.HomeGroupId == groupId)
            .Include(m => m.Person).ThenInclude(p => p.PersonStatus)
            .Include(m => m.Person).ThenInclude(p => p.OversightUser)
            .ToListAsync();

        var userMembers = await db.UserHomeGroups
            .Where(u => u.HomeGroupId == groupId && u.UserId != 0)
            .Include(u => u.User).ThenInclude(u => u.PersonStatus)
            .ToListAsync();

        var currentPersonIds = personMembers.Select(p => p.PersonId).ToHashSet();
        var currentUserIds = userMembers.Select(u => u.UserId).ToHashSet();

        var histories = await db.GroupMemberHistories
            .Where(h => h.HomeGroupId == groupId && h.LeftAt != null)
            .Include(h => h.Person).ThenInclude(p => p!.PersonStatus)
            .Include(h => h.Person).ThenInclude(p => p!.OversightUser)
            .Include(h => h.User).ThenInclude(u => u!.PersonStatus)
            .ToListAsync();

        var pastMembers = histories
            .Where(h => (h.PersonId == null || !currentPersonIds.Contains(h.PersonId.Value))
                     && (h.UserId == null || !currentUserIds.Contains(h.UserId.Value)))
            .GroupBy(h => h.PersonId.HasValue ? $"p:{h.PersonId}" : $"u:{h.UserId}")
            .Select(g => g.OrderByDescending(h => h.JoinedAt).First())
            .ToList();

        var members = new List<MemberInfo>();
        foreach (var pm in personMembers)
        {
            members.Add(new MemberInfo(
                $"p:{pm.PersonId}", pm.PersonId, null,
                pm.Person.Name, pm.Person.LastName,
                pm.Person.PersonStatus?.Name,
                pm.Person.OversightUser?.Name ?? pm.Person.OversightInfo,
                DateOnly.FromDateTime(pm.JoinedAt), null));
        }
        foreach (var um in userMembers)
        {
            members.Add(new MemberInfo(
                $"u:{um.UserId}", null, um.UserId,
                um.User.Name, um.User.LastName,
                um.User.PersonStatus?.Name,
                null,
                DateOnly.FromDateTime(um.AssignedAt), null));
        }
        foreach (var h in pastMembers)
        {
            if (h.PersonId.HasValue && h.Person != null)
            {
                members.Add(new MemberInfo(
                    $"p:{h.PersonId}", h.PersonId, null,
                    h.Person.Name, h.Person.LastName,
                    h.Person.PersonStatus?.Name,
                    h.Person.OversightUser?.Name ?? h.Person.OversightInfo,
                    DateOnly.FromDateTime(h.JoinedAt),
                    DateOnly.FromDateTime(h.LeftAt!.Value)));
            }
            else if (h.UserId.HasValue && h.User != null)
            {
                members.Add(new MemberInfo(
                    $"u:{h.UserId}", null, h.UserId,
                    h.User.Name, h.User.LastName,
                    h.User.PersonStatus?.Name,
                    null,
                    DateOnly.FromDateTime(h.JoinedAt),
                    DateOnly.FromDateTime(h.LeftAt!.Value)));
            }
        }

        // Attendance map per member
        var allAttendance = await db.Attendances
            .Where(a => a.HomeGroupId == groupId)
            .ToListAsync();

        var attMap = new Dictionary<string, Dictionary<DateOnly, bool>>();
        foreach (var a in allAttendance)
        {
            var key = a.PersonId.HasValue ? $"p:{a.PersonId}" : $"u:{a.UserId}";
            if (!attMap.TryGetValue(key, out var dict))
            {
                dict = new Dictionary<DateOnly, bool>();
                attMap[key] = dict;
            }
            dict[a.MeetingDate] = a.WasPresent;
        }

        return (members, attMap);
    }

    private static string SanitizeSheetName(string raw, XLWorkbook wb)
    {
        var name = string.Concat(raw.Where(c => !"\\/?*[]:".Contains(c)));
        if (string.IsNullOrWhiteSpace(name)) name = "Group";
        if (name.Length > 31) name = name[..31];

        var baseName = name;
        var i = 2;
        while (wb.Worksheets.Any(w => w.Name == name))
        {
            var suffix = $" ({i})";
            name = baseName.Length + suffix.Length <= 31 ? baseName + suffix : baseName[..(31 - suffix.Length)] + suffix;
            i++;
        }
        return name;
    }

    // ============================================================
    // PARSE
    // ============================================================

    public record ParsedSheet(
        int Index,
        string Name,
        List<string?> Dates,              // ISO yyyy-MM-dd (nullable for blank columns)
        List<int> DateColumns,            // absolute column numbers
        Dictionary<string, int> GuestsByDate,
        Dictionary<string, int> OtherGroupsByDate,
        Dictionary<string, string?> NotesByDate,
        Dictionary<string, bool> CancelledByDate,
        List<ParsedPerson> People);

    public record ParsedPerson(
        int RowIndex,
        string? IdHint,
        string FullNameRaw,
        string Name,
        string? LastName,
        string? Status,
        string? Oversight,
        string? JoinedAt,
        Dictionary<string, ParsedCell> Cells);

    public record ParsedCell(string RawValue, bool? AsBool, bool IsCancelMark);

    public static IEnumerable<(string Key, DateOnly Date)> WithDates(IEnumerable<string?> dates) =>
        dates.Where(d => d != null).Select(d => (d!, DateOnly.Parse(d!)));

    public List<ParsedSheet> Parse(Stream xlsx)
    {
        using var wb = new XLWorkbook(xlsx);
        var result = new List<ParsedSheet>();

        int sheetIdx = 0;
        foreach (var ws in wb.Worksheets)
        {
            try
            {
                var parsed = ParseSheet(ws, sheetIdx);
                if (parsed is not null) result.Add(parsed);
            }
            catch
            {
                // Skip unparseable sheets silently — frontend shows "0 parsed sheets"
            }
            sheetIdx++;
        }

        return result;
    }

    private static ParsedSheet? ParseSheet(IXLWorksheet ws, int sheetIdx)
    {
        // Find header row by looking for "Прізвище" / "Ім'я" in column C or thereabouts
        var headerRow = FindHeaderRow(ws);
        if (headerRow == 0) return null;

        var (colName, colId, colStatus, colOversight, colJoinedAt, firstDateCol) =
            DetectColumnLayout(ws, headerRow);

        if (firstDateCol == 0) return null;

        var (dateRow, dates, dateCols) = ExtractDates(ws, firstDateCol);
        if (dates.Count == 0) return null;

        // Meta rows — search labels between row 1 and headerRow
        var guestsRow = FindLabelRow(ws, dateRow + 1, headerRow - 1,
            colName, new[] { "Нові", "невіруючі", "гості" });
        var otherGroupsRow = FindLabelRow(ws, dateRow + 1, headerRow - 1,
            colName, new[] { "інших домашок" });
        var notesRow = FindLabelRow(ws, dateRow + 1, headerRow - 1,
            colName, new[] { "Нотатки", "Темка", "Тема" });

        var guestsByDate = new Dictionary<string, int>();
        var otherByDate = new Dictionary<string, int>();
        var notesByDate = new Dictionary<string, string?>();
        var cancelledByDate = new Dictionary<string, bool>();
        var datesIso = dates.Select(d => d?.ToString("yyyy-MM-dd")).ToList();

        for (int i = 0; i < dates.Count; i++)
        {
            var iso = datesIso[i];
            if (iso is null) continue;
            var col = dateCols[i];

            if (guestsRow > 0)
            {
                var v = ws.Cell(guestsRow, col).GetValue<string>();
                if (int.TryParse(v.Trim(), out var n) && n > 0)
                    guestsByDate[iso] = n;
            }
            if (otherGroupsRow > 0)
            {
                var v = ws.Cell(otherGroupsRow, col).GetValue<string>();
                if (int.TryParse(v.Trim(), out var n) && n > 0)
                    otherByDate[iso] = n;
            }
            if (notesRow > 0)
            {
                var v = ws.Cell(notesRow, col).GetValue<string>();
                if (!string.IsNullOrWhiteSpace(v))
                    notesByDate[iso] = v.Trim();
            }
        }

        // People rows
        var people = new List<ParsedPerson>();
        var lastUsed = ws.LastRowUsed()?.RowNumber() ?? headerRow;
        for (int r = headerRow + 1; r <= lastUsed; r++)
        {
            var nameRaw = ws.Cell(r, colName).GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(nameRaw)) continue;

            // Skip section header rows (no values at all) — heuristic
            var (name, lastName) = SplitFullName(nameRaw);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var idHint = colId > 0 ? ws.Cell(r, colId).GetValue<string>()?.Trim() : null;
            var status = colStatus > 0 ? ws.Cell(r, colStatus).GetValue<string>()?.Trim() : null;
            var oversight = colOversight > 0 ? ws.Cell(r, colOversight).GetValue<string>()?.Trim() : null;

            DateOnly? joinedAt = null;
            if (colJoinedAt > 0)
            {
                var cell = ws.Cell(r, colJoinedAt);
                joinedAt = ParseDateCell(cell);
            }

            var cells = new Dictionary<string, ParsedCell>();
            for (int i = 0; i < dates.Count; i++)
            {
                var iso = datesIso[i];
                if (iso is null) continue;
                var col = dateCols[i];
                var raw = ws.Cell(r, col).GetValue<string>()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (raw == "-" || raw == "—" || raw == "–")
                {
                    cells[iso] = new ParsedCell(raw, null, true);
                    cancelledByDate[iso] = true;
                    continue;
                }

                bool? asBool = raw.ToLowerInvariant() switch
                {
                    "1" or "+" or "✓" or "так" or "yes" or "true" => true,
                    "0" or "-" or "✗" or "ні" or "no" or "false" => false,
                    _ => null,
                };

                cells[iso] = new ParsedCell(raw, asBool, false);
            }

            people.Add(new ParsedPerson(
                r, idHint, nameRaw, name, lastName,
                string.IsNullOrWhiteSpace(status) ? null : status,
                string.IsNullOrWhiteSpace(oversight) ? null : oversight,
                joinedAt?.ToString("yyyy-MM-dd"), cells));
        }

        return new ParsedSheet(
            sheetIdx, ws.Name, datesIso, dateCols,
            guestsByDate, otherByDate, notesByDate, cancelledByDate, people);
    }

    private static int FindHeaderRow(IXLWorksheet ws)
    {
        // Look in first 20 rows for a cell containing "Прізвище" or "Ім'я та прізвище" or "ПІБ"
        var lastRow = Math.Min(20, ws.LastRowUsed()?.RowNumber() ?? 20);
        for (int r = 1; r <= lastRow; r++)
        {
            for (int c = 1; c <= 10; c++)
            {
                var v = ws.Cell(r, c).GetValue<string>()?.Trim() ?? "";
                if (v.Contains("Прізвище", StringComparison.OrdinalIgnoreCase)
                    || v.Contains("ПІБ", StringComparison.OrdinalIgnoreCase)
                    || (v.Contains("Ім", StringComparison.OrdinalIgnoreCase) && v.Contains("прізвищ", StringComparison.OrdinalIgnoreCase)))
                {
                    return r;
                }
            }
        }
        return 0;
    }

    private static (int Name, int Id, int Status, int Oversight, int JoinedAt, int FirstDate)
        DetectColumnLayout(IXLWorksheet ws, int headerRow)
    {
        int nameCol = 0, idCol = 0, statusCol = 0, oversightCol = 0, joinedCol = 0, firstDateCol = 0;

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 50;
        for (int c = 1; c <= lastCol; c++)
        {
            var v = ws.Cell(headerRow, c).GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrEmpty(v)) continue;

            if (nameCol == 0 && (v.Contains("Прізвище", StringComparison.OrdinalIgnoreCase)
                || v.Contains("ПІБ", StringComparison.OrdinalIgnoreCase)
                || (v.Contains("Ім", StringComparison.OrdinalIgnoreCase) && v.Contains("прізвищ", StringComparison.OrdinalIgnoreCase))))
                nameCol = c;
            else if (idCol == 0 && v.Equals("ID", StringComparison.OrdinalIgnoreCase))
                idCol = c;
            else if (statusCol == 0 && v.Equals("Статус", StringComparison.OrdinalIgnoreCase))
                statusCol = c;
            else if (oversightCol == 0 && v.StartsWith("Опіка", StringComparison.OrdinalIgnoreCase))
                oversightCol = c;
            else if (joinedCol == 0 && v.Contains("приєднан", StringComparison.OrdinalIgnoreCase))
                joinedCol = c;
        }

        // First date column = first column that has a date in row 1
        for (int c = (nameCol > 0 ? nameCol : 1) + 1; c <= lastCol; c++)
        {
            var cell = ws.Cell(1, c);
            if (cell.DataType == XLDataType.DateTime)
            {
                firstDateCol = c;
                break;
            }
            var s = cell.GetValue<string>()?.Trim() ?? "";
            if (TryParseDate(s, out _))
            {
                firstDateCol = c;
                break;
            }
        }

        return (nameCol, idCol, statusCol, oversightCol, joinedCol, firstDateCol);
    }

    private static (int DateRow, List<DateOnly?> Dates, List<int> DateCols) ExtractDates(IXLWorksheet ws, int firstDateCol)
    {
        // Dates usually live in row 1; if row 1 doesn't have them, try row 2 then 3
        int dateRow = 0;
        for (int r = 1; r <= 5; r++)
        {
            var cell = ws.Cell(r, firstDateCol);
            if (cell.DataType == XLDataType.DateTime || TryParseDate(cell.GetValue<string>()?.Trim() ?? "", out _))
            {
                dateRow = r;
                break;
            }
        }
        if (dateRow == 0) return (0, [], []);

        var dates = new List<DateOnly?>();
        var cols = new List<int>();
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? firstDateCol;
        for (int c = firstDateCol; c <= lastCol; c++)
        {
            var cell = ws.Cell(dateRow, c);
            DateOnly? d = null;
            if (cell.DataType == XLDataType.DateTime)
                d = DateOnly.FromDateTime(cell.GetValue<DateTime>());
            else
            {
                var s = cell.GetValue<string>()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(s) && TryParseDate(s, out var parsed))
                    d = parsed;
            }
            // Skip empty trailing columns
            if (d is null && c > firstDateCol)
            {
                var allEmpty = true;
                for (int rr = dateRow; rr <= Math.Min(dateRow + 10, ws.LastRowUsed()?.RowNumber() ?? dateRow); rr++)
                {
                    if (!string.IsNullOrWhiteSpace(ws.Cell(rr, c).GetValue<string>())) { allEmpty = false; break; }
                }
                if (allEmpty) break;
            }
            dates.Add(d);
            cols.Add(c);
        }

        return (dateRow, dates, cols);
    }

    private static int FindLabelRow(IXLWorksheet ws, int minRow, int maxRow, int labelCol, string[] keywords)
    {
        if (minRow < 1) minRow = 1;
        if (maxRow < minRow) return 0;
        for (int r = minRow; r <= maxRow; r++)
        {
            // Check label col first, then a few preceding columns
            for (int c = Math.Max(1, labelCol - 5); c <= labelCol + 1; c++)
            {
                var v = ws.Cell(r, c).GetValue<string>()?.Trim() ?? "";
                if (string.IsNullOrEmpty(v)) continue;
                if (keywords.All(k => v.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return r;
                if (keywords.Length == 1 && v.Contains(keywords[0], StringComparison.OrdinalIgnoreCase))
                    return r;
            }
        }
        return 0;
    }

    public static bool TryParseDate(string s, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return DateOnly.TryParseExact(s, DateFormats, null, System.Globalization.DateTimeStyles.None, out result)
            || DateOnly.TryParse(s, out result);
    }

    private static DateOnly? ParseDateCell(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime)
            return DateOnly.FromDateTime(cell.GetValue<DateTime>());
        var s = cell.GetValue<string>()?.Trim() ?? "";
        return TryParseDate(s, out var d) ? d : null;
    }

    public static (string Name, string? LastName) SplitFullName(string raw)
    {
        var parts = raw.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return ("", null);
        if (parts.Length == 1) return (parts[0], null);
        // Heuristic: if first part starts with uppercase and is longer than second part, it's likely a last name (UA "Прізвище Ім'я")
        // But our system uses "Name LastName" — most exports will show "Last First". Treat first part as LastName.
        return (parts[1], parts[0]);
    }
}
