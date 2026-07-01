using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetGradeDistributionEndpoint : ApiEndpoint<EmptyRequest, List<GradeDistributionDto>>
{
    private readonly IGradebookService _gradebookService;

    public GetGradeDistributionEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("gradebook/courses/{offeringId:guid}/distribution");
        Roles("SuperAdmin", "Admin", "Lecturer", "HOD", "Dean");
        Tags("Gradebook");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var offeringId = Route<Guid>("offeringId");
        
        var result = await _gradebookService.GetGradeDistributionAsync(offeringId, ct);

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
