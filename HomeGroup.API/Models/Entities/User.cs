namespace HomeGroup.API.Models.Entities;

public class User
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public long? PrimaryGroupId { get; set; }
    public HomeGroupEntity? PrimaryGroup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserHomeGroup> UserHomeGroups { get; set; } = [];
}
