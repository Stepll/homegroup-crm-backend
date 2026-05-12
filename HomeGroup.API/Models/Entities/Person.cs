namespace HomeGroup.API.Models.Entities;

public class Person
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Active";
    public string? OversightInfo { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public long? PrimaryGroupId { get; set; }
    public HomeGroupEntity? PrimaryGroup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HomeGroupMember> GroupMemberships { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<PersonCustomField> CustomFields { get; set; } = [];
}
