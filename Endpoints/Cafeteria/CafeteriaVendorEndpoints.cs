using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Cafeteria;

internal static class CafeteriaVendorSecurity
{
    public static bool IsStaff(ClaimsPrincipal user) =>
        user.IsInRole("SuperAdmin") || user.IsInRole("Admin") || user.IsInRole("Finance");

    public static string ClaimedVendor(ClaimsPrincipal user) =>
        user.FindFirst("vendorId")?.Value ?? "1";

    public static string? ResolveVendor(ClaimsPrincipal user, string? queryVendor)
    {
        var isStaff = IsStaff(user);
        var claimed = ClaimedVendor(user);
        if (isStaff)
            return queryVendor ?? claimed ?? "1";

        if (!string.IsNullOrWhiteSpace(queryVendor) && queryVendor != claimed)
            return null; // cross-vendor attempt by non-staff

        return claimed ?? queryVendor ?? "1";
    }
}

// ─── GET Vendor Live Order Queue ──────────────────────────────────────────────

public sealed class GetVendorOrderQueueEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<List<VendorOrderDto>>
{
    public override void Configure()
    {
        Get("cafeteria/vendor/orders");
        Roles("SuperAdmin", "Admin", "Cafeteria Operator");
        Tags("CafeteriaVendor");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var queryVendor = Query<string?>("vendorId", isRequired: false);
        var vendorId = CafeteriaVendorSecurity.ResolveVendor(User, queryVendor);
        if (vendorId is null)
        {
            await SendFailureAsync(403, "You can only view your own vendor's orders.", "CROSS_VENDOR", "Vendor mismatch.", ct);
            return;
        }

        var orders = await db.CafeteriaVendorOrders
            .Where(o => o.VendorId == vendorId &&
                        o.Status != CafeteriaOrderStatus.Claimed &&
                        o.Status != CafeteriaOrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new VendorOrderDto(
                o.Id, o.OrderCode, o.StudentUsername, o.StudentName, o.MatricNo,
                o.VendorId, o.VendorName, o.MenuItemId, o.MenuItemName, o.ImageUrl,
                o.Price, o.IsScholarshipCovered, o.Station,
                o.Status.ToString(), o.QrToken, o.CreatedAt, o.ClaimedAt))
            .ToListAsync(ct);

        await SendSuccessAsync(orders, ct);
    }
}

// ─── UPDATE Order Status (KDS Kanban transition) ──────────────────────────────

public sealed class UpdateVendorOrderStatusEndpoint(LmsDbContext db)
    : ApiEndpoint<UpdateVendorOrderStatusRequest, VendorOrderDto>
{
    public override void Configure()
    {
        Post("cafeteria/vendor/orders/{id}/status");
        Roles("SuperAdmin", "Admin", "Cafeteria Operator");
        Tags("CafeteriaVendor");
    }

    public override async Task HandleAsync(UpdateVendorOrderStatusRequest req, CancellationToken ct)
    {
        var idStr = Route<string>("id");
        if (!Guid.TryParse(idStr, out var id))
        {
            await SendFailureAsync(400, "Invalid order ID.", "INVALID_ID", "Order ID must be a valid GUID.", ct);
            return;
        }

        var queryVendor = Query<string?>("vendorId", isRequired: false);
        var vendorId = CafeteriaVendorSecurity.ResolveVendor(User, queryVendor);
        if (vendorId is null)
        {
            await SendFailureAsync(403, "You can only update your own vendor's orders.", "CROSS_VENDOR", "Vendor mismatch.", ct);
            return;
        }

        var order = await db.CafeteriaVendorOrders.FindAsync([id], ct);
        if (order is null)
        {
            await SendFailureAsync(404, "Order not found.", "NOT_FOUND", "Order not found.", ct);
            return;
        }

        if (!CafeteriaVendorSecurity.IsStaff(User) && order.VendorId != vendorId)
        {
            await SendFailureAsync(403, "This order does not belong to your station.", "WRONG_VENDOR", "Vendor mismatch.", ct);
            return;
        }

        if (!Enum.TryParse<CafeteriaOrderStatus>(req.Status, ignoreCase: true, out var newStatus))
        {
            await SendFailureAsync(400, $"Invalid status '{req.Status}'.", "INVALID_STATUS", "Use: New, Preparing, ReadyForPickup, Claimed, Cancelled.", ct);
            return;
        }

        if (!CafeteriaOrderStateMachine.ValidateTransition(order.Status, newStatus, out var transitionError))
        {
            await SendFailureAsync(409, "Invalid order state transition.", "INVALID_TRANSITION", transitionError, ct);
            return;
        }

        order.Status = newStatus;
        if (newStatus == CafeteriaOrderStatus.Claimed)
            order.ClaimedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await SendSuccessAsync(new VendorOrderDto(
            order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
            order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
            order.Price, order.IsScholarshipCovered, order.Station,
            order.Status.ToString(), order.QrToken, order.CreatedAt, order.ClaimedAt), ct);
    }
}

