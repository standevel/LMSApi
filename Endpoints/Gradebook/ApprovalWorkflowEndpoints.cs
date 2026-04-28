using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetGradeApprovalsEndpoint : ApiEndpointWithoutRequest<List<GradeApprovalDto>>
{
    private readonly IGradebookService _gradebookService;

    public GetGradeApprovalsEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("/api/gradebook/courses/{offeringId:guid}/approvals");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");

        var result = await _gradebookService.GetGradeApprovalsAsync(offeringId, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

public sealed class SubmitForApprovalEndpoint : ApiEndpoint<SubmitForApprovalRequest, GradeApprovalDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public SubmitForApprovalEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/courses/{offeringId:guid}/submit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SubmitForApprovalRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _gradebookService.SubmitForApprovalAsync(offeringId, req, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                ErrorType.Conflict => 409,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Grades submitted for approval");
    }
}

public sealed class ApproveGradesEndpoint : ApiEndpoint<ApproveGradesRequest, GradeApprovalDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public ApproveGradesEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/courses/{offeringId:guid}/approve");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ApproveGradesRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _gradebookService.ApproveGradesAsync(offeringId, req, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                ErrorType.Conflict => 409,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, $"Grades approved at {req.Level} level");
    }
}

public sealed class RejectGradesEndpoint : ApiEndpoint<RejectGradesRequest, GradeApprovalDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public RejectGradesEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/courses/{offeringId:guid}/reject");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RejectGradesRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _gradebookService.RejectGradesAsync(offeringId, req, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                ErrorType.Validation => 400,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, $"Grades rejected at {req.Level} level");
    }
}
