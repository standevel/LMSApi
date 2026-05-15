using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class Country
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;          // ISO 3166-1 alpha-2 (e.g., "NG", "US")
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }                   // Local/native name
    public Region Region { get; set; } = Region.Other;
    public string? CallingCode { get; set; }                   // e.g., "+234"
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
}
