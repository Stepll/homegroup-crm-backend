using HomeGroup.API.Models.DTOs.PersonStatuses;

namespace HomeGroup.API.Models.DTOs.People;

public record CreatePersonRequest(string Name, string? LastName, long? PrimaryGroupId);

public record UpdatePersonRequest(
    string Name,
    string? LastName,
    string? Phone,
    string? Email,
    string? Telegram,
    string? Notes,
    string? Gender,
    string? MaritalStatus,
    string? Address,
    DateOnly? DateOfBirth,
    bool IsBaptized,
    string? Church,
    string? Ministry,
    bool IsBaptizedWithSpirit,
    long? PersonStatusId,
    string? OversightInfo,
    long? OversightUserId,
    long? PrimaryGroupId);

public record PersonResponse(long Id, string Name, string? LastName, string? Phone, string? Email, string? Notes, PersonStatusDto? Status, long? PrimaryGroupId, string? PrimaryGroupName, string? PrimaryGroupColor, DateTime CreatedAt);

public record PersonDetailResponse(
    long Id,
    string Name,
    string? LastName,
    string? Phone,
    string? Email,
    string? Telegram,
    string? Notes,
    string? Gender,
    string? MaritalStatus,
    string? Address,
    DateOnly? DateOfBirth,
    bool IsBaptized,
    string? Church,
    string? Ministry,
    bool IsBaptizedWithSpirit,
    PersonStatusDto? Status,
    string? OversightInfo,
    long? OversightUserId,
    string? OversightUserName,
    long? PrimaryGroupId,
    string? PrimaryGroupName,
    DateTime CreatedAt,
    List<CustomFieldDto> CustomFields);

public record CustomFieldDto(long Id, string Name, string? Value);

public record CreateCustomFieldRequest(string Name);

public record UpdateCustomFieldRequest(string? Value);
