using System.Security.Claims;
using System.Text.Json;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Admins;
using HomeGroup.API.Models.DTOs.Groups;
using HomeGroup.API.Models.DTOs.People;
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
        return Ok(await ToResponseWithFields(admin));
    }

    [HttpGet]
    [RequirePermission("settings.admins")]
    public async Task<ActionResult<List<AdminResponse>>> GetAll()
    {
        var admins = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PrimaryGroup)
            .Include(u => u.UserHomeGroups).ThenInclude(ug => ug.HomeGroup)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var result = new List<AdminResponse>();
        foreach (var a in admins) result.Add(await ToResponseWithFields(a));
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminResponse>> GetById(long id)
    {
        var admin = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.PrimaryGroup)
            .Include(u => u.UserHomeGroups).ThenInclude(ug => ug.HomeGroup)
            .Include(u => u.PersonStatus)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (admin is null) return NotFound();
        return Ok(await ToResponseWithFields(admin));
    }

    [HttpPost]
    [RequirePermission("settings.admins")]
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
        return CreatedAtAction(nameof(GetById), new { id = admin.Id }, await ToResponseWithFields(created!));
    }

    [HttpPut("{id:long}")]
    [RequirePermission("settings.admins")]
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
        return Ok(await ToResponseWithFields(updated!));
    }

    [HttpPut("{id:long}/profile")]
    public async Task<ActionResult<AdminResponse>> UpdateProfile(long id, UpdateAdminProfileRequest request)
    {
        var admin = await db.Users.Include(u => u.PersonStatus).FirstOrDefaultAsync(u => u.Id == id);
        if (admin is null) return NotFound();

        var oldStatusId = admin.PersonStatusId;
        var oldStatusName = admin.PersonStatus?.Name;
        var oldStatusColor = admin.PersonStatus?.Color;

        if (!string.IsNullOrWhiteSpace(request.Name)) admin.Name = request.Name.Trim();
        if (request.LastName is not null) admin.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
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

        if (oldStatusId != request.PersonStatusId)
        {
            await db.Entry(admin).Reference(u => u.PersonStatus).LoadAsync();
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);
            db.UserActivities.Add(new UserActivity
            {
                UserId = id,
                Type = "status_change",
                AuthorId = actorId == 0 ? null : actorId,
                OldStatusId = oldStatusId,
                OldStatusName = oldStatusName,
                OldStatusColor = oldStatusColor,
                NewStatusId = admin.PersonStatus?.Id,
                NewStatusName = admin.PersonStatus?.Name,
                NewStatusColor = admin.PersonStatus?.Color,
            });
            await db.SaveChangesAsync();
        }

        var updated = await LoadAdmin(id);
        return Ok(await ToResponseWithFields(updated!));
    }

    [HttpPost("me/set-password")]
    public async Task<IActionResult> SetMyPassword(SetPasswordRequest request)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId)) return Unauthorized();

        var admin = await db.Users.FindAsync(userId);
        if (admin is null) return NotFound();

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:long}/set-password")]
    [RequirePermission("settings.admins")]
    public async Task<IActionResult> SetPassword(long id, SetPasswordRequest request)
    {
        var admin = await db.Users.FindAsync(id);
        if (admin is null) return NotFound();

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("me/dashboard")]
    public async Task<ActionResult<List<WidgetConfigItem>>> GetDashboardConfig()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId)) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (string.IsNullOrEmpty(user.DashboardConfigJson))
            return Ok(new List<WidgetConfigItem>());

        var config = JsonSerializer.Deserialize<List<WidgetConfigItem>>(
            user.DashboardConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return Ok(config ?? new List<WidgetConfigItem>());
    }

    [HttpPut("me/dashboard")]
    public async Task<IActionResult> SaveDashboardConfig(SaveDashboardConfigRequest request)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId)) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        user.DashboardConfigJson = JsonSerializer.Serialize(request.Config);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("settings.admins")]
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

    // ── Tasks ─────────────────────────────────────────────────────────────────

    [HttpGet("me/tasks")]
    public async Task<ActionResult<List<AdminTaskDto>>> GetMyTasks()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId)) return Unauthorized();

        var tasks = await db.AdminTasks
            .Include(t => t.CreatedBy)
            .Where(t => t.TargetUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tasks.Select(ToTaskDto).ToList());
    }

    [HttpPatch("me/tasks/{taskId}/toggle")]
    public async Task<ActionResult<AdminTaskDto>> ToggleMyTask(long taskId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var userId)) return Unauthorized();

        var task = await db.AdminTasks.Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TargetUserId == userId);
        if (task is null) return NotFound();

        task.IsCompleted = !task.IsCompleted;
        await db.SaveChangesAsync();

        return Ok(ToTaskDto(task));
    }

    [HttpGet("{id}/tasks")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<ActionResult<List<AdminTaskDto>>> GetTasks(long id)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();

        var tasks = await db.AdminTasks
            .Include(t => t.CreatedBy)
            .Where(t => t.TargetUserId == id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tasks.Select(ToTaskDto).ToList());
    }

    [HttpPost("{id}/tasks")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<ActionResult<AdminTaskDto>> CreateTask(long id, CreateAdminTaskRequest request)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Назва обов'язкова" });

        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);

        var task = new AdminTask
        {
            TargetUserId = id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            CreatedByUserId = actorId == 0 ? null : actorId,
        };
        db.AdminTasks.Add(task);
        await db.SaveChangesAsync();
        await db.Entry(task).Reference(t => t.CreatedBy).LoadAsync();

        return Ok(ToTaskDto(task));
    }

    [HttpPut("{id}/tasks/{taskId}")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<ActionResult<AdminTaskDto>> UpdateTask(long id, long taskId, UpdateAdminTaskRequest request)
    {
        var task = await db.AdminTasks.Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TargetUserId == id);
        if (task is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Назва обов'язкова" });

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        await db.SaveChangesAsync();

        return Ok(ToTaskDto(task));
    }

    [HttpPatch("{id}/tasks/{taskId}/toggle")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<ActionResult<AdminTaskDto>> ToggleTask(long id, long taskId)
    {
        var task = await db.AdminTasks.Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TargetUserId == id);
        if (task is null) return NotFound();

        task.IsCompleted = !task.IsCompleted;
        await db.SaveChangesAsync();

        return Ok(ToTaskDto(task));
    }

    [HttpDelete("{id}/tasks/{taskId}")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<IActionResult> DeleteTask(long id, long taskId)
    {
        var task = await db.AdminTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TargetUserId == id);
        if (task is null) return NotFound();

        db.AdminTasks.Remove(task);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static AdminTaskDto ToTaskDto(AdminTask t) => new(
        t.Id, t.Title, t.Description, t.IsCompleted,
        t.CreatedByUserId,
        t.CreatedBy is null ? null : $"{t.CreatedBy.Name}{(t.CreatedBy.LastName is null ? "" : " " + t.CreatedBy.LastName)}",
        t.CreatedAt);

    private async Task<AdminResponse> ToResponseWithFields(User u)
    {
        var customFields = await GetCustomFields(u.Id, u.PrimaryGroupId);
        return new AdminResponse(
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
            u.PersonStatus is null ? null : new PersonStatusDto(u.PersonStatus.Id, u.PersonStatus.Name, u.PersonStatus.Color),
            customFields
        );
    }

    private async Task<List<CustomFieldDto>> GetCustomFields(long userId, long? primaryGroupId)
    {
        if (!primaryGroupId.HasValue) return [];

        var fields = await db.HomeGroupCustomFields
            .Where(f => f.HomeGroupId == primaryGroupId.Value)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();

        var fieldIds = fields.Select(f => f.Id).ToList();
        var values = await db.UserCustomFieldValues
            .Where(v => v.UserId == userId && fieldIds.Contains(v.FieldId))
            .ToListAsync();

        return fields.Select(f => new CustomFieldDto(
            f.Id, f.Name,
            values.FirstOrDefault(v => v.FieldId == f.Id)?.Value)).ToList();
    }

    // ── Activity ──────────────────────────────────────────────────────────────

    [HttpGet("{id:long}/activity")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<ActionResult<List<PersonActivityDto>>> GetActivity(long id)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();

        var entries = await db.UserActivities
            .Include(a => a.Author)
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(entries.Select(a => new PersonActivityDto(
            a.Id,
            a.Type,
            a.Content,
            a.AuthorId,
            a.Author is null ? null : $"{a.Author.Name}{(a.Author.LastName is null ? "" : " " + a.Author.LastName)}",
            a.OldStatusId is null && a.OldStatusName is null ? null : new PersonStatusDto(a.OldStatusId ?? 0, a.OldStatusName ?? "", a.OldStatusColor ?? "#888"),
            a.NewStatusId is null && a.NewStatusName is null ? null : new PersonStatusDto(a.NewStatusId ?? 0, a.NewStatusName ?? "", a.NewStatusColor ?? "#888"),
            a.OldValue,
            a.NewValue,
            a.CreatedAt)).ToList());
    }

    [HttpPost("{id:long}/comments")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<ActionResult<PersonActivityDto>> AddComment(long id, AddPersonCommentRequest request)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Коментар не може бути порожнім" });

        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var authorId);

        var entry = new UserActivity
        {
            UserId = id,
            Type = "comment",
            Content = request.Content.Trim(),
            AuthorId = authorId == 0 ? null : authorId,
        };
        db.UserActivities.Add(entry);
        await db.SaveChangesAsync();
        await db.Entry(entry).Reference(a => a.Author).LoadAsync();

        return Ok(new PersonActivityDto(
            entry.Id,
            entry.Type,
            entry.Content,
            entry.AuthorId,
            entry.Author is null ? null : $"{entry.Author.Name}{(entry.Author.LastName is null ? "" : " " + entry.Author.LastName)}",
            null, null, null, null,
            entry.CreatedAt));
    }

    [HttpDelete("{id:long}/activity/{entryId:long}")]
    [RequirePermission("admins.viewProfiles")]
    public async Task<IActionResult> DeleteActivity(long id, long entryId)
    {
        var entry = await db.UserActivities
            .FirstOrDefaultAsync(a => a.Id == entryId && a.UserId == id && a.Type == "comment");
        if (entry is null) return NotFound();
        db.UserActivities.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Custom fields (definitions on PrimaryGroup, values per-user) ──────────

    [HttpPost("{id:long}/custom-fields")]
    [RequirePermission("people.customFields")]
    public async Task<ActionResult<CustomFieldDto>> AddCustomField(long id, CreateCustomFieldRequest request)
    {
        var admin = await db.Users.FindAsync(id);
        if (admin is null) return NotFound();
        if (!admin.PrimaryGroupId.HasValue)
            return BadRequest(new { message = "Адмін не прив'язаний до домашньої групи" });

        var field = new HomeGroupCustomField
        {
            HomeGroupId = admin.PrimaryGroupId.Value,
            Name = request.Name.Trim(),
        };
        db.HomeGroupCustomFields.Add(field);
        await db.SaveChangesAsync();

        return Ok(new CustomFieldDto(field.Id, field.Name, null));
    }

    [HttpPut("{id:long}/custom-fields/{fieldId:long}")]
    [RequirePermission("people.customFields")]
    public async Task<ActionResult<CustomFieldDto>> UpdateCustomField(long id, long fieldId, UpdateCustomFieldRequest request)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();

        var field = await db.HomeGroupCustomFields.FindAsync(fieldId);
        if (field is null) return NotFound();

        var value = await db.UserCustomFieldValues
            .FirstOrDefaultAsync(v => v.UserId == id && v.FieldId == fieldId);

        if (value is null)
        {
            value = new UserCustomFieldValue { UserId = id, FieldId = fieldId };
            db.UserCustomFieldValues.Add(value);
        }
        value.Value = request.Value?.Trim();

        await db.SaveChangesAsync();
        return Ok(new CustomFieldDto(field.Id, field.Name, value.Value));
    }

    [HttpDelete("{id:long}/custom-fields/{fieldId:long}")]
    [RequirePermission("people.customFields")]
    public async Task<IActionResult> DeleteCustomField(long id, long fieldId)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();

        var field = await db.HomeGroupCustomFields.FindAsync(fieldId);
        if (field is null) return NotFound();

        db.HomeGroupCustomFields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
