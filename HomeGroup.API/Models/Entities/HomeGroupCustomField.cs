namespace HomeGroup.API.Models.Entities;

public class HomeGroupCustomField
{
    public long Id { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PersonCustomFieldValue> Values { get; set; } = [];
}
