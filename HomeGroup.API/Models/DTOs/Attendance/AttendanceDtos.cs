namespace HomeGroup.API.Models.DTOs.Attendance;

public record RecordAttendanceRequest(long HomeGroupId, DateOnly MeetingDate, List<AttendanceEntry> Entries);

public record AttendanceEntry(long? PersonId, long? UserId, bool WasPresent, string? Notes);

public record AttendanceResponse(long Id, long? PersonId, long? UserId, string MemberName, long HomeGroupId, DateOnly MeetingDate, bool WasPresent, string? Notes);

public record AttendanceSummary(DateOnly MeetingDate, int TotalMembers, int PresentCount, double AttendanceRate);

public record AttendanceMetaResponse(int GuestCount, string? GuestInfo);

public record SaveAttendanceMetaRequest(long HomeGroupId, DateOnly MeetingDate, int GuestCount, string? GuestInfo);
