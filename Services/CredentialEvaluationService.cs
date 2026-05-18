using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

/// <summary>
/// Service for managing credential evaluations for international students.
/// Supports integration with external evaluators (WES, ECE, CES, etc.).
/// </summary>
public sealed class CredentialEvaluationService(LmsDbContext dbContext) : ICredentialEvaluationService
{
    public async Task<CredentialEvaluation> SubmitEvaluationAsync(
        Guid applicationId,
        string evaluator,
        string documentUrl,
        string documentFileName,
        CancellationToken ct = default)
    {
        var evaluation = new CredentialEvaluation
        {
            ApplicationId = applicationId,
            Evaluator = evaluator,
            ReportDocumentUrl = documentUrl,
            ReportDocumentFileName = documentFileName,
            EvaluationDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.CredentialEvaluations.Add(evaluation);
        await dbContext.SaveChangesAsync(ct);
        return evaluation;
    }

    public async Task<CredentialEvaluation?> GetEvaluationResultAsync(
        Guid evaluationId,
        CancellationToken ct = default)
    {
        var evaluation = await dbContext.CredentialEvaluations
            .FirstOrDefaultAsync(e => e.Id == evaluationId, ct);

        return evaluation;
    }

    public async Task<IEnumerable<CredentialEvaluationProvider>> GetAvailableProvidersAsync(CancellationToken ct = default)
    {
        // Return default providers — external provider config can be added later
        return new[]
        {
            new CredentialEvaluationProvider("WES", "World Education Services", "https://www.wes.org", "Credential evaluation for U.S. and Canadian institutions", true),
            new CredentialEvaluationProvider("ECE", "Educational Credentials Evaluators", "https://www.ece.org", "Credential evaluation services", true),
            new CredentialEvaluationProvider("CES", "Credential Evaluation Service", "https://www.ces.edu", "Academic credential evaluation", true)
        };
    }

    public async Task<bool> HasValidEvaluationAsync(Guid applicationId, CancellationToken ct = default)
    {
        return await dbContext.CredentialEvaluations
            .AnyAsync(e => e.ApplicationId == applicationId
                && !string.IsNullOrEmpty(e.EquivalencyDegree));
    }
}

/// <summary>
/// Entity for storing credential evaluation providers.
/// </summary>
public sealed class CredentialEvaluationProviderEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
