namespace HomeGroup.API.Models.Entities;

public class UserCustomFieldValue
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long FieldId { get; set; }
    public HomeGroupCustomField Field { get; set; } = null!;
    public string? Value { get; set; }
}
