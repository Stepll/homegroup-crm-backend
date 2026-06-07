using System.Text;
using HomeGroup.API.Data;
using HomeGroup.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<GoogleCalendarSyncService>();
builder.Services.AddScoped<AttendanceExcelService>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HomeGroup CRM API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();

    await SeedSuperAdmin(context, builder.Configuration);
    await MigrateLegacyScheduleData(context);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedSuperAdmin(AppDbContext db, IConfiguration config)
{
    var email = config["SuperAdmin:Email"];
    var password = config["SuperAdmin:Password"];

    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        return;

    var hash = BCrypt.Net.BCrypt.HashPassword(password);

    await db.Database.ExecuteSqlAsync($"""
        INSERT INTO "Users" ("Id", "Email", "PasswordHash", "Name", "CreatedAt")
        VALUES (0, {email}, {hash}, 'SuperAdmin', NOW())
        ON CONFLICT ("Id") DO UPDATE
            SET "Email" = EXCLUDED."Email",
                "PasswordHash" = EXCLUDED."PasswordHash"
        """);

    await db.Database.ExecuteSqlAsync($"""
        INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
        VALUES (0, 1, NOW())
        ON CONFLICT ("UserId", "RoleId") DO NOTHING
        """);
}

// One-time migration: convert legacy NextMeetingOverrideDate (future-only) into a CalendarEvent override.
// Cancellations from AttendanceMeta.IsCancelled already sync to CalendarEvent via SyncCancellationToCalendar.
static async Task MigrateLegacyScheduleData(AppDbContext db)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var groups = await db.HomeGroups
        .Where(g => g.NextMeetingOverrideDate != null)
        .ToListAsync();

    foreach (var group in groups)
    {
        if (string.IsNullOrEmpty(group.NextMeetingOverrideDate)) continue;
        if (!DateOnly.TryParse(group.NextMeetingOverrideDate, out var overrideDate))
        {
            group.NextMeetingOverrideDate = null;
            continue;
        }
        if (overrideDate < today)
        {
            // Stale — clear it
            group.NextMeetingOverrideDate = null;
            continue;
        }

        // If a CalendarEvent already exists at this date as a real meeting, nothing to do
        var existing = await db.CalendarEvents.FirstOrDefaultAsync(e =>
            e.HomeGroupId == group.Id
            && e.Type == HomeGroup.API.Models.Entities.CalendarEventType.HomeGroup
            && !e.IsRecurring
            && e.Date == overrideDate);

        if (existing is null)
        {
            db.CalendarEvents.Add(new HomeGroup.API.Models.Entities.CalendarEvent
            {
                Type = HomeGroup.API.Models.Entities.CalendarEventType.HomeGroup,
                HomeGroupId = group.Id,
                IsRecurring = false,
                Date = overrideDate,
                IsHomeGroupMeeting = true,
                Title = group.Name,
            });
        }
        else if (existing.IsHomeGroupMeeting != true)
        {
            existing.IsHomeGroupMeeting = true;
        }

        // Clear after conversion — schedule is now driven by CalendarEvent
        group.NextMeetingOverrideDate = null;
    }

    // Convert AttendanceMeta.IsCancelled to CalendarEvent (so Schedule page sees old cancellations)
    var cancelledMetas = await db.AttendanceMetas
        .Where(m => m.IsCancelled)
        .ToListAsync();

    var calendarKeys = await db.CalendarEvents
        .Where(e => e.Type == HomeGroup.API.Models.Entities.CalendarEventType.HomeGroup
            && !e.IsRecurring
            && e.IsHomeGroupMeeting == false
            && e.Date != null)
        .Select(e => new { e.HomeGroupId, e.Date })
        .ToListAsync();
    var existingCancelKeys = calendarKeys
        .Select(x => (x.HomeGroupId, x.Date!.Value))
        .ToHashSet();

    var groupNamesById = await db.HomeGroups.ToDictionaryAsync(g => g.Id, g => g.Name);

    var addedAny = false;
    foreach (var meta in cancelledMetas)
    {
        var key = (meta.HomeGroupId, meta.MeetingDate);
        if (existingCancelKeys.Contains(key)) continue;
        if (!groupNamesById.TryGetValue(meta.HomeGroupId, out var groupName)) continue;

        db.CalendarEvents.Add(new HomeGroup.API.Models.Entities.CalendarEvent
        {
            Type = HomeGroup.API.Models.Entities.CalendarEventType.HomeGroup,
            HomeGroupId = meta.HomeGroupId,
            IsRecurring = false,
            Date = meta.MeetingDate,
            IsHomeGroupMeeting = false,
            Title = groupName,
        });
        existingCancelKeys.Add(key);
        addedAny = true;
    }

    if (groups.Count > 0 || addedAny)
        await db.SaveChangesAsync();
}
