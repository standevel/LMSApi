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
    string? CallbackUrl = null
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
