using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public record SimpleUserDto(Guid Id, string? Name, string? Email);

public record CourseOfferingDto(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    Guid? LecturerId,
    string? LecturerName,
    int Semester);

public record CourseDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    bool IsActive,
    List<CourseOfferingDto> Offerings);

public record CreateCourseOfferingRequest(
    Guid ProgramId,
    Guid LevelId,
    Guid AcademicSessionId,
    Guid? LecturerId,
    int Semester);

public record CreateCourseRequest(
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    List<CreateCourseOfferingRequest> Offerings);

public record UpdateCourseRequest(
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    List<CreateCourseOfferingRequest> Offerings);

public record ToggleCourseStatusRequest(Guid Id);
