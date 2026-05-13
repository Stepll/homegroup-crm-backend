using System.Security.Claims;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Admins;
using HomeGroup.API.Models.DTOs.PersonStatuses;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/admins")]
[Authorize]
public class AdminsController(AppDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<AdminResponse>> GetMe()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId)) return Unauthorized();

        var admin = await LoadAdmin(userId);
        if (admin is null) return NotFound();
        return Ok(ToResponse(admin));
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminResponse>>> GetAll()
    {
        var admins = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PrimaryGroup)
            .Include(u => u.UserHomeGroups).ThenInclude(ug => ug.HomeGroup)
            .OrderBy(u => u.Name)
            .ToListAsync();

        return Ok(admins.Select(ToResponse));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminResponse>> GetById(long id)
    {
        var admin = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PrimaryGroup)
            .Include(u => u.UserHomeGroups).ThenInclude(ug => ug.HomeGroup)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (admin is null) return NotFound();
        return Ok(ToResponse(admin));
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse>> Create(CreateAdminRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Адмін з таким email вже існує" });

        var admin = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Name = request.Name,
            LastName = request.LastName,
            PrimaryGroupId = request.PrimaryGroupId,
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        await SyncRoles(admin.Id, request.RoleIds);
        await SyncVisibleGroups(admin.Id, request.VisibleGroupIds);

        var created = await LoadAdmin(admin.Id);
        return CreatedAtAction(nameof(GetById), new { id = admin.Id }, ToResponse(created!));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminResponse>> Update(long id, UpdateAdminRequest request)
    {
        var admin = await db.Users.FindAsync(id);
        if (admin is null) return NotFound();

        if (await db.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            return Conflict(new { message = "Адмін з таким email вже існує" });

        admin.Name = request.Name;
        admin.LastName = request.LastName;
        admin.Email = request.Email;
        admin.PrimaryGroupId = request.PrimaryGroupId;

        await db.SaveChangesAsync();
        await SyncRoles(id, request.RoleIds);
        await SyncVisibleGroups(id, request.VisibleGroupIds);

        var updated = await LoadAdmin(id);
        return Ok(ToResponse(updated!));
    }

    [HttpPut("{id:long}/profile")]
    public async Task<ActionResult<AdminResponse>> UpdateProfile(long id, UpdateAdminProfileRequest request)
    {
        var admin = await db.Users.FindAsync(id);
        if (admin is null) return NotFound();

        admin.Phone = request.Phone?.Trim();
        admin.Telegram = request.Telegram?.Trim();
        admin.Notes = request.Notes?.Trim();
        admin.Gender = request.Gender;
        admin.MaritalStatus = request.MaritalStatus;
        admin.Address = request.Address?.Trim();
        admin.DateOfBirth = request.DateOfBirth is null ? null : DateOnly.Parse(request.DateOfBirth);
        admin.IsBaptized = request.IsBaptized;
        admin.Church = request.Church?.Trim();
        admin.Ministry = request.Ministry?.Trim();
        admin.IsBaptizedWithSpirit = request.IsBaptizedWithSpirit;
        admin.PersonStatusId = request.PersonStatusId;

        await db.SaveChangesAsync();
        var updated = await LoadAdmin(id);
        return Ok(ToResponse(updated!));
    }

    [HttpPost("{id:long}/set-password")]
    public async Task<IActionResult> SetPassword(long id, SetPasswordRequest request)
    {
        var admin = await db.Users.FindAsync(id);
        if (admin is null) return NotFound();

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var admin = await db.Users.FindAsync(id);
        if (admin is null) return NotFound();
        if (admin.Id == 0) return BadRequest(new { message = "Не можна видалити суперадміна" });

        db.Users.Remove(admin);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SyncRoles(long userId, List<long> roleIds)
    {
        var existing = await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        db.UserRoles.RemoveRange(existing);

        foreach (var roleId in roleIds.Distinct())
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

        await db.SaveChangesAsync();
    }

    private async Task SyncVisibleGroups(long userId, List<long> groupIds)
    {
        var existing = await db.UserHomeGroups.Where(ug => ug.UserId == userId).ToListAsync();
        db.UserHomeGroups.RemoveRange(existing);

        foreach (var groupId in groupIds.Distinct())
            db.UserHomeGroups.Add(new UserHomeGroup { UserId = userId, HomeGroupId = groupId });

        await db.SaveChangesAsync();
    }

    private Task<User?> LoadAdmin(long id) =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PrimaryGroup)
            .Include(u => u.UserHomeGroups).ThenInclude(ug => ug.HomeGroup)
            .Include(u => u.PersonStatus)
            .FirstOrDefaultAsync(u => u.Id == id);

    private static AdminResponse ToResponse(User u) => new(
        u.Id,
        u.Name,
        u.LastName,
        u.Email,
        u.UserRoles.OrderBy(ur => ur.Role.Name).Select(ur => new RoleTagDto(ur.RoleId, ur.Role.Name, ur.Role.Color)).ToList(),
        u.PrimaryGroupId,
        u.PrimaryGroup?.Name,
        u.PrimaryGroup?.Color,
        u.UserHomeGroups.OrderBy(ug => ug.HomeGroup.Name).Select(ug => new GroupTagDto(ug.HomeGroupId, ug.HomeGroup.Name, ug.HomeGroup.Color)).ToList(),
        u.CreatedAt,
        u.Phone,
        u.Telegram,
        u.Notes,
        u.Gender,
        u.MaritalStatus,
        u.Address,
        u.DateOfBirth?.ToString("yyyy-MM-dd"),
        u.IsBaptized,
        u.Church,
        u.Ministry,
        u.IsBaptizedWithSpirit,
        u.PersonStatus is null ? null : new PersonStatusDto(u.PersonStatus.Id, u.PersonStatus.Name, u.PersonStatus.Color)
    );
}
