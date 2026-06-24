using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public record CourseRegistrationDto(
    Guid Id,
    Guid StudentId,
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    DateTime RegisteredAt,
    DateTime? DroppedAt,
    string Status,
    int CreditUnits);

public record CreateRegistrationRequest(
    Guid CourseOfferingId);

public record RegistrationBlockerDto(string Code, string Message);

public record RegistrationOfferingDto(
    Guid Id,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    int Semester,
    string LecturerName,
    IReadOnlyList<string> Schedule,
    bool IsRegistered,
    bool CanRegister,
    IReadOnlyList<RegistrationBlockerDto> Blockers,
    bool IsCarryover = false);

public record RegistrationSummaryDto(
    Guid StudentId,
    string StudentName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int RegisteredCredits,
    int MaxCredits,
    IReadOnlyList<CourseRegistrationDto> RegisteredCourses,
    IReadOnlyList<RegistrationOfferingDto> AvailableOfferings,
    string ProgramName = "",
    string LevelName = "",
    string RegistrationStrategy = "Single",
    int MinCredits = 0);

public record WaitlistDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    int WaitlistRank,
    string Status,
    DateTime JoinedAtUtc);

public record JoinWaitlistRequest(
    Guid StudentId,
    Guid CourseOfferingId);

public record CourseSwapRequestDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid CurrentCourseOfferingId,
    string CurrentCourseCode,
    Guid NewCourseOfferingId,
    string NewCourseCode,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessedAt,
    string? ProcessedByName,
    string? Remarks);

public record CreateCourseSwapRequest(
    Guid CurrentCourseOfferingId,
    Guid NewCourseOfferingId);

public record CourseSwapOptionDto(
    Guid Id,
    string CourseCode,
    string CourseTitle,
    string SessionName);

public record CourseSwapOptionsDto(
    List<CourseSwapOptionDto> CurrentCourses,
    List<CourseSwapOptionDto> AvailableCourses);

public record ScheduleDto(
    Guid Id,
    Guid StudentId,
    Guid AcademicSessionId,
    Guid CourseOfferingId,
    string? CourseCode,
    string? CourseTitle,
    int? DayOfWeek,
    string? StartTime,
    string? EndTime,
    string? Venue,
    Guid? LecturerId,
    string? LecturerName,
    bool IsOnline = false,
    string? OnlineMeetingJoinUrl = null);

public record StudentExamDto(
    Guid Id,
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    string Title,
    string? Description,
    DateTime? ExamDate,
    string? Venue,
    decimal MaxMarks,
    bool IsOnline,
    Guid? QuizId);

public record ScheduleAdjustmentRequestDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Reason,
    string DesiredSlotDetails,
    string Status,
    DateTime RequestedDate,
    DateTime CreatedAt);

public record CreateScheduleAdjustmentRequest(
    Guid StudentId,
    string Reason,
    string DesiredSlotDetails);

public record PrerequisiteOverrideDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid CourseOfferingId,
    string CourseCode,
    string Reason,
    string Status,
    DateTime RequestedAt,
    DateTime? ApprovedAt,
    string? ApprovedByName,
    string? RejectionReason);

public record CreatePrerequisiteOverrideRequest(
    Guid StudentId,
    Guid CourseOfferingId,
    string Reason);

// ==================== PROGRAM SWITCH ====================

/// <summary>Request payload to initiate a program switch.</summary>
public record CreateProgramSwitchRequest(
    Guid TargetProgramId,
    string Reason);

/// <summary>Payload used by HoD, Dean, or Admin to approve or reject a switch request.</summary>
public record ReviewProgramSwitchRequest(
    bool Approved,
    string? Notes,
    string? RejectionReason);

/// <summary>Full detail DTO for a program switch request.</summary>
public record ProgramSwitchRequestDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string StudentNumber,
    Guid FromProgramId,
    string FromProgramName,
    Guid ToProgramId,
    string ToProgramName,
    string Reason,
    string Status,
    int StatusCode,
    // Document
    string? JambDocumentUrl,
    string? JambDocumentFileName,
    DateTime? JambDocumentUploadedAt,
    // HoD
    string? HoDReviewedByName,
    DateTime? HoDReviewedAt,
    string? HoDNotes,
    // Dean
    string? DeanReviewedByName,
    DateTime? DeanReviewedAt,
    string? DeanNotes,
    // Admin
    string? AdminCompletedByName,
    DateTime? AdminCompletedAt,
    string? AdminNotes,
    // Rejection
    string? RejectionReason,
    string? RejectedByName,
    DateTime? RejectedAt,
    // Audit
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Summary DTO for list views (queue listings).</summary>
public record ProgramSwitchRequestSummaryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string StudentNumber,
    string FromProgramName,
    string ToProgramName,
    string Status,
    bool HasJambDocument,
    DateTime CreatedAt);
