using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Webhooks;

public sealed class CreateWebhookSubscriptionEndpoint(IWebhookService webhookService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateWebhookSubscriptionRequest, WebhookSubscriptionDto>
{
    public override void Configure()
    {
        Post("webhooks/subscriptions");
        Policies(PermissionPolicy.Build(LmsPermissions.IntegrationsManage));
        Tags("Webhooks");
    }

    public override async Task HandleAsync(CreateWebhookSubscriptionRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await webhookService.CreateSubscriptionAsync(req, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetWebhookSubscriptionsEndpoint(IWebhookService webhookService)
    : ApiEndpointWithoutRequest<List<WebhookSubscriptionDto>>
{
    public override void Configure()
    {
        Get("webhooks/subscriptions");
        Policies(PermissionPolicy.Build(LmsPermissions.IntegrationsManage));
        Tags("Webhooks");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await webhookService.GetSubscriptionsAsync(ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteWebhookSubscriptionEndpoint(IWebhookService webhookService)
    : ApiEndpoint<DeleteWebhookSubscriptionEndpoint.DeleteWebhookSubscriptionRequest, bool>
{
    public class DeleteWebhookSubscriptionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("webhooks/subscriptions/{Id}");
        Policies(PermissionPolicy.Build(LmsPermissions.IntegrationsManage));
        Tags("Webhooks");
    }

    public override async Task HandleAsync(DeleteWebhookSubscriptionRequest req, CancellationToken ct)
    {
        var result = await webhookService.DeleteSubscriptionAsync(req.Id, ct);
        await SendAsync(result.Match(deleted => true, errors => false), ct);
    }
}

public sealed class TestWebhookSubscriptionEndpoint(IWebhookService webhookService)
    : ApiEndpoint<TestWebhookSubscriptionEndpoint.TestWebhookSubscriptionRequest, bool>
{
    public class TestWebhookSubscriptionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Post("webhooks/subscriptions/{Id}/test");
        Policies(PermissionPolicy.Build(LmsPermissions.IntegrationsManage));
        Tags("Webhooks");
    }

    public override async Task HandleAsync(TestWebhookSubscriptionRequest req, CancellationToken ct)
    {
        var result = await webhookService.TestSubscriptionAsync(req.Id, ct);
        await SendAsync(result.Match(success => true, errors => false), ct);
    }
}
