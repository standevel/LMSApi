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

public sealed record GetApplicationsRequest(string? Status = null, Guid? SessionId = null, string? Filter = null);

public sealed class GetApplicationsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetApplicationsRequest, IEnumerable<AdmissionApplicationResponse>>
{
    public override void Configure()
    {
        Get("admissions/applications");
        Policies(LmsPolicies.Management);
        Tags("Admissions");
        Description(d => d
            .WithName("Get Applications")
            .WithTags("Admissions")
            .WithSummary("List all admission applications with optional filtering by status, session, and automated reminder eligibility. By default returns Draft and Submitted applications for registry view.")
            .WithDescription("Note: When Status is not specified, applications with Draft or Submitted status are returned."));
    }

    public override async Task HandleAsync(GetApplicationsRequest req, CancellationToken ct)
    {
        AdmissionStatus? status = null;
        if (!string.IsNullOrEmpty(req.Status) && Enum.TryParse<AdmissionStatus>(req.Status, true, out var s))
        {
            status = s;
        }

        var apps = await admissionService.GetApplicationsAsync(status, req.SessionId);

        // For registry view, filter to Draft and Submitted by default (unless Status is explicitly set)
        var filteredApps = status.HasValue
            ? apps
            : apps.Where(a => a.Status == AdmissionStatus.Draft || a.Status == AdmissionStatus.Submitted);

        if (req.Filter == "drafts-eligible-for-reminder")
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            filteredApps = filteredApps.Where(a => a.Status == AdmissionStatus.Draft && a.CreatedAt <= cutoffTime);
        }

        var response = filteredApps.Select(app => AdmissionResponseMapper.Map(app));

        await SendSuccessAsync(response, ct);
    }
}