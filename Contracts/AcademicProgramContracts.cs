using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public record FacultyDto(
    Guid Id,
    string Name,
    string Label,
    Guid? DeanId,
    string? DeanName,
    DateOnly CreatedDate,
    DateOnly UpdatedDate);

public record CreateFacultyRequest(
    string Name,
    string Label,
    Guid? DeanId = null);

public record UpdateFacultyRequest(
    string Name,
    string Label,
    Guid? DeanId = null);

public record DepartmentDto(
    Guid Id,
    string Name,
    string Code,
    Guid FacultyId,
    string FacultyName,
    Guid? HeadId,
    string? HeadName,
    FacultyDto Faculty,
    DateOnly CreatedDate,
    DateOnly UpdatedDate);

public record CreateDepartmentRequest(
    string Name,
    string Code,
    Guid FacultyId,
    Guid? HeadId = null);

public record UpdateDepartmentRequest(
    string Name,
    string Code,
    Guid FacultyId,
    Guid? HeadId = null);

public record AcademicProgramDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string DegreeAwarded,
    DepartmentDto Department,
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
    Guid DepartmentId,
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
    Guid DepartmentId,
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

/// <summary>Lightweight program DTO for self-service program switch selection.</summary>
public record AcademicProgramSummaryDto(
    Guid Id,
    string Name,
    string Code,
    string? DepartmentName,
    string? FacultyName);

