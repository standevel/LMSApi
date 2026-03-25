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

public sealed record GetApplicationsRequest(string? Status = null, Guid? SessionId = null);

public sealed class GetApplicationsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetApplicationsRequest, IEnumerable<AdmissionApplicationResponse>>
{
    public override void Configure()
    {
        Get("/api/admissions/applications");
        Policies(LmsPolicies.Management);
    }

    public override async Task HandleAsync(GetApplicationsRequest req, CancellationToken ct)
    {
        AdmissionStatus? status = null;
        if (!string.IsNullOrEmpty(req.Status) && Enum.TryParse<AdmissionStatus>(req.Status, true, out var s))
        {
            status = s;
        }

        var apps = await admissionService.GetApplicationsAsync(status, req.SessionId);

        var response = apps.Select(app => new AdmissionApplicationResponse(
            app.Id,
            app.ApplicationNumber,
            app.StudentName, app.StudentEmail, app.JambRegNumber, app.AcademicSessionId,
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
        ));

        await SendSuccessAsync(response, ct);
    }
}
