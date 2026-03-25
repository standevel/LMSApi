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

public sealed record AutoAdmitRequest(Guid SessionId, bool IsDryRun);

public sealed class AutoAdmitEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<AutoAdmitRequest, IEnumerable<AutoAdmitResult>>
{
    public override void Configure()
    {
        Post("/api/admissions/auto-admit");
        Policies(LmsPolicies.Management);
    }

    public override async Task HandleAsync(AutoAdmitRequest req, CancellationToken ct)
    {
        var results = await admissionService.AutoAdmitAsync(req.SessionId, req.IsDryRun);
        await SendSuccessAsync(results, ct);
    }
}
