namespace HomeGroup.API.Models.DTOs.Calendar;

public record RoomDto(long Id, string Name, string Building, int Floor, string Color);

public record CalendarEventDto(
    long Id,
    string Title,
    string? Description,
    string? Location,
    long? RoomId,
    RoomDto? Room,
    string Type,
    long? HomeGroupId,
    string? HomeGroupName,
    string? HomeGroupColor,
    bool IsRecurring,
    int? RecurringDayOfWeek,
    string? StartTime,
    string? EndTime,
    string? Date);

public record CalendarOccurrenceDto(
    long EventId,
    string Title,
    string? Description,
    string? Location,
    long? RoomId,
    RoomDto? Room,
    string Type,
    long? HomeGroupId,
    string? HomeGroupName,
    string? HomeGroupColor,
    string Date,
    string? StartTime,
    string? EndTime);

public record CreateCalendarEventRequest(
    string Title,
    string? Description,
    string? Location,
    long? RoomId,
    string Type,
    long? HomeGroupId,
    bool IsRecurring,
    int? RecurringDayOfWeek,
    string? StartTime,
    string? EndTime,
    string? Date);

public record UpdateCalendarEventRequest(
    string Title,
    string? Description,
    string? Location,
    long? RoomId,
    string Type,
    long? HomeGroupId,
    bool IsRecurring,
    int? RecurringDayOfWeek,
    string? StartTime,
    string? EndTime,
    string? Date);

public record RoomRequest(string Name, string Building, int Floor, string Color);