// ─── Claim QR Meal Pass (counter scanner or manual code entry) ────────────────

public sealed class ClaimQrMealPassEndpoint(LmsDbContext db)
    : ApiEndpoint<ClaimQrMealPassRequest, ClaimQrMealPassResponse>
{
    public override void Configure()
    {
        Post("cafeteria/vendor/claim-qr");
        Roles("SuperAdmin", "Admin", "Cafeteria Operator");
        Tags("CafeteriaVendor");
    }

    public override async Task HandleAsync(ClaimQrMealPassRequest req, CancellationToken ct)
    {
        // Require a vendor identity: authenticated claim or explicit vendorId on the request.
        string? resolvedVendor;
        if (CafeteriaVendorSecurity.IsStaff(User))
        {
            resolvedVendor = req.VendorId ?? CafeteriaVendorSecurity.ClaimedVendor(User);
        }
        else
        {
            resolvedVendor = User.FindFirst("vendorId")?.Value ?? req.VendorId;
            if (string.IsNullOrWhiteSpace(resolvedVendor))
            {
                await SendFailureAsync(403, "A vendor identity is required to claim a meal pass.", "VENDOR_REQUIRED", "A vendorId claim or vendorId is required.", ct);
                return;
            }
        }

        CafeteriaVendorOrder? order = null;

        if (!string.IsNullOrWhiteSpace(req.QrToken))
            order = await db.CafeteriaVendorOrders
                .FirstOrDefaultAsync(o => o.QrToken == req.QrToken && o.Status != CafeteriaOrderStatus.Claimed && o.Status != CafeteriaOrderStatus.Cancelled, ct);

        if (order is null && !string.IsNullOrWhiteSpace(req.OrderCode))
            order = await db.CafeteriaVendorOrders
                .FirstOrDefaultAsync(o => o.OrderCode == req.OrderCode && o.Status != CafeteriaOrderStatus.Claimed && o.Status != CafeteriaOrderStatus.Cancelled, ct);

        if (order is null)
        {
            await SendFailureAsync(404, "Order not found or already claimed.", "NOT_FOUND", "The QR token or order code did not match any active order.", ct);
            return;
        }

        // Reject cross-vendor claims unless staff.
        if (!CafeteriaVendorSecurity.IsStaff(User) && order.VendorId != resolvedVendor)
        {
            await SendFailureAsync(403, "This order does not belong to your station.", "WRONG_VENDOR", "Vendor mismatch.", ct);
            return;
        }

        if (!CafeteriaOrderStateMachine.ValidateTransition(order.Status, CafeteriaOrderStatus.Claimed, out var transitionError))
        {
            await SendFailureAsync(409, "This order cannot be claimed in its current state.", "INVALID_TRANSITION", transitionError, ct);
            return;
        }

        order.Status    = CafeteriaOrderStatus.Claimed;
        order.ClaimedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = new VendorOrderDto(
            order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
            order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
            order.Price, order.IsScholarshipCovered, order.Station,
            order.Status.ToString(), order.QrToken, order.CreatedAt, order.ClaimedAt);

        await SendSuccessAsync(new ClaimQrMealPassResponse(true, $"✓ Order #{order.OrderCode} claimed successfully.", dto), ct);
    }
}

// ─── Vendor Daily Stats ───────────────────────────────────────────────────────

