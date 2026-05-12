namespace HomeGroup.API.Models.DTOs.Groups;

public record GroupCabinetResponse(
    CabinetGroupInfo Group,
    string? NextMeetingDate,
    string? LastMeetingDate,
    CabinetAttendanceSummary? LastAttendance,
    List<CabinetUpcomingEvent> UpcomingEvents,
    List<CabinetOrgMember> OrgTeam,
    CabinetStats Stats);

public record CabinetGroupInfo(
    long Id, string Name, string Color,
    string? MeetingDay, string? MeetingTime, string? Location);

public record CabinetAttendanceSummary(int Present, int Total);

public record CabinetUpcomingEvent(
    long PersonId, string FullName, string DateOfBirth, int DaysUntil);

public record CabinetOrgMember(
    long Id, string Name, string? LastName, string Email,
    int OverseeCount, List<CabinetOverseePerson> Oversees);

public record CabinetOverseePerson(long Id, string FullName);

public record CabinetStats(double AvgAttendanceRate, int NewMembersThisMonth, int TotalMembers);
