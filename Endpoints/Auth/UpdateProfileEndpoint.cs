using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Repositories;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using LMS.Api.Endpoints;

namespace LMS.Api.Endpoints.Auth;

public sealed class UpdateProfileEndpoint(IUserRepository userRepository) : ApiEndpoint<UpdateProfileRequest, bool>
{
    public override void Configure()
    {
        Patch("auth/profile");
        Tags("Authentication");
    }

    public override async Task HandleAsync(UpdateProfileRequest req, CancellationToken ct)
    {
        string? objectId = User.FindFirstValue("oid")
            ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

        string? subjectId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        objectId ??= subjectId;

        if (string.IsNullOrEmpty(objectId))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var user = await userRepository.GetByEntraObjectIdAsync(objectId, ct);
        if (user is null && !string.IsNullOrEmpty(subjectId) && Guid.TryParse(subjectId, out var subjectGuid))
        {
            user = await userRepository.GetByIdAsync(subjectGuid, ct);
        }

        if (user is null)
        {
            await SendFailureAsync(404, "User not found.", "user_not_found", "User not found.", ct);
            return;
        }

        user.DepartmentId = req.DepartmentId;
        user.FacultyId = req.FacultyId;
        user.UpdatedUtc = DateTime.UtcNow;

        await userRepository.SaveChangesAsync(ct);

        await SendSuccessAsync(true, ct);
    }
}

public sealed record UpdateProfileRequest(Guid? DepartmentId, Guid? FacultyId);
