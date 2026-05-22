namespace HomeGroup.API.Models.Entities;

public class UserActivity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string Type { get; set; } = "status_change"; // "status_change"
    public long? AuthorId { get; set; }
    public User? Author { get; set; }
    public long? OldStatusId { get; set; }
    public string? OldStatusName { get; set; }
    public string? OldStatusColor { get; set; }
    public long? NewStatusId { get; set; }
    public string? NewStatusName { get; set; }
    public string? NewStatusColor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
