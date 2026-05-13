namespace HomeGroup.API.Models.Entities;

public class HomeMeetingPlan
{
    public long Id { get; set; }
    public long HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public string MeetingDate { get; set; } = string.Empty; // "YYYY-MM-DD"
    public string? AppliedTemplateName { get; set; }
    public List<MeetingPlanBlock> Blocks { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
