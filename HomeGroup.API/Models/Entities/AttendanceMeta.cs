namespace HomeGroup.API.Models.Entities;

public class AttendanceMeta
{
    public long Id { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public DateOnly MeetingDate { get; set; }
    public int GuestCount { get; set; }
    public string? GuestInfo { get; set; }
}
