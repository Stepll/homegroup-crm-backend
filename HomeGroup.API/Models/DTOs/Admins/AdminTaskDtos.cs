namespace HomeGroup.API.Models.DTOs.Admins;

public record AdminTaskDto(
    long Id,
    string Title,
    string? Description,
    bool IsCompleted,
    long? CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt);

public record CreateAdminTaskRequest(string Title, string? Description);
public record UpdateAdminTaskRequest(string Title, string? Description);
