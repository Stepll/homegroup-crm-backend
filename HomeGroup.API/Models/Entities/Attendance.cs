namespace HomeGroup.API.Models.Entities;

public class Attendance
{
    public long Id { get; set; }
    public long? PersonId { get; set; }
    public Person? Person { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public DateOnly MeetingDate { get; set; }
    public bool WasPresent { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
