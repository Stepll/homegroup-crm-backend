using HomeGroup.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<HomeGroupEntity> HomeGroups => Set<HomeGroupEntity>();
    public DbSet<HomeGroupMember> HomeGroupMembers => Set<HomeGroupMember>();
    public DbSet<UserHomeGroup> UserHomeGroups => Set<UserHomeGroup>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserHomeGroup>()
            .HasKey(ug => new { ug.UserId, ug.HomeGroupId });

        modelBuilder.Entity<UserHomeGroup>()
            .HasOne(ug => ug.User)
            .WithMany(u => u.UserHomeGroups)
            .HasForeignKey(ug => ug.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserHomeGroup>()
            .HasOne(ug => ug.HomeGroup)
            .WithMany(g => g.UserHomeGroups)
            .HasForeignKey(ug => ug.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeGroupMember>()
            .HasIndex(m => new { m.PersonId, m.HomeGroupId })
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.PersonId, a.HomeGroupId, a.MeetingDate })
            .IsUnique();

        modelBuilder.Entity<HomeGroupEntity>()
            .HasOne(g => g.Leader)
            .WithMany()
            .HasForeignKey(g => g.LeaderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "SuperAdmin", Description = "Повний доступ до системи", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Name = "Admin", Description = "Адміністратор системи", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
