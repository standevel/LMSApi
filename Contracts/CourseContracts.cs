using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public record SimpleUserDto(Guid Id, string? Name, string? Email, Guid? DepartmentId = null, string? DepartmentName = null);

// ─── Offering sub-DTOs ───────────────────────────────────────────────────────

/// <summary>One program+level pair attached to a CourseOffering.</summary>
public record OfferingProgramDto(
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName);

/// <summary>One lecturer assigned to a CourseOffering with their role.</summary>
public record OfferingLecturerDto(
    Guid LecturerId,
    string? LecturerName,
    CourseLecturerRole Role);

// ─── CourseOffering ───────────────────────────────────────────────────────────

public record CourseOfferingDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    List<OfferingProgramDto> Programs,
    List<OfferingLecturerDto> Lecturers);

// ─── Course ───────────────────────────────────────────────────────────────────

public record CourseDto(
    Guid Id,
    Guid ProgramId,
    string? ProgramName,
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    Guid? LevelId,
    string? LevelName,
    Semester? Semester,
    bool IsActive,
    List<CourseOfferingDto> Offerings);

// ─── Create / Update requests ─────────────────────────────────────────────────

/// <summary>Creates a course offering with program+level attached.</summary>
public record CreateCourseOfferingRequest(
    Guid AcademicSessionId,
    int Semester,
    Guid? ProgramId = null,
    Guid? LevelId = null);

/// <summary>Creates a course. If offerings include program+level, they will be attached automatically.</summary>
public record CreateCourseRequest(
    Guid ProgramId,
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    Guid? LevelId,
    Semester? Semester,
    List<CreateCourseOfferingRequest> Offerings);

public record UpdateCourseRequest(
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    Guid? LevelId,
    Semester? Semester,
    List<CreateCourseOfferingRequest> Offerings,
    Guid? ProgramId = null);

public record ToggleCourseStatusRequest(Guid Id);

// ─── Program attachment ───────────────────────────────────────────────────────

public record AttachOfferingProgramRequest(Guid ProgramId, Guid LevelId);

public record DetachOfferingProgramRequest(Guid ProgramId, Guid LevelId);

// ─── Lecturer assignment ──────────────────────────────────────────────────────

/// <summary>Assigns a single lecturer with a role to an offering.</summary>
public record AssignOfferingLecturerRequest(Guid LecturerId, CourseLecturerRole Role);

/// <summary>Removes a lecturer from an offering.</summary>
public record RemoveOfferingLecturerRequest(Guid LecturerId);

/// <summary>One offering → lecturer assignment pair. Used in bulk assign.</summary>
public record OfferingAssignment(Guid OfferingId, Guid? LecturerId, List<Guid>? CoLecturerIds = null);

/// <summary>Assigns (or clears) lecturers for multiple offerings in one call.</summary>
public record BulkAssignLecturersRequest(List<OfferingAssignment> Assignments);

/// <summary>Result per offering after a bulk assign.</summary>
public record BulkAssignLecturersResult(
    List<CourseOfferingDto> Updated,
    List<string> Errors);
