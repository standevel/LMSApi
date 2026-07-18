using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using ErrorOr;

namespace LMS.Api.Endpoints.SelfService;

public sealed class GetSpecializationOptionsEndpoint(
    IMajorSelectionService majorService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<List<SpecializationOptionDto>>
{
    public override void Configure()
    {
        Get("self-service/major-selection/options");
        Roles("Student", "SuperAdmin");
        Tags("MajorSelection");
        Description(d => d
            .WithName("GetSpecializationOptions")
            .WithSummary("Retrieve all specialized child programs available for the authenticated student's current program."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = await currentUser.GetUserIdAsync(ct);
        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await majorService.GetAvailableSpecializationsAsync(studentId.Value, ct);
        if (result.IsError)
        {
            var first = result.FirstError;
            await SendFailureAsync(first.Type == ErrorType.NotFound ? 404 : 400, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

public sealed class CreateMajorDeclarationRequestEndpoint(
    IMajorSelectionService majorService,
    ICurrentUserContext currentUser)
    : ApiEndpoint<CreateMajorDeclarationRequest, MajorDeclarationRequestDto>
{
    public override void Configure()
    {
        Post("self-service/major-selection/request");
        Roles("Student", "SuperAdmin");
        Tags("MajorSelection");
        Description(d => d
            .WithName("CreateMajorDeclarationRequest")
            .WithSummary("Student submits a request to declare their major specialization/offshoot."));
    }

    public override async Task HandleAsync(CreateMajorDeclarationRequest req, CancellationToken ct)
    {
        var studentId = await currentUser.GetUserIdAsync(ct);
        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await majorService.CreateDeclarationRequestAsync(studentId.Value, req, ct);
        if (result.IsError)
        {
            var first = result.FirstError;
            var code = first.Type == ErrorType.NotFound ? 404 : first.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(code, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendCreatedAsync(result.Value, ct);
    }
}

public sealed class GetMyMajorDeclarationsEndpoint(
    IMajorSelectionService majorService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<List<MajorDeclarationRequestDto>>
{
    public override void Configure()
    {
        Get("self-service/major-selection/my-requests");
        Roles("Student", "SuperAdmin");
        Tags("MajorSelection");
        Description(d => d
            .WithName("GetMyMajorDeclarations")
            .WithSummary("Retrieve all major declaration requests submitted by the authenticated student."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = await currentUser.GetUserIdAsync(ct);
        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await majorService.GetStudentRequestsAsync(studentId.Value, ct);
        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

public sealed class GetPendingAdviserDeclarationsEndpoint(
    IMajorSelectionService majorService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<List<MajorDeclarationRequestDto>>
{
    public override void Configure()
    {
        Get("self-service/major-selection/pending");
        Roles("Lecturer", "SuperAdmin", "Admin", "Registrar");
        Tags("MajorSelection");
        Description(d => d
            .WithName("GetPendingAdviserDeclarations")
            .WithSummary("Retrieve pending major declaration requests for the students assigned to the calling Academic Adviser."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var adviserId = await currentUser.GetUserIdAsync(ct);
        if (!adviserId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await majorService.GetPendingRequestsForAdviserAsync(adviserId.Value, ct);
        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

public sealed class ReviewMajorDeclarationEndpoint(
    IMajorSelectionService majorService,
    ICurrentUserContext currentUser)
    : ApiEndpoint<ReviewMajorDeclarationRequest, MajorDeclarationRequestDto>
{
    public override void Configure()
    {
        Post("self-service/major-selection/{id}/review");
        Roles("Lecturer", "SuperAdmin", "Admin", "Registrar");
        Tags("MajorSelection");
        Description(d => d
            .WithName("ReviewMajorDeclaration")
            .WithSummary("Adviser approves or rejects a student's major declaration request. On approval, the specialization is executed."));
    }

    public override async Task HandleAsync(ReviewMajorDeclarationRequest req, CancellationToken ct)
    {
        var adviserId = await currentUser.GetUserIdAsync(ct);
        if (!adviserId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var requestId = Route<Guid>("id");
        var result = await majorService.ReviewRequestAsync(requestId, adviserId.Value, req, ct);

        if (result.IsError)
        {
            var first = result.FirstError;
            var code = first.Type == ErrorType.NotFound ? 404 : first.Type == ErrorType.Forbidden ? 403 : 400;
            await SendFailureAsync(code, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
