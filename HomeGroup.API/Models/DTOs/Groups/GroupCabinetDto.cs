namespace HomeGroup.API.Models.DTOs.Groups;

public record GroupCabinetResponse(
    CabinetGroupInfo Group,
    string? NextMeetingDate,
    string? LastMeetingDate,
    CabinetAttendanceSummary? LastAttendance,
    List<CabinetUpcomingEvent> UpcomingEvents,
    List<CabinetOrgMember> OrgTeam,
    CabinetStats Stats,
    bool HasPlanForNextMeeting = false);

public record CabinetGroupInfo(
    long Id, string Name, string Color,
    string? MeetingDay, string? MeetingTime, string? Location,
    string? TelegramGroupId = null);

public record CabinetAttendanceSummary(int Present, int Total);

public record CabinetUpcomingEvent(
    long PersonId, string FullName, string DateOfBirth, int DaysUntil);

public record CabinetOrgMember(
    long Id, string Name, string? LastName, string Email,
    int OverseeCount, List<CabinetOverseePerson> Oversees,
    CabinetRoleTag? Role = null);

public record CabinetOverseePerson(long Id, string FullName);

public record CabinetRoleTag(string Name, string Color);

public record CabinetStats(double AvgAttendanceRate, int NewMembersThisMonth, int TotalMembers);

public record GroupEventDto(long Id, string Name, int Month, int Day, int? Year, int DaysUntil);
public record CreateGroupEventRequest(string Name, int Month, int Day, int? Year);
