namespace HomeGroup.API.Models.Entities;

public class UserHomeGroup
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
