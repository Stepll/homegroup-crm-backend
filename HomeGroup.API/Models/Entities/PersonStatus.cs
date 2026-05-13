namespace HomeGroup.API.Models.Entities;

public class PersonStatus
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366F1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
