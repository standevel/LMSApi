using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Communication;

public sealed class CreateMessageEndpoint(IMessageService messageService)
    : ApiEndpoint<CreateMessageRequest, MessageDto>
{
    public override void Configure()
    {
        Post("messages");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CreateMessageRequest req, CancellationToken ct)
    {
        var result = await messageService.CreateAsync(req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetUserMessagesEndpoint(IMessageService messageService)
    : ApiEndpointWithoutRequest<List<MessageDto>>
{
    public override void Configure()
    {
        Get("messages");
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

        // We'll get messages where the user is the recipient (inbox)
        var result = await messageService.GetByRecipientIdAsync(userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetMessageByIdEndpoint(IMessageService messageService)
    : ApiEndpoint<GetMessageByIdEndpoint.GetMessageByIdRequest, MessageDto>
{
    public class GetMessageByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("messages/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(GetMessageByIdRequest req, CancellationToken ct)
    {
        var result = await messageService.GetByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class MarkMessageAsReadEndpoint(IMessageService messageService)
    : ApiEndpoint<MarkMessageAsReadEndpoint.MarkMessageAsReadRequest, MessageDto>
{
    public class MarkMessageAsReadRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Put("messages/{Id}/read");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(MarkMessageAsReadRequest req, CancellationToken ct)
    {
        var result = await messageService.MarkAsReadAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteMessageEndpoint(IMessageService messageService)
    : ApiEndpoint<DeleteMessageEndpoint.DeleteMessageRequest, bool>
{
    public class DeleteMessageRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("messages/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(DeleteMessageRequest req, CancellationToken ct)
    {
        var result = await messageService.DeleteAsync(req.Id, ct);
        await SendAsync(result.Match(
            deleted => true,
            errors => false), ct);
    }
}
