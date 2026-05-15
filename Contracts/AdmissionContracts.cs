using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public sealed record IdentifyRequest(string Email, string JambRegNumber, string? ResultName = null);

public sealed class SubmitApplicationRequest
{
    public Guid Id { get; set; }
}

public sealed record AdmissionApplicationResponse(
    Guid Id,
    string ApplicationNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string StudentEmail,
    string JambRegNumber,
    Guid AcademicSessionId,
    string AcademicSessionName,
    string Persona,
    Guid? FacultyId,
    string FacultyName,
    Guid? AcademicProgramId,
    string AcademicProgramName,
    string ProgramReason,
    string QualificationsJson,
    string Phone,
    string EmergencyContactJson,
    string SponsorshipJson,
    string Status,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    IEnumerable<DocumentResponse> Documents,
    Guid? StudentUserId = null,
    Guid? AcceptanceFeeRecordId = null,
    decimal? AcceptanceFeeAmount = null,
    decimal? AcceptanceFeeBalance = null,
    string? AcceptanceFeeStatus = null,
    bool RequiresAcceptanceFee = false,
    // New fields for transfer/international/direct entry
    string? ApplicantType = null,
    string? PreviousInstitutionName = null,
    string? PreviousInstitutionCountry = null,
    decimal? PreviousCGPA = null,
    int? CreditsEarned = null,
    Guid? StartingLevelId = null,
    string? StartingLevelName = null,
    string? Nationality = null,
    string? PassportNumber = null,
    string? EnglishProficiencyScore = null,
    string? EnglishProficiencyType = null,
    // --- Phase 1: Country & Region ---
    string? CountryOfOrigin = null,
    string? CountryName = null,
    string? Region = null,
    // --- Phase 1: Enhanced Visa Fields ---
    string? VisaStatus = null,
    string? VisaType = null,
    DateTime? VisaExpiryDate = null,
    string? ImmigrationStatus = null,
    decimal? FinancialProofAmount = null,
    Guid? FinancialProofDocumentId = null,
    // --- Phase 3: Transfer Fields ---
    decimal? ConvertedCGPA = null,
    string? CGPAScaleName = null,
    decimal? CGPAScaleMax = null,
    decimal? TransferableCredits = null,
    int? TransferLevelSuggestion = null,
    int? IntendedSemester = null,
    // --- Phase 4: Exchange Fields ---
    string? ExchangeProgramType = null,
    string? ExchangeStatus = null,
    string? HomeInstitutionName = null,
    string? HomeInstitutionCountry = null,
    Guid? ExchangePartnerAgreementId = null,
    int? ExchangeDurationMonths = null,
    DateTime? ExchangeStartDate = null,
    DateTime? ExchangeEndDate = null,
    string? HomeInstitutionStanding = null,
    bool? HomeInstitutionVerified = null,
    DateTime? HomeInstitutionVerifiedAt = null,
    // --- Phase 2: Direct Entry Fields ---
    string? DirectEntryQualification = null,
    string? DirectEntryGrade = null,
    decimal? DirectEntryPoints = null,
    string? DirectEntryInstitution = null,
    int? DirectEntryYear = null,
    string? DirectEntrySubject1 = null,
    string? DirectEntrySubject2 = null,
    string? DirectEntrySubject3 = null
);


public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string FileUrl,
    Guid DocumentTypeId,
    string DocumentTypeName,
    string DocumentTypeCode,
    string Status,
    string? RejectionReason
);

public sealed record UpdateDocumentStatusRequest(string Status, string? RejectionReason = null);

public sealed record UploadMultipleDocumentsResponse(IEnumerable<DocumentResponse> Documents);

public sealed class SaveApplicationRequest
{
    public Guid? Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string StudentEmail { get; set; } = string.Empty;
    public string JambRegNumber { get; set; } = string.Empty;
    public Guid AcademicSessionId { get; set; }
    public string Persona { get; set; } = string.Empty;
    public string? FacultyId { get; set; }  // Changed to string to handle empty values
    public string? AcademicProgramId { get; set; }  // Changed to string to handle empty values
    public string ProgramReason { get; set; } = string.Empty;
    public string QualificationsJson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string EmergencyContactJson { get; set; } = string.Empty;
    public string SponsorshipJson { get; set; } = string.Empty;
    public IEnumerable<Guid>? DocumentIds { get; set; }

    // New fields for transfer/international/direct entry
    public string? ApplicantType { get; set; }
    public string? PreviousInstitutionName { get; set; }
    public string? PreviousInstitutionCountry { get; set; }
    public decimal? PreviousCGPA { get; set; }
    public int? CreditsEarned { get; set; }
    public Guid? StartingLevelId { get; set; }
    public string? Nationality { get; set; }
    public string? PassportNumber { get; set; }
    public string? EnglishProficiencyScore { get; set; }
    public string? EnglishProficiencyType { get; set; }
}

public sealed record DocumentTypeResponse(
    Guid Id,
    string Name,
    string Code,
    string Category,
    bool IsCompulsory,
    bool InternationalOnly = false,
    bool DirectEntryOnly = false,
    bool TransferOnly = false,
    bool NigeriaOnly = false
);

public sealed record FacultyResponse(Guid Id, string Name, string Label);
public sealed record ProgramResponse(Guid Id, string Name, string Code);
public sealed record AcademicLevelResponse(Guid Id, string Name, int Order, Guid ProgramId, string ProgramName);
public sealed record AcademicSessionResponse(Guid Id, string Name, DateTime StartDate, DateTime EndDate, bool IsActive);
public sealed record SponsorOrganizationResponse(Guid Id, string Name, string Code);
public sealed record SubjectResponse(Guid Id, string Name);

// Admin Contracts
public sealed record UpdateApplicationStatusRequest(string Status);
public sealed record AdmissionOfferDecisionRequest(bool AcceptOffer);
public sealed record InitiateOfferPaymentRequest(string Gateway, string CallbackUrl);
public sealed record AutoAdmitRequest(Guid SessionId, bool IsDryRun);
public sealed record UpdateProgramCriteriaRequest(int MinJambScore, int MaxAdmissions, string RequiredJambSubjectsJson, string RequiredOLevelSubjectsJson);

public sealed record LetterTemplateResponse(
    Guid Id,
    string Name,
    string TemplateType,
    string HeaderTitle,
    string HeaderSubtitle,
    string HeaderContact,
    string HeaderDate,
    string LogoBase64,
    string SignatureBase64,
    string SectionsJson,
    bool IsDefault
);

public sealed record SaveLetterTemplateRequest(
    string Name,
    string TemplateType,
    string HeaderTitle,
    string HeaderSubtitle,
    string HeaderContact,
    string HeaderDate,
    string LogoBase64,
    string SignatureBase64,
    string SectionsJson,
    bool IsDefault
);
public sealed record RegistryStatsResponse(
    int TotalStudents,
    int UndergraduateStudents,
    int PostgraduateStudents,
    int NewAdmissions,
    int PendingDocuments
);

public sealed record DocumentResubmissionContextResponse(
    Guid DocumentId,
    string FileName,
    string FileUrl,
    string DocumentTypeName,
    string DocumentTypeCode,
    Guid DocumentTypeId,
    string RejectionReason,
    DateTime UploadedAt,
    Guid ApplicationId
);
