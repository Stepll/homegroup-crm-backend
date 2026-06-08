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
    public DbSet<AttendanceMeta> AttendanceMetas => Set<AttendanceMeta>();
    public DbSet<HomeGroupCustomField> HomeGroupCustomFields => Set<HomeGroupCustomField>();
    public DbSet<PersonCustomFieldValue> PersonCustomFieldValues => Set<PersonCustomFieldValue>();
    public DbSet<GroupEvent> GroupEvents => Set<GroupEvent>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<PlanTemplate> PlanTemplates => Set<PlanTemplate>();
    public DbSet<PlanTemplateBlock> PlanTemplateBlocks => Set<PlanTemplateBlock>();
    public DbSet<HomeMeetingPlan> MeetingPlans => Set<HomeMeetingPlan>();
    public DbSet<MeetingPlanBlock> MeetingPlanBlocks => Set<MeetingPlanBlock>();
    public DbSet<PersonStatus> PersonStatuses => Set<PersonStatus>();
    public DbSet<PersonActivity> PersonActivities => Set<PersonActivity>();
    public DbSet<AdminTask> AdminTasks => Set<AdminTask>();
    public DbSet<GroupNeed> GroupNeeds => Set<GroupNeed>();
    public DbSet<GroupMemberHistory> GroupMemberHistories => Set<GroupMemberHistory>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<UserCustomFieldValue> UserCustomFieldValues => Set<UserCustomFieldValue>();
    public DbSet<AttendanceImport> AttendanceImports => Set<AttendanceImport>();

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
            .HasOne(a => a.Person)
            .WithMany(p => p.Attendances)
            .HasForeignKey(a => a.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.PersonId, a.HomeGroupId, a.MeetingDate })
            .IsUnique()
            .HasFilter("\"PersonId\" IS NOT NULL");

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.UserId, a.HomeGroupId, a.MeetingDate })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");

        modelBuilder.Entity<AttendanceMeta>()
            .HasOne(m => m.HomeGroup)
            .WithMany()
            .HasForeignKey(m => m.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AttendanceMeta>()
            .HasIndex(m => new { m.HomeGroupId, m.MeetingDate })
            .IsUnique();

        modelBuilder.Entity<HomeGroupEntity>()
            .HasOne(g => g.Leader)
            .WithMany()
            .HasForeignKey(g => g.LeaderId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<HomeGroupEntity>()
            .HasOne(g => g.AutoBookRoom)
            .WithMany()
            .HasForeignKey(g => g.AutoBookRoomId)
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

        modelBuilder.Entity<Person>()
            .HasOne(p => p.PersonStatus)
            .WithMany()
            .HasForeignKey(p => p.PersonStatusId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasOne(u => u.PersonStatus)
            .WithMany()
            .HasForeignKey(u => u.PersonStatusId)
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

        modelBuilder.Entity<CalendarEvent>()
            .HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.RoomId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CalendarEvent>()
            .HasOne(e => e.HomeGroup)
            .WithMany()
            .HasForeignKey(e => e.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlanTemplateBlock>()
            .HasOne(b => b.Template)
            .WithMany(t => t.Blocks)
            .HasForeignKey(b => b.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeMeetingPlan>()
            .HasOne(p => p.HomeGroup)
            .WithMany()
            .HasForeignKey(p => p.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeMeetingPlan>()
            .HasIndex(p => new { p.HomeGroupId, p.MeetingDate })
            .IsUnique();

        modelBuilder.Entity<MeetingPlanBlock>()
            .HasOne(b => b.Plan)
            .WithMany(p => p.Blocks)
            .HasForeignKey(b => b.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdminTask>()
            .HasOne(t => t.TargetUser)
            .WithMany()
            .HasForeignKey(t => t.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdminTask>()
            .HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PersonActivity>()
            .HasOne(a => a.Person)
            .WithMany()
            .HasForeignKey(a => a.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersonActivity>()
            .HasOne(a => a.Author)
            .WithMany()
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GroupMemberHistory>()
            .HasOne(h => h.Person)
            .WithMany()
            .HasForeignKey(h => h.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupMemberHistory>()
            .HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupMemberHistory>()
            .HasOne(h => h.HomeGroup)
            .WithMany()
            .HasForeignKey(h => h.HomeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserActivity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserActivity>()
            .HasOne(a => a.Author)
            .WithMany()
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserCustomFieldValue>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCustomFieldValue>()
            .HasOne(v => v.Field)
            .WithMany()
            .HasForeignKey(v => v.FieldId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCustomFieldValue>()
            .HasIndex(v => new { v.UserId, v.FieldId })
            .IsUnique();

        modelBuilder.Entity<AttendanceImport>()
            .HasOne(i => i.CreatedBy)
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "SuperAdmin", Description = "Повний доступ до системи", Color = "#2AAFCA", PermissionsJson = "[\"*\"]", IsSystem = true, IsDefault = false, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Name = "Admin", Description = "Адміністратор системи", Color = "#6366F1", PermissionsJson = "[\"dashboard\",\"people\",\"groups\",\"admins\"]", IsSystem = true, IsDefault = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
