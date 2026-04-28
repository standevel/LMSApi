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
    bool RequiresAcceptanceFee = false
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
}

public sealed record DocumentTypeResponse(
    Guid Id,
    string Name,
    string Code,
    string Category,
    bool IsCompulsory
);

public sealed record FacultyResponse(Guid Id, string Name, string Label);
public sealed record ProgramResponse(Guid Id, string Name, string Code);
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
