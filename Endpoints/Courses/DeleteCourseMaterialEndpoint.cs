using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Courses;

public sealed class DeleteCourseMaterialEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly ICourseService _courseService;
    private readonly ICurrentUserContext _currentUserContext;

    public DeleteCourseMaterialEndpoint(ICourseService courseService, ICurrentUserContext currentUserContext)
    {
        _courseService = courseService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Delete("/api/courses/materials/{materialId:guid}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Check authentication
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        // Check roles
        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();
        
        var allowedRoles = new[] { "Lecturer", "Admin", "SuperAdmin", "Dean", "HOD" };
        if (!userRoles.Any(r => allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "You do not have permission to access this resource.", ct);
            return;
        }

        var materialId = Route<Guid>("materialId");
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _courseService.DeleteCourseMaterialAsync(materialId, userId.Value, ct);

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

        await SendSuccessAsync(new { deleted = true }, ct, "Material deleted successfully");
    }
}
