using System;

namespace LMS.Api.Contracts;

public sealed class StudentImportRequest
{
    public IFormFile? CsvFile { get; set; }
    public Guid? DefaultSessionId { get; set; }
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
