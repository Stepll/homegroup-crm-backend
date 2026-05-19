namespace HomeGroup.API.Models.Entities;

public class AdminTask
{
    public long Id { get; set; }
    public long TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }

    public long? CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
