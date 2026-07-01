using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class UpdateStudentGradeSummariesEndpoint : ApiEndpoint<UpdateStudentGradeSummaryRequest, int>
{
    private readonly IGradebookService _gradebookService;

    public UpdateStudentGradeSummariesEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Post("gradebook/courses/{offeringId:guid}/students/grades/bulk");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Gradebook");
    }

    public override async Task HandleAsync(UpdateStudentGradeSummaryRequest req, CancellationToken ct)
    {
        var userIdString = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User ID not found in token", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");

        var result = await _gradebookService.UpdateStudentGradeSummariesAsync(offeringId, req, userId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorOr.ErrorType.NotFound => 404,
                ErrorOr.ErrorType.Forbidden => 403,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
