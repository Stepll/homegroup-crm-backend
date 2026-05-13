namespace HomeGroup.API.Models.Entities;

public class PlanTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PlanTemplateBlock> Blocks { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
