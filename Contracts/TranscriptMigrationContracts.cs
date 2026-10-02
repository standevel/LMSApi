using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public sealed class TranscriptCourseSourceItem
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public decimal CreditUnits { get; set; }
    public string Grade { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string? TermOrSession { get; set; }
}

public sealed class TranscriptMappingPreviewItem
{
    public string SourceCourseCode { get; set; } = string.Empty;
    public string SourceCourseTitle { get; set; } = string.Empty;
    public decimal SourceCredits { get; set; }
    public string SourceGrade { get; set; } = string.Empty;
    public decimal? SourceScore { get; set; }
    public string? TermOrSession { get; set; }

    // Target course mapping
    public Guid? TargetCourseId { get; set; }
    public string? TargetCourseCode { get; set; }
    public string? TargetCourseTitle { get; set; }
    public decimal TargetCredits { get; set; }
    public bool AutoMapped { get; set; }
    public string Status { get; set; } = "Unmapped"; // "Mapped" | "Unmapped"
    public string? MappingNotes { get; set; }
}

public sealed class PreviewTranscriptMigrationRequest
{
    public Guid StudentId { get; set; }
    public List<TranscriptCourseSourceItem> Courses { get; set; } = new();
}

public sealed class PreviewTranscriptMigrationResponse
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public Guid? LevelId { get; set; }
    public string? LevelName { get; set; }
    public bool IsDirectEntry { get; set; }
    public List<TranscriptMappingPreviewItem> Mappings { get; set; } = new();
    public int TotalSourceCourses { get; set; }
    public int AutoMappedCount { get; set; }
    public int UnmappedCount { get; set; }
    public decimal TotalSourceCredits { get; set; }
    public decimal TotalMappedCredits { get; set; }
}

public sealed class CommitTranscriptMigrationItem
{
    public string SourceCourseCode { get; set; } = string.Empty;
    public string SourceCourseTitle { get; set; } = string.Empty;
    public decimal SourceCredits { get; set; }
    public string Grade { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string? TermOrSession { get; set; }
    public Guid TargetCourseId { get; set; }
    public bool SaveAsEquivalencyRule { get; set; } = true;
}

public sealed class CommitTranscriptMigrationRequest
{
    public Guid StudentId { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public string? SourceInstitution { get; set; }
    public List<CommitTranscriptMigrationItem> Items { get; set; } = new();
}

public sealed class CommitTranscriptMigrationResponse
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int MigratedCount { get; set; }
    public decimal TotalCreditsTransferred { get; set; }
    public List<MigratedCourseResultDto> MigratedCourses { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public sealed class MigratedCourseResultDto
{
    public Guid CourseOfferingId { get; set; }
    public Guid TargetCourseId { get; set; }
    public string TargetCourseCode { get; set; } = string.Empty;
    public string TargetCourseTitle { get; set; } = string.Empty;
    public decimal CreditUnits { get; set; }
    public string Semester { get; set; } = string.Empty;
    public string AcademicSessionName { get; set; } = string.Empty;
    public string LetterGrade { get; set; } = string.Empty;
    public decimal GradePoints { get; set; }
    public decimal TotalScore { get; set; }
    public string SourceCourseCode { get; set; } = string.Empty;
    public string SourceCourseTitle { get; set; } = string.Empty;
    public string? SourceInstitution { get; set; }
    public DateTime MigratedAt { get; set; }
}

public sealed record UpdateStudentDirectEntryRequest(
    bool IsDirectEntry,
    string? DirectEntryQualification = null,
    string? DirectEntryInstitution = null,
    decimal? DirectEntryPoints = null
);

public sealed record BatchUpdateStudentDirectEntryRequest(
    IReadOnlyList<Guid> StudentIds,
    bool IsDirectEntry,
    string? DirectEntryQualification = null
);

public sealed record UpdateStudentDirectEntryResult(
    Guid StudentId,
    string StudentName,
    string? StudentNumber,
    bool IsDirectEntry,
    string? DirectEntryQualification,
    bool Success,
    string? Message
);

public sealed record BatchUpdateStudentDirectEntryResponse(
    int TotalRequested,
    int UpdatedCount,
    bool IsDirectEntry,
    string Message,
    IReadOnlyList<UpdateStudentDirectEntryResult>? Results = null
);

public sealed class TranscriptTargetCourseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal CreditUnits { get; set; }
    public string? DepartmentName { get; set; }
    public string? ProgramName { get; set; }
    public string? Semester { get; set; }
    public string? LevelName { get; set; }
}


