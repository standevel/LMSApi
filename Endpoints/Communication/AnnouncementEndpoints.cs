using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Communication;

public sealed class CreateAnnouncementEndpoint(IAnnouncementService announcementService)
    : ApiEndpoint<CreateAnnouncementRequest, AnnouncementDto>
{
    public override void Configure()
    {
        Post("announcements");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Communication");
    }

    public override async Task HandleAsync(CreateAnnouncementRequest req, CancellationToken ct)
    {
        var result = await announcementService.CreateAsync(req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetAnnouncementsEndpoint(IAnnouncementService announcementService)
    : ApiEndpointWithoutRequest<List<AnnouncementDto>>
{
    public override void Configure()
    {
        Get("announcements");
        AllowAnonymous(); // Or require authentication? Let's require authentication for now.
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await announcementService.GetAllAsync(ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetAnnouncementByIdEndpoint(IAnnouncementService announcementService)
    : ApiEndpoint<GetAnnouncementByIdEndpoint.GetAnnouncementByIdRequest, AnnouncementDto>
{
    public class GetAnnouncementByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("announcements/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(GetAnnouncementByIdRequest req, CancellationToken ct)
    {
        var result = await announcementService.GetByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateAnnouncementEndpoint(IAnnouncementService announcementService)
    : ApiEndpoint<UpdateAnnouncementEndpoint.UpdateAnnouncementRequest, AnnouncementDto>
{
    public class UpdateAnnouncementRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Title { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public string? Content { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public bool IsGlobal { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public DateTime? ExpiresAt { get; set; }
    }

    public override void Configure()
    {
        Put("announcements/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Communication");
    }

    public override async Task HandleAsync(UpdateAnnouncementRequest req, CancellationToken ct)
    {
        var updateRequest = new LMS.Api.Contracts.UpdateAnnouncementRequest(req.Title, req.Content, req.IsGlobal, req.ExpiresAt);
        var result = await announcementService.UpdateAsync(req.Id, updateRequest, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteAnnouncementEndpoint(IAnnouncementService announcementService)
    : ApiEndpoint<DeleteAnnouncementEndpoint.DeleteAnnouncementRequest, bool>
{
    public class DeleteAnnouncementRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("announcements/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Communication");
    }

    public override async Task HandleAsync(DeleteAnnouncementRequest req, CancellationToken ct)
    {
        var result = await announcementService.DeleteAsync(req.Id, ct);
        await SendAsync(result.Match(
            deleted => true,
            errors => false), ct);
    }
}
