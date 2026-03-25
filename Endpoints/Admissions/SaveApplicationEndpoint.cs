using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;

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
        var app = new AdmissionApplication
        {
            Id = req.Id ?? Guid.NewGuid(),
            StudentName = req.StudentName,
            StudentEmail = req.StudentEmail,
            JambRegNumber = req.JambRegNumber,
            AcademicSessionId = req.AcademicSessionId,
            Persona = req.Persona,
            FacultyId = req.FacultyId,
            AcademicProgramId = req.AcademicProgramId,
            ProgramReason = req.ProgramReason,
            QualificationsJson = req.QualificationsJson,
            Phone = req.Phone,
            EmergencyContactJson = req.EmergencyContactJson,
            SponsorshipJson = req.SponsorshipJson,
            Status = AdmissionStatus.Draft,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await admissionService.SaveApplicationAsync(app, req.DocumentIds);

        // Map back to response
        var response = new AdmissionApplicationResponse(
            saved.Id,
            saved.ApplicationNumber,
            saved.StudentName,
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
            ))
        );

        await SendSuccessAsync(response, ct);
    }
}
