using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace LMS.Api.Services;

public class PushNotificationService : BaseService, IPushNotificationService
{
    private readonly LmsDbContext _context;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly string _vapidSubject;
    private readonly string _vapidPublicKey;
    private readonly string _vapidPrivateKey;

    // Static fallback VAPID keys to ensure persistence during dev server runs
    private static readonly string _fallbackPublicKey;
    private static readonly string _fallbackPrivateKey;

    static PushNotificationService()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        _fallbackPublicKey = keys.PublicKey;
        _fallbackPrivateKey = keys.PrivateKey;
    }

    public PushNotificationService(
        LmsDbContext context,
        IConfiguration configuration,
        ILogger<PushNotificationService> logger,
        IAuditService auditService) : base(auditService)
    {
        _context = context;
        _logger = logger;

        _vapidSubject = configuration["PushNotifications:VapidSubject"] ?? "mailto:admin@wigweuniversity.edu.ng";
        
        var pubKey = configuration["PushNotifications:VapidPublicKey"];
        var privKey = configuration["PushNotifications:VapidPrivateKey"];

        if (string.IsNullOrEmpty(pubKey) || string.IsNullOrEmpty(privKey))
        {
            _vapidPublicKey = _fallbackPublicKey;
            _vapidPrivateKey = _fallbackPrivateKey;
            _logger.LogWarning("VAPID Keys not fully configured. Using auto-generated fallback keys. PUBLIC KEY: {PublicKey}", _vapidPublicKey);
        }
        else
        {
            _vapidPublicKey = pubKey;
            _vapidPrivateKey = privKey;
            _logger.LogInformation("VAPID keys loaded from configuration.");
        }
    }

    public string GetVapidPublicKey() => _vapidPublicKey;

    public async Task<ErrorOr<Deleted>> SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return Error.Validation("InvalidEndpoint", "Subscription endpoint is required.");
            }

            // Check if user exists
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId, ct);
            if (!userExists)
            {
                return Error.NotFound("User.NotFound", "User not found.");
            }

            // Check if subscription already exists for this endpoint
            var existing = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

            if (existing != null)
            {
                // Update owner/keys if they changed
                existing.UserId = userId;
                existing.P256dh = p256dh;
                existing.Auth = auth;
                existing.CreatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                var subscription = new LMS.Api.Data.Entities.PushSubscription
                {
                    UserId = userId,
                    Endpoint = endpoint,
                    P256dh = p256dh,
                    Auth = auth
                };
                _context.PushSubscriptions.Add(subscription);
            }

            await _context.SaveChangesAsync(ct);

            await LogActionAsync("PushSubscribe", "PushSubscription", userId.ToString(),
                $"User subscribed to browser push notifications: {endpoint.Substring(0, Math.Min(endpoint.Length, 30))}...", ct);

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing user {UserId} to push notifications", userId);
            return Error.Failure("PushSubscriptionFailed", $"Failed to subscribe: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Deleted>> UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return Error.Validation("InvalidEndpoint", "Subscription endpoint is required.");
            }

            var subscription = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);

            if (subscription == null)
            {
                return Error.NotFound("PushSubscription.NotFound", "Subscription not found.");
            }

            _context.PushSubscriptions.Remove(subscription);
            await _context.SaveChangesAsync(ct);

            await LogActionAsync("PushUnsubscribe", "PushSubscription", userId.ToString(),
                $"User unsubscribed from browser push notifications: {endpoint.Substring(0, Math.Min(endpoint.Length, 30))}...", ct);

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing user {UserId}", userId);
            return Error.Failure("PushUnsubscriptionFailed", $"Failed to unsubscribe: {ex.Message}");
        }
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, string? url = null, CancellationToken ct = default)
    {
        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (!subscriptions.Any())
        {
            _logger.LogInformation("No active push subscriptions found for user {UserId}", userId);
            return;
        }

        var webPushClient = new WebPushClient();
        var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);

        // Serialize notification payload
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = title,
            body = message,
            url = url
        });

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                
                _logger.LogInformation("Sending web push notification to user {UserId} endpoint {Endpoint}", userId, sub.Endpoint.Substring(0, Math.Min(sub.Endpoint.Length, 30)));
                
                await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails, ct);
            }
            catch (WebPushException ex)
            {
                _logger.LogWarning(ex, "Failed to deliver push notification to user {UserId} (Status: {StatusCode})", userId, ex.StatusCode);
                
                // If endpoint is no longer active (410 Gone or 404 Not Found), clean it up from DB
                if (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Removing inactive subscription endpoint for user {UserId}", userId);
                    try
                    {
                        // Use a fresh context or remove direct to avoid tracking issues
                        var entry = _context.Entry(sub);
                        if (entry.State == EntityState.Detached)
                        {
                            _context.PushSubscriptions.Attach(sub);
                        }
                        _context.PushSubscriptions.Remove(sub);
                        await _context.SaveChangesAsync(ct);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Failed to clean up stale push subscription {SubId}", sub.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending push notification to user {UserId} subscription {SubId}", userId, sub.Id);
            }
        }
    }
}
