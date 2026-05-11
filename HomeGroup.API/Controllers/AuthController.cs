using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Auth;
using HomeGroup.API.Models.Entities;
using HomeGroup.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Email вже використовується" });

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(jwt.GenerateToken(user), user.Name, user.Email, user.Role));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Невірний email або пароль" });

        return Ok(new AuthResponse(jwt.GenerateToken(user), user.Name, user.Email, user.Role));
    }
}
