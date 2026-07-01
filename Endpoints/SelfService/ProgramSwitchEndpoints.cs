using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.SelfService;

// ─────────────────────────────────────────────────────────────────────────────
// POST /self-service/program-switch  (Student)
// Creates a new program switch request (JAMB doc can be uploaded separately).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CreateProgramSwitchEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpoint<CreateProgramSwitchRequest, ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Post("self-service/program-switch");
        Roles("Student");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("CreateProgramSwitchRequest")
            .WithSummary("Student submits a request to switch academic programs. JAMB document can be uploaded later via the upload endpoint, but no approvals can proceed without it."));
    }

    public override async Task HandleAsync(CreateProgramSwitchRequest req, CancellationToken ct)
    {
        var studentId = await currentUser.GetUserIdAsync(ct);
        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await switchService.CreateRequestAsync(studentId.Value, req, ct);
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

// ─────────────────────────────────────────────────────────────────────────────
// POST /self-service/program-switch/{id}/upload-document  (Student)
// Upload the required JAMB admission letter. Advances status Draft → PendingHoDReview.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class UploadProgramSwitchDocumentEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Post("self-service/program-switch/{id}/upload-document");
        Roles("Student");
        AllowFileUploads();
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("UploadProgramSwitchDocument")
            .WithSummary("Upload the JAMB admission letter for a program switch request. Required before any approval can proceed."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = await currentUser.GetUserIdAsync(ct);
        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var requestId = Route<Guid>("id");
        var file = Files.FirstOrDefault();

        if (file == null)
        {
            await SendFailureAsync(400, "Bad Request", "NO_FILE", "No file was uploaded.", ct);
            return;
        }

        var result = await switchService.UploadJambDocumentAsync(requestId, studentId.Value, file, ct);
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

// ─────────────────────────────────────────────────────────────────────────────
// GET /self-service/program-switch/my-requests  (Student)
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GetMyProgramSwitchRequestsEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<List<ProgramSwitchRequestSummaryDto>>
{
    public override void Configure()
    {
        Get("self-service/program-switch/my-requests");
        Roles("Student");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("GetMyProgramSwitchRequests")
            .WithSummary("Returns all program switch requests submitted by the authenticated student."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = await currentUser.GetUserIdAsync(ct);
        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await switchService.GetStudentRequestsAsync(studentId.Value, ct);
        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /self-service/program-switch/{id}  (Student, HoD, Dean, Admin)
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GetProgramSwitchRequestByIdEndpoint(IProgramSwitchService switchService)
    : ApiEndpointWithoutRequest<ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Get("self-service/program-switch/{id}");
        Roles("Student", "HoD", "Dean", "SuperAdmin", "Admin", "Registrar");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("GetProgramSwitchRequestById")
            .WithSummary("Get full detail of a specific program switch request."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await switchService.GetByIdAsync(id, ct);

        if (result.IsError)
        {
            var first = result.FirstError;
            await SendFailureAsync(first.Type == ErrorType.NotFound ? 404 : 400, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /self-service/program-switch/pending?role=HoD  (Staff)
// Returns requests in the caller's approval queue.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GetPendingProgramSwitchRequestsEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<List<ProgramSwitchRequestSummaryDto>>
{
    public override void Configure()
    {
        Get("self-service/program-switch/pending");
        Roles("HoD", "Dean", "SuperAdmin", "Admin", "Registrar");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("GetPendingProgramSwitchRequests")
            .WithSummary("Returns pending program switch requests for a given role queue. Pass ?role=HoD|Dean|Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var role = Query<string>("role", isRequired: false) ?? "Admin";
        
        var userId = await currentUser.GetUserIdAsync(ct);
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await switchService.GetPendingForRoleAsync(role, userId.Value, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /self-service/program-switch/all?status=  (Admin)
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GetAllProgramSwitchRequestsEndpoint(IProgramSwitchService switchService)
    : ApiEndpointWithoutRequest<List<ProgramSwitchRequestSummaryDto>>
{
    public override void Configure()
    {
        Get("self-service/program-switch/all");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("GetAllProgramSwitchRequests")
            .WithSummary("Returns all program switch requests. Optionally filter by ?status=Draft|PendingHoDReview|..."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = Query<string>("status", isRequired: false);
        var result = await switchService.GetAllRequestsAsync(status, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
