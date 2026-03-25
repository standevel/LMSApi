using System;

namespace LMS.Api.Data.Entities;

public sealed class SponsorOrganization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g., SP-001
    public bool IsActive { get; set; } = true;
}
