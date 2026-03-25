using FastEndpoints;
using LMS.Api.Contracts;
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

        var objectId = User.FindFirstValue("oid")
            ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        Console.WriteLine($"--- ME ENDPOINT: EXTRACTED --- Name: {name}, Email: {email}, ObjectId: {objectId}");

        var roles = User.FindAll("roles")
            .Concat(User.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        if (!string.IsNullOrEmpty(objectId))
        {
            var user = await userRepository.GetByEntraObjectIdAsync(objectId, ct);
            if (user == null)
            {
                Console.WriteLine($"Creating new user for ObjectId: {objectId}");
                user = new AppUser
                {
                    EntraObjectId = objectId,
                    DisplayName = name,
                    Email = email,
                    Username = email ?? objectId,
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
                await userRepository.AddAsync(user, ct);
                await userRepository.SaveChangesAsync(ct);
                Console.WriteLine($"User created with ID: {user.Id}");
            }
            else
            {
                // Update email or display name if missing on existing record
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
            }

            // Merge local roles from database with identity roles
            var localRoles = user.UserRoles
                .Select(ur => ur.Role.Name)
                .ToList();

            if (localRoles.Count > 0)
            {
                Console.WriteLine($"Merging local roles: {string.Join(", ", localRoles)}");
                roles = roles.Union(localRoles).ToList();
            }
        }

        var data = new MeResponse(name, email, objectId, roles);
        await Send.OkAsync(ApiResponse<MeResponse>.Ok(data), ct);
    }
}

public sealed record MeResponse(string? Name, string? Email, string? ObjectId, List<string> Roles);
