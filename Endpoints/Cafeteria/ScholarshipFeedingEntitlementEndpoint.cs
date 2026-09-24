using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Cafeteria;

public sealed class GetStudentFeedingEntitlementEndpoint(IScholarshipService scholarshipService)
    : ApiEndpointWithoutRequest<StudentFeedingEntitlementDto>
{
    public override void Configure()
    {
        Get("scholarship/feeding-entitlement");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
                      || User.IsInRole("Finance") || User.IsInRole("Registrar")
                      || User.IsInRole("Cafeteria Operator");

        var requestedUser = Query<string?>("username", isRequired: false);
        var currentUsername = User.Identity?.Name
                              ?? User.FindFirst("name")?.Value
                              ?? User.FindFirst("preferred_username")?.Value
                              ?? User.FindFirst(ClaimTypes.Name)?.Value;

        string targetUsername;
        if (!string.IsNullOrWhiteSpace(requestedUser))
        {
            if (!isStaff)
            {
                await SendFailureAsync(403, "You are not authorized to view another user's feeding entitlement.",
                    "FORBIDDEN", "Only staff may specify a username.", ct);
                return;
            }
            targetUsername = requestedUser;
        }
        else
        {
            targetUsername = currentUsername ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            await SendFailureAsync(401, "Unable to resolve the authenticated user's identity.",
                "UNAUTHORIZED", "A valid authenticated username is required.", ct);
            return;
        }

        var response = await scholarshipService.GetFeedingEntitlementAsync(targetUsername, ct);
        await SendSuccessAsync(response, ct);
    }
}
