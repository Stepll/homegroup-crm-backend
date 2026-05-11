using HomeGroup.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<HomeGroupEntity> HomeGroups => Set<HomeGroupEntity>();
    public DbSet<HomeGroupMember> HomeGroupMembers => Set<HomeGroupMember>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HomeGroupMember>()
            .HasIndex(m => new { m.PersonId, m.HomeGroupId })
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.PersonId, a.HomeGroupId, a.MeetingDate })
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<HomeGroupEntity>()
            .HasOne(g => g.Leader)
            .WithMany()
            .HasForeignKey(g => g.LeaderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
