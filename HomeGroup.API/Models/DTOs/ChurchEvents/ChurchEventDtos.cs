namespace HomeGroup.API.Models.DTOs.ChurchEvents;

public record ChurchEventDto(long Id, string Name, int Month, int Day, int DaysUntil);
public record CreateChurchEventRequest(string Name, int Month, int Day);
