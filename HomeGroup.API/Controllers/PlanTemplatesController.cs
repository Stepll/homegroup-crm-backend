using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Planning;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/plan-templates")]
[Authorize]
public class PlanTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("planning.view")]
    public async Task<ActionResult<List<PlanTemplateDto>>> GetAll()
    {
        var templates = await db.PlanTemplates
            .Include(t => t.Blocks.OrderBy(b => b.Order))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(templates.Select(ToDto));
    }

    [HttpPost]
    [RequirePermission("planning.templates")]
    public async Task<ActionResult<PlanTemplateDto>> Create(CreatePlanTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Назва шаблону обов'язкова" });

        var template = new PlanTemplate
        {
            Name = request.Name.Trim(),
            Blocks = request.Blocks.Select(b => new PlanTemplateBlock
            {
                Order = b.Order,
                Time = b.Time.Trim(),
                Title = b.Title.Trim(),
                Info = b.Info?.Trim(),
                Responsible = b.Responsible?.Trim(),
            }).ToList(),
        };

        db.PlanTemplates.Add(template);
        await db.SaveChangesAsync();
        return Ok(ToDto(template));
    }

    [HttpDelete("{id}")]
    [RequirePermission("planning.templates")]
    public async Task<IActionResult> Delete(long id)
    {
        var template = await db.PlanTemplates.FindAsync(id);
        if (template is null) return NotFound();
        db.PlanTemplates.Remove(template);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static PlanTemplateDto ToDto(PlanTemplate t) => new(
        t.Id, t.Name,
        t.Blocks.Select(b => new TemplateBlockDto(b.Id, b.Order, b.Time, b.Title, b.Info, b.Responsible)).ToList(),
        t.CreatedAt);
}
