using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetStudentDegreeAuditsEndpoint : ApiEndpointWithoutRequest<List<DegreeAuditDto>>
{
    private readonly IDegreeAuditService _degreeAuditService;

    public GetStudentDegreeAuditsEndpoint(IDegreeAuditService degreeAuditService)
    {
        _degreeAuditService = degreeAuditService;
    }

    public override void Configure()
    {
        Get("api/reports/degree-audit/student/{studentId:guid}");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var studentId = Route<Guid>("studentId");
        var result = await _degreeAuditService.GetStudentDegreeAuditsAsync(studentId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
