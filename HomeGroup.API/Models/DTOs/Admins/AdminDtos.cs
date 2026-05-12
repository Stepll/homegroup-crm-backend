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
    DateTime CreatedAt);

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

public record SetPasswordRequest(string NewPassword);
