using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetCourseAssessmentCategoriesEndpoint : ApiEndpointWithoutRequest<List<AssessmentCategoryDto>>
{
    private readonly IGradebookService _gradebookService;

    public GetCourseAssessmentCategoriesEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("gradebook/courses/{offeringId:guid}/categories");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var offeringId = Route<Guid>("offeringId");
        var result = await _gradebookService.GetAssessmentCategoriesAsync(offeringId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Assessment categories retrieved successfully");
    }
}

public sealed class UpdateCourseAssessmentCategoriesEndpoint : ApiEndpoint<UpdateCourseAssessmentCategoriesRequest, List<AssessmentCategoryDto>>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateCourseAssessmentCategoriesEndpoint> _logger;

    public UpdateCourseAssessmentCategoriesEndpoint(
        IGradebookService gradebookService,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateCourseAssessmentCategoriesEndpoint> logger)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Put("gradebook/courses/{offeringId:guid}/categories");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(UpdateCourseAssessmentCategoriesRequest req, CancellationToken ct)
    {
        try
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

            var offeringId = Route<Guid>("offeringId");
            var result = await _gradebookService.UpdateCourseAssessmentCategoriesAsync(offeringId, req, userId.Value, ct);

            if (result.IsError)
            {
                var error = result.FirstError;
                await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
                return;
            }

            await SendSuccessAsync(result.Value, ct, "Course assessment categories updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course assessment categories for offering {OfferingId}", Route<Guid>("offeringId"));
            await SendFailureAsync(500, ex.InnerException?.Message ?? ex.Message, "UPDATE_FAILED", "Failed to update course assessment categories: " + (ex.InnerException?.Message ?? ex.Message), ct);
        }
    }
}

public sealed class ResetCourseAssessmentCategoriesEndpoint : ApiEndpointWithoutRequest<List<AssessmentCategoryDto>>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ResetCourseAssessmentCategoriesEndpoint> _logger;

    public ResetCourseAssessmentCategoriesEndpoint(
        IGradebookService gradebookService,
        ICurrentUserContext currentUserContext,
        ILogger<ResetCourseAssessmentCategoriesEndpoint> logger)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("gradebook/courses/{offeringId:guid}/categories/reset-to-default");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
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

            var offeringId = Route<Guid>("offeringId");
            var result = await _gradebookService.ResetCourseAssessmentCategoriesToDefaultAsync(offeringId, userId.Value, ct);

            if (result.IsError)
            {
                var error = result.FirstError;
                await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
                return;
            }

            await SendSuccessAsync(result.Value, ct, "Course assessment categories reset to system default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting course assessment categories for offering {OfferingId}", Route<Guid>("offeringId"));
            await SendFailureAsync(500, ex.InnerException?.Message ?? ex.Message, "RESET_FAILED", "Failed to reset course assessment categories to default: " + (ex.InnerException?.Message ?? ex.Message), ct);
        }
    }
}
