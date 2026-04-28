using System;

namespace LMS.Api.Contracts;

// ==================== Requests ====================

public record CreateLectureTimetableSlotRequest(
    Guid CourseOfferingId,
    Guid? LecturerId,
    Guid? VenueId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes
);

public record UpdateLectureTimetableSlotRequest(
    Guid? LecturerId,
    Guid? VenueId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Notes
);

public record DetectConflictsRequest(
    Guid CourseOfferingId,
    Guid? LecturerId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? VenueId
);

public record AutoResolveConflictRequest(
    Guid TimetableSlotId,
    string ResolutionStrategy // "reschedule", "reassign_lecturer", "split_group"
);

// ==================== Responses ====================

public record LectureTimetableSlotResponse(
    Guid Id,
    Guid CourseOfferingId,
    string CourseName,
    Guid? LecturerId,
    string? LecturerName,
    Guid? VenueId,
    string? VenueName,
    int DayOfWeek,
    string DayName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int DurationMinutes,
    string? Notes,
    DateOnly CreatedDate,
    DateOnly UpdatedDate
);

public record ConflictResponse(
    Guid Id,
    string Title,
    string Description,
    Guid ConflictingSlotId,
    Guid ConflictingLecturerId,
    string ConflictingLecturerName,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ConflictType // "lecturer_double_booking", "venue_double_booking"
);

public record ConflictDetectionResult(
    bool HasConflicts,
    List<ConflictResponse> Conflicts,
    List<LectureTimetableSlotResponse> AffectedSlots
);

public record AutoResolveResult(
    bool Success,
    string Message,
    LectureTimetableSlotResponse? UpdatedSlot,
    List<LectureTimetableSlotResponse> ModifiedSlots
);

public record TimetableWeekViewResponse(
    int WeekNumber,
    DateOnly WeekStartDate,
    List<LectureTimetableSlotResponse> Slots,
    List<ConflictResponse> WeekConflicts
);

public record LecturerTimetableResponse(
    Guid LecturerId,
    string LecturerName,
    List<LectureTimetableSlotResponse> Slots,
    List<ConflictResponse> Conflicts,
    int TotalHoursPerWeek
);
