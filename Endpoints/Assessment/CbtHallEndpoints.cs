using System.Text.Json;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Assessment;

public sealed class GetCbtHallsEndpoint(LmsDbContext dbContext) : ApiEndpointWithoutRequest<List<CbtHallDto>>
{
    public override void Configure()
    {
        Get("cbt-halls");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var halls = await dbContext.CbtHalls
            .OrderByDescending(hall => hall.IsActive)
            .ThenBy(hall => hall.Name)
            .ToListAsync(ct);

        await SendSuccessAsync(halls.Select(MapToDto).ToList(), ct);
    }

    private static CbtHallDto MapToDto(CbtHall hall) => CbtHallEndpointMapper.MapToDto(hall);
}

public sealed class CreateCbtHallEndpoint(LmsDbContext dbContext, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateCbtHallRequest, CbtHallDto>
{
    public override void Configure()
    {
        Post("cbt-halls");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CreateCbtHallRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var validation = await ValidateRequestAsync(req.Name, req.Code, req.IpRanges, null, ct);
        if (validation is not null)
        {
            await SendFailureAsync(400, "Validation failed", validation.Value.Code, validation.Value.Message, ct);
            return;
        }

        var hall = new CbtHall
        {
            Name = req.Name.Trim(),
            Code = req.Code.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            IpRangesJson = JsonSerializer.Serialize(IpRangeMatcher.NormalizeRanges(req.IpRanges)),
            IsActive = true,
            CreatedBy = userId.Value,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.CbtHalls.Add(hall);
        await dbContext.SaveChangesAsync(ct);
        await SendCreatedAsync(CbtHallEndpointMapper.MapToDto(hall), ct);
    }

    private async Task<(string Code, string Message)?> ValidateRequestAsync(string name, string code, List<string> ranges, Guid? existingId, CancellationToken ct) =>
        await CbtHallEndpointMapper.ValidateRequestAsync(dbContext, name, code, ranges, existingId, ct);
}

public sealed class UpdateCbtHallEndpoint(LmsDbContext dbContext, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateCbtHallRequest, CbtHallDto>
{
    public override void Configure()
    {
        Put("cbt-halls/{id:guid}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateCbtHallRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var hall = await dbContext.CbtHalls.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (hall is null)
        {
            await SendFailureAsync(404, "CBT hall not found", "CBT_HALL_NOT_FOUND", "CBT hall not found.", ct);
            return;
        }

        var validation = await CbtHallEndpointMapper.ValidateRequestAsync(dbContext, req.Name, req.Code, req.IpRanges, id, ct);
        if (validation is not null)
        {
            await SendFailureAsync(400, "Validation failed", validation.Value.Code, validation.Value.Message, ct);
            return;
        }

        hall.Name = req.Name.Trim();
        hall.Code = req.Code.Trim().ToUpperInvariant();
        hall.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        hall.IpRangesJson = JsonSerializer.Serialize(IpRangeMatcher.NormalizeRanges(req.IpRanges));
        hall.UpdatedAt = DateTime.UtcNow;
        hall.UpdatedBy = userId.Value;

        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(CbtHallEndpointMapper.MapToDto(hall), ct);
    }
}

public sealed class UpdateCbtHallStatusEndpoint(LmsDbContext dbContext, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateCbtHallStatusRequest, CbtHallDto>
{
    public override void Configure()
    {
        Patch("cbt-halls/{id:guid}/status");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateCbtHallStatusRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var hall = await dbContext.CbtHalls.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (hall is null)
        {
            await SendFailureAsync(404, "CBT hall not found", "CBT_HALL_NOT_FOUND", "CBT hall not found.", ct);
            return;
        }

        hall.IsActive = req.IsActive;
        hall.UpdatedAt = DateTime.UtcNow;
        hall.UpdatedBy = userId.Value;

        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(CbtHallEndpointMapper.MapToDto(hall), ct);
    }
}

internal static class CbtHallEndpointMapper
{
    public static CbtHallDto MapToDto(CbtHall hall) => new()
    {
        Id = hall.Id,
        Name = hall.Name,
        Code = hall.Code,
        Description = hall.Description,
        IpRanges = DeserializeRanges(hall.IpRangesJson),
        IsActive = hall.IsActive,
        CreatedAt = hall.CreatedAt,
        CreatedBy = hall.CreatedBy,
        UpdatedAt = hall.UpdatedAt,
        UpdatedBy = hall.UpdatedBy
    };

    public static async Task<(string Code, string Message)?> ValidateRequestAsync(
        LmsDbContext dbContext,
        string name,
        string code,
        List<string> ranges,
        Guid? existingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ("CBT_HALL_NAME_REQUIRED", "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return ("CBT_HALL_CODE_REQUIRED", "Code is required.");
        }

        var normalizedRanges = IpRangeMatcher.NormalizeRanges(ranges);
        if (normalizedRanges.Count == 0)
        {
            return ("CBT_HALL_RANGES_REQUIRED", "At least one IP address or CIDR range is required.");
        }

        var rangeErrors = IpRangeMatcher.ValidateRanges(normalizedRanges);
        if (rangeErrors.Count > 0)
        {
            return ("CBT_HALL_INVALID_RANGES", string.Join(" ", rangeErrors));
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var codeExists = await dbContext.CbtHalls
            .AnyAsync(hall => hall.Code == normalizedCode && (!existingId.HasValue || hall.Id != existingId.Value), ct);

        return codeExists
            ? ("CBT_HALL_CODE_EXISTS", "A CBT hall with this code already exists.")
            : null;
    }

    private static List<string> DeserializeRanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
