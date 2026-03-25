using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public record CurriculumDto(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    Guid AdmissionSessionId,
    string AdmissionSessionName,
    string Name,
    int MinCreditUnitsForGraduation,
    CurriculumStatus Status,
    bool IsActive,
    List<CurriculumCourseDto> Courses);

public record CurriculumCourseDto(
    Guid Id,
    Guid LevelId,
    string LevelName,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    Semester Semester,
    CourseCategory Category);

public record CreateCurriculumRequest(
    Guid AdmissionSessionId,
    string Name,
    int MinCreditUnitsForGraduation);

public record AddCurriculumCourseRequest(
    Guid LevelId,
    Guid CourseId,
    Semester Semester,
    CourseCategory Category,
    int CreditUnits);

public record UpdateCurriculumCourseRequest(
    Guid LevelId,
    Semester Semester,
    CourseCategory Category,
    int CreditUnits);

public record BulkAddCurriculumCourseRequest(
    Guid LevelId,
    Semester Semester,
    List<CourseSelectionDto> Selections);

public record CourseSelectionDto(
    Guid CourseId,
    CourseCategory Category,
    int CreditUnits);

public record CurriculumSummaryDto(
    Guid Id,
    string Name,
    string AdmissionSessionName,
    CurriculumStatus Status,
    bool IsActive);

public record CloneCurriculumRequest(string NewName);

public record AddCoursePrerequisiteRequest(Guid PrerequisiteCourseId, PrerequisiteType Type);

public record CurriculumHistoryDto(
    Guid Id,
    string Action,
    string? Changes,
    string? PerformedBy,
    DateTime Timestamp);
