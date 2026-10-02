using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Contracts;

public record ScholarshipDto(
    Guid Id,
    string Name,
    string Description,
    ScholarshipType Type,
    ScholarshipCoverageFlags CoverageFlags,
    decimal PercentageCovered,
    Guid? SponsorOrganizationId,
    string? SponsorOrganizationName,
    int? MinJambScore,
    int? MaxJambScore,
    bool IsActive,
    DateTime CreatedAt);

public record CreateScholarshipRequest(
    [Required] string Name,
    string Description,
    ScholarshipType Type,
    ScholarshipCoverageFlags CoverageFlags,
    decimal PercentageCovered,
    Guid? SponsorOrganizationId,
    string? SponsorOrganizationName,
    int? MinJambScore,
    int? MaxJambScore,
    bool IsActive = true);

public record UpdateScholarshipRequest(
    [Required] string Name,
    string Description,
    ScholarshipType Type,
    ScholarshipCoverageFlags CoverageFlags,
    decimal PercentageCovered,
    Guid? SponsorOrganizationId,
    string? SponsorOrganizationName,
    int? MinJambScore,
    int? MaxJambScore,
    bool IsActive);

public record StudentScholarshipDto(
    Guid Id,
    Guid StudentId,
    string? StudentName,
    string? StudentIdentifier,
    Guid ScholarshipId,
    Guid SessionId,
    decimal CalculatedAmount,
    DateTime CreatedAt,
    ScholarshipDto Scholarship);

public record AssignScholarshipRequest(
    [Required] Guid StudentId,
    [Required] Guid ScholarshipId,
    [Required] Guid SessionId);

public sealed record StudentFeedingEntitlementDto(
    bool HasActiveScholarship,
    decimal CoveragePercent,
    bool IsFeedingFullyCovered,
    Dictionary<string, bool>? DailyMealWindowsClaimed = null,
    List<string>? AvailableMealWindowsRightNow = null,
    string? CurrentSessionName = null,
    string? RolloverSummary = null
);

public sealed record ScholarshipMealClaimResult(
    bool CanClaim,
    string Reason,
    string TargetMealWindow,
    bool IsRolledOver,
    Dictionary<string, bool> DailyWindowsClaimed,
    List<string> AvailableWindowsRightNow,
    string CurrentSession
);
