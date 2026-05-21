namespace HomeGroup.API.Models.Entities;

public class GroupNeed
{
    public long Id { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public string SubjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "active"; // active | answered | irrelevant
    public long? PersonId { get; set; }
    public Person? Person { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
