using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface ICreditTransferService
{
    /// <summary>
    /// Calculates transferable credits for a transfer applicant based on program rules and source country.
    /// </summary>
    Task<TransferCreditResult> CalculateTransferableCreditsAsync(
        Guid programId,
        string? sourceCountryCode,
        int creditsEarned,
        decimal previousCGPA,
        CancellationToken ct = default);

    /// <summary>
    /// Suggests a starting level based on transferable credits and program credit mapping.
    /// </summary>
    Task<LevelSuggestionResult> SuggestStartingLevelAsync(
        Guid programId,
        decimal transferableCredits,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the credit transfer rules for a program and optional source country.
    /// </summary>
    Task<CreditTransferRule?> GetTransferRuleAsync(
        Guid programId,
        string? sourceCountryCode,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the program credit mapping for a program.
    /// </summary>
    Task<ProgramCreditMapping?> GetProgramCreditMappingAsync(
        Guid programId,
        CancellationToken ct = default);
}

public sealed record TransferCreditResult(
    decimal TransferableCredits,
    decimal MaxAllowedCredits,
    decimal MaxAllowedPercentage,
    decimal CreditsPerYear,
    bool IsEligible,
    string? Reason);

public sealed record LevelSuggestionResult(
    int? SuggestedLevel,
    string? SuggestedLevelName,
    decimal CreditsRemainingAtLMS,
    bool IsEligible,
    string? Reason);

public sealed record DirectEntryPointsResult(
    double Points,
    bool IsPassing,
    string? Reason);

/// <summary>
/// Represents a single grade entry in a GradingScale's GradesJson.
/// </summary>
public sealed class GradingScaleGradeEntry
{
    public string Grade { get; set; } = string.Empty;
    public double MinScore { get; set; }
    public double MaxScore { get; set; }
    public double Points { get; set; }
}
