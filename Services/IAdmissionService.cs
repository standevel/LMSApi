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
    Task<IEnumerable<AcademicSession>> GetAdmissionSessionsAsync();
    Task<AcademicSession?> GetActiveAdmissionSessionAsync();
    Task<IEnumerable<SponsorOrganization>> GetAdmissionSponsorsAsync();
    Task<IEnumerable<Subject>> GetAdmissionSubjectsAsync();
    Task<IEnumerable<AcademicLevel>> GetAcademicLevelsAsync();
    Task<IEnumerable<DocumentType>> GetRequiredDocumentTypesAsync(ApplicantType applicantType, Guid? programId = null);

    // Admin Methods
    Task<AdmissionApplication?> GetApplicationByIdAsync(Guid id);
    Task<IEnumerable<AdmissionApplication>> GetApplicationsAsync(AdmissionStatus? status = null, Guid? sessionId = null);
    Task<AdmissionApplication> UpdateApplicationStatusAsync(Guid id, AdmissionStatus status);
    Task<AdmissionApplication> RespondToOfferAsync(Guid id, bool acceptOffer);
    Task<IEnumerable<AutoAdmitResult>> AutoAdmitAsync(Guid sessionId, bool isDryRun);
    Task<AcademicProgram> UpdateProgramCriteriaAsync(Guid programId, int minScore, int maxAdmissions, string jambSubjectsJson, string oLevelSubjectsJson);
    Task<TransferValidationResult> ValidateTransferEligibilityAsync(Guid applicationId);

    // Registrar Methods - Student Account Creation
    Task<StudentAccountCreationResult> CreateStudentAccountAsync(Guid applicationId, CancellationToken ct = default);
    Task<List<PendingStudentAccountDto>> GetPendingStudentAccountsAsync(CancellationToken ct = default);
    
    // Document Auto-Suggestion
    Task<DocumentSuggestionResult> GetSuggestedDocumentsAsync(ApplicantType applicantType, string? nationality = null, Guid? programId = null);
}

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
