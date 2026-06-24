namespace HomeGroup.API.Models.Entities;

public class ChurchServiceRecord
{
    public long Id { get; set; }
    public string ServiceType { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int AttendanceCount { get; set; }
    public int? CommunionCount { get; set; }
    public string? Notes { get; set; }
    public long? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
