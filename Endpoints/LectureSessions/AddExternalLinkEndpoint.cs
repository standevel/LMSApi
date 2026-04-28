using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class AddExternalLinkEndpoint : ApiEndpoint<AddExternalLinkRequest, ExternalLinkInfo>
{
    private readonly ISessionManagementService _sessionManagementService;

    public AddExternalLinkEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Post("/api/lecture-sessions/{id}/external-links");
        Roles("SuperAdmin", "Admin", "Lecturer");
    }

    public override async Task HandleAsync(AddExternalLinkRequest req, CancellationToken ct)
    {
        var sessionId = Route<Guid>("id");
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Url))
        {
            await SendFailureAsync(400, "Bad request", "VALIDATION_ERROR", "Title and URL are required.", ct);
            return;
        }

        // Basic URL validation
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uriResult) ||
            !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            await SendFailureAsync(400, "Bad request", "INVALID_URL", "Please provide a valid HTTP or HTTPS URL.", ct);
            return;
        }

        try
        {
            var result = await _sessionManagementService.AddExternalLinkAsync(sessionId, req, userId.Value);
            await SendSuccessAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Bad request", "VALIDATION_ERROR", ex.Message, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendFailureAsync(403, "Forbidden", "UNAUTHORIZED_ACCESS", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Internal server error", "INTERNAL_ERROR", ex.Message, ct);
        }
    }
}
