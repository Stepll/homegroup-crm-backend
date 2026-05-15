namespace HomeGroup.API.Models.DTOs.Auth;

public record RegisterRequest(string Name, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Name, string Email, string Role, List<string> Roles, long? PrimaryGroupId, List<string> Permissions);
