namespace HomeGroup.API.Models.Entities;

public class UserActivity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string Type { get; set; } = "status_change"; // "comment" | "status_change" | "oversight_change" | "person_converted"

    public string? Content { get; set; }

    public long? AuthorId { get; set; }
    public User? Author { get; set; }

    public long? OldStatusId { get; set; }
    public string? OldStatusName { get; set; }
    public string? OldStatusColor { get; set; }
    public long? NewStatusId { get; set; }
    public string? NewStatusName { get; set; }
    public string? NewStatusColor { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
