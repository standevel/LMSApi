using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Fees;

// ─── Helper to map FeeTemplate → FeeTemplateResponse ─────────────────────────

internal static class FeeMapper
{
    internal static FeeTemplateResponse ToResponse(FeeTemplate t) => new(
        t.Id, t.Name, t.Description, 
        t.FeeCategoryId, t.Category?.Name ?? "Unknown",
        t.Scope.ToString(),
        t.LineItems.Sum(li => li.Amount),
        t.IsActive,
        t.SessionId, t.Session?.Name,
        t.FacultyId, t.Faculty?.Name,
        t.ProgramId, t.Program?.Name,
        t.DueDate, t.LateFeeType.ToString(), t.LateFeeAmount, t.HasLateFee,
        t.LineItems.OrderBy(li => li.SortOrder).Select(li => new FeeLineItemResponse(
            li.Id, li.Name, li.Description, li.Amount, li.IsOptional, li.SortOrder)));

    internal static FeeCategoryResponse ToResponse(FeeCategory c) => new(
        c.Id, c.Name, c.Description, c.IsActive);

    internal static FeePaymentResponse ToPaymentResponse(FeePayment p) => new(
        p.Id, p.Amount, p.PaymentMethod.ToString(),
        p.ReferenceNumber, p.ReceiptUrl, p.GatewayReference,
        p.Status.ToString(), p.RejectionReason,
        p.PaidAt, p.ConfirmedAt, p.ConfirmedBy);
}
