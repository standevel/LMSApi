using System;

namespace LMS.Api.Contracts;

public sealed class StudentImportRowDto
{
    public string? StartTime { get; set; }
    public string? CompletionTime { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? MatricNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PersonalEmail { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianEmail { get; set; }
    public string? Level { get; set; }
    public string? AcademicProgram { get; set; }
    public string? Sponsor { get; set; }
    public string? JambNumber { get; set; }
    public int? JambScore { get; set; }
}

public sealed record StudentImportResponse(
    Guid BulkOperationId,
    int TotalRows,
    int ProcessedRows,
    int FailedRows,
    string Status,
    IEnumerable<StudentImportErrorDto> Errors);

public sealed record StudentImportErrorDto(
    int RowNumber,
    string? Email,
    string Reason);

public sealed record StudentImportSummaryDto(
    Guid BulkOperationId,
    string Status,
    int TotalRows,
    int ProcessedRows,
    int FailedRows,
    string? ErrorMessage,
    DateTime UpdatedAt);
