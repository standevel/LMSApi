using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.RegistrationConfig;

public record RegistrationConfigDto(
    string Strategy,
    bool EnforceMinCredits,
    string MatricNumberFormat,
    bool EnableAutoRegistration,
    bool AutoRegisterOnEnrollment,
    bool AutoRegisterOnRegistrationStart,
    string AutoRegisterCourseCategories,
    bool AutoRegisterCarryovers,
    string AutoRegisterCreditLimitHandling,
    string AutoRegisterTargetLevels,
    bool AllowMultiSemesterRegistration = false,
    bool RequireJambForProgramTransfer = true);

public record UpdateRegistrationConfigRequest(
    string Strategy,
    bool EnforceMinCredits,
    string MatricNumberFormat,
    bool EnableAutoRegistration = false,
    bool AutoRegisterOnEnrollment = false,
    bool AutoRegisterOnRegistrationStart = false,
    string AutoRegisterCourseCategories = "Compulsory",
    bool AutoRegisterCarryovers = true,
    string AutoRegisterCreditLimitHandling = "Strict",
    string AutoRegisterTargetLevels = "All",
    bool AllowMultiSemesterRegistration = false,
    bool RequireJambForProgramTransfer = true);

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
            var newConfig = new SystemRegistrationConfiguration
            {
                Strategy = "Single",
                EnforceMinCredits = true,
                AllowMultiSemesterRegistration = false,
                MatricNumberFormat = "WU/{PROGRAM}/{YYYY}/{SEQ}",
                EnableAutoRegistration = false,
                AutoRegisterOnEnrollment = false,
                AutoRegisterOnRegistrationStart = false,
                AutoRegisterCourseCategories = "Compulsory",
                AutoRegisterCarryovers = true,
                AutoRegisterCreditLimitHandling = "Strict",
                AutoRegisterTargetLevels = "All"
            };
            context.SystemRegistrationConfigurations.Add(newConfig);
            await context.SaveChangesAsync(ct);
            await SendAsync(ToDto(newConfig), ct);
            return;
        }

        await SendAsync(ToDto(config), ct);
    }

    internal static RegistrationConfigDto ToDto(SystemRegistrationConfiguration c) => new(
        c.Strategy,
        c.EnforceMinCredits,
        c.MatricNumberFormat ?? "WU/{PROGRAM}/{YYYY}/{SEQ}",
        c.EnableAutoRegistration,
        c.AutoRegisterOnEnrollment,
        c.AutoRegisterOnRegistrationStart,
        c.AutoRegisterCourseCategories ?? "Compulsory",
        c.AutoRegisterCarryovers,
        c.AutoRegisterCreditLimitHandling ?? "Strict",
        c.AutoRegisterTargetLevels ?? "All",
        c.AllowMultiSemesterRegistration,
        c.RequireJambForProgramTransfer);
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
        config.AllowMultiSemesterRegistration = req.AllowMultiSemesterRegistration;
        config.RequireJambForProgramTransfer = req.RequireJambForProgramTransfer;
        config.MatricNumberFormat = req.MatricNumberFormat.Trim();
        config.EnableAutoRegistration = req.EnableAutoRegistration;
        config.AutoRegisterOnEnrollment = req.AutoRegisterOnEnrollment;
        config.AutoRegisterOnRegistrationStart = req.AutoRegisterOnRegistrationStart;
        config.AutoRegisterCourseCategories = string.IsNullOrWhiteSpace(req.AutoRegisterCourseCategories) ? "Compulsory" : req.AutoRegisterCourseCategories;
        config.AutoRegisterCarryovers = req.AutoRegisterCarryovers;
        config.AutoRegisterCreditLimitHandling = string.IsNullOrWhiteSpace(req.AutoRegisterCreditLimitHandling) ? "Strict" : req.AutoRegisterCreditLimitHandling;
        config.AutoRegisterTargetLevels = string.IsNullOrWhiteSpace(req.AutoRegisterTargetLevels) ? "All" : req.AutoRegisterTargetLevels;
        config.UpdatedAt = System.DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        await SendAsync(GetRegistrationConfigEndpoint.ToDto(config), ct);
    }
}

public sealed class RunBatchAutoRegistrationEndpoint(IAutoRegistrationService autoRegistrationService)
    : ApiEndpoint<BatchAutoRegistrationRequest, BatchAutoRegistrationResultDto>
{
    public override void Configure()
    {
        Post("admin/registration/auto-register");
        Roles("Admin", "SuperAdmin");
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Execute or dry-run batch automatic course registration";
            s.Description = "Automatically evaluates curriculum and carryovers, and enrolls eligible students in course offerings for the target session.";
            s.Response<BatchAutoRegistrationResultDto>(200, "Successfully executed batch automatic registration.");
        });
    }

    public override async Task HandleAsync(BatchAutoRegistrationRequest req, CancellationToken ct)
    {
        if (req.AcademicSessionId == System.Guid.Empty)
        {
            await SendFailureAsync(400, "Academic session ID is required.", "INVALID_SESSION", "Invalid Session", ct);
            return;
        }

        var result = await autoRegistrationService.RunBatchAutoRegistrationAsync(req, ct);
        await SendAsync(result, ct);
    }
}
