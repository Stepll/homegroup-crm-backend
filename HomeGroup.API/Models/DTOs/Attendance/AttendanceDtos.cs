namespace HomeGroup.API.Models.DTOs.Attendance;

public record RecordAttendanceRequest(long HomeGroupId, DateOnly MeetingDate, List<AttendanceEntry> Entries);

public record AttendanceEntry(long PersonId, bool WasPresent, string? Notes);

public record AttendanceResponse(long Id, long PersonId, string PersonName, long HomeGroupId, DateOnly MeetingDate, bool WasPresent, string? Notes);

public record AttendanceSummary(DateOnly MeetingDate, int TotalMembers, int PresentCount, double AttendanceRate);
