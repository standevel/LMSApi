using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

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

    // Applicant Type & International/Transfer Info
    public ApplicantType ApplicantType { get; set; } = ApplicantType.UTME;
    public string? PreviousInstitutionName { get; set; }
    public string? PreviousInstitutionCountry { get; set; }
    public decimal? PreviousCGPA { get; set; }
    public int? CreditsEarned { get; set; }
    public Guid? StartingLevelId { get; set; }
    public AcademicLevel? StartingLevel { get; set; }

    // --- Original International Student Fields (kept for compatibility) ---
    public string? Nationality { get; set; }
    public string? PassportNumber { get; set; }
    public string? EnglishProficiencyScore { get; set; }
    public EnglishProficiencyType? EnglishProficiencyType { get; set; }

    // --- Phase 1: Country & Region Support ---
    public string? CountryOfOrigin { get; set; }              // ISO 3166-1 alpha-2 code
    public string? CountryName { get; set; }                   // Display name
    public Region? Region { get; set; }                        // Geographic region

    // --- Phase 1: Enhanced Visa & Immigration Fields ---
    public VisaStatus? VisaStatus { get; set; }
    public VisaType? VisaType { get; set; }
    public DateTime? VisaExpiryDate { get; set; }
    public ImmigrationStatus? ImmigrationStatus { get; set; }

    // Keep legacy fields for backward compatibility
    public bool? VisaRequired { get; set; }
    public string? VisaApplicationNumber { get; set; }
    public bool? FinancialProofProvided { get; set; }
    public decimal? FinancialProofAmount { get; set; }         // Converted from string
    public string? FinancialProofCurrency { get; set; }
    public Guid? FinancialProofDocumentId { get; set; }

    // --- Phase 3: Transfer Student Fields ---
    public decimal? ConvertedCGPA { get; set; }                // CGPA converted to LMS standard
    public string? CGPAScaleName { get; set; }                 // Original scale name
    public decimal? CGPAScaleMax { get; set; }                 // Original scale max
    public decimal? TransferableCredits { get; set; }          // Calculated transferable credits
    public int? TransferLevelSuggestion { get; set; }          // Suggested starting level based on credits
    public int? IntendedSemester { get; set; }                 // 1 = First, 2 = Second

    // --- Phase 4: Exchange Program Fields ---
    public ExchangeProgramType ExchangeProgramType { get; set; } = ExchangeProgramType.None;
    public ExchangeStatus ExchangeStatus { get; set; } = ExchangeStatus.Pending;
    public string? HomeInstitutionName { get; set; }
    public string? HomeInstitutionCountry { get; set; }
    public Guid? ExchangePartnerAgreementId { get; set; }
    public int? ExchangeDurationMonths { get; set; }
    public DateTime? ExchangeStartDate { get; set; }
    public DateTime? ExchangeEndDate { get; set; }
    public AcademicStanding? HomeInstitutionStanding { get; set; }

    // Exchange verification
    public bool HomeInstitutionVerified { get; set; } = false;
    public DateTime? HomeInstitutionVerifiedAt { get; set; }
    public string? HomeInstitutionVerifiedBy { get; set; }

    // Keep legacy flag for backward compatibility
    public bool IsExchangeProgram => ExchangeProgramType != ExchangeProgramType.None;

    // --- Phase 2: Direct Entry Enhanced Fields ---
    public DirectEntryQualification DirectEntryQualification { get; set; } = DirectEntryQualification.None;
    public string? DirectEntryGrade { get; set; }
    public decimal? DirectEntryPoints { get; set; }
    public string? DirectEntryInstitution { get; set; }
    public int? DirectEntryYear { get; set; }
    public string? DirectEntrySubject1 { get; set; }
    public string? DirectEntrySubject2 { get; set; }
    public string? DirectEntrySubject3 { get; set; }

    // Legacy field kept for backward compatibility
    public string? ALevelPoints { get; set; }

    // --- Phase 3: Home Institution Verification Documents ---
    public Guid? HomeInstitutionApprovalDocumentId { get; set; }
    public Guid? DeansCertificateDocumentId { get; set; }
    public Guid? HomeInstitutionTranscriptDocumentId { get; set; }

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

    // Credential Evaluations (for international students)
    public ICollection<CredentialEvaluation> CredentialEvaluations { get; set; } = new List<CredentialEvaluation>();
}
