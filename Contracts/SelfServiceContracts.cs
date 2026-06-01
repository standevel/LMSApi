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
    string Status);

public record CreateRegistrationRequest(
    Guid StudentId,
    Guid CourseOfferingId);

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
    Guid StudentId,
    Guid CurrentCourseOfferingId,
    Guid NewCourseOfferingId);

public record ScheduleDto(
    Guid Id,
    string StudentName,
    List<ScheduleCourseDto> Courses,
    DateTime GeneratedAt);

public record ScheduleCourseDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    string Venue,
    string TimeSlot,
    Guid LecturerId,
    string LecturerName);

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