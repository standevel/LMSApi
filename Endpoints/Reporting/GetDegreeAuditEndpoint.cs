using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetDegreeAuditEndpoint : ApiEndpointWithoutRequest<DegreeAuditDto>
{
    private readonly IDegreeAuditService _degreeAuditService;

    public GetDegreeAuditEndpoint(IDegreeAuditService degreeAuditService)
    {
        _degreeAuditService = degreeAuditService;
    }

public override void Configure()
{
    Get("reports/degree-audit/{auditId:guid}");
    Tags("Reporting");
}

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var auditId = Route<Guid>("auditId");
        var result = await _degreeAuditService.GetDegreeAuditAsync(auditId, ct);

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
