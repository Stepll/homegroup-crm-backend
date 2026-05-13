namespace HomeGroup.API.Models.DTOs.Planning;

// ── Blocks ────────────────────────────────────────────────────────────────────

public record PlanBlockDto(long Id, int Order, string Time, string Title, string? Info, string? Responsible);

// ── Meeting plans ─────────────────────────────────────────────────────────────

public record MeetingPlanDto(
    long Id, long HomeGroupId, string MeetingDate,
    string? AppliedTemplateName, List<PlanBlockDto> Blocks, DateTime UpdatedAt);

public record MeetingPlanSummaryDto(
    long Id, string MeetingDate, int BlockCount, string? AppliedTemplateName);

public record SavePlanRequest(
    string MeetingDate,
    string? AppliedTemplateName,
    List<SavePlanBlockRequest> Blocks);

public record SavePlanBlockRequest(int Order, string Time, string Title, string? Info, string? Responsible);

// ── Templates ─────────────────────────────────────────────────────────────────

public record TemplateBlockDto(long Id, int Order, string Time, string Title, string? Info, string? Responsible);

public record PlanTemplateDto(long Id, string Name, List<TemplateBlockDto> Blocks, DateTime CreatedAt);

public record CreatePlanTemplateRequest(string Name, List<CreateTemplateBlockRequest> Blocks);

public record CreateTemplateBlockRequest(int Order, string Time, string Title, string? Info, string? Responsible);
