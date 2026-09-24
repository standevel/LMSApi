using System;

namespace LMS.Api.Data.Entities;

public enum CafeteriaOrderStatus
{
    New = 0,
    Preparing = 1,
    ReadyForPickup = 2,
    Claimed = 3,
    Cancelled = 4
}

public sealed class CafeteriaVendorOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderCode { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string MatricNo { get; set; } = string.Empty;
    public string VendorId { get; set; } = "1";
    public string VendorName { get; set; } = "Wigwe Gourmet Kitchen";
    public string MenuItemId { get; set; } = string.Empty;
    public string MenuItemName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    public bool IsScholarshipCovered { get; set; }
    public string? Station { get; set; }
    public CafeteriaOrderStatus Status { get; set; } = CafeteriaOrderStatus.New;
    public string QrToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
}
