using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

// --- Preview DTO ---

public record CourseCatalogPreviewRow(
    Guid Id,
    string ProgramName,
    int Level,
    Semester Semester,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    CourseCategory Status,
    int? LectureHours,
    int? PracticalHours,
    string? Error
);

public record CourseCatalogImportPreview(
    Guid UploadId,
    string FileName,
    string? ProgramName,
    string? AcademicSession,
    List<CourseCatalogPreviewRow> Rows,
    int TotalRows
);

// --- Request DTOs ---

public record UploadCourseCatalogRequest(
    Guid ProgramId,
    Guid? AcademicSessionId,
    string FileName
);

public record ApplyCourseCatalogImportRequest(
    Guid UploadId,
    Guid? ProgramId,
    IEnumerable<Guid>? ProgramIds,
    Guid? CurriculumId,
    string? CurriculumName,
    Guid? AcademicSessionId
);

// --- Result DTOs ---

public record CourseCatalogImportResult(
    Guid UploadId,
    bool Success,
    int CoursesCreated,
    int CoursesUpdated,
    int CoursesSkipped,
    int CurriculumCoursesAdded,
    int CurriculumCoursesUpdated,
    string? CreatedCurriculumId,
    List<ImportErrorRow> Errors
);

public record ImportErrorRow(
    int RowNumber,
    string CourseCode,
    string CourseTitle,
    string Error
);
