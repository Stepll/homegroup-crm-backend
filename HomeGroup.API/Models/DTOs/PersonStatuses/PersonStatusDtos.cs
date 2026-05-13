namespace HomeGroup.API.Models.DTOs.PersonStatuses;

public record PersonStatusDto(long Id, string Name, string Color);

public record CreatePersonStatusRequest(string Name, string Color);

public record UpdatePersonStatusRequest(string Name, string Color);
