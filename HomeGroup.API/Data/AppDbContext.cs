using HomeGroup.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<HomeGroupEntity> HomeGroups => Set<HomeGroupEntity>();
    public DbSet<HomeGroupMember> HomeGroupMembers => Set<HomeGroupMember>();
    public DbSet<UserHomeGroup> UserHomeGroups => Set<UserHomeGroup>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<HomeGroupCustomField> HomeGroupCustomFields => Set<HomeGroupCustomField>();
    public DbSet<PersonCustomFieldValue> PersonCustomFieldValues => Set<PersonCustomFieldValue>();
    public DbSet<GroupEvent> GroupEvents => Set<GroupEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.PrimaryGroup)
            .WithMany()
            .HasForeignKey(u => u.PrimaryGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<Person>()
            .HasOne(p => p.PrimaryGroup)
            .WithMany()
            .HasForeignKey(p => p.PrimaryGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Person>()
            .HasOne(p => p.OversightUser)
            .WithMany()
            .HasForeignKey(p => p.OversightUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<HomeGroupCustomField>()
            .HasOne(f => f.HomeGroup)
            .WithMany()
            .HasForeignKey(f => f.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersonCustomFieldValue>()
            .HasOne(v => v.Person)
            .WithMany()
            .HasForeignKey(v => v.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersonCustomFieldValue>()
            .HasOne(v => v.Field)
            .WithMany(f => f.Values)
            .HasForeignKey(v => v.FieldId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersonCustomFieldValue>()
            .HasIndex(v => new { v.PersonId, v.FieldId })
            .IsUnique();

        modelBuilder.Entity<GroupEvent>()
            .HasOne(e => e.HomeGroup)
            .WithMany()
            .HasForeignKey(e => e.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "SuperAdmin", Description = "Повний доступ до системи", Color = "#2AAFCA", PermissionsJson = "[\"*\"]", IsSystem = true, IsDefault = false, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Name = "Admin", Description = "Адміністратор системи", Color = "#6366F1", PermissionsJson = "[\"dashboard\",\"people\",\"groups\",\"admins\"]", IsSystem = true, IsDefault = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
