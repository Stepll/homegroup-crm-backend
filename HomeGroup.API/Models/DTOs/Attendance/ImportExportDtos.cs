namespace HomeGroup.API.Models.DTOs.Attendance;

// ===== Preview response =====

public record ImportPreviewResponse(
    string ImportId,
    DateTime ExpiresAt,
    List<ImportSheetPreview> Sheets,
    List<GroupOption> AvailableGroups);

public record GroupOption(long Id, string Name);

public record ImportSheetPreview(
    int SheetIndex,
    string SheetName,
    long? MatchedGroupId,
    string? MatchedGroupName,
    List<ImportDatePreview> Dates,
    List<ImportPersonPreview> People,
    List<ImportConflict> Conflicts,
    ImportChangesSummary Changes);

public record ImportDatePreview(
    int ColIndex,
    string Date,
    bool ExistedInDb,
    bool FileCancelled,
    bool? DbCancelled,
    int FileGuests,
    int? DbGuests,
    string? FileNotes,
    string? DbNotes);

public record ImportPersonPreview(
    int RowIndex,
    string Name,
    string? LastName,
    string? FileIdHint,
    long? MatchedPersonId,
    long? MatchedUserId,
    string MatchType,
    List<PersonMatchSuggestion> Suggestions,
    string? StatusFromFile,
    string? OversightFromFile,
    string? JoinedAtFromFile,
    string? DetectedLeftAt,
    int FilePresentCount,
    int FileAbsentCount);

public record PersonMatchSuggestion(
    long? PersonId,
    long? UserId,
    string Name,
    string? LastName,
    string? PrimaryGroupName,
    bool IsAdmin);

public record ImportChangesSummary(
    int NewAttendanceRecords,
    int UpdatedAttendanceRecords,
    int NewMeetings,
    int CancelledMeetings,
    int NewMetaRecords,
    int UpdatedMetaRecords);

public record ImportConflict(
    int Index,
    string Type,
    string Date,
    int? PersonRowIndex,
    string? PersonName,
    string? FileValue,
    string? DbValue);

// ===== Apply request =====

public record ImportApplyRequest(
    string ImportId,
    List<ImportSheetDecision> Sheets);

public record ImportSheetDecision(
    int SheetIndex,
    long? GroupId,
    List<PersonDecision> PersonDecisions,
    List<ConflictResolution> ConflictResolutions,
    bool ImportStatus,
    bool ImportOversight,
    bool ImportJoinedAt,
    bool ImportLeftAt);

public record PersonDecision(
    int RowIndex,
    string Action,
    long? TargetPersonId,
    long? TargetUserId);

public record ConflictResolution(
    string Type,
    string Date,
    int? PersonRowIndex,
    bool UseFile);

public record ImportApplyResponse(
    int SheetsProcessed,
    int AttendanceCreated,
    int AttendanceUpdated,
    int MetaCreated,
    int MetaUpdated,
    int PeopleCreated,
    int MembershipsCreated,
    int MembershipsLeft);
