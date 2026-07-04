using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Fees;

public sealed class AssignScholarshipEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpoint<AssignScholarshipRequest, StudentScholarshipDto>
{
    public override void Configure()
    {
        Post("fees/scholarships/assign");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(AssignScholarshipRequest req, CancellationToken ct)
    {
        var result = await scholarshipService.AssignScholarshipAsync(req);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class RemoveScholarshipAssignmentEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpointWithoutRequest<object>
{
    public override void Configure()
    {
        Delete("fees/scholarships/assignments/{id}");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await scholarshipService.RemoveScholarshipAssignmentAsync(id);
        await SendSuccessAsync(new object(), ct);
    }
}

public sealed class GetStudentScholarshipsEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpointWithoutRequest<IEnumerable<StudentScholarshipDto>>
{
    public override void Configure()
    {
        Get("fees/scholarships/student/{studentId}");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = Route<Guid>("studentId");
        var result = await scholarshipService.GetStudentScholarshipsAsync(studentId);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class GetAllAssignmentsEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpointWithoutRequest<IEnumerable<StudentScholarshipDto>>
{
    public override void Configure()
    {
        Get("fees/scholarships/assignments");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await scholarshipService.GetAllStudentScholarshipsAsync(100);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class AutoApplyJambScholarshipsEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpointWithoutRequest<object>
{
    public override void Configure()
    {
        Post("fees/scholarships/jamb/auto-apply/{admissionSessionId}");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var admissionSessionId = Route<Guid>("admissionSessionId");
        await scholarshipService.ApplyJambScholarshipsForAdmissionSessionAsync(admissionSessionId);
        await SendSuccessAsync(new object(), ct);
    }
}
