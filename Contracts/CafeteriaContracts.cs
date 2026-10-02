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
    DateTime UpdatedAt,
    bool EnforceMealSessionWindows = false,
    bool AllowPreOrdersOutsideWindows = true
);

public sealed record UpdateSystemCafeteriaConfigurationRequest(
    bool EnableSelfServiceTopUp,
    bool AllowStudentSelfTopUp,
    bool AllowParentTopUp,
    decimal MaxSingleTopUpAmount = 100000m,
    decimal DailySpendLimit = 15000m,
    bool EnforceMealSessionWindows = false,
    bool AllowPreOrdersOutsideWindows = true
);

public sealed record VendorSettlementReportDto(
    string VendorId,
    string VendorName,
    DateOnly SettlementDate,
    decimal TotalRevenue,
    decimal DirectWalletRevenue,
    decimal ScholarshipSubsidyRevenue,
    int TotalOrders,
    int ClaimedOrders,
    int CancelledOrders,
    int PendingOrders,
    List<VendorSettlementItemDto> Items
);

public sealed record VendorSettlementItemDto(
    Guid OrderId,
    string OrderCode,
    string StudentUsername,
    string StudentName,
    string MatricNo,
    string MenuItemName,
    decimal Price,
    bool IsScholarshipCovered,
    string Status,
    DateTime CreatedAt,
    DateTime? ClaimedAt
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
    DateTime UpdatedAt,
    string? Allergens = null,
    string? DietaryFlags = null,
    int? Calories = null
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

public sealed record PlaceBatchStudentOrderRequest(
    string StudentId,
    List<string> MenuItemIds,
    string? VendorId = null
);

public sealed record PlaceBatchStudentOrderResponse(
    bool Success,
    string Message,
    int OrdersPlaced,
    decimal TotalChargedToWallet,
    int ScholarshipCoveredCount,
    List<PlaceStudentOrderResponse> Orders
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

public sealed record ParentStudentCafeteriaPolicyDto(
    string StudentUsername,
    string StudentName,
    decimal WalletBalance,
    decimal? ParentDailySpendLimit,
    decimal SystemDailySpendLimit,
    decimal EffectiveDailySpendLimit,
    decimal SpentToday
);

public sealed record SetParentDailyLimitRequest(
    string StudentUsername,
    decimal? DailyLimit
);

public sealed record SetParentDailyLimitResponse(
    bool Success,
    string Message,
    decimal? ParentDailySpendLimit,
    decimal EffectiveDailySpendLimit
);

public sealed record CancelStudentOrderRequest(
    Guid? OrderId = null,
    string? Reason = null
);

public sealed record CancelStudentOrderResponse(
    bool Success,
    string Message,
    Guid OrderId,
    string OrderCode,
    decimal RefundedAmount,
    decimal NewWalletBalance
);

public sealed record ParentChildMealOrderDto(
    Guid OrderId,
    string OrderCode,
    string MenuItemName,
    string? ImageUrl,
    decimal Price,
    bool IsScholarshipCovered,
    string VendorName,
    string Status,
    DateTime CreatedAt,
    DateTime? ClaimedAt
);

