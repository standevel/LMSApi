using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Hostels;

public sealed class GetHostelMaintenanceRequestsEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<IEnumerable<HostelMaintenanceRequestResponse>>
{
    public override void Configure()
    {
        Get("hostels/maintenance");
        AllowAnonymous();
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var blockIdStr = Query<string?>("blockId", isRequired: false);
        Guid? blockId = Guid.TryParse(blockIdStr, out var bId) ? bId : null;

        var statusStr = Query<string?>("status", isRequired: false);
        MaintenanceStatus? status = Enum.TryParse<MaintenanceStatus>(statusStr, true, out var st) ? st : null;

        var requests = await hostelService.GetMaintenanceRequestsAsync(blockId, status);
        await SendSuccessAsync(requests, ct);
    }
}

public sealed class CreateHostelMaintenanceRequestEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<CreateMaintenanceRequestDto, HostelMaintenanceRequestResponse>
{
    public override void Configure()
    {
        Post("hostels/maintenance");
        AllowAnonymous(); // Accessible by students & wardens
        Tags("Hostels");
    }

    public override async Task HandleAsync(CreateMaintenanceRequestDto req, CancellationToken ct)
    {
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var result = await hostelService.CreateMaintenanceRequestAsync(userId, req);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class UpdateHostelMaintenanceStatusEndpoint(IHostelService hostelService)
    : ApiEndpoint<UpdateMaintenanceStatusDto, HostelMaintenanceRequestResponse>
{
    public override void Configure()
    {
        Patch("hostels/maintenance/{id}/status");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(UpdateMaintenanceStatusDto req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var result = await hostelService.UpdateMaintenanceStatusAsync(id, req);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}
