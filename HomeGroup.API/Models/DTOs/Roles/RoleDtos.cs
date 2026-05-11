using System.Text.Json;

namespace HomeGroup.API.Models.DTOs.Roles;

public record RoleResponse(
    long Id,
    string Name,
    string? Description,
    string Color,
    List<string> Permissions,
    bool IsSystem,
    bool IsDefault,
    int UserCount,
    DateTime CreatedAt);

public record CreateRoleRequest(
    string Name,
    string? Description,
    string Color,
    List<string> Permissions,
    bool IsDefault);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    string Color,
    List<string> Permissions,
    bool IsDefault);

public static class RoleExtensions
{
    private static readonly JsonSerializerOptions Opts = new();

    public static List<string> GetPermissions(this HomeGroup.API.Models.Entities.Role role) =>
        JsonSerializer.Deserialize<List<string>>(role.PermissionsJson, Opts) ?? [];

    public static void SetPermissions(this HomeGroup.API.Models.Entities.Role role, List<string> permissions) =>
        role.PermissionsJson = JsonSerializer.Serialize(permissions, Opts);
}
