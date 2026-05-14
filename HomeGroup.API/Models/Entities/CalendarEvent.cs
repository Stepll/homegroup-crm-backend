namespace HomeGroup.API.Models.Entities;

public enum CalendarEventType
{
    Recurring = 0,
    Global = 1,
    HomeGroup = 2
}

public class CalendarEvent
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public long? RoomId { get; set; }
    public Room? Room { get; set; }
    public CalendarEventType Type { get; set; }
    public long? HomeGroupId { get; set; }
    public HomeGroupEntity? HomeGroup { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurringDayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public DateOnly? Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
