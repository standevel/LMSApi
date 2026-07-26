using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Hostels;

public sealed class GetHostelExeatsEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<IEnumerable<HostelExeatResponse>>
{
    public override void Configure()
    {
        Get("hostels/exeats");
        Roles("SuperAdmin", "Admin", "HostelWarden", "Student", "Parent");
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentIdStr = Query<string?>("studentId", isRequired: false);
        Guid? studentId = Guid.TryParse(studentIdStr, out var sId) ? sId : null;

        var statusStr = Query<string?>("status", isRequired: false);
        ExeatStatus? status = Enum.TryParse<ExeatStatus>(statusStr, true, out var st) ? st : null;

        var exeats = await hostelService.GetExeatRequestsAsync(studentId, status);
        await SendSuccessAsync(exeats, ct);
    }
}

public sealed class ApplyHostelExeatEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<ApplyExeatRequest, HostelExeatResponse>
{
    public override void Configure()
    {
        Post("hostels/exeats");
        Roles("Student");
        Tags("Hostels");
    }

    public override async Task HandleAsync(ApplyExeatRequest req, CancellationToken ct)
    {
        var studentId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var exeat = await hostelService.ApplyExeatAsync(studentId, req);
        await SendSuccessAsync(exeat, ct);
    }
}

public sealed class ApproveHostelExeatEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<ApproveExeatRequest, HostelExeatResponse>
{
    public override void Configure()
    {
        Patch("hostels/exeats/{id}/approve");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsExeatManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(ApproveExeatRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.ApproveExeatAsync(id, req, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class MarkHostelExeatReturnEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<HostelExeatResponse>
{
    public override void Configure()
    {
        Post("hostels/exeats/{id}/mark-return");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsExeatManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.MarkExeatReturnAsync(id, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class GetHostelStatsEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<HostelStatsResponse>
{
    public override void Configure()
    {
        Get("hostels/stats");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsView));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stats = await hostelService.GetHostelStatsAsync();
        await SendSuccessAsync(stats, ct);
    }
}
