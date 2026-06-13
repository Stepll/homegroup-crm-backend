using System.Security.Claims;
using HomeGroup.API.Authorization;
using HomeGroup.API.Data;
using HomeGroup.API.Models.DTOs.Groups;
using HomeGroup.API.Models.DTOs.People;
using HomeGroup.API.Models.DTOs.PersonStatuses;
using HomeGroup.API.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonActivityEntity = HomeGroup.API.Models.Entities.PersonActivity;

namespace HomeGroup.API.Controllers;

[ApiController]
[Route("api/v1/people")]
[Authorize]
public class PeopleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("people.view")]
    public async Task<ActionResult<List<GroupMemberResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool noGroup = false,
        [FromQuery] bool includeAdmins = false,
        [FromQuery] bool myOversight = false)
    {
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var currentUserId);
        bool isSuperAdmin = currentUserId == 0;

        List<long>? visibleGroupIds = null;
        if (!isSuperAdmin)
        {
            visibleGroupIds = await db.UserHomeGroups
                .Where(ug => ug.UserId == currentUserId)
                .Select(ug => ug.HomeGroupId)
                .ToListAsync();
        }

        // ── Persons ───────────────────────────────────────────────────────────
        var query = db.People.Include(p => p.PrimaryGroup).Include(p => p.PersonStatus).Include(p => p.OversightUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                (p.LastName != null && p.LastName.Contains(search)) ||
                (p.Phone != null && p.Phone.Contains(search)));

        if (noGroup)
            query = query.Where(p => p.PrimaryGroupId == null);

        if (myOversight)
            query = query.Where(p => p.OversightUserId == currentUserId);

        if (!isSuperAdmin && visibleGroupIds is { Count: > 0 })
            query = query.Where(p => p.PrimaryGroupId != null && visibleGroupIds.Contains(p.PrimaryGroupId.Value));

        var persons = await query.OrderBy(p => p.Name).ToListAsync();

        var result = persons.Select(p => new GroupMemberResponse(
            p.Id, p.Name, p.LastName, p.Phone, p.Email, p.Notes,
            p.PersonStatus != null ? new PersonStatusDto(p.PersonStatus.Id, p.PersonStatus.Name, p.PersonStatus.Color) : null,
            p.PrimaryGroupId, p.PrimaryGroup?.Name, p.PrimaryGroup?.Color,
            p.CreatedAt, false, null, null,
            p.OversightUser is null ? null : $"{p.OversightUser.Name}{(p.OversightUser.LastName is null ? "" : " " + p.OversightUser.LastName)}",
            Telegram: p.Telegram,
            TelegramChatId: p.TelegramChatId)).ToList();

        // ── Admins ────────────────────────────────────────────────────────────
        if (includeAdmins && !myOversight && !noGroup)
        {
            var adminQuery = db.Users
                .Where(u => u.Id != 0 && u.PrimaryGroupId != null)
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.PersonStatus)
                .Include(u => u.PrimaryGroup)
                .AsQueryable();

            if (!isSuperAdmin && visibleGroupIds is not null)
                adminQuery = adminQuery.Where(u => visibleGroupIds.Contains(u.PrimaryGroupId!.Value));

            if (!string.IsNullOrWhiteSpace(search))
                adminQuery = adminQuery.Where(u =>
                    u.Name.Contains(search) ||
                    (u.LastName != null && u.LastName.Contains(search)) ||
                    (u.Phone != null && u.Phone.Contains(search)));

            var admins = await adminQuery.OrderBy(u => u.Name).ToListAsync();

            foreach (var a in admins)
            {
                var primaryRole = a.UserRoles.Select(ur => ur.Role).FirstOrDefault();
                var roleTag = primaryRole is null ? null : new MemberRoleTagDto(primaryRole.Name, primaryRole.Color);
                var status = a.PersonStatus is null ? null : new PersonStatusDto(a.PersonStatus.Id, a.PersonStatus.Name, a.PersonStatus.Color);
                result.Add(new GroupMemberResponse(
                    a.Id, a.Name, a.LastName, a.Phone, a.Email, a.Notes,
                    status, a.PrimaryGroupId, a.PrimaryGroup?.Name, a.PrimaryGroup?.Color,
                    a.CreatedAt, true, a.Id, roleTag));
            }

            result = result.OrderBy(r => r.Name).ToList();
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequirePermission("people.view")]
    public async Task<ActionResult<PersonDetailResponse>> GetById(long id)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .Include(p => p.PersonStatus)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        var statusDto = person.PersonStatus is null ? null : new PersonStatusDto(person.PersonStatus.Id, person.PersonStatus.Name, person.PersonStatus.Color);

        return Ok(new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Telegram, person.Notes,
            person.Gender, person.MaritalStatus, person.Address, person.DateOfBirth,
            person.IsBaptized, person.Church, person.Ministry, person.IsBaptizedWithSpirit,
            statusDto, person.OversightInfo, person.OversightUserId,
            person.OversightUser is null ? null : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}",
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt,
            await GetCustomFields(id, person.PrimaryGroupId)));
    }

    [HttpPost]
    [RequirePermission("people.create")]
    public async Task<ActionResult<PersonDetailResponse>> Create(CreatePersonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Ім'я обов'язкове" });

        var person = new Person
        {
            Name = request.Name.Trim(),
            LastName = request.LastName?.Trim(),
            PrimaryGroupId = request.PrimaryGroupId,
        };

        db.People.Add(person);
        await db.SaveChangesAsync();

        if (person.PrimaryGroupId.HasValue)
            db.HomeGroupMembers.Add(new HomeGroupMember { HomeGroupId = person.PrimaryGroupId.Value, PersonId = person.Id });
        await db.SaveChangesAsync();

        await db.Entry(person).Reference(p => p.PrimaryGroup).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = person.Id }, new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Telegram, person.Notes,
            person.Gender, person.MaritalStatus, person.Address, person.DateOfBirth,
            person.IsBaptized, person.Church, person.Ministry, person.IsBaptizedWithSpirit,
            null, person.OversightInfo, null, null,
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt, []));
    }

    [HttpPut("{id}")]
    [RequirePermission("people.edit")]
    public async Task<ActionResult<PersonDetailResponse>> Update(long id, UpdatePersonRequest request)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .Include(p => p.OversightUser)
            .Include(p => p.PersonStatus)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null) return NotFound();

        var oldGroupId = person.PrimaryGroupId;
        var oldStatusId = person.PersonStatusId;
        var oldStatusName = person.PersonStatus?.Name;
        var oldStatusColor = person.PersonStatus?.Color;
        var oldOversightUserId = person.OversightUserId;
        var oldOversightUserName = person.OversightUser is null ? null
            : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}";

        person.Name = request.Name.Trim();
        person.LastName = request.LastName?.Trim();
        person.Phone = request.Phone?.Trim();
        person.Email = request.Email?.Trim();
        person.Telegram = request.Telegram?.Trim();
        person.Notes = request.Notes?.Trim();
        person.Gender = request.Gender;
        person.MaritalStatus = request.MaritalStatus;
        person.Address = request.Address?.Trim();
        person.DateOfBirth = request.DateOfBirth;
        person.IsBaptized = request.IsBaptized;
        person.Church = request.Church?.Trim();
        person.Ministry = request.Ministry?.Trim();
        person.IsBaptizedWithSpirit = request.IsBaptizedWithSpirit;
        person.PersonStatusId = request.PersonStatusId;
        person.OversightInfo = request.OversightInfo?.Trim();
        person.OversightUserId = request.OversightUserId;
        person.PrimaryGroupId = request.PrimaryGroupId;

        // Sync HomeGroupMembers when primary group changes
        if (oldGroupId != request.PrimaryGroupId)
        {
            if (oldGroupId.HasValue)
            {
                var oldMembership = await db.HomeGroupMembers
                    .FirstOrDefaultAsync(m => m.PersonId == id && m.HomeGroupId == oldGroupId.Value);
                if (oldMembership != null) db.HomeGroupMembers.Remove(oldMembership);
            }
            if (request.PrimaryGroupId.HasValue &&
                !await db.HomeGroupMembers.AnyAsync(m => m.PersonId == id && m.HomeGroupId == request.PrimaryGroupId.Value))
            {
                db.HomeGroupMembers.Add(new HomeGroupMember { PersonId = id, HomeGroupId = request.PrimaryGroupId.Value });
            }
        }

        await db.SaveChangesAsync();
        await db.Entry(person).Reference(p => p.PrimaryGroup).LoadAsync();
        await db.Entry(person).Reference(p => p.OversightUser).LoadAsync();
        await db.Entry(person).Reference(p => p.PersonStatus).LoadAsync();

        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);
        var actorIdNullable = actorId == 0 ? (long?)null : actorId;

        if (oldStatusId != request.PersonStatusId)
        {
            db.PersonActivities.Add(new PersonActivityEntity
            {
                PersonId = id,
                Type = "status_change",
                AuthorId = actorIdNullable,
                OldStatusId = oldStatusId,
                OldStatusName = oldStatusName,
                OldStatusColor = oldStatusColor,
                NewStatusId = person.PersonStatus?.Id,
                NewStatusName = person.PersonStatus?.Name,
                NewStatusColor = person.PersonStatus?.Color,
            });
        }

        if (oldOversightUserId != request.OversightUserId)
        {
            var newOversightName = person.OversightUser is null ? null
                : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}";
            db.PersonActivities.Add(new PersonActivityEntity
            {
                PersonId = id,
                Type = "oversight_change",
                AuthorId = actorIdNullable,
                OldValue = oldOversightUserName,
                NewValue = newOversightName,
            });
        }

        if (oldStatusId != request.PersonStatusId || oldOversightUserId != request.OversightUserId)
            await db.SaveChangesAsync();

        var updatedStatusDto = person.PersonStatus is null ? null : new PersonStatusDto(person.PersonStatus.Id, person.PersonStatus.Name, person.PersonStatus.Color);

        return Ok(new PersonDetailResponse(
            person.Id, person.Name, person.LastName, person.Phone, person.Email, person.Telegram, person.Notes,
            person.Gender, person.MaritalStatus, person.Address, person.DateOfBirth,
            person.IsBaptized, person.Church, person.Ministry, person.IsBaptizedWithSpirit,
            updatedStatusDto, person.OversightInfo, person.OversightUserId,
            person.OversightUser is null ? null : $"{person.OversightUser.Name}{(person.OversightUser.LastName is null ? "" : " " + person.OversightUser.LastName)}",
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            person.CreatedAt,
            await GetCustomFields(id, person.PrimaryGroupId)));
    }

    [HttpDelete("{id}")]
    [RequirePermission("people.delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        db.People.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record SetTelegramChatIdRequest(long? ChatId);

    [HttpPut("{id}/telegram-chat-id")]
    public async Task<IActionResult> SetTelegramChatId(long id, SetTelegramChatIdRequest request)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();
        person.TelegramChatId = request.ChatId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/activity")]
    [RequirePermission("people.view")]
    public async Task<ActionResult<List<PersonActivityDto>>> GetActivity(long id)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var entries = await db.PersonActivities
            .Include(a => a.Author)
            .Where(a => a.PersonId == id)
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

    [HttpPost("{id}/comments")]
    [RequirePermission("people.edit")]
    public async Task<ActionResult<PersonActivityDto>> AddComment(long id, AddPersonCommentRequest request)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Коментар не може бути порожнім" });

        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var authorId);

        var entry = new PersonActivityEntity
        {
            PersonId = id,
            Type = "comment",
            Content = request.Content.Trim(),
            AuthorId = authorId == 0 ? null : authorId,
        };
        db.PersonActivities.Add(entry);
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

    [HttpDelete("{id}/activity/{entryId}")]
    [RequirePermission("people.edit")]
    public async Task<IActionResult> DeleteActivity(long id, long entryId)
    {
        var entry = await db.PersonActivities
            .FirstOrDefaultAsync(a => a.Id == entryId && a.PersonId == id && a.Type == "comment");
        if (entry is null) return NotFound();
        db.PersonActivities.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Custom fields — definitions live on the HomeGroup, values are per-person

    [HttpPost("{id}/custom-fields")]
    [RequirePermission("people.customFields")]
    public async Task<ActionResult<CustomFieldDto>> AddCustomField(long id, CreateCustomFieldRequest request)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();
        if (!person.PrimaryGroupId.HasValue)
            return BadRequest(new { message = "Людина не прив'язана до домашньої групи" });

        var field = new HomeGroupCustomField
        {
            HomeGroupId = person.PrimaryGroupId.Value,
            Name = request.Name.Trim(),
        };
        db.HomeGroupCustomFields.Add(field);
        await db.SaveChangesAsync();

        return Ok(new CustomFieldDto(field.Id, field.Name, null));
    }

    [HttpPut("{id}/custom-fields/{fieldId}")]
    [RequirePermission("people.customFields")]
    public async Task<ActionResult<CustomFieldDto>> UpdateCustomField(long id, long fieldId, UpdateCustomFieldRequest request)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var field = await db.HomeGroupCustomFields.FindAsync(fieldId);
        if (field is null) return NotFound();

        var value = await db.PersonCustomFieldValues
            .FirstOrDefaultAsync(v => v.PersonId == id && v.FieldId == fieldId);

        if (value is null)
        {
            value = new PersonCustomFieldValue { PersonId = id, FieldId = fieldId };
            db.PersonCustomFieldValues.Add(value);
        }
        value.Value = request.Value?.Trim();

        await db.SaveChangesAsync();
        return Ok(new CustomFieldDto(field.Id, field.Name, value.Value));
    }

    [HttpDelete("{id}/custom-fields/{fieldId}")]
    [RequirePermission("people.customFields")]
    public async Task<IActionResult> DeleteCustomField(long id, long fieldId)
    {
        if (!await db.People.AnyAsync(p => p.Id == id)) return NotFound();

        var field = await db.HomeGroupCustomFields.FindAsync(fieldId);
        if (field is null) return NotFound();

        db.HomeGroupCustomFields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<CustomFieldDto>> GetCustomFields(long personId, long? primaryGroupId)
    {
        if (!primaryGroupId.HasValue) return [];

        var fields = await db.HomeGroupCustomFields
            .Where(f => f.HomeGroupId == primaryGroupId.Value)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();

        var fieldIds = fields.Select(f => f.Id).ToList();
        var values = await db.PersonCustomFieldValues
            .Where(v => v.PersonId == personId && fieldIds.Contains(v.FieldId))
            .ToListAsync();

        return fields.Select(f => new CustomFieldDto(
            f.Id, f.Name,
            values.FirstOrDefault(v => v.FieldId == f.Id)?.Value)).ToList();
    }

    // ── Convert Person → Admin ────────────────────────────────────────────────

    [HttpGet("{id}/convert-to-admin/preview")]
    [RequirePermission("people.convertToAdmin")]
    public async Task<ActionResult<ConvertToAdminPreview>> ConvertPreview(long id)
    {
        var person = await db.People
            .Include(p => p.PrimaryGroup)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (person is null) return NotFound();

        var emailAvailable = string.IsNullOrWhiteSpace(person.Email)
            || !await db.Users.AnyAsync(u => u.Email == person.Email);

        var attendanceCount = await db.Attendances.CountAsync(a => a.PersonId == id);
        var customFieldCount = await db.PersonCustomFieldValues.CountAsync(v => v.PersonId == id);
        var activityCount = await db.PersonActivities.CountAsync(a => a.PersonId == id);
        var groupNeedCount = await db.GroupNeeds.CountAsync(n => n.PersonId == id);
        var membershipCount = await db.HomeGroupMembers.CountAsync(m => m.PersonId == id);
        var historyCount = await db.GroupMemberHistories.CountAsync(h => h.PersonId == id);

        return Ok(new ConvertToAdminPreview(
            person.Id, person.Name, person.LastName, person.Email,
            emailAvailable,
            person.PrimaryGroupId, person.PrimaryGroup?.Name,
            attendanceCount, customFieldCount, activityCount,
            groupNeedCount, membershipCount, historyCount));
    }

    [HttpPost("{id}/convert-to-admin")]
    [RequirePermission("people.convertToAdmin")]
    public async Task<ActionResult<long>> ConvertToAdmin(long id, ConvertToAdminRequest request)
    {
        var person = await db.People.FirstOrDefaultAsync(p => p.Id == id);
        if (person is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email обов'язковий" });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new { message = "Пароль мінімум 6 символів" });
        if (request.RoleIds is null || request.RoleIds.Count == 0)
            return BadRequest(new { message = "Виберіть хоча б одну роль" });
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Адмін з таким email вже існує" });

        await using var tx = await db.Database.BeginTransactionAsync();

        var user = new User
        {
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Name = person.Name,
            LastName = person.LastName,
            Phone = person.Phone,
            Telegram = person.Telegram,
            Notes = person.Notes,
            Gender = person.Gender,
            MaritalStatus = person.MaritalStatus,
            Address = person.Address,
            DateOfBirth = person.DateOfBirth,
            IsBaptized = person.IsBaptized,
            Church = person.Church,
            Ministry = person.Ministry,
            IsBaptizedWithSpirit = person.IsBaptizedWithSpirit,
            PersonStatusId = person.PersonStatusId,
            PrimaryGroupId = request.PrimaryGroupId ?? person.PrimaryGroupId,
            CreatedAt = person.CreatedAt,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        foreach (var roleId in request.RoleIds.Distinct())
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        foreach (var gid in request.VisibleGroupIds.Distinct())
            db.UserHomeGroups.Add(new UserHomeGroup { UserId = user.Id, HomeGroupId = gid });

        // Migrate Attendance: PersonId → UserId
        var attendances = await db.Attendances.Where(a => a.PersonId == id).ToListAsync();
        foreach (var a in attendances)
        {
            a.PersonId = null;
            a.UserId = user.Id;
        }

        // Migrate GroupNeed
        var needs = await db.GroupNeeds.Where(n => n.PersonId == id).ToListAsync();
        foreach (var n in needs)
        {
            n.PersonId = null;
            n.UserId = user.Id;
        }

        // Migrate GroupMemberHistory
        var history = await db.GroupMemberHistories.Where(h => h.PersonId == id).ToListAsync();
        foreach (var h in history)
        {
            h.PersonId = null;
            h.UserId = user.Id;
        }

        // Migrate custom field values → UserCustomFieldValue
        var pcfv = await db.PersonCustomFieldValues.Where(v => v.PersonId == id).ToListAsync();
        foreach (var v in pcfv)
        {
            db.UserCustomFieldValues.Add(new UserCustomFieldValue
            {
                UserId = user.Id,
                FieldId = v.FieldId,
                Value = v.Value,
            });
        }
        db.PersonCustomFieldValues.RemoveRange(pcfv);

        // Migrate PersonActivity → UserActivity
        var activities = await db.PersonActivities.Where(a => a.PersonId == id).ToListAsync();
        foreach (var a in activities)
        {
            db.UserActivities.Add(new UserActivity
            {
                UserId = user.Id,
                Type = a.Type,
                Content = a.Content,
                AuthorId = a.AuthorId,
                OldStatusId = a.OldStatusId,
                OldStatusName = a.OldStatusName,
                OldStatusColor = a.OldStatusColor,
                NewStatusId = a.NewStatusId,
                NewStatusName = a.NewStatusName,
                NewStatusColor = a.NewStatusColor,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = a.CreatedAt,
            });
        }
        db.PersonActivities.RemoveRange(activities);

        // Delete HomeGroupMembers — admin membership is via PrimaryGroupId
        var memberships = await db.HomeGroupMembers.Where(m => m.PersonId == id).ToListAsync();
        db.HomeGroupMembers.RemoveRange(memberships);

        await db.SaveChangesAsync();

        // Mark conversion in activity
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);
        db.UserActivities.Add(new UserActivity
        {
            UserId = user.Id,
            Type = "person_converted",
            AuthorId = actorId == 0 ? null : actorId,
            NewValue = $"{person.Name}{(person.LastName is null ? "" : " " + person.LastName)}",
        });

        // Delete Person (other Person.OversightInfo text refs remain as text; FK refs were to User already)
        db.People.Remove(person);
        await db.SaveChangesAsync();

        await tx.CommitAsync();
        return Ok(user.Id);
    }
}
