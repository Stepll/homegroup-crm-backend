namespace HomeGroup.API.Models.Entities;

public class MeetingPlanBlock
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public HomeMeetingPlan Plan { get; set; } = null!;
    public int Order { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Info { get; set; }
    public string? Responsible { get; set; }
}
