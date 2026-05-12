using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Auth;
using HomeGroup.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Невірний email або пароль" });

        var primaryRole = user.UserRoles.OrderBy(ur => ur.Role.Name).FirstOrDefault()?.Role;

        return Ok(new AuthResponse(jwt.GenerateToken(user), user.Name, user.Email, primaryRole?.Name ?? string.Empty));
    }
}
