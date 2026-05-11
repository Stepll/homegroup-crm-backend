namespace HomeGroup.API.Models.DTOs.Attendance;

public record RecordAttendanceRequest(int HomeGroupId, DateOnly MeetingDate, List<AttendanceEntry> Entries);

public record AttendanceEntry(int PersonId, bool WasPresent, string? Notes);

public record AttendanceResponse(int Id, int PersonId, string PersonName, int HomeGroupId, DateOnly MeetingDate, bool WasPresent, string? Notes);

public record AttendanceSummary(DateOnly MeetingDate, int TotalMembers, int PresentCount, double AttendanceRate);
