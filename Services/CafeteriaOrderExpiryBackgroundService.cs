using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

/// <summary>
/// Background service that periodically scans for expired cafeteria meal passes.
/// Orders that expired before fulfillment are marked as Cancelled and automatically refunded to the student wallet.
/// </summary>
public sealed class CafeteriaOrderExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<CafeteriaOrderExpiryBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CafeteriaOrderExpiryBackgroundService is starting.");

        // Initial delay before first pass scan to allow application startup
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error occurred during expired meal orders sweep.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        logger.LogInformation("CafeteriaOrderExpiryBackgroundService is stopping.");
    }

    private async Task SweepExpiredOrdersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
        var walletService = scope.ServiceProvider.GetRequiredService<ICafeteriaWalletService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<CafeteriaHub>>();

        var now = DateTime.UtcNow;

        // Find active unfulfilled orders past their expiration TTL
        var expiredOrders = await db.CafeteriaVendorOrders
            .Where(o => o.ExpiresAt.HasValue && o.ExpiresAt.Value < now)
            .Where(o => o.Status == CafeteriaOrderStatus.New 
                     || o.Status == CafeteriaOrderStatus.Preparing
                     || o.Status == CafeteriaOrderStatus.ReadyForPickup)
            .Take(100)
            .ToListAsync(ct);

        if (expiredOrders.Count == 0) return;

        logger.LogInformation("Found {Count} expired meal orders to reconcile.", expiredOrders.Count);

        foreach (var order in expiredOrders)
        {
            var previousStatus = order.Status;
            order.Status = CafeteriaOrderStatus.Cancelled;

            try
            {
                await db.SaveChangesAsync(ct);

                // If unfulfilled (kitchen never prepared it) or uncollected, refund the student wallet
                if (!order.IsScholarshipCovered && order.Price > 0)
                {
                    var reason = previousStatus == CafeteriaOrderStatus.ReadyForPickup
                        ? "Uncollected meal pass expired at counter"
                        : "Unfulfilled meal pass expired before preparation";

                    var refundResult = await walletService.RefundMealOrderAsync(order.Id, reason, ct);
                    if (refundResult.Success)
                    {
                        logger.LogInformation("Auto-refunded {Amount:N2} NGN for expired order {OrderCode}.", order.Price, order.OrderCode);
                    }
                }

                // Notify vendor station
                var dto = new VendorOrderDto(
                    order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
                    order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
                    order.Price, order.IsScholarshipCovered, order.Station,
                    order.Status.ToString(), order.QrToken, order.CreatedAt, order.ClaimedAt);

                await hubContext.Clients.Group($"Vendor_{order.VendorId}").SendAsync("OrderStatusUpdated", dto, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to reconcile expired order {OrderCode}.", order.OrderCode);
            }
        }
    }
}
