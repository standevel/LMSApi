using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;

namespace LMS.Api.Endpoints.Admissions;

public sealed class SaveApplicationEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<SaveApplicationRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("admissions/save");
        AllowAnonymous(); // Admission is public until student is fully onboarded
        Tags("Admissions");
        Description(d => d
            .WithName("Save Draft Application") 
            .WithTags("Admissions")
            .WithSummary("Save or update a draft admission application without submitting it"));
    }

    public override async Task HandleAsync(SaveApplicationRequest req, CancellationToken ct)
    {
        // Parse GUID strings safely
        Guid? facultyId = null;
        if (!string.IsNullOrEmpty(req.FacultyId))
        {
            if (Guid.TryParse(req.FacultyId, out var facultyGuid))
            {
                facultyId = facultyGuid;
            }
        }

        Guid? academicProgramId = null;
        if (!string.IsNullOrEmpty(req.AcademicProgramId))
        {
            if (Guid.TryParse(req.AcademicProgramId, out var programGuid))
            {
                academicProgramId = programGuid;
            }
        }

        // Parse applicant type
        ApplicantType applicantType = ApplicantType.UTME;
        if (!string.IsNullOrEmpty(req.ApplicantType))
        {
            if (Enum.TryParse<ApplicantType>(req.ApplicantType, out var parsedType))
            {
                applicantType = parsedType;
            }
        }

        // Parse English proficiency type
        EnglishProficiencyType? englishProficiencyType = null;
        if (!string.IsNullOrEmpty(req.EnglishProficiencyType))
        {
            if (Enum.TryParse<EnglishProficiencyType>(req.EnglishProficiencyType, out var parsedEngType))
            {
                englishProficiencyType = parsedEngType;
            }
        }

        Region? region = null;
        if (!string.IsNullOrEmpty(req.Region) && Enum.TryParse<Region>(req.Region, out var parsedRegion))
        {
            region = parsedRegion;
        }

        VisaStatus? visaStatus = null;
        if (!string.IsNullOrEmpty(req.VisaStatus) && Enum.TryParse<VisaStatus>(req.VisaStatus, out var parsedVisaStatus))
        {
            visaStatus = parsedVisaStatus;
        }

        VisaType? visaType = null;
        if (!string.IsNullOrEmpty(req.VisaType) && Enum.TryParse<VisaType>(req.VisaType, out var parsedVisaType))
        {
            visaType = parsedVisaType;
        }

        ImmigrationStatus? immigrationStatus = null;
        if (!string.IsNullOrEmpty(req.ImmigrationStatus) && Enum.TryParse<ImmigrationStatus>(req.ImmigrationStatus, out var parsedImmStatus))
        {
            immigrationStatus = parsedImmStatus;
        }

        ExchangeProgramType exchangeProgramType = ExchangeProgramType.None;
        if (!string.IsNullOrEmpty(req.ExchangeProgramType) && Enum.TryParse<ExchangeProgramType>(req.ExchangeProgramType, out var parsedExchangeType))
        {
            exchangeProgramType = parsedExchangeType;
        }

        ExchangeStatus exchangeStatus = ExchangeStatus.Pending;
        if (!string.IsNullOrEmpty(req.ExchangeStatus) && Enum.TryParse<ExchangeStatus>(req.ExchangeStatus, out var parsedExchangeStatus))
        {
            exchangeStatus = parsedExchangeStatus;
        }

        DirectEntryQualification directEntryQualification = DirectEntryQualification.None;
        if (!string.IsNullOrEmpty(req.DirectEntryQualification) && Enum.TryParse<DirectEntryQualification>(req.DirectEntryQualification, out var parsedDeQual))
        {
            directEntryQualification = parsedDeQual;
        }

        var app = new AdmissionApplication
        {
            Id = req.Id ?? Guid.NewGuid(),
            FirstName = req.FirstName,
            LastName = req.LastName,
            MiddleName = req.MiddleName,
            StudentEmail = req.StudentEmail,
            JambRegNumber = req.JambRegNumber,
            AcademicSessionId = req.AcademicSessionId ?? Guid.Empty,
            Persona = req.Persona,
            FacultyId = facultyId,
            AcademicProgramId = academicProgramId,
            ProgramReason = req.ProgramReason,
            QualificationsJson = req.QualificationsJson,
            Phone = req.Phone,
            Gender = req.Gender,
            EmergencyContactName = req.EmergencyContactName ?? string.Empty,
            EmergencyContactPhone = req.EmergencyContactPhone ?? string.Empty,
            EmergencyContactEmail = req.EmergencyContactEmail ?? string.Empty,
            EmergencyContactJson = req.EmergencyContactJson,
            SponsorshipJson = req.SponsorshipJson,
            Status = AdmissionStatus.Draft,
            UpdatedAt = DateTime.UtcNow,
            // New fields
            ApplicantType = applicantType,
            DateOfBirth = req.DateOfBirth,
            PreviousInstitutionName = req.PreviousInstitutionName,
            PreviousInstitutionCountry = req.PreviousInstitutionCountry,
            PreviousCGPA = req.PreviousCGPA,
            CreditsEarned = req.CreditsEarned,
            StartingLevelId = req.StartingLevelId,
            Nationality = req.Nationality,
            PassportNumber = req.PassportNumber,
            EnglishProficiencyScore = req.EnglishProficiencyScore,
            EnglishProficiencyType = englishProficiencyType,
            // Phase 1: Country & Region
            CountryOfOrigin = req.CountryOfOrigin,
            CountryName = req.CountryName,
            Region = region,
            // Phase 1: Enhanced Visa & Immigration Fields
            VisaStatus = visaStatus,
            VisaType = visaType,
            VisaExpiryDate = req.VisaExpiryDate,
            ImmigrationStatus = immigrationStatus,
            FinancialProofAmount = req.FinancialProofAmount,
            FinancialProofCurrency = req.FinancialProofCurrency,
            FinancialProofDocumentId = req.FinancialProofDocumentId,
            // Phase 2: Direct Entry Fields
            DirectEntryQualification = directEntryQualification,
            DirectEntryGrade = req.DirectEntryGrade,
            DirectEntryPoints = req.DirectEntryPoints,
            DirectEntryInstitution = req.DirectEntryInstitution,
            DirectEntryYear = req.DirectEntryYear,
            DirectEntrySubject1 = req.DirectEntrySubject1,
            DirectEntrySubject2 = req.DirectEntrySubject2,
            DirectEntrySubject3 = req.DirectEntrySubject3,
            // Phase 3: Transfer Student Fields
            ConvertedCGPA = req.ConvertedCGPA,
            CGPAScaleName = req.CGPAScaleName,
            CGPAScaleMax = req.CGPAScaleMax,
            CGPAScaleMin = req.CGPAScaleMin,
            TransferableCredits = req.TransferableCredits,
            TransferLevelSuggestion = req.TransferLevelSuggestion,
            IntendedSemester = req.IntendedSemester,
            // Phase 4: Exchange Program Fields
            ExchangeProgramType = exchangeProgramType,
            ExchangeStatus = exchangeStatus,
            HomeInstitutionName = req.HomeInstitutionName,
            HomeInstitutionCountry = req.HomeInstitutionCountry,
            ExchangePartnerAgreementId = req.ExchangePartnerAgreementId,
            ExchangeDurationMonths = req.ExchangeDurationMonths,
            ExchangeStartDate = req.ExchangeStartDate,
            ExchangeEndDate = req.ExchangeEndDate,
            HomeInstitutionApprovalDocumentId = req.HomeInstitutionApprovalDocumentId,
            DeansCertificateDocumentId = req.DeansCertificateDocumentId,
            HomeInstitutionTranscriptDocumentId = req.HomeInstitutionTranscriptDocumentId
        };

        try
        {
            var saved = await admissionService.SaveApplicationAsync(app, req.DocumentIds);

            // Map back to response
            var response = new AdmissionApplicationResponse(
                saved.Id,
                saved.ApplicationNumber,
                saved.FirstName,
                saved.LastName,
                saved.MiddleName,
                saved.StudentEmail,
                saved.JambRegNumber,
                saved.AcademicSessionId,
                saved.AcademicSession?.Name ?? string.Empty,
                saved.Persona,
                saved.FacultyId,
                saved.Faculty?.Name ?? string.Empty,
                saved.AcademicProgramId,
                saved.AcademicProgram?.Name ?? string.Empty,
                saved.ProgramReason,
                saved.QualificationsJson,
                saved.Phone,
                saved.EmergencyContactJson,
                saved.SponsorshipJson,
                saved.Status.ToString(),
                saved.CreatedAt,
                saved.SubmittedAt,
                saved.Documents.Select(d => new DocumentResponse(
                    d.Id,
                    d.FileName,
                    d.FileUrl,
                    d.DocumentTypeId,
                    d.DocumentType?.Name ?? "Admission Document",
                    d.DocumentType?.Code ?? string.Empty,
                    d.Status.ToString(),
                    d.RejectionReason
                )),
                // Optional fields
                null, // StudentUserId
                null, // AcceptanceFeeRecordId
                null, // AcceptanceFeeAmount
                null, // AcceptanceFeeBalance
                null, // AcceptanceFeeStatus
                false, // RequiresAcceptanceFee
                // New fields
                saved.ApplicantType.ToString(),
                saved.PreviousInstitutionName,
                saved.PreviousInstitutionCountry,
                saved.PreviousCGPA,
                saved.CreditsEarned,
                saved.StartingLevelId,
                saved.StartingLevel?.Name,
                saved.Nationality,
                saved.PassportNumber,
                saved.EnglishProficiencyScore,
                saved.EnglishProficiencyType?.ToString(),
                saved.DateOfBirth,
                saved.EmergencyContactEmail,
                saved.CountryOfOrigin,
                saved.CountryName,
                saved.Region?.ToString(),
                saved.VisaStatus?.ToString(),
                saved.VisaType?.ToString(),
                saved.VisaExpiryDate,
                saved.ImmigrationStatus?.ToString(),
                saved.FinancialProofAmount,
                saved.FinancialProofCurrency,
                saved.FinancialProofDocumentId,
                saved.ConvertedCGPA,
                saved.CGPAScaleName,
                saved.CGPAScaleMax,
                saved.CGPAScaleMin,
                saved.TransferableCredits,
                saved.TransferLevelSuggestion,
                saved.IntendedSemester,
                saved.ExchangeProgramType.ToString(),
                saved.ExchangeStatus.ToString(),
                saved.HomeInstitutionName,
                saved.HomeInstitutionCountry,
                saved.ExchangePartnerAgreementId,
                saved.ExchangeDurationMonths,
                saved.ExchangeStartDate,
                saved.ExchangeEndDate,
                saved.HomeInstitutionStanding?.ToString(),
                saved.HomeInstitutionVerified,
                saved.HomeInstitutionVerifiedAt,
                saved.HomeInstitutionVerifiedBy,
                saved.DirectEntryQualification.ToString(),
                saved.DirectEntryGrade,
                saved.DirectEntryPoints,
                saved.DirectEntryInstitution,
                saved.DirectEntryYear,
                saved.DirectEntrySubject1,
                saved.DirectEntrySubject2,
                saved.DirectEntrySubject3,
                saved.HomeInstitutionApprovalDocumentId,
                saved.DeansCertificateDocumentId,
                saved.HomeInstitutionTranscriptDocumentId,
                saved.Gender,
                saved.EmergencyContactName,
                saved.EmergencyContactPhone
            );

            await SendSuccessAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            await SendFailureAsync(400, "Validation failed", "validation_error", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Failed to save application", "save_failed", ex.Message, ct);
        }
    }
}
