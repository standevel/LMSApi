using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;

namespace LMS.Api.Endpoints.Admissions;

public sealed class IdentifyEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<IdentifyRequest, AdmissionApplicationResponse?>
{
    public override void Configure()
    {
        Post("/api/admissions/identify");
        AllowAnonymous();
    }

    public override async Task HandleAsync(IdentifyRequest req, CancellationToken ct)
    {
        try
        {
            var app = await admissionService.VerifyIdentityAsync(req.Email, req.JambRegNumber);

            if (app == null)
            {
                await SendSuccessAsync(null, ct, "No record found");
                return;
            }

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
                ))
            );

            await SendSuccessAsync(response, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Identification Failed", "internal_error", ex.Message, ct);
        }
    }
}
