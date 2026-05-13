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

    // Profile fields (same as Person)
    public string? Phone { get; set; }
    public string? Telegram { get; set; }
    public string? Notes { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsBaptized { get; set; }
    public string? Church { get; set; }
    public string? Ministry { get; set; }
    public bool IsBaptizedWithSpirit { get; set; }
    public long? PersonStatusId { get; set; }
    public PersonStatus? PersonStatus { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserHomeGroup> UserHomeGroups { get; set; } = [];
}
