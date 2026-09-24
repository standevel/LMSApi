using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Cafeteria;

// ─── Place Student Order ──────────────────────────────────────────────────────

public sealed class PlaceStudentOrderEndpoint(
    LmsDbContext db,
    IScholarshipService scholarshipService,
    ICafeteriaWalletService walletService)
    : ApiEndpoint<PlaceStudentOrderRequest, PlaceStudentOrderResponse>
{
    public override void Configure()
    {
        Post("cafeteria/order/PlaceOrder");
        Post("cafeteria/order/Order");
        Tags("CafeteriaOrder");
    }

    public override async Task HandleAsync(PlaceStudentOrderRequest req, CancellationToken ct)
    {
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
        var currentUsername = User.Identity?.Name
                              ?? User.FindFirst("name")?.Value
                              ?? User.FindFirst("preferred_username")?.Value;

        // Resolve the student. Non-staff may only place orders for themselves.
        var student = await ResolveStudentAsync(req.StudentId, currentUsername, ct);
        if (student is null)
        {
            await SendFailureAsync(404, "Student not found.", "STUDENT_NOT_FOUND", "Could not resolve the student for this order.", ct);
            return;
        }

        if (!isStaff)
        {
            var ownUser = currentUsername ?? string.Empty;
            var cleanOwn = ownUser.Trim().ToLowerInvariant();
            var isSelf = string.Equals(student.OfficialEmail, ownUser, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(student.StudentNumber, ownUser, StringComparison.OrdinalIgnoreCase)
                         || (student.StudentNumber != null && string.Equals(student.StudentNumber, cleanOwn, StringComparison.OrdinalIgnoreCase));
            if (!isSelf)
            {
                await SendFailureAsync(403, "Students may only place orders for themselves.", "FORBIDDEN", "You may only order for your own account.", ct);
                return;
            }
        }

        // Resolve menu item.
        string itemName;
        decimal price;
        string? imageUrl;
        string menuItemIdStr;
        string? resolvedVendorId = null;

        if (Guid.TryParse(req.MenuItemId, out var menuGuid) && await db.CafeteriaMenuItems.FindAsync([menuGuid], ct) is { } menuItem)
        {
            itemName = req.MenuName ?? menuItem.Name;
            price = req.Price ?? menuItem.Price;
            imageUrl = menuItem.ImageUrl;
            menuItemIdStr = menuGuid.ToString();
            resolvedVendorId = menuItem.VendorId;
        }
        else
        {
            itemName = req.MenuName ?? "Campus Meal";
            price = req.Price ?? 0m;
            imageUrl = null;
            menuItemIdStr = req.MenuItemId;
        }

        if (price <= 0)
        {
            await SendFailureAsync(400, "A valid menu price is required.", "INVALID_PRICE", "Price must be greater than zero.", ct);
            return;
        }

        var vendorId = !string.IsNullOrWhiteSpace(req.VendorId) ? req.VendorId : (resolvedVendorId ?? "1");
        var vendorName = "Campus Cafeteria";

        // Scholarship feeding coverage check.
        var isCovered = await scholarshipService.IsFeedingFullyCoveredAsync(student.Id, ct);

        // If not fully covered, charge the wallet (daily spend limit enforced inside).
        string studentUsername = student.OfficialEmail;
        if (!isCovered)
        {
            await walletService.EnsureAccountExistsAsync(studentUsername, ct);
            var debit = await walletService.TryDebitForMealAsync(studentUsername, price, "Meal Purchase", ct);
            if (!debit.Success)
            {
                await SendFailureAsync(
                    debit.DailyLimitExceeded ? 409 : 402,
                    debit.Message,
                    debit.DailyLimitExceeded ? "DAILY_LIMIT_EXCEEDED" : "INSUFFICIENT_FUNDS",
                    debit.Message, ct);
                return;
            }
        }

        var orderCode = "ORD-" + Random.Shared.Next(1000, 9999);
        var qrToken = GenerateQrToken();

        var order = new CafeteriaVendorOrder
        {
            OrderCode = orderCode,
            StudentUsername = studentUsername,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            MatricNo = student.StudentNumber ?? student.Id.ToString(),
            VendorId = vendorId,
            VendorName = vendorName,
            MenuItemId = menuItemIdStr,
            MenuItemName = itemName,
            ImageUrl = imageUrl ?? "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=600&q=80",
            Price = price,
            IsScholarshipCovered = isCovered,
            Station = null,
            Status = CafeteriaOrderStatus.New,
            QrToken = qrToken,
            CreatedAt = DateTime.UtcNow
        };

        db.CafeteriaVendorOrders.Add(order);
        await db.SaveChangesAsync(ct);

        await SendSuccessAsync(new PlaceStudentOrderResponse(
            order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
            order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
            order.Price, order.IsScholarshipCovered, "New", order.QrToken, order.CreatedAt), ct);
    }

    private async Task<Student?> ResolveStudentAsync(string? studentId, string? currentUserUsername, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(studentId) && Guid.TryParse(studentId, out var guidId))
        {
            return await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == guidId, ct);
        }

        // Treat as a username / matric / email lookup.
        var clean = (studentId ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(clean))
        {
            var found = await db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.OfficialEmail.ToLower() == clean
                      || (s.StudentNumber != null && s.StudentNumber.ToLower() == clean)
                      || s.Id.ToString().ToLower() == clean, ct);
            if (found is not null) return found;
        }

        // Fall back to the authenticated user's own identity.
        if (!string.IsNullOrWhiteSpace(currentUserUsername))
        {
            var own = currentUserUsername.Trim().ToLowerInvariant();
            return await db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.OfficialEmail.ToLower() == own
                      || (s.StudentNumber != null && s.StudentNumber.ToLower() == own), ct);
        }

        return null;
    }

    private static string GenerateQrToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
}

