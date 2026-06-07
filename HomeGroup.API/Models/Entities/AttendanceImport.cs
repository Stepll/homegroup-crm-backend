namespace HomeGroup.API.Models.Entities;

public class AttendanceImport
{
    public long Id { get; set; }
    public long? CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
