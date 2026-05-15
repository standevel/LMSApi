using System;

namespace LMS.Api.Data.Entities;

public sealed class CredentialEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public AdmissionApplication Application { get; set; } = null!;

    // Evaluation provider info
    public string Evaluator { get; set; } = string.Empty;  // e.g., "WES", "ECE", "CES"
    public string EvaluationReportId { get; set; } = string.Empty;
    public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;

    // Equivalency results
    public string? EquivalencyDegree { get; set; }         // e.g., "Bachelor's", "Master's"
    public string? EquivalencyMajor { get; set; }           // e.g., "Computer Science"
    public decimal? EquivalencyGPA { get; set; }
    public string? EquivalencyScale { get; set; }           // e.g., "4.0"
    public string? Notes { get; set; }

    // Document attachment
    public string? ReportDocumentUrl { get; set; }
    public string? ReportDocumentFileName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
