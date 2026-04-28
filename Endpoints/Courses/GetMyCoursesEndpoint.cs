using System;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Courses;

public sealed class GetMyCoursesEndpoint : ApiEndpointWithoutRequest<LecturerCoursesResponse>
{
    private readonly ICourseService _courseService;
    private readonly ICurrentUserContext _currentUserContext;

    public GetMyCoursesEndpoint(ICourseService courseService, ICurrentUserContext currentUserContext)
    {
        _courseService = courseService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Get("/api/courses/my-courses");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Debug: Log all request info
        Console.WriteLine($"[MyCourses] Request received");
        Console.WriteLine($"[MyCourses] Auth header: {HttpContext.Request.Headers.Authorization}");
        Console.WriteLine($"[MyCourses] User authenticated: {HttpContext.User?.Identity?.IsAuthenticated}");
        Console.WriteLine($"[MyCourses] User identity name: {HttpContext.User?.Identity?.Name}");
        Console.WriteLine($"[MyCourses] Claims count: {HttpContext.User?.Claims?.Count() ?? 0}");
        foreach (var claim in HttpContext.User?.Claims ?? [])
        {
            Console.WriteLine($"[MyCourses] Claim: {claim.Type} = {claim.Value}");
        }

        // Manual authentication and role check
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        // Check if user has required role
        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();
        
        Console.WriteLine($"[MyCourses] User roles: {string.Join(", ", userRoles)}");
        
        var allowedRoles = new[] { "Lecturer", "Admin", "SuperAdmin", "Dean", "HOD" };
        if (!userRoles.Any(r => allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "You do not have permission to access this resource.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _courseService.GetMyCoursesAsync(userId.Value, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, "Bad request", "ERROR", result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
