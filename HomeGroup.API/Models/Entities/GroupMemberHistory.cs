namespace HomeGroup.API.Models.Entities;

public class GroupMemberHistory
{
    public long Id { get; set; }
    public long? PersonId { get; set; }
    public Person? Person { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
