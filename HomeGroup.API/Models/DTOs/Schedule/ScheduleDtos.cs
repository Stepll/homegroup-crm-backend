namespace HomeGroup.API.Models.DTOs.Schedule;

public record ScheduleWeekDto(
    string WeekStart,          // "yyyy-MM-dd" — Monday
    string DefaultDate,         // calendar default meeting date this week (based on MeetingDay)
    string? EffectiveDate,      // actual meeting date this week (null = cancelled without replacement)
    string Status,              // "default" | "cancelled" | "rescheduled_internal" | "moved_in" | "moved_out"
    string? MovedFromDate,      // source date if status = moved_in
    string? MovedToDate,        // destination date if status = moved_out
    bool HasPlan,
    int AttendanceRecordCount   // how many attendance entries exist on EffectiveDate (for warning)
);

public record ScheduleCancelRequest(string Date);

public record ScheduleMoveRequest(string FromDate, string ToDate, bool MovePlan, bool MoveAttendance);

public record ScheduleResetRequest(string WeekStart, bool RestorePlan);
