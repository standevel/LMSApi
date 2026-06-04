using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class CreateDegreeAuditEndpoint : ApiEndpoint<CreateDegreeAuditRequest, DegreeAuditDto>
{
    private readonly IDegreeAuditService _degreeAuditService;
    private readonly ICurrentUserContext _currentUserContext;

    public CreateDegreeAuditEndpoint(IDegreeAuditService degreeAuditService, ICurrentUserContext currentUserContext)
    {
        _degreeAuditService = degreeAuditService;
        _currentUserContext = currentUserContext;
    }

public override void Configure()
{
    Post("reports/degree-audit");
    Tags("Reporting");
}

    public override async Task HandleAsync(CreateDegreeAuditRequest request, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
            return;
        }

        var result = await _degreeAuditService.CreateDegreeAuditAsync(request.StudentId, request, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Conflict => 409,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendCreatedAsync(result.Value, ct);
    }
}
