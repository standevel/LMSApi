using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.ParentPortal;

public sealed class GetParentPortalConfigurationEndpoint(IGuardianProvisioningService guardianProvisioningService)
    : ApiEndpointWithoutRequest<SystemParentPortalConfigurationDto>
{
    public override void Configure()
    {
        Get("admin/parent-portal/configuration");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var config = await guardianProvisioningService.GetConfigurationAsync(ct);
        await SendSuccessAsync(new SystemParentPortalConfigurationDto(
            config.AutoCreateGuardianAccountsOnStudentCreation,
            config.SendGuardianInvitationEmail,
            config.DefaultRelationship), ct);
    }
}

public sealed class UpdateParentPortalConfigurationEndpoint(
    IGuardianProvisioningService guardianProvisioningService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateSystemParentPortalConfigurationRequest, SystemParentPortalConfigurationDto>
{
    public override void Configure()
    {
        Put("admin/parent-portal/configuration");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Administration");
    }

    public override async Task HandleAsync(UpdateSystemParentPortalConfigurationRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var config = await guardianProvisioningService.UpdateConfigurationAsync(req, userId, ct);
        await SendSuccessAsync(config, ct);
    }
}

public sealed class ProvisionStudentGuardianEndpoint(IGuardianProvisioningService guardianProvisioningService)
    : ApiEndpoint<ProvisionStudentGuardianEndpoint.ProvisionStudentGuardianRouteRequest, ProvisionGuardianResultDto>
{
    public sealed class ProvisionStudentGuardianRouteRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        public bool? SendInvitationEmail { get; set; }
    }

    public override void Configure()
    {
        Post("admin/students/{StudentId:guid}/guardian/provision");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(ProvisionStudentGuardianRouteRequest req, CancellationToken ct)
    {
        var result = await guardianProvisioningService.ProvisionForStudentAsync(req.StudentId, req.SendInvitationEmail, ct);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class ProvisionStudentGuardiansBatchEndpoint(IGuardianProvisioningService guardianProvisioningService)
    : ApiEndpoint<ProvisionGuardianBatchRequest, ProvisionGuardianBatchResponse>
{
    public override void Configure()
    {
        Post("admin/students/guardians/provision-batch");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(ProvisionGuardianBatchRequest req, CancellationToken ct)
    {
        if (!req.AllEligible && (req.StudentIds == null || req.StudentIds.Count == 0))
        {
            await SendFailureAsync(400, "Select at least one student or use allEligible.", "INVALID_REQUEST", "No students selected.", ct);
            return;
        }

        var result = await guardianProvisioningService.ProvisionBatchAsync(req, ct);
        await SendSuccessAsync(result, ct);
    }
}
