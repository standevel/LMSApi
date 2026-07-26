using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Hostels;

public sealed class GetHostelAllocationsEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<IEnumerable<HostelAllocationResponse>>
{
    public override void Configure()
    {
        Get("hostels/allocations");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsView));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionIdStr = Query<string?>("sessionId", isRequired: false);
        Guid? sessionId = Guid.TryParse(sessionIdStr, out var sId) ? sId : null;

        var statusStr = Query<string?>("status", isRequired: false);
        AllocationStatus? status = Enum.TryParse<AllocationStatus>(statusStr, true, out var st) ? st : null;

        var studentIdStr = Query<string?>("studentId", isRequired: false);
        Guid? studentId = Guid.TryParse(studentIdStr, out var stId) ? stId : null;

        var allocations = await hostelService.GetAllocationsAsync(sessionId, status, studentId);
        await SendSuccessAsync(allocations, ct);
    }
}

public sealed class GetMyHostelAllocationEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<HostelAllocationResponse?>
{
    public override void Configure()
    {
        Get("hostels/my-allocation");
        Roles("Student");
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var allocation = await hostelService.GetStudentActiveAllocationAsync(studentId);
        await SendSuccessAsync(allocation, ct);
    }
}

public sealed class ApplyHostelEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<ApplyHostelRequest, HostelAllocationResponse>
{
    public override void Configure()
    {
        Post("hostels/apply");
        Roles("Student");
        Tags("Hostels");
    }

    public override async Task HandleAsync(ApplyHostelRequest req, CancellationToken ct)
    {
        var studentId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;

        var querySessionIdStr = Query<string?>("academicSessionId", isRequired: false);
        var effectiveSessionId = req.AcademicSessionId != Guid.Empty
            ? req.AcademicSessionId
            : (Guid.TryParse(querySessionIdStr, out var qId) ? qId : Guid.Empty);

        if (effectiveSessionId == Guid.Empty)
        {
            await SendFailureAsync(400, "Academic session ID is required.", "BAD_REQUEST", "Academic session ID is required.", ct);
            return;
        }

        var effectiveReq = req with { AcademicSessionId = effectiveSessionId };

        try
        {
            var allocation = await hostelService.ApplyForHostelAsync(studentId, effectiveReq);
            await SendSuccessAsync(allocation, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "BAD_REQUEST", ex.Message, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class AssignHostelBedEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<AssignBedRequest, HostelAllocationResponse>
{
    public override void Configure()
    {
        Post("hostels/allocations/assign");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(AssignBedRequest req, CancellationToken ct)
    {
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.AssignBedAsync(req, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "BAD_REQUEST", ex.Message, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class AutoAllocateHostelsEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<int>
{
    public override void Configure()
    {
        Post("hostels/allocations/auto-allocate");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionIdStr = Query<string?>("sessionId", isRequired: true);
        if (!Guid.TryParse(sessionIdStr, out var sessionId))
        {
            await SendFailureAsync(400, "Valid sessionId query parameter is required.", "BAD_REQUEST", "Invalid session", ct);
            return;
        }

        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var count = await hostelService.AutoAllocateAsync(sessionId, userId);
        await SendSuccessAsync(count, ct);
    }
}

public sealed class CheckInHostelEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<HostelAllocationResponse>
{
    public override void Configure()
    {
        Post("hostels/allocations/{id}/check-in");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.CheckInAsync(id, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "BAD_REQUEST", ex.Message, ct);
        }
    }
}

public sealed class CheckOutHostelEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<HostelAllocationResponse>
{
    public override void Configure()
    {
        Post("hostels/allocations/{id}/check-out");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.CheckOutAsync(id, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class CancelHostelAllocationEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpointWithoutRequest<HostelAllocationResponse>
{
    public override void Configure()
    {
        Post("hostels/allocations/{id}/cancel");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var result = await hostelService.CancelAllocationAsync(id, userId);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}
