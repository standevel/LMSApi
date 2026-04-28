using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

// ─── Template ───────────────────────────────────────────────────────────────

public sealed record CreateFeeTemplateRequest(
    string Name,
    string Description,
    Guid FeeCategoryId,
    FeeScope Scope,
    Guid? SessionId,
    Guid? FacultyId,
    Guid? ProgramId,
    DateTime? DueDate,
    LateFeeType LateFeeType,
    decimal LateFeeAmount
);

public sealed record UpdateFeeTemplateRequest(
    string Name,
    string Description,
    Guid FeeCategoryId,
    Guid? SessionId,
    Guid? FacultyId,
    Guid? ProgramId,
    DateTime? DueDate,
    LateFeeType LateFeeType,
    decimal LateFeeAmount
);

public sealed record CreateFeeCategoryRequest(string Name, string Description);
public sealed record UpdateFeeCategoryRequest(string Name, string Description, bool IsActive);
public sealed record FeeCategoryResponse(Guid Id, string Name, string Description, bool IsActive);

// ─── Line Items ──────────────────────────────────────────────────────────────

public sealed record AddFeeLineItemRequest(string Name, string Description, decimal Amount, bool IsOptional, int SortOrder);
public sealed record UpdateFeeLineItemRequest(string Name, string Description, decimal Amount, bool IsOptional, int SortOrder);

// ─── Assignments ─────────────────────────────────────────────────────────────

public sealed record AssignFeeRequest(
    Guid FeeTemplateId,
    FeeScope Scope,
    Guid? FacultyId,
    Guid? ProgramId,
    Guid? StudentId,
    Guid? SessionId,
    decimal? AmountOverride,
    DateTime? DueDateOverride
);

// ─── Payments ─────────────────────────────────────────────────────────────────

public sealed record InitiateGatewayPaymentRequest(
    Guid StudentFeeRecordId,
    decimal Amount,
    string Gateway,     // "Paystack" or "Hydrogen"
    string CallbackUrl,
    string CustomerEmail,
    string CustomerName
);

public sealed record GatewayInitResponse(string CheckoutUrl, string GatewayReference);

public sealed record RecordManualPaymentRequest(
    Guid StudentFeeRecordId,
    decimal Amount,
    string ReferenceNumber
);

public sealed record ConfirmPaymentRequest(string? Note = null);
public sealed record RejectPaymentRequest(string RejectionReason);

// ─── Late Fees ────────────────────────────────────────────────────────────────

public sealed record ApplyLateFeesRequest(Guid SessionId, bool IsDryRun = true);

// ─── Responses ────────────────────────────────────────────────────────────────

public sealed record FeeLineItemResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Amount,
    bool IsOptional,
    int SortOrder
);

public sealed record FeeTemplateResponse(
    Guid Id,
    string Name,
    string Description,
    Guid FeeCategoryId,
    string CategoryName,
    string Scope,
    decimal TotalAmount,
    bool IsActive,
    Guid? SessionId,
    string? SessionName,
    Guid? FacultyId,
    string? FacultyName,
    Guid? ProgramId,
    string? ProgramName,
    DateTime? DueDate,
    string LateFeeType,
    decimal LateFeeAmount,
    bool HasLateFee,
    IEnumerable<FeeLineItemResponse> LineItems
);

public sealed record FeeAssignmentResponse(
    Guid Id,
    Guid FeeTemplateId,
    string FeeTemplateName,
    string Scope,
    Guid? FacultyId,
    string? FacultyName,
    Guid? ProgramId,
    string? ProgramName,
    Guid? StudentId,
    string? StudentName,
    Guid? SessionId,
    string? SessionName,
    decimal? AmountOverride,
    DateTime? DueDateOverride,
    bool IsActive
);

public sealed record FeePaymentResponse(
    Guid Id,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? ReceiptUrl,
    string? GatewayReference,
    string Status,
    string? RejectionReason,
    DateTime PaidAt,
    DateTime? ConfirmedAt,
    string? ConfirmedBy
);

public sealed record LateFeeApplicationResponse(
    Guid Id,
    Guid FeeTemplateId,
    string FeeTemplateName,
    decimal AmountCharged,
    string FeeType,
    decimal BaseRateUsed,
    DateTime EffectiveDueDate,
    DateTime AppliedAt,
    string AppliedBy
);

public sealed record StudentBillResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid SessionId,
    string SessionName,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    bool LateFeeApplied,
    decimal LateFeeTotal,
    string Status,
    DateTime GeneratedAt,
    IEnumerable<FeeTemplateResponse> AppliedTemplates,
    IEnumerable<FeePaymentResponse> Payments,
    IEnumerable<LateFeeApplicationResponse> LateFeeApplications
);

public sealed record ApplyLateFeesResult(
    Guid StudentFeeRecordId,
    string StudentName,
    string FeeTemplateName,
    decimal SurchargeApplied,
    DateTime DueDate,
    bool Applied,
    string? Reason
);
