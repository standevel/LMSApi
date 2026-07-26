using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Hostels;

public sealed class GetHostelRoomsEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<IEnumerable<HostelRoomResponse>>
{
    public override void Configure()
    {
        Get("hostels/rooms");
        AllowAnonymous();
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var blockIdStr = Query<string?>("blockId", isRequired: false);
        Guid? blockId = Guid.TryParse(blockIdStr, out var bId) ? bId : null;

        var statusStr = Query<string?>("status", isRequired: false);
        RoomStatus? status = Enum.TryParse<RoomStatus>(statusStr, true, out var st) ? st : null;

        var rooms = await hostelService.GetRoomsAsync(blockId, status);
        await SendSuccessAsync(rooms, ct);
    }
}

public sealed class GetHostelRoomByIdEndpoint(IHostelService hostelService)
    : ApiEndpointWithoutRequest<HostelRoomResponse>
{
    public override void Configure()
    {
        Get("hostels/rooms/{id}");
        AllowAnonymous();
        Tags("Hostels");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var room = await hostelService.GetRoomByIdAsync(id);
            await SendSuccessAsync(room, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
    }
}

public sealed class CreateHostelRoomEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<CreateHostelRoomRequest, HostelRoomResponse>
{
    public override void Configure()
    {
        Post("hostels/rooms");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(CreateHostelRoomRequest req, CancellationToken ct)
    {
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        var room = await hostelService.CreateRoomAsync(req, userId);
        await SendSuccessAsync(room, ct);
    }
}

public sealed class UpdateHostelRoomEndpoint(IHostelService hostelService, ICurrentUserContext userContext)
    : ApiEndpoint<UpdateHostelRoomRequest, HostelRoomResponse>
{
    public override void Configure()
    {
        Put("hostels/rooms/{id}");
        Policies(PermissionPolicy.Build(LmsPermissions.HostelsManage));
        Tags("Hostels");
    }

    public override async Task HandleAsync(UpdateHostelRoomRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = (await userContext.GetUserIdAsync(ct)) ?? Guid.Empty;
        try
        {
            var room = await hostelService.UpdateRoomAsync(id, req, userId);
            await SendSuccessAsync(room, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "BAD_REQUEST", ex.Message, ct);
        }
    }
}
