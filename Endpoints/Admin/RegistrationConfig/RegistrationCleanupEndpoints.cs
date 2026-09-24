using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.RegistrationConfig;

public sealed class RegistrationCleanupScanEndpoint(IRegistrationService registrationService)
    : ApiEndpoint<RegistrationCleanupScanRequest, RegistrationCleanupScanResultDto>
{
    public override void Configure()
    {
        Post("admin/registration/cleanup-scan");
        Roles("Admin", "SuperAdmin");
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Scan for wrong course registrations";
            s.Description = "Audits active course registrations against student program curriculum, academic level rules (with optional carryover allowance), and prior passed courses without modifying any records.";
            s.Responses[200] = "Scan completed successfully.";
            s.Responses[400] = "Validation or scan failure.";
        });
    }

    public override async Task HandleAsync(RegistrationCleanupScanRequest req, CancellationToken ct)
    {
        var result = await registrationService.ScanInvalidRegistrationsAsync(req, ct);
        if (result.IsError)
        {
            var err = result.FirstError;
            await SendFailureAsync(400, err.Description, err.Code, err.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

public sealed class RegistrationCleanupExecuteEndpoint(
    IRegistrationService registrationService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<ExecuteRegistrationCleanupRequest, RegistrationCleanupExecutionResultDto>
{
    public override void Configure()
    {
        Post("admin/registration/cleanup-execute");
        Roles("Admin", "SuperAdmin");
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Execute course registration cleanup";
            s.Description = "Drops wrong or invalid course registrations identified by curriculum mismatch, level mismatch (exceeding student level), or previously passed status.";
            s.Responses[200] = "Cleanup executed successfully.";
            s.Responses[400] = "Cleanup execution failed.";
        });
    }

    public override async Task HandleAsync(ExecuteRegistrationCleanupRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct) ?? Guid.Empty;

        var result = await registrationService.ExecuteCleanupAsync(req, userId, ct);
        if (result.IsError)
        {
            var err = result.FirstError;
            await SendFailureAsync(400, err.Description, err.Code, err.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
