namespace HomeGroup.API.Models.Entities;

public class PersonCustomFieldValue
{
    public long Id { get; set; }
    public long PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public long FieldId { get; set; }
    public HomeGroupCustomField Field { get; set; } = null!;
    public string? Value { get; set; }
}
