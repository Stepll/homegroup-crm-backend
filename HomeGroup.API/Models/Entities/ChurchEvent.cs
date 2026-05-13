namespace HomeGroup.API.Models.Entities;

public class ChurchEvent
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Day { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
