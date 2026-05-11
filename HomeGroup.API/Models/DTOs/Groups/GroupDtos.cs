namespace HomeGroup.API.Models.DTOs.Groups;

public record CreateGroupRequest(string Name, string? Description, string? MeetingDay, string? MeetingTime, string? Location, int? LeaderId);

public record UpdateGroupRequest(string Name, string? Description, string? MeetingDay, string? MeetingTime, string? Location, int? LeaderId, bool IsActive);

public record GroupResponse(int Id, string Name, string? Description, string? MeetingDay, string? MeetingTime, string? Location, int? LeaderId, string? LeaderName, bool IsActive, int MemberCount);

public record AddMemberRequest(int PersonId, string Role = "Member");
