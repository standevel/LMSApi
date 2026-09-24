using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public sealed record CafeteriaWalletBalanceResponse(
    Guid AccountId,
    Guid? UserId,
    string Username,
    string FullName,
    decimal WalletBalance,
    IEnumerable<CafeteriaWalletTransactionDto> RecentTransactions
);

public sealed record CafeteriaWalletTransactionDto(
    Guid TransactionId,
    decimal Amount,
    string TransactionType,
    string Gateway,
    string Reference,
    string Status,
    string Description,
    decimal BalanceAfter,
    DateTime CreatedAt,
    DateTime? VerifiedAt
);

public sealed record InitializeWalletTopUpRequest(
    string Username,
    decimal Amount,
    string Gateway = "Paystack", // Paystack or Hydrogen
    string? CallbackUrl = null,
    string? PayerEmail = null,
    string? PayerRole = null,
    string? TargetUsername = null
);

public sealed record InitializeWalletTopUpResponse(
    bool Status,
    string Message,
    string AuthorizationUrl,
    string Reference,
    decimal Amount
);

public sealed record VerifyWalletTopUpRequest(
    string Reference,
    string Username,
    decimal Amount = 0m,
    string Gateway = "Paystack"
);

public sealed record VerifyWalletTopUpResponse(
    bool Status,
    string ResponseCode,
    string ResponseMessage,
    decimal NewBalance,
    string PaymentReference,
    string Gateway
);

public sealed record DirectWalletTopUpRequest(
    string Username,
    decimal Amount,
    string PaymentReference,
    string PaymentChannel = "Direct Instant",
    string Description = "Direct Wallet Credit"
);

public sealed record SystemCafeteriaConfigurationDto(
    Guid Id,
    bool EnableSelfServiceTopUp,
    bool AllowStudentSelfTopUp,
    bool AllowParentTopUp,
    decimal MaxSingleTopUpAmount,
    decimal DailySpendLimit,
    DateTime UpdatedAt
);

public sealed record UpdateSystemCafeteriaConfigurationRequest(
    bool EnableSelfServiceTopUp,
    bool AllowStudentSelfTopUp,
    bool AllowParentTopUp,
    decimal MaxSingleTopUpAmount = 100000m,
    decimal DailySpendLimit = 15000m
);

public sealed record VendorOrderDto(
    Guid Id,
    string OrderCode,
    string StudentUsername,
    string StudentName,
    string MatricNo,
    string VendorId,
    string VendorName,
    string MenuItemId,
    string MenuItemName,
    string ImageUrl,
    decimal Price,
    bool IsScholarshipCovered,
    string? Station,
    string Status,
    string QrToken,
    DateTime CreatedAt,
    DateTime? ClaimedAt
);

public sealed record UpdateVendorOrderStatusRequest(
    string Status
);

public sealed record ClaimQrMealPassRequest(
    string? QrToken,
    string? OrderCode,
    string? VendorId
);

public sealed record ClaimQrMealPassResponse(
    bool Success,
    string Message,
    VendorOrderDto? Order
);

public sealed record VendorDailyStatsResponse(
    string VendorId,
    string VendorName,
    decimal TotalRevenue,
    int TotalOrders,
    int ClaimedOrders,
    int PendingOrders,
    decimal ScholarshipCoveredRevenue,
    decimal WalletRevenue
);

public sealed record FeedingTimeDto(int Id, string Name, string? StartTime, string? EndTime);

public sealed record MenuItemDto(
    Guid Id,
    string VendorId,
    string Name,
    string? Description,
    string? ImageUrl,
    decimal Price,
    int FeedingTimeId,
    string? FeedingTimeName,
    bool IsAvailable,
    DateOnly AvailableDate,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record UpdateMenuItemAvailabilityRequest(string MenuItemId, bool IsAvailable);
public sealed record UpdateMenuItemAvailabilityResponse(string MenuItemId, bool IsAvailable, string Message);

public sealed record PayWithWalletRequest(string Username, decimal Amount, string? Description = "Meal Purchase");
public sealed record PayWithWalletResponse(bool Status, string Message, decimal NewBalance);

public sealed record WalletDebitResult(
    bool Success,
    decimal NewBalance,
    string Message,
    bool DailyLimitExceeded = false,
    bool InsufficientFunds = false
);

public sealed record PlaceStudentOrderRequest(
    string StudentId,
    string MenuItemId,
    string? MenuName = null,
    decimal? Price = null,
    string? VendorId = null
);

public sealed record PlaceStudentOrderResponse(
    Guid OrderId,
    string OrderCode,
    string StudentUsername,
    string StudentName,
    string MatricNo,
    string VendorId,
    string VendorName,
    string MenuItemId,
    string MenuItemName,
    string ImageUrl,
    decimal Price,
    bool IsScholarshipCovered,
    string Status,
    string QrToken,
    DateTime CreatedAt
);

public sealed record StudentOrderHistoryItemDto(
    Guid OrderId,
    string OrderCode,
    string StudentUsername,
    string MenuItemName,
    decimal Price,
    bool IsScholarshipCovered,
    string Status,
    DateTime CreatedAt,
    DateTime? ClaimedAt
);
