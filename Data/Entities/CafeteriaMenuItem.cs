using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Api.Data.Entities;

public sealed class CafeteriaMenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string VendorId { get; set; } = "1";

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int FeedingTimeId { get; set; } = 1;

    [MaxLength(100)]
    public string? FeedingTimeName { get; set; }

    public bool IsAvailable { get; set; } = true;
    public DateOnly AvailableDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
