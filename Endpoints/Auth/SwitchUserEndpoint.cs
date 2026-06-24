using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Repositories;
using LMS.Api.Endpoints;
using LMS.Api.Security;
using System.Security.Claims;

namespace LMS.Api.Endpoints.Auth;

public sealed class SwitchUserRequest
{
    /// <summary>The EntraObjectId (or internal Guid) of the user to switch into.</summary>
    public string EntraObjectId { get; set; } = string.Empty;
}

public sealed record SwitchedUserInfo(
    string Id,
    string? Name,
    string? Email,
    string[] Roles);

public sealed record SwitchUserResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    SwitchedUserInfo TargetUser);

public sealed class SwitchUserEndpoint(
    ICurrentUserContext currentUserContext,
    IUserRepository userRepository,
    IUserRoleRepository userRoleRepository,
    IPermissionService permissionService,
    ITokenService tokenService)
    : ApiEndpoint<SwitchUserRequest, SwitchUserResponse>
{
    public override void Configure()
    {
        Post("auth/switch-user-by-oid");
        Tags("Authentication");
        // Requires a valid, authenticated JWT — no anonymous
    }

    public override async Task HandleAsync(SwitchUserRequest req, CancellationToken ct)
    {
        // 1. Reject chained switching (switched_by claim already present)
        var alreadySwitched = User.FindFirstValue("switched_by");
        if (!string.IsNullOrEmpty(alreadySwitched))
        {
            await SendFailureAsync(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "switch_chaining_not_allowed",
                "You cannot switch users while already in a switched session. Switch back first.",
                ct);
            return;
        }

        // 2. Resolve actor and verify they exist
        var actorUserId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorUserId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        // 3. Check actor has users.switch permission
        var actorPermissions = await permissionService.GetEffectivePermissionsAsync(actorUserId.Value, ct);
        if (!actorPermissions.Contains(LmsPermissions.UsersSwitch))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        // 4. Verify actor is a SuperAdmin
        var actorRoles = await userRoleRepository.GetRoleNamesAsync(actorUserId.Value, ct);
        if (!actorRoles.Contains(LmsRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        // 5. Validate request
        if (string.IsNullOrWhiteSpace(req.EntraObjectId))
        {
            await SendFailureAsync(
                StatusCodes.Status400BadRequest,
                "Bad request",
                "invalid_request",
                "EntraObjectId is required.",
                ct);
            return;
        }

        // 6. Load the target user by EntraObjectId
        var targetUser = await userRepository.GetByEntraObjectIdAsync(req.EntraObjectId, ct);
        if (targetUser is null)
        {
            await SendFailureAsync(
                StatusCodes.Status404NotFound,
                "Not found",
                "user_not_found",
                "The specified user was not found.",
                ct);
            return;
        }

        // 7. Prevent switching into another SuperAdmin account
        var targetRoles = await userRoleRepository.GetRoleNamesAsync(targetUser.Id, ct);
        if (targetRoles.Contains(LmsRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            await SendFailureAsync(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "cannot_switch_to_super_admin",
                "You cannot switch into a SuperAdmin account.",
                ct);
            return;
        }

        // 8. Generate the switched-user token (capped at 60 min, carries switched_by claim)
        var switchedToken = await tokenService.CreateSwitchedUserTokenAsync(actorUserId.Value, targetUser.Id, ct);

        var targetUserInfo = new SwitchedUserInfo(
            Id: targetUser.Id.ToString(),
            Name: targetUser.DisplayName ?? targetUser.Username ?? targetUser.Email,
            Email: targetUser.Email,
            Roles: targetRoles.ToArray());

        var data = new SwitchUserResponse(
            AccessToken: switchedToken,
            TokenType: "Bearer",
            ExpiresInSeconds: 60 * 60, // 60 minutes
            TargetUser: targetUserInfo);

        await SendSuccessAsync(data, ct);
    }
}