public sealed class GetVendorDailyStatsEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<VendorDailyStatsResponse>
{
    public override void Configure()
    {
        Get("cafeteria/vendor/stats");
        Roles("SuperAdmin", "Admin", "Finance", "Cafeteria Operator");
        Tags("CafeteriaVendor");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var queryVendor = Query<string?>("vendorId", isRequired: false);
        var vendorId = CafeteriaVendorSecurity.ResolveVendor(User, queryVendor);
        if (vendorId is null)
        {
            await SendFailureAsync(403, "You can only view your own vendor's statistics.", "CROSS_VENDOR", "Vendor mismatch.", ct);
            return;
        }

        var today = DateTime.UtcNow.Date;

        var orders = await db.CafeteriaVendorOrders
            .Where(o => o.VendorId == vendorId && o.CreatedAt >= today)
            .ToListAsync(ct);

        var claimed   = orders.Where(o => o.Status == CafeteriaOrderStatus.Claimed).ToList();
        var pending   = orders.Where(o => o.Status != CafeteriaOrderStatus.Claimed && o.Status != CafeteriaOrderStatus.Cancelled).ToList();

        var totalRevenue            = claimed.Sum(o => o.Price);
        var scholarshipRevenue      = claimed.Where(o => o.IsScholarshipCovered).Sum(o => o.Price);
        var walletRevenue           = claimed.Where(o => !o.IsScholarshipCovered).Sum(o => o.Price);
        var vendorName              = orders.FirstOrDefault()?.VendorName ?? vendorId;

        await SendSuccessAsync(new VendorDailyStatsResponse(
            vendorId, vendorName,
            totalRevenue, orders.Count, claimed.Count, pending.Count,
            scholarshipRevenue, walletRevenue), ct);
    }
}

// ─── Toggle Menu Item Availability (Sold Out / In Stock) ─────────────────────

public sealed record ToggleMenuItemAvailabilityRequest(string MenuItemId, bool IsAvailable);
public sealed record ToggleMenuItemAvailabilityResponse(string MenuItemId, bool IsAvailable, string Message);

public sealed class ToggleMenuItemAvailabilityEndpoint(LmsDbContext db)
    : ApiEndpoint<ToggleMenuItemAvailabilityRequest, ToggleMenuItemAvailabilityResponse>
{
    public override void Configure()
    {
        Post("cafeteria/vendor/menu/toggle-availability");
        Roles("SuperAdmin", "Admin", "Cafeteria Operator");
        Tags("CafeteriaVendor");
    }

    public override async Task HandleAsync(ToggleMenuItemAvailabilityRequest req, CancellationToken ct)
    {
        var queryVendor = Query<string?>("vendorId", isRequired: false);
        var vendorId = CafeteriaVendorSecurity.ResolveVendor(User, queryVendor);
        if (vendorId is null)
        {
            await SendFailureAsync(403, "You can only toggle availability for your own vendor's menu items.", "CROSS_VENDOR", "Vendor mismatch.", ct);
            return;
        }

        if (!Guid.TryParse(req.MenuItemId, out var menuItemGuid))
        {
            await SendFailureAsync(400, "Invalid menu item ID.", "INVALID_ID", "Menu item ID must be a valid GUID.", ct);
            return;
        }

        var update = db.CafeteriaMenuItems.Where(mi => mi.Id == menuItemGuid);
        if (!CafeteriaVendorSecurity.IsStaff(User))
            update = update.Where(mi => mi.VendorId == vendorId);

        var affected = await update.ExecuteUpdateAsync(
            mi => mi.SetProperty(m => m.IsAvailable, req.IsAvailable)
                   .SetProperty(m => m.UpdatedAt, DateTime.UtcNow), ct);

        if (affected == 0)
        {
            await SendFailureAsync(404, "Menu item not found or not owned by your vendor.", "NOT_FOUND", "Menu item not found.", ct);
            return;
        }

        var message = req.IsAvailable
            ? $"Menu item {req.MenuItemId} is now available."
            : $"Menu item {req.MenuItemId} marked as sold out.";

        await SendSuccessAsync(new ToggleMenuItemAvailabilityResponse(req.MenuItemId, req.IsAvailable, message), ct);
    }
}
