namespace HomeGroup.API.Models.Entities;

public class HomeGroupEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#64748B";
    public string? MeetingDay { get; set; }
    public string? MeetingTime { get; set; }
    public string? Location { get; set; }
    public long? LeaderId { get; set; }
    public Person? Leader { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HomeGroupMember> Members { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<UserHomeGroup> UserHomeGroups { get; set; } = [];
}
