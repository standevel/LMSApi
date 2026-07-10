using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class CreditTransferService(LmsDbContext dbContext) : ICreditTransferService
{
    public async Task<TransferCreditResult> CalculateTransferableCreditsAsync(
        Guid programId,
        string? sourceCountryCode,
        int creditsEarned,
        decimal previousCGPA,
        CancellationToken ct = default)
    {
        var rule = await GetTransferRuleAsync(programId, sourceCountryCode, ct);
        if (rule == null)
        {
            return new TransferCreditResult(0, 0, 0, 0, false, "No credit transfer rule found for the selected program and country.");
        }
        // Check minimum CGPA requirement
        if (previousCGPA < rule.MinCGPA)
        {
            return new TransferCreditResult(0, rule.MaxTransferableCredits, rule.MaxTransferablePercentage, rule.CreditsPerYear, false,
                $"Minimum CGPA of {rule.MinCGPA} required for credit transfer consideration. Your CGPA is {previousCGPA}.");
        }

        if (creditsEarned <= 0)
        {
            return new TransferCreditResult(0, rule.MaxTransferableCredits, rule.MaxTransferablePercentage, rule.CreditsPerYear, false, "Credits earned must be greater than zero.");
        }

        // Calculate credits awarded based on years of study at source institution
        var calculatedCredits = creditsEarned * rule.CreditsPerYear;

        // Apply maximum transferable credits cap
        var transferableCredits = Math.Min(calculatedCredits, rule.MaxTransferableCredits);

        // Apply maximum transferable percentage (of total program credits)
        var programMapping = await GetProgramCreditMappingAsync(programId, ct);
        var totalProgramCredits = programMapping != null
            ? programMapping.CreditsPerLevel * 8m // assuming 8 levels (4 years × 2 semesters)
            : 240m; // default assumption
        var maxByPercentage = totalProgramCredits * rule.MaxTransferablePercentage / 100m;
        transferableCredits = Math.Min(transferableCredits, maxByPercentage);

        return new TransferCreditResult(
            Math.Round(transferableCredits, 2),
            rule.MaxTransferableCredits,
            rule.MaxTransferablePercentage,
            rule.CreditsPerYear,
            true,
            null);
    }

    public async Task<LevelSuggestionResult> SuggestStartingLevelAsync(
        Guid programId,
        decimal transferableCredits,
        CancellationToken ct = default)
    {
        var mapping = await GetProgramCreditMappingAsync(programId, ct);
        if (mapping == null)
        {
            return new LevelSuggestionResult(null, null, 0, false, "No program credit mapping found.");
        }

        if (transferableCredits <= 0)
        {
            return new LevelSuggestionResult(1, null, 0, true, "No transferable credits — starting at level 100.");
        }

        // Calculate how many levels the credits cover
        var levelsCovered = transferableCredits / mapping.CreditsPerLevel;
        var suggestedLevelOrder = (int)Math.Ceiling((double)levelsCovered) + 1;

        // Cap at maximum level
        var maxLevel = 8; // 8 semesters = 4 years
        if (suggestedLevelOrder > maxLevel)
        {
            suggestedLevelOrder = maxLevel;
        }

        // Enforce minimum — must complete at least MinCreditsAtLMS
        var minLevelOrder = (int)Math.Ceiling((double)(mapping.MinCreditsAtLMS / mapping.CreditsPerLevel)) + 1;
        if (suggestedLevelOrder < minLevelOrder)
        {
            suggestedLevelOrder = minLevelOrder;
        }

        // Calculate total program credits for remaining calculation
        var totalProgramCredits = mapping.CreditsPerLevel * 8m;

        // Look up level name
        var level = await dbContext.Levels
            .Where(l => l.ProgramId == programId && l.Order == suggestedLevelOrder)
            .FirstOrDefaultAsync(l => l.ProgramId == programId, ct);

        var creditsRemaining = Math.Max(0, totalProgramCredits - transferableCredits);

        return new LevelSuggestionResult(
            suggestedLevelOrder,
            level?.Name,
            creditsRemaining,
            true,
            null);
    }

    public async Task<CreditTransferRule?> GetTransferRuleAsync(
        Guid programId,
        string? sourceCountryCode,
        CancellationToken ct = default)
    {
        // Try country-specific rule first, fall back to generic (null country) rule
        if (!string.IsNullOrWhiteSpace(sourceCountryCode))
        {
            var rule = await dbContext.CreditTransferRules
                .FirstOrDefaultAsync(r => r.ProgramId == programId
                    && r.SourceCountryCode == sourceCountryCode
                    && r.IsActive, ct);
            if (rule != null) return rule;
        }

        return await dbContext.CreditTransferRules
            .FirstOrDefaultAsync(r => r.ProgramId == programId
                && r.SourceCountryCode == null
                && r.IsActive, ct);
    }

    public async Task<ProgramCreditMapping?> GetProgramCreditMappingAsync(
        Guid programId,
        CancellationToken ct = default)
    {
        return await dbContext.ProgramCreditMappings
            .FirstOrDefaultAsync(m => m.ProgramId == programId && m.IsActive, ct);
    }
}
