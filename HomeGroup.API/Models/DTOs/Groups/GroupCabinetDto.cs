namespace HomeGroup.API.Models.DTOs.Groups;

public record GroupCabinetResponse(
    CabinetGroupInfo Group,
    string? NextMeetingDate,
    string? LastMeetingDate,
    CabinetAttendanceSummary? LastAttendance,
    List<CabinetUpcomingEvent> UpcomingEvents,
    List<CabinetOrgMember> OrgTeam,
    CabinetStats Stats,
    bool HasPlanForNextMeeting = false,
    long? NextMeetingRoomId = null,
    List<CabinetCalendarEvent>? NextMeetingEvents = null,
    List<CabinetCalendarEvent>? NextMeetingConflicts = null,
    bool AutoBookEnabled = false,
    string? PrevScheduledMeetingDate = null);

public record CabinetGroupInfo(
    long Id, string Name, string Color,
    string? MeetingDay, string? MeetingTime, string? Location,
    string? TelegramGroupId = null,
    string? MeetingEndTime = null,
    long? AutoBookRoomId = null);

public record CabinetCalendarEvent(
    long EventId,
    string Title,
    string Type,
    string? StartTime,
    string? EndTime,
    long? RoomId,
    string? RoomColor,
    string? HomeGroupColor);

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
