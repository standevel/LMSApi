using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;

namespace LMS.Api.Services;

public interface IAdmissionService
{
    Task<AdmissionApplication?> VerifyIdentityAsync(string email, string jambRegNumber);
    Task<AdmissionApplication> SaveApplicationAsync(AdmissionApplication application, IEnumerable<Guid>? documentIds = null);
    Task<AdmissionApplication> SubmitApplicationAsync(Guid applicationId);
    Task<IEnumerable<AdmissionApplication>> GetHistoryByEmailAsync(string email);
    Task<IEnumerable<AdmissionApplication>> GetHistoryByJambAsync(string jambRegNumber);
    Task<IEnumerable<Faculty>> GetFacultiesAsync();
    Task<IEnumerable<AcademicProgram>> GetProgramsByFacultyAsync(Guid facultyId);
    Task<IEnumerable<Department>> GetDepartmentsByFacultyAsync(Guid facultyId);
    Task<IEnumerable<AcademicProgram>> GetProgramsByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<AcademicSession>> GetAdmissionSessionsAsync();
    Task<AcademicSession?> GetActiveAdmissionSessionAsync();
    Task<IEnumerable<SponsorOrganization>> GetAdmissionSponsorsAsync();
    Task<SponsorOrganization> CreateSponsorAsync(string name, string? email = null, string? phone = null, CancellationToken ct = default);
    Task<IEnumerable<Subject>> GetAdmissionSubjectsAsync();
    Task<IEnumerable<AcademicLevel>> GetAcademicLevelsAsync();
    Task<IEnumerable<DocumentType>> GetRequiredDocumentTypesAsync(ApplicantType applicantType, Guid? programId = null);

    // Admin Methods
    Task<AdmissionApplication?> GetApplicationByIdAsync(Guid id);
    Task<IEnumerable<AdmissionApplication>> GetApplicationsAsync(AdmissionStatus? status = null, Guid? sessionId = null);
    Task<AdmissionApplication> UpdateApplicationStatusAsync(Guid id, AdmissionStatus status, Guid? updatedBy = null);
    Task<AdmissionApplication> RespondToOfferAsync(Guid id, bool acceptOffer);
    Task<IEnumerable<AutoAdmitResult>> AutoAdmitAsync(Guid sessionId, bool isDryRun);
    Task<AcademicProgram> UpdateProgramCriteriaAsync(Guid programId, int minScore, int maxAdmissions, string jambSubjectsJson, string oLevelSubjectsJson);
    Task<TransferValidationResult> ValidateTransferEligibilityAsync(Guid applicationId);

    // Registrar Methods - Student Account Creation
    Task<StudentAccountCreationResult> CreateStudentAccountAsync(Guid applicationId, Guid? updatedBy = null, CancellationToken ct = default);
    Task<List<PendingStudentAccountDto>> GetPendingStudentAccountsAsync(CancellationToken ct = default);
    
    // Document Auto-Suggestion
    Task<DocumentSuggestionResult> GetSuggestedDocumentsAsync(ApplicantType applicantType, string? nationality = null, Guid? programId = null);

    // Transfer student enhancements
    Task<TransferCreditResult> CalculateTransferableCreditsAsync(
        Guid applicationId,
        CancellationToken ct = default);

    Task<GradeConversionResult> ConvertCGPAAsync(
        Guid applicationId,
        CancellationToken ct = default);

    // Exchange student support
    Task<ExchangeEligibilityResult> ValidateExchangeEligibilityAsync(
        Guid applicationId,
        CancellationToken ct = default);

    // Direct entry prerequisite validation
    Task<PrerequisiteValidationResult> ValidateDirectEntryPrerequisitesAsync(
        Guid applicationId,
        CancellationToken ct = default);

    // Direct entry enhancement methods
    Task<DirectEntryPointsResult> CalculateDirectEntryPointsAsync(
        DirectEntryQualification qualification,
        DirectEntryGrade grade,
        CancellationToken ct = default);

    Task<LevelSuggestionResult> SuggestStartingLevelForQualificationAsync(
        DirectEntryQualification qualification,
        CancellationToken ct = default);

    // Visa & immigration validation
    Task<VisaValidationResult> ValidateVisaRequirementsAsync(
        Guid applicationId,
        CancellationToken ct = default);

    // Home institution verification (transfer/exchange students)
    Task<HomeInstitutionValidationResult> ValidateHomeInstitutionRequirementsAsync(
        Guid applicationId,
        CancellationToken ct = default);

    // Registry reminder methods
    Task<ReminderSendResult> SendReminderAsync(Guid applicationId, CancellationToken ct = default);
    Task<BulkReminderResult> SendBulkRemindersAsync(IEnumerable<Guid> applicationIds, CancellationToken ct = default);
}

public record VisaValidationResult(
    bool IsCompliant,
    string? Reason,
    bool VisaRequired,
    bool VisaApplied,
    bool FinancialProofProvided,
    bool PassportValid);

public record ExchangeEligibilityResult(
    bool IsEligible,
    string? Reason,
    bool HomeInstitutionApproved,
    bool DeansCertificateProvided,
    bool AcademicStandingVerified,
    bool PartnerAgreementActive);

public record PrerequisiteValidationResult(
    bool IsEligible,
    string? Reason,
    IEnumerable<RequiredSubject> MissingSubjects);

public record RequiredSubject(string SubjectCode, string SubjectName, string MinGrade, bool Met);

public record AutoAdmitResult(Guid ApplicationId, string FirstName, string LastName, string? MiddleName, string ProgramName, int JambScore, bool IsAdmitted, string? Reason);

public record TransferValidationResult(
    bool IsEligible,
    string? Reason,
    decimal? MinimumCGPA,
    int? MinimumCredits,
    Guid? EligibleStartingLevelId,
    string? EligibleStartingLevelName
);

public class StudentAccountCreationResult
{
    [System.Text.Json.Serialization.JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("studentId")]
    public Guid? StudentId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("officialEmail")]
    public string? OfficialEmail { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("temporaryPassword")]
    public string? TemporaryPassword { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("isExistingAccount")]
    public bool IsExistingAccount { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("amountDue")]
    public decimal AmountDue { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class PendingStudentAccountDto
{
    [System.Text.Json.Serialization.JsonPropertyName("applicationId")]
    public Guid ApplicationId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("applicationNumber")]
    public string ApplicationNumber { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("programName")]
    public string ProgramName { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("sessionName")]
    public string SessionName { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("offerAcceptedAt")]
    public DateTime? OfferAcceptedAt { get; set; }
}

public record DocumentSuggestionResult(
    IEnumerable<DocumentType> Required,
    IEnumerable<DocumentType> Recommended,
    string? Reason
);

public record HomeInstitutionValidationResult(
    bool IsEligible,
    string? Reason,
    bool HomeInstitutionApproved,
    bool DeansCertificateProvided,
    bool AcademicStandingVerified,
    bool AcademicStandingGood);

public record ReminderSendResult(
    bool Success,
    Guid ApplicationId,
    string? ErrorMessage,
    string? StudentEmail,
    string? StudentName);

public record BulkReminderResult(
    int TotalCount,
    int SentCount,
    int FailedCount,
    IEnumerable<ReminderSendResult> Results);
