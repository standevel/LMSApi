using System;

namespace LMS.Api.Data.Entities;

public sealed class SystemParentPortalConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool AutoCreateGuardianAccountsOnStudentCreation { get; set; } = true;
    public bool SendGuardianInvitationEmail { get; set; } = true;
    public string DefaultRelationship { get; set; } = "Guardian";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
