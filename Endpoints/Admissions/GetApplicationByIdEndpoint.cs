using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetApplicationByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetApplicationByIdEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetApplicationByIdRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Get("/api/admissions/applications/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetApplicationByIdRequest req, CancellationToken ct)
    {
        var app = await admissionService.GetApplicationByIdAsync(req.Id);

        if (app is null)
        {
            await SendFailureAsync(404, "Application not found", "not_found", "No application found with the given ID.", ct);
            return;
        }

        var response = new AdmissionApplicationResponse(
            app.Id, app.ApplicationNumber,
            app.StudentName, app.StudentEmail, app.JambRegNumber,
            app.AcademicSessionId, app.AcademicSession?.Name ?? string.Empty,
            app.Persona, app.FacultyId, app.Faculty?.Name ?? string.Empty,
            app.AcademicProgramId, app.AcademicProgram?.Name ?? string.Empty,
            app.ProgramReason, app.QualificationsJson, app.Phone,
            app.EmergencyContactJson, app.SponsorshipJson,
            app.Status.ToString(), app.CreatedAt, app.SubmittedAt,
            app.Documents.Select(d => new DocumentResponse(
                d.Id, d.FileName, d.FileUrl, d.DocumentTypeId,
                d.DocumentType?.Name ?? "Document",
                d.DocumentType?.Code ?? string.Empty,
                d.Status.ToString(),
                d.RejectionReason
            ))
        );

        await SendSuccessAsync(response, ct);
    }
}
