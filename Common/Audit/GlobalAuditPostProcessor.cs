using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using System.Security.Claims;
using System.Text.Json;

namespace LMS.Api.Common.Audit;

/// <summary>
/// A global post-processor that logs all mutation requests (POST, PUT, PATCH, DELETE)
/// made by any authenticated user.
/// </summary>
public class GlobalAuditPostProcessor : global::FastEndpoints.IGlobalPostProcessor
{
    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        // Only log for authenticated users
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var method = context.HttpContext.Request.Method;
        
        // Only log mutations
        if (method != HttpMethods.Post && 
            method != HttpMethods.Put && 
            method != HttpMethods.Patch && 
            method != HttpMethods.Delete)
        {
            return;
        }

        // Avoid logging GET/HEAD/OPTIONS, or if an exception happened
        if (context.HasExceptionOccurred)
        {
            // Could log failure here if desired, but we'll stick to successful mutations for now.
            return;
        }

        var path = context.HttpContext.Request.Path.ToString();
        
        // Attempt to extract EntityName from path
        // e.g. /api/admin/programs -> programs
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var entityName = segments.Length > 0 ? segments.Last() : "Unknown";

        var action = $"{method} {path}";
        
        // Extract User ID
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = null;
        if (Guid.TryParse(userIdClaim, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        // Get AuditService
        var auditService = context.HttpContext.RequestServices.GetService<IAuditService>();
        if (auditService != null)
        {
            // Details can just be a note that this was automatically logged
            var changes = $"Global Audit: Automatically logged {method} request by user.";
            
            await auditService.LogAsync(
                action: action,
                entityName: entityName,
                entityId: "N/A", // EntityId is hard to infer generically
                changes: changes,
                ct: ct
            );
        }
    }
}
