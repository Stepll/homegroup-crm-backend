namespace HomeGroup.API.Models.DTOs.Groups;

public record GroupStatsResponse(
    StatsSummary Summary,
    List<MeetingStatsItem> Meetings,
    List<PersonAttendanceStat> PersonStats);

public record StatsSummary(
    double AvgAttendanceRate,
    int MeetingCount,
    int TotalGuests,
    int NewMembers);

public record MeetingStatsItem(
    string Date,
    int PresentCount,
    int TotalMembers,
    double AttendanceRate,
    int GuestCount,
    List<string> Absentees);

public record PersonAttendanceStat(
    long PersonId,
    string FullName,
    int PresentCount,
    int TotalMeetings,
    double AttendanceRate);
