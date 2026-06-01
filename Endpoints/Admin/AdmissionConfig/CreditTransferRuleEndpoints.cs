using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListCreditTransferRulesEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<CreditTransferRuleDto>>
{
    public override void Configure()
    {
        Get("admin/admission-config/credit-transfer-rules");
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rules = await dbContext.CreditTransferRules
            .Include(r => r.Program)
            .OrderBy(r => r.Program!.Name)
            .ThenBy(r => r.SourceCountryCode ?? "")
            .ToListAsync(ct);

        var response = rules.Select(r => new CreditTransferRuleDto(
            r.Id, r.ProgramId, r.Program?.Name, r.SourceCountryCode,
            r.CreditsPerYear, r.MaxTransferablePercentage, r.MaxTransferableCredits,
            r.MinCGPA, r.IsActive, r.CreatedAt, r.UpdatedAt));
        await SendSuccessAsync(response, ct);
    }
}

public record CreditTransferRuleDto(
    Guid Id,
    Guid ProgramId,
    string? ProgramName,
    string? SourceCountryCode,
    decimal CreditsPerYear,
    decimal MaxTransferablePercentage,
    int MaxTransferableCredits,
    decimal MinCGPA,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateCreditTransferRuleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateCreditTransferRuleRequest, CreditTransferRuleDto>
{
    public override void Configure()
    {
        Post("admin/admission-config/credit-transfer-rules");
    }

    public override async Task HandleAsync(CreateCreditTransferRuleRequest req, CancellationToken ct)
    {
        var rule = new CreditTransferRule
        {
            ProgramId = req.ProgramId,
            SourceCountryCode = req.SourceCountryCode,
            CreditsPerYear = req.CreditsPerYear,
            MaxTransferablePercentage = req.MaxTransferablePercentage,
            MaxTransferableCredits = req.MaxTransferableCredits,
            MinCGPA = req.MinCGPA,
            IsActive = req.IsActive
        };
        dbContext.CreditTransferRules.Add(rule);
        await dbContext.SaveChangesAsync(ct);

        var dto = new CreditTransferRuleDto(
            rule.Id, rule.ProgramId, null, rule.SourceCountryCode,
            rule.CreditsPerYear, rule.MaxTransferablePercentage, rule.MaxTransferableCredits,
            rule.MinCGPA, rule.IsActive, rule.CreatedAt, rule.UpdatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class UpdateCreditTransferRuleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateCreditTransferRuleRequest, CreditTransferRuleDto>
{
    public override void Configure()
    {
        Patch("admin/admission-config/credit-transfer-rules/{Id}");
    }

    public override async Task HandleAsync(UpdateCreditTransferRuleRequest req, CancellationToken ct)
    {
        var rule = await dbContext.CreditTransferRules.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Credit transfer rule not found");

        rule.SourceCountryCode = req.SourceCountryCode ?? rule.SourceCountryCode;
        rule.CreditsPerYear = req.CreditsPerYear ?? rule.CreditsPerYear;
        rule.MaxTransferablePercentage = req.MaxTransferablePercentage ?? rule.MaxTransferablePercentage;
        rule.MaxTransferableCredits = req.MaxTransferableCredits ?? rule.MaxTransferableCredits;
        rule.MinCGPA = req.MinCGPA ?? rule.MinCGPA;
        rule.IsActive = req.IsActive ?? rule.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var dto = new CreditTransferRuleDto(
            rule.Id, rule.ProgramId, null, rule.SourceCountryCode,
            rule.CreditsPerYear, rule.MaxTransferablePercentage, rule.MaxTransferableCredits,
            rule.MinCGPA, rule.IsActive, rule.CreatedAt, rule.UpdatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class DeleteCreditTransferRuleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteCreditTransferRuleRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/admission-config/credit-transfer-rules/{Id}");
    }

    public override async Task HandleAsync(DeleteCreditTransferRuleRequest req, CancellationToken ct)
    {
        var rule = await dbContext.CreditTransferRules.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Credit transfer rule not found");

        rule.IsActive = false;
        rule.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}

public record CreateCreditTransferRuleRequest(
    Guid ProgramId,
    string? SourceCountryCode,
    decimal CreditsPerYear,
    decimal MaxTransferablePercentage,
    int MaxTransferableCredits,
    decimal MinCGPA,
    bool IsActive = true);

public record UpdateCreditTransferRuleRequest(
    Guid Id,
    string? SourceCountryCode,
    decimal? CreditsPerYear,
    decimal? MaxTransferablePercentage,
    int? MaxTransferableCredits,
    decimal? MinCGPA,
    bool? IsActive);

public record DeleteCreditTransferRuleRequest(Guid Id);
