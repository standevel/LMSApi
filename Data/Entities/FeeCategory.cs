using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public sealed class FeeCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsTuition { get; set; } = false;
    public bool IsAccommodation { get; set; } = false;

    // Navigation
    public ICollection<FeeTemplate> Templates { get; set; } = new List<FeeTemplate>();
}
