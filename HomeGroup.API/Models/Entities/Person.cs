namespace HomeGroup.API.Models.Entities;

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Active"; // Active, Inactive, Guest
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HomeGroupMember> GroupMemberships { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
}
