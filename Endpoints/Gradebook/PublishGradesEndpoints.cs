using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetPublicationStatusEndpoint : ApiEndpointWithoutRequest<GradePublicationDto>
{
    private readonly IGradebookService _gradebookService;

    public GetPublicationStatusEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("/api/gradebook/courses/{offeringId:guid}/publication");
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

        var result = await _gradebookService.GetPublicationStatusAsync(offeringId, ct);

        if (result.IsError)
        {
            await SendFailureAsync(404, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

public sealed class PublishGradesEndpoint : ApiEndpoint<PublishGradesRequest, GradePublicationDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public PublishGradesEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/courses/{offeringId:guid}/publish");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PublishGradesRequest req, CancellationToken ct)
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

        var result = await _gradebookService.PublishGradesAsync(offeringId, req, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Grades published successfully");
    }
}

public sealed class UnpublishGradesEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public UnpublishGradesEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/courses/{offeringId:guid}/unpublish");
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
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _gradebookService.UnpublishGradesAsync(offeringId, userId.Value, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(new { message = "Grades unpublished successfully" }, ct);
    }
}
