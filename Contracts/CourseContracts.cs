using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public record SimpleUserDto(Guid Id, string? Name, string? Email, Guid? DepartmentId = null, string? DepartmentName = null);

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
    Guid ProgramId,
    string Code,
    string Title,
    string? Description,
    int CreditUnits,
    Guid? LevelId,
    string? LevelName,
    Semester? Semester,
    bool IsActive,
    List<CourseOfferingDto> Offerings);

public record CreateCourseOfferingRequest(
    Guid ProgramId,
    Guid LevelId,
    Guid AcademicSessionId,
    Guid? LecturerId,
    int Semester);

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
    List<CreateCourseOfferingRequest> Offerings);

public record ToggleCourseStatusRequest(Guid Id);
