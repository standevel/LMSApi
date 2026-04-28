using FastEndpoints;
using LMS.Api.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Endpoints.Auth;

public sealed class MeEndpoint(IUserRepository userRepository) : EndpointWithoutRequest<ApiResponse<MeResponse>>
{
    public override void Configure()
    {
        Get("/api/me");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Debugging: Print all claims to help diagnose issues with Microsoft login
        Console.WriteLine("--- ME ENDPOINT: INBOUND CLAIMS ---");
        foreach (var claim in User.Claims)
        {
            Console.WriteLine($"Type: {claim.Type}, Value: {claim.Value}");
        }

        var name = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("preferred_username")
            ?? User.Identity?.Name;

        var email = User.FindFirstValue("unique_name")
            ?? User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Upn)
            ?? User.FindFirstValue("upn");

        string? objectId = User.FindFirstValue("oid")
            ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

        string? subjectId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        objectId ??= subjectId;

        Console.WriteLine($"--- ME ENDPOINT: EXTRACTED --- Name: {name}, Email: {email}, ObjectId: {objectId}, Subject: {subjectId}");

        var roles = User.FindAll("roles")
            .Concat(User.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        AppUser? user = null;
        if (!string.IsNullOrEmpty(objectId))
        {
            user = await userRepository.GetByEntraObjectIdAsync(objectId, ct);
        }

        if (user is null && !string.IsNullOrEmpty(subjectId) && Guid.TryParse(subjectId, out var subjectGuid))
        {
            user = await userRepository.GetByIdAsync(subjectGuid, ct);
        }

        if (user is not null)
        {
            if (user.EntraObjectId is null && !string.IsNullOrEmpty(objectId) && objectId != subjectId)
            {
                user.EntraObjectId = objectId;
            }

            bool changed = false;
            if (string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(email))
            {
                Console.WriteLine($"Updating email for existing user {user.Id} to {email}");
                user.Email = email;
                user.Username ??= email;
                changed = true;
            }
            if (string.IsNullOrEmpty(user.DisplayName) && !string.IsNullOrEmpty(name))
            {
                Console.WriteLine($"Updating display name for existing user {user.Id} to {name}");
                user.DisplayName = name;
                changed = true;
            }

            if (changed)
            {
                user.UpdatedUtc = DateTime.UtcNow;
                await userRepository.SaveChangesAsync(ct);
                Console.WriteLine($"Existing user {user.Id} updated.");
            }

            var localRoles = user.UserRoles
                .Select(ur => ur.Role.Name)
                .ToList();

            if (localRoles.Count > 0)
            {
                Console.WriteLine($"Merging local roles: {string.Join(", ", localRoles)}");
                roles = roles.Union(localRoles).Distinct().ToList();
            }
        }

        if (!string.IsNullOrEmpty(objectId))
        {
            Console.WriteLine($"--- ME ENDPOINT: FINAL --- ObjectId: {objectId}, Roles: {string.Join(", ", roles)}");
        }

        var data = new MeResponse(name, email, objectId, roles);
        await Send.OkAsync(ApiResponse<MeResponse>.Ok(data), ct);
    }
}

public sealed record MeResponse(string? Name, string? Email, string? ObjectId, List<string> Roles);
