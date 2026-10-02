using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Hubs;
using LMS.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Cafeteria;

// ─── Place Student Order ──────────────────────────────────────────────────────

public sealed class PlaceStudentOrderEndpoint(
    LmsDbContext db,
    IScholarshipService scholarshipService,
    ICafeteriaWalletService walletService,
    IHubContext<CafeteriaHub> hubContext)
    : ApiEndpoint<PlaceStudentOrderRequest, PlaceStudentOrderResponse>
{
    public override void Configure()
    {
        Post("cafeteria/order/PlaceOrder");
        Post("cafeteria/order/Order");
        Post("cafeteria/order/claim-zero-pay");
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

        CafeteriaMenuItem? menuItem = null;
        if (Guid.TryParse(req.MenuItemId, out var menuGuid) && (menuItem = await db.CafeteriaMenuItems.FindAsync([menuGuid], ct)) is not null)
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


        var vendorId = !string.IsNullOrWhiteSpace(req.VendorId) ? req.VendorId : (resolvedVendorId ?? menuItem?.VendorId ?? "1");
        var vendorName = (Guid.TryParse(vendorId, out var vGuid) ? await db.Users.Where(u => u.Id == vGuid).Select(u => u.DisplayName ?? u.Username).FirstOrDefaultAsync(ct) : null)
                         ?? "Wigwe Campus Dining";
        string studentUsername = student.OfficialEmail;

        var isZeroPayRoute = HttpContext.Request.Path.Value?.Contains("claim-zero-pay", StringComparison.OrdinalIgnoreCase) == true;
        var hasScholarship = await scholarshipService.IsFeedingFullyCoveredAsync(student.Id, ct);

        bool isCovered = false;
        string mealSession = "Lunch";

        if (hasScholarship)
        {
            var claimResult = await scholarshipService.EvaluateScholarshipMealClaimAsync(
                student.Id,
                menuItem?.FeedingTimeId ?? 1,
                menuItem?.Name ?? itemName,
                ct);

            mealSession = claimResult.TargetMealWindow;

            if (claimResult.CanClaim)
            {
                isCovered = true;
            }
            else
            {
                if (isZeroPayRoute)
                {
                    await SendFailureAsync(422,
                        claimResult.Reason,
                        "SCHOLARSHIP_WINDOW_UNAVAILABLE",
                        claimResult.Reason, ct);
                    return;
                }

                // If placed through standard order channel, fall back to self-pay with wallet:
                // "others that pay by themselves can decide how they spend their money on meals"
                isCovered = false;
            }
        }
        else
        {
            if (isZeroPayRoute)
            {
                await SendFailureAsync(403,
                    "This student account does not have active scholarship feeding coverage. Please use standard order checkout with digital wallet.",
                    "NOT_SCHOLARSHIP_COVERED",
                    "Feeding is not covered by scholarship.", ct);
                return;
            }

            // Self-paying student:
            // "others that pay by themselves can decide how they spend their money on meals"
            isCovered = false;
            mealSession = (menuItem?.FeedingTimeId == 1 || itemName.Contains("breakfast", StringComparison.OrdinalIgnoreCase)) ? "Breakfast"
                        : (menuItem?.FeedingTimeId == 3 || itemName.Contains("dinner", StringComparison.OrdinalIgnoreCase)) ? "Dinner"
                        : "Lunch";
        }

        // If not covered by scholarship, charge the wallet (daily spend limit enforced inside).
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
            ImageUrl = imageUrl ?? menuItem?.ImageUrl,
            Price = price,
            IsScholarshipCovered = isCovered,
            MealSession = mealSession,
            Station = null,
            Status = CafeteriaOrderStatus.New,
            QrToken = qrToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(14)
        };

        db.CafeteriaVendorOrders.Add(order);
        await db.SaveChangesAsync(ct);

        var vendorOrderDto = new VendorOrderDto(
            order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
            order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
            order.Price, order.IsScholarshipCovered, order.Station,
            order.Status.ToString(), order.QrToken, order.CreatedAt, order.ClaimedAt);

        await hubContext.Clients.Group($"Vendor_{order.VendorId}").SendAsync("OrderCreated", vendorOrderDto, ct);

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

    private static (bool InWindow, string WindowName, string Hours) IsWithinMealSession(int feedingTimeId)
    {
        var watZone = GetWatTimeZone();
        var watNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, watZone);
        var timeOfDay = watNow.TimeOfDay;

        return feedingTimeId switch
        {
            1 => (timeOfDay >= new TimeSpan(7, 0, 0) && timeOfDay <= new TimeSpan(10, 0, 0), "Breakfast", "07:00 - 10:00"),
            2 => (timeOfDay >= new TimeSpan(12, 0, 0) && timeOfDay <= new TimeSpan(15, 30, 0), "Lunch", "12:00 - 15:30"),
            3 => (timeOfDay >= new TimeSpan(18, 0, 0) && timeOfDay <= new TimeSpan(21, 0, 0), "Dinner", "18:00 - 21:00"),
            _ => (true, "All-Day Dining", "07:00 - 21:00")
        };
    }

    private static TimeZoneInfo GetWatTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("WAT", TimeSpan.FromHours(1), "WAT", "WAT"); }
        }
    }
}

