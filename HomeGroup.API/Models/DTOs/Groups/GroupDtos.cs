using HomeGroup.API.Models.DTOs.PersonStatuses;

namespace HomeGroup.API.Models.DTOs.Groups;

public record CreateGroupRequest(string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, string? TelegramGroupId = null, string? MeetingEndTime = null);

public record UpdateGroupRequest(string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, bool IsActive, string? TelegramGroupId = null, string? MeetingEndTime = null);

public record GroupResponse(long Id, string Name, string? Description, string Color, string? MeetingDay, string? MeetingTime, string? Location, long? LeaderId, string? LeaderName, bool IsActive, int MemberCount, string? TelegramGroupId = null, string? MeetingEndTime = null, string? NextMeetingDate = null);

public record BookRoomRequest(string Date, long? RoomId, bool AutoBook);

public record AddMemberRequest(long PersonId, string Role = "Member");

public record SyncMembersRequest(List<long> PersonIds);

public record SetNextMeetingRequest(string? Date, string? OldDate = null, string? Time = null);

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
    string? OversightUserName = null,
    DateTime? JoinedAt = null,
    bool IsFormer = false,
    DateTime? LeftAt = null);

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

public record AllNeedsDto(
    long Id, long HomeGroupId, string GroupName, string GroupColor,
    string SubjectName, string Description, string Status, string CreatedAt,
    long? PersonId, long? UserId
);

public record CreateGroupNeedRequest(string SubjectName, string Description, long? PersonId = null, long? UserId = null);

public record UpdateGroupNeedRequest(string SubjectName, string Description, string Status, long? PersonId = null, long? UserId = null);

public record SetMemberJoinedAtRequest(long? PersonId, long? UserId, DateTime JoinedAt);
public record SetMemberLeftAtRequest(long? PersonId, long? UserId, DateTime LeftAt);

public record TransferMemberRequest(long? PersonId, long? UserId, long ToGroupId);

public record GroupMemberHistoryDto(long Id, long? PersonId, string? PersonName, long? UserId, string? UserName, long HomeGroupId, string HomeGroupName, DateTime JoinedAt, DateTime? LeftAt);

public record TimelineEventDto(
    string Type,
    DateTime Date,
    long? GroupId = null,
    string? GroupName = null,
    string? GroupColor = null,
    string? StatusName = null,
    string? StatusColor = null,
    string? OldStatusName = null,
    string? OldStatusColor = null,
    string? OversightName = null,
    string? OldOversightName = null);
