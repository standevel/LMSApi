using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetCountriesEndpoint(LmsDbContext dbContext) : ApiEndpoint<EmptyRequest, IEnumerable<CountryResponse>>
{
    public override void Configure()
    {
        Get("/api/countries");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var countries = await dbContext.Countries
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        var response = countries.Select(c => new CountryResponse(c.Id, c.Code, c.Name, c.DisplayName, c.Region, c.CallingCode, c.DisplayOrder));
        await SendSuccessAsync(response, ct);
    }
}

public record CountryResponse(
    Guid Id,
    string Code,
    string Name,
    string? DisplayName,
    Region Region,
    string? CallingCode,
    int DisplayOrder);
