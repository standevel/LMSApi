using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.RegistrationConfig;

public record RegistrationConfigDto(string Strategy, bool EnforceMinCredits, string MatricNumberFormat);
public record UpdateRegistrationConfigRequest(string Strategy, bool EnforceMinCredits, string MatricNumberFormat);

public sealed class GetRegistrationConfigEndpoint(LmsDbContext context)
    : ApiEndpointWithoutRequest<RegistrationConfigDto>
{
    public override void Configure()
    {
        Get("admin/registration/config");
        Roles("Admin", "SuperAdmin");
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var config = await context.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        if (config is null)
        {
            // Seed a default config dynamically if missing
            var newConfig = new SystemRegistrationConfiguration { Strategy = "Single", EnforceMinCredits = true, MatricNumberFormat = "WU/{YY}/{PROGRAM}/{SEQ}" };
            context.SystemRegistrationConfigurations.Add(newConfig);
            await context.SaveChangesAsync(ct);
            await SendAsync(new RegistrationConfigDto(newConfig.Strategy, newConfig.EnforceMinCredits, newConfig.MatricNumberFormat), ct);
            return;
        }

        await SendAsync(new RegistrationConfigDto(config.Strategy, config.EnforceMinCredits, config.MatricNumberFormat ?? "WU/{YY}/{PROGRAM}/{SEQ}"), ct);
    }
}

public sealed class UpdateRegistrationConfigEndpoint(LmsDbContext context)
    : ApiEndpoint<UpdateRegistrationConfigRequest, RegistrationConfigDto>
{
    public override void Configure()
    {
        Post("admin/registration/config");
        Roles("Admin", "SuperAdmin");
        Tags("Administration");
    }

    public override async Task HandleAsync(UpdateRegistrationConfigRequest req, CancellationToken ct)
    {
        if (req.Strategy != "Single" && req.Strategy != "Bulk")
        {
            await SendFailureAsync(400, "Strategy must be either 'Single' or 'Bulk'.", "INVALID_STRATEGY", "Invalid Strategy", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.MatricNumberFormat))
        {
            await SendFailureAsync(400, "Matric number format template is required.", "INVALID_FORMAT_TEMPLATE", "Invalid Format Template", ct);
            return;
        }

        if (!req.MatricNumberFormat.Contains("{SEQ}"))
        {
            await SendFailureAsync(400, "Matric number format template must contain the '{SEQ}' placeholder.", "INVALID_FORMAT_TEMPLATE", "Invalid Format Template", ct);
            return;
        }

        var config = await context.SystemRegistrationConfigurations.FirstOrDefaultAsync(ct);
        if (config is null)
        {
            config = new SystemRegistrationConfiguration();
            context.SystemRegistrationConfigurations.Add(config);
        }

        config.Strategy = req.Strategy;
        config.EnforceMinCredits = req.EnforceMinCredits;
        config.MatricNumberFormat = req.MatricNumberFormat.Trim();
        config.UpdatedAt = System.DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        await SendAsync(new RegistrationConfigDto(config.Strategy, config.EnforceMinCredits, config.MatricNumberFormat), ct);
    }
}
