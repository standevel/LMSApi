using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;

namespace LMS.Api.Endpoints.Admissions;

public sealed class SubmitApplicationEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<SubmitApplicationRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("/api/admissions/submit/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SubmitApplicationRequest req, CancellationToken ct)
    {
        try
        {
            var app = await admissionService.SubmitApplicationAsync(req.Id);

            var response = new AdmissionApplicationResponse(
                app.Id,
                app.ApplicationNumber,
                app.FirstName,
                app.LastName,
                app.MiddleName,
                app.StudentEmail,
                app.JambRegNumber,
                app.AcademicSessionId,
                app.AcademicSession?.Name ?? string.Empty,
                app.Persona,
                app.FacultyId,
                app.Faculty?.Name ?? string.Empty,
                app.AcademicProgramId,
                app.AcademicProgram?.Name ?? string.Empty,
                app.ProgramReason,
                app.QualificationsJson,
                app.Phone,
                app.EmergencyContactJson,
                app.SponsorshipJson,
                app.Status.ToString(),
                app.CreatedAt,
                app.SubmittedAt,
                app.Documents.Select(d => new DocumentResponse(
                    d.Id,
                    d.FileName,
                    d.FileUrl,
                    d.DocumentTypeId,
                    d.DocumentType?.Name ?? "Admission Document",
                    d.DocumentType?.Code ?? string.Empty,
                    d.Status.ToString(),
                    d.RejectionReason
                )),
                null, // StudentUserId
                null, // AcceptanceFeeRecordId
                null, // AcceptanceFeeAmount
                null, // AcceptanceFeeBalance
                null, // AcceptanceFeeStatus
                false, // RequiresAcceptanceFee
                // New fields
                app.ApplicantType.ToString(),
                app.PreviousInstitutionName,
                app.PreviousInstitutionCountry,
                app.PreviousCGPA,
                app.CreditsEarned,
                app.StartingLevelId,
                app.StartingLevel?.Name,
                app.Nationality,
                app.PassportNumber,
                app.EnglishProficiencyScore,
                app.EnglishProficiencyType?.ToString()
            );

            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application Not Found", "not_found", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Incomplete Application", "missing_requirements", ex.Message, ct);
        }
    }
}
