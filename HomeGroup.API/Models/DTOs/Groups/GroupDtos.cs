using HomeGroup.API.Models.DTOs.PersonStatuses;

namespace HomeGroup.API.Models.DTOs.Groups;

public record CreateGroupRequest(string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, string? TelegramGroupId = null, string? MeetingEndTime = null);

public record UpdateGroupRequest(string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, bool IsActive, string? TelegramGroupId = null, string? MeetingEndTime = null);

public record GroupResponse(long Id, string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, string? LeaderName, bool IsActive, int MemberCount, string? TelegramGroupId = null, string? MeetingEndTime = null);

public record BookRoomRequest(string Date, long? RoomId, bool AutoBook);

public record AddMemberRequest(long PersonId, string Role = "Member");

public record SyncMembersRequest(List<long> PersonIds);

public record SetNextMeetingRequest(string? Date, string? OldDate = null);

public record GroupCustomFieldDto(long Id, string Name);

public record CreateGroupCustomFieldRequest(string Name);

public record MemberRoleTagDto(string Name, string Color);

public record GroupMemberResponse(
    long Id,
    string Name,
    string? LastName,
    string? Phone,
    string? Email,
    string? Notes,
    PersonStatusDto? Status,
    long? PrimaryGroupId,
    string? PrimaryGroupName,
    string? PrimaryGroupColor,
    DateTime CreatedAt,
    bool IsAdmin,
    long? UserId,
    MemberRoleTagDto? RoleTag,
    string? OversightUserName = null);

public record NotifSettingsDto(
    bool EventSevenDays,
    bool EventDay,
    bool Conflict,
    bool ConflictResolved,
    bool AttendanceAsk);

public record UpdateNotifSettingsRequest(
    bool EventSevenDays,
    bool EventDay,
    bool Conflict,
    bool ConflictResolved,
    bool AttendanceAsk);

public record GroupNeedDto(long Id, string SubjectName, string Description, string Status, DateTime CreatedAt, long? PersonId = null, long? UserId = null);

public record CreateGroupNeedRequest(string SubjectName, string Description, long? PersonId = null, long? UserId = null);

public record UpdateGroupNeedRequest(string SubjectName, string Description, string Status, long? PersonId = null, long? UserId = null);
