using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class FamilyCommunicationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentGuardianId { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    public bool AllowMessageSending { get; set; } = true;
    public bool ReceiveAcademicUpdates { get; set; } = true;
    public bool ReceiveAttendanceAlerts { get; set; } = true;
    public bool ReceiveGradeUpdates { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ParentGuardian? ParentGuardian { get; set; }
}