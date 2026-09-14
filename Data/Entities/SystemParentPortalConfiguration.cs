using System;

namespace LMS.Api.Data.Entities;

public sealed class SystemParentPortalConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool AutoCreateGuardianAccountsOnStudentCreation { get; set; } = true;
    public bool SendGuardianInvitationEmail { get; set; } = true;
    public string DefaultRelationship { get; set; } = "Guardian";

    /// <summary>JSON array of enabled ParentAccessCategory ints. Empty/"[]" means all categories are enabled.</summary>
    public string AllowedCategoriesJson { get; set; } = "[]";

    /// <summary>When true, sensitive categories (PrivateMessages, LecturerConversations) require explicit student consent.</summary>
    public bool RequireStudentConsentForSensitive { get; set; } = true;

    /// <summary>When true, adult students are restricted by default and require explicit student consent for any category.</summary>
    public bool TreatAdultStudentsAsRestricted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
