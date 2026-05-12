namespace HomeGroup.API.Models.DTOs.People;

public record CreatePersonRequest(string Name, string? LastName, long? PrimaryGroupId);

public record UpdatePersonRequest(
    string Name,
    string? LastName,
    string? Phone,
    string? Email,
    string? Notes,
    string Status,
    string? OversightInfo,
    DateOnly? DateOfBirth,
    long? PrimaryGroupId);

public record PersonResponse(long Id, string Name, string? LastName, string? Phone, string? Email, string? Notes, string Status, long? PrimaryGroupId, string? PrimaryGroupName, DateTime CreatedAt);

public record PersonDetailResponse(
    long Id,
    string Name,
    string? LastName,
    string? Phone,
    string? Email,
    string? Notes,
    string Status,
    string? OversightInfo,
    DateOnly? DateOfBirth,
    long? PrimaryGroupId,
    string? PrimaryGroupName,
    DateTime CreatedAt,
    List<CustomFieldDto> CustomFields);

public record CustomFieldDto(long Id, string Name, string? Value);

public record CreateCustomFieldRequest(string Name);

public record UpdateCustomFieldRequest(string? Value);
