using System;

namespace LMS.Api.Data.Entities;

public sealed class FeeLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "NGN";
    public decimal? ExchangeRate { get; set; }
    public DateTime? RateDate { get; set; }
    public bool IsOptional { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}
