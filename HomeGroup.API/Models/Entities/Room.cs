namespace HomeGroup.API.Models.Entities;

public class Room
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Building { get; set; } = "Church"; // "Church" | "SocialCenter"
    public int Floor { get; set; } = 1;
    public string Color { get; set; } = "#6B7280";
}
