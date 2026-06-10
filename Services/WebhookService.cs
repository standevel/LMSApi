using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using LMS.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;
public class WebhookService : BaseService, IWebhookService
{
    private readonly LmsDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookService(LmsDbContext context, IHttpClientFactory httpClientFactory, IAuditService auditService) : base(auditService)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }
    public async Task<ErrorOr<WebhookSubscriptionDto>> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest request, Guid createdById, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return Error.Validation("InvalidInput", "URL is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EventTypes))
        {
            return Error.Validation("InvalidInput", "Event types are required.");
        }

        var subscription = new WebhookSubscription
        {
            Url = request.Url,
            Secret = Guid.NewGuid().ToString(), // Generate a secret for signature validation
            EventTypes = request.EventTypes,
            IsActive = true,
            RetryAttempts = request.RetryAttempts,
            TimeoutSeconds = request.TimeoutSeconds,
            CreatedById = createdById
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("CreateWebhookSubscription", "WebhookSubscription", subscription.Id.ToString(),
            $"Webhook subscription created for URL: {request.Url}", ct);

        return new WebhookSubscriptionDto(
            subscription.Id,
            subscription.Url,
            subscription.EventTypes,
            subscription.IsActive,
            subscription.RetryAttempts,
            subscription.TimeoutSeconds,
            subscription.CreatedAt);
    }

    public async Task<ErrorOr<List<WebhookSubscriptionDto>>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        var subscriptions = await _context.WebhookSubscriptions
            .Include(ws => ws.CreatedBy)
            .ToListAsync(ct);

        return subscriptions.Select(ws => new WebhookSubscriptionDto(
            ws.Id,
            ws.Url,
            ws.EventTypes,
            ws.IsActive,
            ws.RetryAttempts,
            ws.TimeoutSeconds,
            ws.CreatedAt)).ToList();
    }

    public async Task<ErrorOr<Deleted>> DeleteSubscriptionAsync(Guid id, CancellationToken ct = default)
    {
        var subscription = await _context.WebhookSubscriptions.FindAsync(new object[] { id }, ct);
        if (subscription == null)
        {
            return Error.NotFound("WebhookSubscription.NotFound", "Webhook subscription not found.");
        }

        _context.WebhookSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("DeleteWebhookSubscription", "WebhookSubscription", id.ToString(),
            $"Webhook subscription deleted: {id}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> TestSubscriptionAsync(Guid id, CancellationToken ct = default)
    {
        var subscription = await _context.WebhookSubscriptions
            .Include(ws => ws.CreatedBy)
            .FirstOrDefaultAsync(ws => ws.Id == id, ct);

        if (subscription == null)
        {
            return Error.NotFound("WebhookSubscription.NotFound", "Webhook subscription not found.");
        }

        if (!subscription.IsActive)
        {
            return Error.Validation("InvalidStatus", "Webhook subscription is not active.");
        }

        // In a real implementation, we would send a test payload to the webhook URL
        // For now, just simulate success
        await LogActionAsync("TestWebhookSubscription", "WebhookSubscription", id.ToString(),
            $"Webhook subscription tested: {id}", ct);

        return Result.Deleted;
    }
}