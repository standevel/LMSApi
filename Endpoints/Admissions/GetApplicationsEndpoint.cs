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

        var response = apps.Select(app => AdmissionResponseMapper.Map(app));

        await SendSuccessAsync(response, ct);
    }
}
