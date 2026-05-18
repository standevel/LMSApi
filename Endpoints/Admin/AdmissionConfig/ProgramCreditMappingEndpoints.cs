using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListProgramCreditMappingsEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<ProgramCreditMappingDto>>
{
    public override void Configure()
    {
        Get("/api/admin/admission-config/program-credit-mappings");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var mappings = await dbContext.ProgramCreditMappings
            .Include(m => m.Program)
            .OrderBy(m => m.Program!.Name)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var response = mappings.Select(m => new ProgramCreditMappingDto(
            m.Id, m.ProgramId, m.Program?.Name, m.CreditsPerLevel,
            m.MaxTransferablePercentage, m.MaxTransferableCredits,
            m.MinCreditsAtLMS, m.IsActive, m.CreatedAt, m.UpdatedAt));

        await SendSuccessAsync(response, ct);
    }
}

public record ProgramCreditMappingDto(
    Guid Id,
    Guid ProgramId,
    string? ProgramName,
    int CreditsPerLevel,
    decimal MaxTransferablePercentage,
    int MaxTransferableCredits,
    int MinCreditsAtLMS,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateProgramCreditMappingEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateProgramCreditMappingRequest, ProgramCreditMappingDto>
{
    public override void Configure()
    {
        Post("/api/admin/admission-config/program-credit-mappings");
    }

    public override async Task HandleAsync(CreateProgramCreditMappingRequest req, CancellationToken ct)
    {
        var mapping = new ProgramCreditMapping
        {
            ProgramId = req.ProgramId,
            CreditsPerLevel = req.CreditsPerLevel,
            MaxTransferablePercentage = req.MaxTransferablePercentage,
            MaxTransferableCredits = req.MaxTransferableCredits,
            MinCreditsAtLMS = req.MinCreditsAtLMS,
            IsActive = req.IsActive
        };
        dbContext.ProgramCreditMappings.Add(mapping);
        await dbContext.SaveChangesAsync(ct);

        var dto = new ProgramCreditMappingDto(
            mapping.Id, mapping.ProgramId, null, mapping.CreditsPerLevel,
            mapping.MaxTransferablePercentage, mapping.MaxTransferableCredits,
            mapping.MinCreditsAtLMS, mapping.IsActive, mapping.CreatedAt, mapping.UpdatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class UpdateProgramCreditMappingEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateProgramCreditMappingRequest, ProgramCreditMappingDto>
{
    public override void Configure()
    {
        Patch("/api/admin/admission-config/program-credit-mappings/{Id}");
    }

    public override async Task HandleAsync(UpdateProgramCreditMappingRequest req, CancellationToken ct)
    {
        var mapping = await dbContext.ProgramCreditMappings.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Program credit mapping not found");

        mapping.ProgramId = req.ProgramId ?? mapping.ProgramId;
        mapping.CreditsPerLevel = req.CreditsPerLevel ?? mapping.CreditsPerLevel;
        mapping.MaxTransferablePercentage = req.MaxTransferablePercentage ?? mapping.MaxTransferablePercentage;
        mapping.MaxTransferableCredits = req.MaxTransferableCredits ?? mapping.MaxTransferableCredits;
        mapping.MinCreditsAtLMS = req.MinCreditsAtLMS ?? mapping.MinCreditsAtLMS;
        mapping.IsActive = req.IsActive ?? mapping.IsActive;
        mapping.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var dto = new ProgramCreditMappingDto(
            mapping.Id, mapping.ProgramId, null, mapping.CreditsPerLevel,
            mapping.MaxTransferablePercentage, mapping.MaxTransferableCredits,
            mapping.MinCreditsAtLMS, mapping.IsActive, mapping.CreatedAt, mapping.UpdatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class DeleteProgramCreditMappingEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteProgramCreditMappingRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("/api/admin/admission-config/program-credit-mappings/{Id}");
    }

    public override async Task HandleAsync(DeleteProgramCreditMappingRequest req, CancellationToken ct)
    {
        var mapping = await dbContext.ProgramCreditMappings.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Program credit mapping not found");

        mapping.IsActive = false;
        mapping.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}

public record CreateProgramCreditMappingRequest(
    Guid ProgramId,
    int CreditsPerLevel,
    decimal MaxTransferablePercentage,
    int MaxTransferableCredits,
    int MinCreditsAtLMS,
    bool IsActive = true);

public record UpdateProgramCreditMappingRequest(
    Guid Id,
    Guid? ProgramId,
    int? CreditsPerLevel,
    decimal? MaxTransferablePercentage,
    int? MaxTransferableCredits,
    int? MinCreditsAtLMS,
    bool? IsActive);

public record DeleteProgramCreditMappingRequest(Guid Id);
