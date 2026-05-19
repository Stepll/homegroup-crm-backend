namespace HomeGroup.API.Models.Entities;

public class PersonActivity
{
    public long Id { get; set; }
    public long PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public string Type { get; set; } = "comment"; // "comment" | "status_change" | "oversight_change"

    public string? Content { get; set; }

    public long? AuthorId { get; set; }
    public User? Author { get; set; }

    // Stored inline so history survives status rename/delete
    public long? OldStatusId { get; set; }
    public string? OldStatusName { get; set; }
    public string? OldStatusColor { get; set; }
    public long? NewStatusId { get; set; }
    public string? NewStatusName { get; set; }
    public string? NewStatusColor { get; set; }

    // Generic old/new value for non-status system events (e.g. oversight_change)
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
