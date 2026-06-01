using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IWebhookService
{
    Task<ErrorOr<WebhookSubscriptionDto>> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest request, Guid createdById, CancellationToken ct = default);
    Task<ErrorOr<List<WebhookSubscriptionDto>>> GetSubscriptionsAsync(CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteSubscriptionAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> TestSubscriptionAsync(Guid id, CancellationToken ct = default);
}