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
    string StudentName,
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
    IEnumerable<DocumentResponse> Documents
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

public sealed record SaveApplicationRequest(
    Guid? Id,
    string StudentName,
    string StudentEmail,
    string JambRegNumber,
    Guid AcademicSessionId,
    string Persona,
    Guid? FacultyId,
    Guid? AcademicProgramId,
    string ProgramReason,
    string QualificationsJson,
    string Phone,
    string EmergencyContactJson,
    string SponsorshipJson,
    IEnumerable<Guid>? DocumentIds = null
);

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
