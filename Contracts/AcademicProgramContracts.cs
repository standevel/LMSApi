using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public record FacultyDto(
    Guid Id,
    string Name,
    string Label,
    DateOnly CreatedDate,
    DateOnly UpdatedDate);

public record CreateFacultyRequest(
    string Name,
    string Label);

public record UpdateFacultyRequest(
    string Name,
    string Label);

public record DepartmentDto(
    Guid Id,
    string Name,
    string Code,
    FacultyDto Faculty,
    DateOnly CreatedDate,
    DateOnly UpdatedDate);

public record CreateDepartmentRequest(
    string Name,
    string Code,
    Guid FacultyId);

public record UpdateDepartmentRequest(
    string Name,
    string Code,
    Guid FacultyId);

public record AcademicProgramDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string DegreeAwarded,
    FacultyDto Faculty,
    ProgramType Type,
    int DurationYears,
    bool IsActive,
    List<AcademicLevelDto> Levels,
    int MinJambScore,
    int MaxAdmissions,
    string RequiredJambSubjectsJson,
    string RequiredOLevelSubjectsJson);

public record AcademicLevelDto(
    Guid Id,
    Guid ProgramId,
    string Name,
    int Order,
    List<LevelSemesterConfigDto> Semesters);

public record LevelSemesterConfigDto(
    Guid Id,
    Semester Semester,
    int MaxCreditLoad);

public record CreateAcademicProgramRequest(
    string Name,
    string Code,
    string? Description,
    string DegreeAwarded,
    Guid FacultyId,
    ProgramType Type,
    int DurationYears,
    List<CreateAcademicLevelRequest> Levels,
    int MinJambScore = 150,
    int MaxAdmissions = 100,
    string RequiredJambSubjectsJson = "[]",
    string RequiredOLevelSubjectsJson = "[]");

public record CreateAcademicLevelRequest(
    string Name,
    int Order,
    List<CreateLevelSemesterConfigRequest> Semesters);

public record CreateLevelSemesterConfigRequest(
    Semester Semester,
    int MaxCreditLoad);

public record UpdateAcademicProgramRequest(
    string Name,
    string Code,
    string? Description,
    string DegreeAwarded,
    Guid FacultyId,
    ProgramType Type,
    int DurationYears,
    int MinJambScore,
    int MaxAdmissions,
    string RequiredJambSubjectsJson,
    string RequiredOLevelSubjectsJson);

public record EnrollStudentRequest(
    Guid StudentId,
    Guid ProgramId,
    Guid LevelId,
    Guid AcademicSessionId,
    Guid CurriculumId);

public record GetProgramEnrollmentsRequest(Guid Id);

public record ToggleAcademicProgramStatusRequest(Guid Id);

public record EnrollmentDto(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName,
    Guid UserId,
    string StudentName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    Guid CurriculumId,
    string CurriculumName,
    DateTime EnrolledAtUtc);
