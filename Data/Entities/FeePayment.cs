using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class FeePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentFeeRecordId { get; set; }
    public StudentFeeRecord StudentFeeRecord { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Manual;

    // Manual payment fields
    public string? ReferenceNumber { get; set; }
    public string? ReceiptUrl { get; set; }

    // Gateway fields (Paystack / Hydrogen)
    public string? GatewayReference { get; set; }   // reference/transactionRef from gateway
    public string? GatewayCheckoutUrl { get; set; } // authorization_url / authorizationUrl

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? RejectionReason { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; } // username or "Gateway" for auto-confirmed
}
