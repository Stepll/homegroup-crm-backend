namespace HomeGroup.API.Models.Entities;

public class AnonymousPoll
{
    public long Id { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity? HomeGroup { get; set; }
    public long? StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public long DestinationChatId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
