namespace HomeGroup.API.Models.DTOs.People;

public record CreatePersonRequest(string Name, string? Phone, string? Email, string? Notes);

public record UpdatePersonRequest(string Name, string? Phone, string? Email, string? Notes, string Status);

public record PersonResponse(int Id, string Name, string? Phone, string? Email, string? Notes, string Status, DateTime CreatedAt);