// ─── Student Order History ───────────────────────────────────────────────────

public sealed class GetStudentOrderHistoryEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<List<StudentOrderHistoryItemDto>>
{
    public override void Configure()
    {
        Get("cafeteria/order/OrderHistory/Student/{userId}");
        Tags("CafeteriaOrder");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<string>("userId");
        var orders = await db.CafeteriaVendorOrders
            .Where(o => o.StudentUsername == userId || o.MatricNo == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new StudentOrderHistoryItemDto(
                o.Id, o.OrderCode, o.StudentUsername, o.MenuItemName, o.Price,
                o.IsScholarshipCovered, o.Status.ToString(), o.CreatedAt, o.ClaimedAt))
            .ToListAsync(ct);

        await SendSuccessAsync(orders, ct);
    }
}

// ─── Confirm Meal Received ───────────────────────────────────────────────────

public sealed class ConfirmMealReceivedEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<bool>
{
    public override void Configure()
    {
        Post("cafeteria/order/ConfirmMealReceived/{id}");
        Tags("CafeteriaOrder");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idStr = Route<string>("id");
        var userId = Query<string?>("userId", isRequired: false) ?? string.Empty;

        if (!Guid.TryParse(idStr, out var id))
        {
            await SendFailureAsync(400, "Invalid order ID.", "INVALID_ID", "Order ID must be a valid GUID.", ct);
            return;
        }

        var order = await db.CafeteriaVendorOrders.FindAsync([id], ct);
        if (order is null)
        {
            await SendFailureAsync(404, "Order not found.", "NOT_FOUND", "Order not found.", ct);
            return;
        }

        // Non-staff: only confirm your own order.
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
        if (!isStaff && order.StudentUsername != userId && order.MatricNo != userId)
        {
            await SendFailureAsync(403, "You may only confirm your own meal.", "FORBIDDEN", "Not your order.", ct);
            return;
        }

        if (!CafeteriaOrderStateMachine.ValidateTransition(order.Status, CafeteriaOrderStatus.Claimed, out var transitionError))
        {
            await SendFailureAsync(409, "Order cannot be claimed in its current state.", "INVALID_TRANSITION", transitionError, ct);
            return;
        }

        order.Status = CafeteriaOrderStatus.Claimed;
        order.ClaimedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await SendSuccessAsync(true, ct);
    }
}
