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
        Post("/api/admissions/save");
        AllowAnonymous(); // Admission is public until student is fully onboarded
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

        var app = new AdmissionApplication
        {
            Id = req.Id ?? Guid.NewGuid(),
            FirstName = req.FirstName,
            LastName = req.LastName,
            MiddleName = req.MiddleName,
            StudentEmail = req.StudentEmail,
            JambRegNumber = req.JambRegNumber,
            AcademicSessionId = req.AcademicSessionId,
            Persona = req.Persona,
            FacultyId = facultyId,
            AcademicProgramId = academicProgramId,
            ProgramReason = req.ProgramReason,
            QualificationsJson = req.QualificationsJson,
            Phone = req.Phone,
            EmergencyContactJson = req.EmergencyContactJson,
            SponsorshipJson = req.SponsorshipJson,
            Status = AdmissionStatus.Draft,
            UpdatedAt = DateTime.UtcNow,
            // New fields
            ApplicantType = applicantType,
            PreviousInstitutionName = req.PreviousInstitutionName,
            PreviousInstitutionCountry = req.PreviousInstitutionCountry,
            PreviousCGPA = req.PreviousCGPA,
            CreditsEarned = req.CreditsEarned,
            StartingLevelId = req.StartingLevelId,
            Nationality = req.Nationality,
            PassportNumber = req.PassportNumber,
            EnglishProficiencyScore = req.EnglishProficiencyScore,
            EnglishProficiencyType = englishProficiencyType
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
                // Optional fields (nulls for now)
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
                saved.EnglishProficiencyType?.ToString()
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
