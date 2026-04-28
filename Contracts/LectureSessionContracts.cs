using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

// ==================== Requests ====================

public record GenerateSessionsRequest(
    List<Guid> TimetableSlotIds,
    DateOnly EndDate
);

public record GenerateBulkSessionsRequest(
    Guid AcademicSessionId,
    DateOnly EndDate
);

public record CreateManualSessionRequest(
    Guid CourseOfferingId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    List<Guid> LecturerIds,
    Guid? VenueId,
    string? Notes
);

public record ValidateConflictsRequest(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    List<Guid> LecturerIds,
    Guid? VenueId
);

// ==================== Responses ====================

public record LectureSessionResponse(
    Guid Id,
    Guid CourseOfferingId,
    string CourseName,
    Guid? TimetableSlotId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? VenueId,
    string? VenueName,
    List<LecturerInfo> Lecturers,
    string? Notes,
    bool IsManuallyCreated,
    DateTime CreatedAt,
    Guid CreatedBy,
    string CreatedByName
);

public record LecturerInfo(
    Guid Id,
    string Name,
    string Email
);

public record ConflictWarning(
    ConflictType Type,
    string Description,
    Guid ConflictingSessionId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime
);

public enum ConflictType
{
    LecturerConflict,
    VenueConflict
}

public record SessionGenerationResult(
    int TotalSessionsCreated,
    Dictionary<Guid, int> SessionsPerSlot,
    List<ConflictWarning> Conflicts
);

public record BulkSessionGenerationResult(
    int TotalSessionsCreated,
    Dictionary<Guid, CourseGenerationSummary> SessionsPerCourse,
    List<ConflictWarning> Conflicts
);

public record CourseGenerationSummary(
    string CourseName,
    int TotalSessions,
    Dictionary<Guid, int> SessionsPerSlot
);

public record CourseOfferingWithSlotCount(
    Guid Id,
    string CourseName,
    int TimetableSlotCount
);
