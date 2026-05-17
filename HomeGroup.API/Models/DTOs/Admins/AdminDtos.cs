using HomeGroup.API.Models.DTOs.PersonStatuses;

namespace HomeGroup.API.Models.DTOs.Admins;

public record RoleTagDto(long Id, string Name, string Color);

public record GroupTagDto(long Id, string Name, string Color);

public record AdminResponse(
    long Id,
    string Name,
    string? LastName,
    string Email,
    List<RoleTagDto> Roles,
    long? PrimaryGroupId,
    string? PrimaryGroupName,
    string? PrimaryGroupColor,
    List<GroupTagDto> VisibleGroups,
    DateTime CreatedAt,
    // Profile fields
    string? Phone,
    string? Telegram,
    string? Notes,
    string? Gender,
    string? MaritalStatus,
    string? Address,
    string? DateOfBirth,
    bool IsBaptized,
    string? Church,
    string? Ministry,
    bool IsBaptizedWithSpirit,
    PersonStatusDto? Status);

public record CreateAdminRequest(
    string Name,
    string? LastName,
    string Email,
    string Password,
    List<long> RoleIds,
    long? PrimaryGroupId,
    List<long> VisibleGroupIds);

public record UpdateAdminRequest(
    string Name,
    string? LastName,
    string Email,
    List<long> RoleIds,
    long? PrimaryGroupId,
    List<long> VisibleGroupIds);

public record UpdateAdminProfileRequest(
    string? Phone,
    string? Telegram,
    string? Notes,
    string? Gender,
    string? MaritalStatus,
    string? Address,
    string? DateOfBirth,
    bool IsBaptized,
    string? Church,
    string? Ministry,
    bool IsBaptizedWithSpirit,
    long? PersonStatusId,
    string? Name = null,
    string? LastName = null);

public record SetPasswordRequest(string NewPassword);

public record WidgetConfigItem(string Id, bool Enabled);

public record SaveDashboardConfigRequest(List<WidgetConfigItem> Config);
