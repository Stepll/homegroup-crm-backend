using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HomeGroup.API.Models.DTOs.Roles;
using HomeGroup.API.Models.Entities;
using Microsoft.IdentityModel.Tokens;

namespace HomeGroup.API.Services;

public class JwtService(IConfiguration config)
{
    public const string PermissionClaimType = "permission";

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var roleNames = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList();
        var permissions = GetMergedPermissions(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
        };

        foreach (var roleName in roleNames)
            claims.Add(new Claim(ClaimTypes.Role, roleName));

        foreach (var perm in permissions)
            claims.Add(new Claim(PermissionClaimType, perm));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static List<string> GetMergedPermissions(User user)
    {
        var all = user.UserRoles
            .SelectMany(ur => ur.Role.GetPermissions())
            .Distinct()
            .ToList();
        return all;
    }
}
