using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Roles;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize]
public class RolesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RoleResponse>>> GetAll()
    {
        var roles = await db.Roles
            .Include(r => r.UserRoles)
            .OrderBy(r => r.Id)
            .Select(r => new RoleResponse(
                r.Id, r.Name, r.Description, r.Color,
                r.GetPermissions(), r.IsSystem, r.IsDefault,
                r.UserRoles.Count, r.CreatedAt))
            .ToListAsync();

        return Ok(roles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleResponse>> GetById(long id)
    {
        var role = await db.Roles.Include(r => r.UserRoles).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        return Ok(new RoleResponse(
            role.Id, role.Name, role.Description, role.Color,
            role.GetPermissions(), role.IsSystem, role.IsDefault,
            role.UserRoles.Count, role.CreatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<RoleResponse>> Create(CreateRoleRequest request)
    {
        if (await db.Roles.AnyAsync(r => r.Name == request.Name))
            return Conflict(new { message = "Роль з такою назвою вже існує" });

        if (request.IsDefault)
            await db.Roles.Where(r => r.IsDefault).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDefault, false));

        var role = new Role
        {
            Name = request.Name,
            Description = request.Description,
            Color = request.Color,
            IsDefault = request.IsDefault,
        };
        role.SetPermissions(request.Permissions);

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = role.Id },
            new RoleResponse(role.Id, role.Name, role.Description, role.Color,
                role.GetPermissions(), role.IsSystem, role.IsDefault, 0, role.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<RoleResponse>> Update(long id, UpdateRoleRequest request)
    {
        var role = await db.Roles.Include(r => r.UserRoles).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        if (await db.Roles.AnyAsync(r => r.Name == request.Name && r.Id != id))
            return Conflict(new { message = "Роль з такою назвою вже існує" });

        if (request.IsDefault && !role.IsDefault)
            await db.Roles.Where(r => r.IsDefault && r.Id != id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDefault, false));

        role.Name = request.Name;
        role.Description = request.Description;
        role.Color = request.Color;
        role.IsDefault = request.IsDefault;
        role.SetPermissions(request.Permissions);

        await db.SaveChangesAsync();

        return Ok(new RoleResponse(
            role.Id, role.Name, role.Description, role.Color,
            role.GetPermissions(), role.IsSystem, role.IsDefault,
            role.UserRoles.Count, role.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        if (role.IsSystem) return Conflict(new { message = "Системну роль не можна видалити" });
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id))
            return Conflict(new { message = "Неможливо видалити роль — є користувачі з цією роллю" });

        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
