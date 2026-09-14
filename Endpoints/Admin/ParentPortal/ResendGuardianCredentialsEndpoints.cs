using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.ParentPortal;

public sealed class ResendGuardianCredentialsEndpoint(
    IGuardianProvisioningService guardianProvisioningService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<ResendGuardianCredentialsEndpoint.RouteRequest, ResendGuardianCredentialsResponse>
{
    public sealed class RouteRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid ParentGuardianId { get; set; }
    }

    public override void Configure()
    {
        Post("admin/parents/{ParentGuardianId:guid}/resend-invite");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Administration");
    }

    public override async Task HandleAsync(RouteRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await guardianProvisioningService.ResendCredentialsAsync(req.ParentGuardianId, userId, ct);

        if (!result.Success)
        {
            await SendFailureAsync(400, result.Message, "RESEND_FAILED", result.Message, ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message);
    }
}

public sealed class ResendGuardianCredentialsForStudentEndpoint(
    IGuardianProvisioningService guardianProvisioningService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<ResendGuardianCredentialsForStudentEndpoint.RouteRequest, ResendGuardianCredentialsResponse>
{
    public sealed class RouteRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Post("admin/students/{StudentId:guid}/guardian/resend-invite");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(RouteRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await guardianProvisioningService.ResendCredentialsForStudentAsync(req.StudentId, userId, ct);

        if (!result.Success)
        {
            await SendFailureAsync(400, result.Message, "RESEND_FAILED", result.Message, ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message);
    }
}