// ─── Place Batch Student Orders ──────────────────────────────────────────────

public sealed class PlaceBatchStudentOrderEndpoint(
    LmsDbContext db,
    IScholarshipService scholarshipService,
    ICafeteriaWalletService walletService,
    IHubContext<CafeteriaHub> hubContext)
    : ApiEndpoint<PlaceBatchStudentOrderRequest, PlaceBatchStudentOrderResponse>
{
    public override void Configure()
    {
        Post("cafeteria/order/PlaceBatchOrder",
             "cafeteria/order/batch");
        Tags("CafeteriaOrder");
    }

    public override async Task HandleAsync(PlaceBatchStudentOrderRequest req, CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name
                              ?? User.FindFirst("name")?.Value
                              ?? User.FindFirst("preferred_username")?.Value;

        var student = await ResolveStudentAsync(req.StudentId, currentUsername, ct);
        if (student is null)
        {
            await SendFailureAsync(404, "Student not found.", "STUDENT_NOT_FOUND", "Could not resolve the student for this batch order.", ct);
            return;
        }

        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
        if (!isStaff && !string.IsNullOrWhiteSpace(currentUsername))
        {
            var cleanOwn = currentUsername.Trim().ToLowerInvariant();
            var isSelf = string.Equals(student.OfficialEmail, cleanOwn, StringComparison.OrdinalIgnoreCase)
                         || (student.StudentNumber != null && string.Equals(student.StudentNumber, cleanOwn, StringComparison.OrdinalIgnoreCase));
            if (!isSelf)
            {
                await SendFailureAsync(403, "Students may only place orders for themselves.", "FORBIDDEN", "You may only order for your own account.", ct);
                return;
            }
        }

        if (req.MenuItemIds == null || req.MenuItemIds.Count == 0)
        {
            await SendFailureAsync(400, "At least one menu item must be selected.", "EMPTY_ORDER", "No menu items provided.", ct);
            return;
        }

        var hasScholarship = await scholarshipService.IsFeedingFullyCoveredAsync(student.Id, ct);
        string studentUsername = student.OfficialEmail;

        var placedResponses = new List<PlaceStudentOrderResponse>();
        var newOrders = new List<CafeteriaVendorOrder>();
        decimal totalWalletDebit = 0m;
        int scholarshipCoveredCount = 0;

        // Resolve each menu item
        var menuItems = new List<(CafeteriaMenuItem? Item, string RawId)>();
        foreach (var rawId in req.MenuItemIds)
        {
            CafeteriaMenuItem? item = null;
            if (Guid.TryParse(rawId, out var g))
            {
                item = await db.CafeteriaMenuItems.FindAsync([g], ct);
            }
            menuItems.Add((item, rawId));
        }

        // Evaluate coverage and pricing for each item
        foreach (var (item, rawId) in menuItems)
        {
            var itemName = item?.Name ?? "Campus Meal";
            var itemPrice = item?.Price ?? 0m;
            var feedingTimeId = item?.FeedingTimeId ?? 1;

            bool isCovered = false;
            string mealSession = "Lunch";

            if (hasScholarship)
            {
                var claimResult = await scholarshipService.EvaluateScholarshipMealClaimAsync(
                    student.Id,
                    feedingTimeId,
                    itemName,
                    ct);

                bool alreadyAllocatedInBatch = newOrders.Any(o => o.IsScholarshipCovered && o.MealSession == claimResult.TargetMealWindow);

                if (claimResult.CanClaim && !alreadyAllocatedInBatch)
                {
                    isCovered = true;
                    mealSession = claimResult.TargetMealWindow;
                    scholarshipCoveredCount++;
                }
                else
                {
                    isCovered = false;
                    mealSession = claimResult.TargetMealWindow;
                    totalWalletDebit += itemPrice;
                }
            }
            else
            {
                isCovered = false;
                mealSession = feedingTimeId switch
                {
                    1 => "Breakfast",
                    2 => "Lunch",
                    3 => "Dinner",
                    _ => "Lunch"
                };
                totalWalletDebit += itemPrice;
            }

            var orderCode = "ORD-" + Random.Shared.Next(1000, 9999);
            var qrToken = GenerateQrToken();
            var order = new CafeteriaVendorOrder
            {
                OrderCode = orderCode,
                StudentUsername = studentUsername,
                StudentName = $"{student.FirstName} {student.LastName}".Trim(),
                MatricNo = student.StudentNumber ?? student.Id.ToString(),
                VendorId = !string.IsNullOrWhiteSpace(req.VendorId) ? req.VendorId : (item?.VendorId ?? "1"),
                VendorName = (item?.VendorId != null && Guid.TryParse(item.VendorId, out var bGuid) ? await db.Users.Where(u => u.Id == bGuid).Select(u => u.DisplayName ?? u.Username).FirstOrDefaultAsync(ct) : null) ?? "Wigwe Campus Dining",
                MenuItemId = item != null ? item.Id.ToString() : rawId,
                MenuItemName = itemName,
                ImageUrl = item?.ImageUrl,
                Price = itemPrice,
                IsScholarshipCovered = isCovered,
                MealSession = mealSession,
                Station = null,
                Status = CafeteriaOrderStatus.New,
                QrToken = qrToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(14)
            };

            newOrders.Add(order);
        }

        // Debit wallet if any portion is self-paid
        if (totalWalletDebit > 0)
        {
            await walletService.EnsureAccountExistsAsync(studentUsername, ct);
            var debit = await walletService.TryDebitForMealAsync(studentUsername, totalWalletDebit, $"Batch Meal Purchase ({newOrders.Count} items)", ct);
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

        db.CafeteriaVendorOrders.AddRange(newOrders);
        await db.SaveChangesAsync(ct);

        foreach (var order in newOrders)
        {
            var vendorOrderDto = new VendorOrderDto(
                order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
                order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
                order.Price, order.IsScholarshipCovered, order.Station,
                order.Status.ToString(), order.QrToken, order.CreatedAt, order.ClaimedAt);

            await hubContext.Clients.Group($"Vendor_{order.VendorId}").SendAsync("OrderCreated", vendorOrderDto, ct);

            placedResponses.Add(new PlaceStudentOrderResponse(
                order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
                order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
                order.Price, order.IsScholarshipCovered, "New", order.QrToken, order.CreatedAt));
        }

        var message = scholarshipCoveredCount > 0 && totalWalletDebit == 0
            ? $"Successfully placed {newOrders.Count} meal orders fully covered under your scholarship plan!"
            : scholarshipCoveredCount > 0
                ? $"Successfully placed {newOrders.Count} meal orders ({scholarshipCoveredCount} covered by scholarship, ₦{totalWalletDebit:F2} charged to wallet)."
                : $"Successfully placed {newOrders.Count} meal orders charged to your digital wallet.";

        await SendSuccessAsync(new PlaceBatchStudentOrderResponse(
            true,
            message,
            newOrders.Count,
            totalWalletDebit,
            scholarshipCoveredCount,
            placedResponses), ct);
    }

    private async Task<Student?> ResolveStudentAsync(string? studentId, string? currentUserUsername, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(studentId) && Guid.TryParse(studentId, out var guidId))
        {
            return await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == guidId, ct);
        }

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

// ─── Cancel Student Order & Instant Refund ───────────────────────────────────

public sealed class CancelStudentOrderEndpoint(
    LmsDbContext db,
    ICafeteriaWalletService walletService,
    IHubContext<CafeteriaHub> hubContext)
    : ApiEndpoint<CancelStudentOrderRequest, CancelStudentOrderResponse>
{
    public override void Configure()
    {
        Post("cafeteria/order/{id}/cancel",
             "cafeteria/order/cancel");
        Tags("CafeteriaOrder");
    }

    public override async Task HandleAsync(CancelStudentOrderRequest req, CancellationToken ct)
    {
        var idStr = Route<string?>("id", isRequired: false);
        Guid orderId;
        if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var routeGuid))
        {
            orderId = routeGuid;
        }
        else if (req.OrderId.HasValue && req.OrderId.Value != Guid.Empty)
        {
            orderId = req.OrderId.Value;
        }
        else
        {
            await SendFailureAsync(400, "A valid order ID is required.", "INVALID_ID", "Order ID must be provided.", ct);
            return;
        }

        var order = await db.CafeteriaVendorOrders.FindAsync([orderId], ct);
        if (order is null)
        {
            await SendFailureAsync(404, "Order not found.", "NOT_FOUND", "Order not found.", ct);
            return;
        }

        var username = User.Identity?.Name
                       ?? User.FindFirst("name")?.Value
                       ?? User.FindFirst("preferred_username")?.Value;

        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance") || User.IsInRole("Cafeteria Operator");
        if (!isStaff && !string.Equals(order.StudentUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            await SendFailureAsync(403, "You may only cancel your own meal orders.", "FORBIDDEN", "Not your order.", ct);
            return;
        }

        if (order.Status != CafeteriaOrderStatus.New)
        {
            await SendFailureAsync(409, 
                $"Order cannot be cancelled because it is already '{order.Status}'. Orders can only be cancelled while in 'New' status before kitchen preparation begins.", 
                "CANNOT_CANCEL", 
                "Order is already being prepared or fulfilled.", ct);
            return;
        }

        order.Status = CafeteriaOrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);

        decimal refunded = 0m;
        decimal newBalance = 0m;
        if (!order.IsScholarshipCovered && order.Price > 0)
        {
            var refundResult = await walletService.RefundMealOrderAsync(order.Id, req.Reason ?? "Student cancelled unfulfilled order", ct);
            if (refundResult.Success)
            {
                refunded = order.Price;
                newBalance = refundResult.NewBalance;
            }
        }
        else
        {
            var bal = await walletService.GetBalanceAsync(order.StudentUsername, ct);
            newBalance = bal.WalletBalance;
        }

        var dto = new VendorOrderDto(
            order.Id, order.OrderCode, order.StudentUsername, order.StudentName, order.MatricNo,
            order.VendorId, order.VendorName, order.MenuItemId, order.MenuItemName, order.ImageUrl,
            order.Price, order.IsScholarshipCovered, order.Station,
            order.Status.ToString(), order.QrToken, order.CreatedAt, order.ClaimedAt);

        await hubContext.Clients.Group($"Vendor_{order.VendorId}").SendAsync("OrderStatusUpdated", dto, ct);

        await SendSuccessAsync(new CancelStudentOrderResponse(
            Success: true,
            Message: refunded > 0 
                ? $"Order {order.OrderCode} has been cancelled and {refunded:N2} NGN has been refunded to your wallet."
                : $"Order {order.OrderCode} has been cancelled.",
            OrderId: order.Id,
            OrderCode: order.OrderCode,
            RefundedAmount: refunded,
            NewWalletBalance: newBalance
        ), ct);
    }
}
