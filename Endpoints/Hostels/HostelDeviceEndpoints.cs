using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Hostels;

public sealed class GetHostelDevicesEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<IEnumerable<HostelDeviceResponse>>
{
    public override void Configure()
    {
        Get("hostels/devices");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsView));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentIdStr = Query<string?>("studentId", isRequired: false);
        Guid? studentId = Guid.TryParse(studentIdStr, out var sId) ? sId : null;

        var blockIdStr = Query<string?>("blockId", isRequired: false);
        Guid? blockId = Guid.TryParse(blockIdStr, out var bId) ? bId : null;

        var statusStr = Query<string?>("status", isRequired: false);
        HostelDeviceStatus? status = Enum.TryParse<HostelDeviceStatus>(statusStr, true, out var st) ? st : null;

        var search = Query<string?>("search", isRequired: false);

        var devices = await hostelService.GetRegisteredDevicesAsync(studentId, blockId, status, search);
        await SendSuccessAsync(devices, ct);
    }
}

public sealed class GetMyHostelDevicesEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<IEnumerable<HostelDeviceResponse>>
{
    public override void Configure()
    {
        Get("hostels/my-devices");
        Roles("Student");
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var devices = await hostelService.GetStudentDevicesAsync(studentId);
        await SendSuccessAsync(devices, ct);
    }
}

public sealed class RegisterHostelDeviceEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<RegisterHostelDeviceRequest, HostelDeviceResponse>
{
    public override void Configure()
    {
        Post("hostels/devices");
        Roles("Student");
        Tags("Hostels");
    }

    public override async Task HandleAsync(RegisterHostelDeviceRequest req, CancellationToken ct)
    {
        var studentId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var device = await hostelService.RegisterDeviceAsync(studentId, req);
            await SendSuccessAsync(device, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "BAD_REQUEST", ex.Message, ct);
        }
    }
}

public sealed class VerifyHostelDeviceEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<VerifyHostelDeviceRequest, HostelDeviceResponse>
{
    public override void Configure()
    {
        Patch("hostels/devices/{id}/verify");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(VerifyHostelDeviceRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.VerifyDeviceAsync(id, req, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class DecommissionHostelDeviceEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<HostelDeviceResponse>
{
    public override void Configure()
    {
        Post("hostels/devices/{id}/decommission");
        Roles("Student", "SuperAdmin", "Admin", "HostelWarden");
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.DecommissionDeviceAsync(id, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}
