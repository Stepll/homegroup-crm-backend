namespace HomeGroup.API.Models.Entities;

public class GroupEvent
{
    public long Id { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Day { get; set; }
    public int? Year { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
