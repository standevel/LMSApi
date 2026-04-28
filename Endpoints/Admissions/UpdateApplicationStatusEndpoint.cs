using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed record UpdateStatusRequest(Guid Id, string Status);

public sealed class UpdateApplicationStatusEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<UpdateStatusRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Patch("/api/admissions/status/{Id}");
        Policies(LmsPolicies.Management);
    }

    public override async Task HandleAsync(UpdateStatusRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<AdmissionStatus>(req.Status, true, out var status))
        {
            await SendFailureAsync(400, "Invalid Status", "invalid_status", $"Status '{req.Status}' is not valid.", ct);
            return;
        }

        try
        {
            var app = await admissionService.UpdateApplicationStatusAsync(req.Id, status);

            var response = new AdmissionApplicationResponse(
                app.Id,
                app.ApplicationNumber,
                app.FirstName,
                app.LastName,
                app.MiddleName,
                app.StudentEmail, app.JambRegNumber, app.AcademicSessionId,
                app.AcademicSession?.Name ?? string.Empty,
                app.Persona, app.FacultyId, app.Faculty?.Name ?? string.Empty,
                app.AcademicProgramId, app.AcademicProgram?.Name ?? string.Empty,
                app.ProgramReason, app.QualificationsJson, app.Phone,
                app.EmergencyContactJson, app.SponsorshipJson,
                app.Status.ToString(), app.CreatedAt, app.SubmittedAt,
                app.Documents.Select(d => new DocumentResponse(
                    d.Id, d.FileName, d.FileUrl, d.DocumentTypeId,
                    d.DocumentType?.Name ?? "Admission Document",
                    d.DocumentType?.Code ?? string.Empty,
                    d.Status.ToString(),
                    d.RejectionReason
                ))
            );

            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application Not Found", "not_found", ex.Message, ct);
        }
    }
}
