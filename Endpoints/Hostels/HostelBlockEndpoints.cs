using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Hostels;

public sealed class GetHostelBlocksEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<IEnumerable<HostelBlockResponse>>
{
    public override void Configure()
    {
        Get("hostels/blocks");
        AllowAnonymous();
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var activeOnlyStr = Query<string?>("activeOnly", isRequired: false);
        bool? activeOnly = activeOnlyStr is null ? null : bool.TryParse(activeOnlyStr, out var b) ? b : null;
        var blocks = await hostelService.GetBlocksAsync(activeOnly);
        await SendSuccessAsync(blocks, ct);
    }
}

public sealed class GetHostelBlockByIdEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<HostelBlockResponse>
{
    public override void Configure()
    {
        Get("hostels/blocks/{id}");
        AllowAnonymous();
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var block = await hostelService.GetBlockByIdAsync(id);
            await SendSuccessAsync(block, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class CreateHostelBlockEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<CreateHostelBlockRequest, HostelBlockResponse>
{
    public override void Configure()
    {
        Post("hostels/blocks");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CreateHostelBlockRequest req, CancellationToken ct)
    {
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var block = await hostelService.CreateBlockAsync(req, userId);
        await SendSuccessAsync(block, ct);
    }
}

public sealed class UpdateHostelBlockEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<UpdateHostelBlockRequest, HostelBlockResponse>
{
    public override void Configure()
    {
        Put("hostels/blocks/{id}");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(UpdateHostelBlockRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var block = await hostelService.UpdateBlockAsync(id, req, userId);
            await SendSuccessAsync(block, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class DeleteHostelBlockEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<bool>
{
    public override void Configure()
    {
        Delete("hostels/blocks/{id}");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var success = await hostelService.DeleteBlockAsync(id);
        await SendSuccessAsync(success, ct);
    }
}
