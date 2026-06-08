namespace HomeGroup.API.Models.DTOs.Dashboard;

public record InactiveMemberDto(
    long? PersonId,
    long? UserId,
    string FullName,
    long? GroupId,
    string? GroupName,
    string? GroupColor,
    int MissedCount,
    string? LastAttendedDate);

public record StatusDistributionItem(
    long? StatusId,
    string Name,
    string Color,
    int Count);

public record StatusDistributionResponse(
    int TotalPeople,
    List<StatusDistributionItem> Items);

public record GroupComparisonPoint(string Date, double AttendanceRate);

public record GroupComparisonSeries(
    long GroupId,
    string GroupName,
    string GroupColor,
    List<GroupComparisonPoint> Points);

public record GroupAttendanceSummaryRow(
    long GroupId,
    string GroupName,
    string GroupColor,
    int TotalMembers,
    double Avg1m,
    double Avg3m,
    double Avg6m);

public record GroupsAttendanceSummaryResponse(
    List<GroupAttendanceSummaryRow> Groups,
    int TotalMembers,
    double Avg1m,
    double Avg3m,
    double Avg6m);
