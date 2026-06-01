using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Communication;

public sealed class CreateNotificationEndpoint(INotificationService notificationService)
    : ApiEndpoint<CreateNotificationRequest, NotificationDto>
{
    public override void Configure()
    {
        Post("notifications");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CreateNotificationRequest req, CancellationToken ct)
    {
        var result = await notificationService.CreateAsync(req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetUserNotificationsEndpoint(INotificationService notificationService)
    : ApiEndpointWithoutRequest<List<NotificationDto>>
{
    public override void Configure()
    {
        Get("notifications");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Get the current user id from the context
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;
        if (userId == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await notificationService.GetByRecipientIdAsync(userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetNotificationByIdEndpoint(INotificationService notificationService)
    : ApiEndpoint<GetNotificationByIdEndpoint.GetNotificationByIdRequest, NotificationDto>
{
    public class GetNotificationByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("notifications/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(GetNotificationByIdRequest req, CancellationToken ct)
    {
        var result = await notificationService.GetByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class MarkNotificationAsReadEndpoint(INotificationService notificationService)
    : ApiEndpoint<MarkNotificationAsReadEndpoint.MarkNotificationAsReadRequest, NotificationDto>
{
    public class MarkNotificationAsReadRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Put("notifications/{Id}/read");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(MarkNotificationAsReadRequest req, CancellationToken ct)
    {
        var result = await notificationService.MarkAsReadAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteNotificationEndpoint(INotificationService notificationService)
    : ApiEndpoint<DeleteNotificationEndpoint.DeleteNotificationRequest, bool>
{
    public class DeleteNotificationRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("notifications/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Communication");
    }

    public override async Task HandleAsync(DeleteNotificationRequest req, CancellationToken ct)
    {
        var result = await notificationService.DeleteAsync(req.Id, ct);
        await SendAsync(result.Match(
            deleted => true,
            errors => false), ct);
    }
}
