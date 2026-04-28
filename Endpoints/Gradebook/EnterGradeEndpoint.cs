using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class EnterGradeEndpoint : ApiEndpoint<EnterGradeRequest, GradeDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public EnterGradeEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/grades");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EnterGradeRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _gradebookService.EnterGradeAsync(req, userId.Value, ct);

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

        await SendSuccessAsync(result.Value, ct, "Grade entered successfully");
    }
}

public sealed class BulkEnterGradesEndpoint : ApiEndpoint<BulkEnterGradesRequest, List<GradeDto>>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public BulkEnterGradesEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/api/gradebook/grades/bulk");
        AllowAnonymous();
    }

    public override async Task HandleAsync(BulkEnterGradesRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var results = new List<GradeDto>();
        foreach (var gradeRequest in req.Grades)
        {
            var result = await _gradebookService.EnterGradeAsync(gradeRequest, userId.Value, ct);
            if (!result.IsError)
            {
                results.Add(result.Value);
            }
        }

        await SendSuccessAsync(results, ct, $"{results.Count} grades entered successfully");
    }
}
