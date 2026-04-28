using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public enum AdmissionStatus
{
    Draft,
    Submitted,
    UnderReview,
    Admitted,
    OfferAccepted,
    FeePaid,
    Rejected,
    Waitlisted
}

public sealed class AdmissionApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationNumber { get; set; } = string.Empty;

    // Identification
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string StudentEmail { get; set; } = string.Empty;
    public string JambRegNumber { get; set; } = string.Empty;

    // Academic Choice
    public Guid AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;

    public string Persona { get; set; } = string.Empty;
    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public Guid? AcademicProgramId { get; set; }
    public AcademicProgram? AcademicProgram { get; set; }

    public string ProgramReason { get; set; } = string.Empty;

    // Qualifications Data (Stored as JSON for flexibility, or could be separate tables)
    // For now, let's keep it structured if possible, but JSON is easier for Nigerian Subject lists
    public string QualificationsJson { get; set; } = "{}";

    // Contact & Sponsorship
    public string Phone { get; set; } = string.Empty;
    public string EmergencyContactJson { get; set; } = "{}";
    public string SponsorshipJson { get; set; } = "{}";

    // Status
    public AdmissionStatus Status { get; set; } = AdmissionStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Offer expiration date (set when status changes to Admitted)
    public DateTime? OfferExpiresAt { get; set; }
    
    // Offer acceptance tracking
    public DateTime? OfferAcceptedAt { get; set; }
    
    // Link to created Student record (set when offer is accepted)
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    
    // Track if Entra ID account was created (for idempotency)
    public string? EntraObjectId { get; set; }
    public string? OfficialEmail { get; set; }
    public DateTime? AccountCreatedAt { get; set; }

    // Linked Documents
    public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
}
