namespace HomeGroup.API.Models.Entities;

public class Attendance
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public int HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public DateOnly MeetingDate { get; set; }
    public bool WasPresent { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
