using System;

namespace LMS.Api.Data.Entities;

public enum DocumentCategory
{
    Admission,
    Academic,
    Staff,
    Finance,
    General
}

public sealed class DocumentType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsCompulsory { get; set; }

    // Conditional flags for applicant types
    public bool InternationalOnly { get; set; } = false;
    public bool DirectEntryOnly { get; set; } = false;
    public bool TransferOnly { get; set; } = false;
    public bool NigeriaOnly { get; set; } = false;
    public bool ExchangeOnly { get; set; } = false;

    // Default permission rules (JSON)
    // Example: { "AllowedRoles": ["Admin", "Registrar"], "AllowOwner": true }
    public string DefaultAccessRules { get; set; } = "{}";
}
