using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;

namespace LMS.Api.Services;

public interface IPushNotificationService
{
    Task<ErrorOr<Deleted>> SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct = default);
    Task SendNotificationAsync(Guid userId, string title, string message, string? url = null, CancellationToken ct = default);
    string GetVapidPublicKey();
}
