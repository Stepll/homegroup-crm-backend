namespace HomeGroup.API.Models.Entities;

public class HomeGroupMember
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public int HomeGroupId { get; set; }
    public HomeGroupEntity HomeGroup { get; set; } = null!;
    public string Role { get; set; } = "Member"; // Leader, Member
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
