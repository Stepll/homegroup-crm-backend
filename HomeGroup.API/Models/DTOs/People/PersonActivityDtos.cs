using HomeGroup.API.Models.DTOs.PersonStatuses;

namespace HomeGroup.API.Models.DTOs.People;

public record PersonActivityDto(
    long Id,
    string Type,
    string? Content,
    long? AuthorId,
    string? AuthorName,
    PersonStatusDto? OldStatus,
    PersonStatusDto? NewStatus,
    string? OldValue,
    string? NewValue,
    DateTime CreatedAt);

public record AddPersonCommentRequest(string Content);
