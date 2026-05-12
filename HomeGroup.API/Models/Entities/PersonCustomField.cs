namespace HomeGroup.API.Models.Entities;

public class PersonCustomField
{
    public long Id { get; set; }
    public long PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
