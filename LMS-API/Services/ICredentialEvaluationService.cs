using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface ICredentialEvaluationService
{
    /// <summary>
    /// Submits a credential for evaluation with the specified provider.
    /// </summary>
    Task<CredentialEvaluation> SubmitEvaluationAsync(
        Guid applicationId,
        string evaluator,
        string documentUrl,
        string documentFileName,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves evaluation results from the provider and updates the record.
    /// </summary>
    Task<CredentialEvaluation?> GetEvaluationResultAsync(
        Guid evaluationId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the list of available evaluation providers.
    /// </summary>
    Task<IEnumerable<CredentialEvaluationProvider>> GetAvailableProvidersAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates that the applicant has a valid credential evaluation on file.
    /// </summary>
    Task<bool> HasValidEvaluationAsync(Guid applicationId, CancellationToken ct = default);
}

/// <summary>
/// Represents a credential evaluation provider (e.g., WES, ECE, CES).
/// </summary>
public record CredentialEvaluationProvider(
    string Code,
    string Name,
    string Website,
    string Description,
    bool IsActive);
