namespace HomeGroup.API.Models.DTOs.Attendance;

public record RecordAttendanceRequest(long HomeGroupId, DateOnly MeetingDate, List<AttendanceEntry> Entries);

public record AttendanceEntry(long? PersonId, long? UserId, bool WasPresent, string? Notes);

public record AttendanceResponse(long Id, long? PersonId, long? UserId, string MemberName, long HomeGroupId, DateOnly MeetingDate, bool WasPresent, string? Notes);

public record AttendanceSummary(DateOnly MeetingDate, int TotalMembers, int PresentCount, double AttendanceRate);

public record AttendanceMetaResponse(int GuestCount, string? GuestInfo, string? Notes, bool IsCancelled);

public record SaveAttendanceMetaRequest(long HomeGroupId, DateOnly MeetingDate, int GuestCount, string? GuestInfo, string? Notes, bool IsCancelled);

// Attendance table (GET /groups/:id/attendance-table)

public record AttendanceTableResponse(
    List<string> Dates,
    List<AttendanceTableMeeting> Meetings,
    List<AttendanceTableMember> Members);

public record AttendanceTableMeeting(
    string Date,
    int GuestCount,
    string? GuestInfo,
    string? Notes,
    bool IsCancelled,
    bool IsInDb);

public record AttendanceTableMember(
    long? PersonId,
    long? UserId,
    string Name,
    string? LastName,
    string JoinedAt,
    double AttendanceRate,
    Dictionary<string, bool> Attendance);

public record BulkAttendanceEntry(string Date, long? PersonId, long? UserId, bool WasPresent);

public record BulkRecordAttendanceRequest(long HomeGroupId, List<BulkAttendanceEntry> Entries);

public record DeleteMeetingRequest(long HomeGroupId, DateOnly MeetingDate);

public record AttendanceDotRecord(long? PersonId, long? UserId, string Date, bool WasPresent);
public record AttendanceDotsResponse(string[] Dates, string[] CancelledDates, AttendanceDotRecord[] Records);
