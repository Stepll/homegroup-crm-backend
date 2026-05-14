using System.Text;
using HomeGroup.API.Data;
using HomeGroup.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<GoogleCalendarSyncService>();

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
