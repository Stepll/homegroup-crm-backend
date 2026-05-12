namespace HomeGroup.API.Models.DTOs.Groups;

public record CreateGroupRequest(string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId);

public record UpdateGroupRequest(string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, bool IsActive);

public record GroupResponse(long Id, string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, string? LeaderName, bool IsActive, int MemberCount);

public record AddMemberRequest(long PersonId, string Role = "Member");

public record SyncMembersRequest(List<long> PersonIds);
