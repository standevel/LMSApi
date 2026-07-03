using FastEndpoints;
using LMS.Api.Endpoints;
using LMS.Api.Security;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Endpoints.Admin;

public sealed record UpdateManagedUserRequest(
    string EntraObjectId,
    Guid? DepartmentId,
    Guid? FacultyId);

public sealed class UpdateManagedUserEndpoint(
    IUserRepository userRepository) 
    : ApiEndpoint<UpdateManagedUserRequest, string>
{
    public override void Configure()
    {
        Patch("admin/users/{entraObjectId}/affiliation");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("Administration");
    }

    public override async Task HandleAsync(UpdateManagedUserRequest req, CancellationToken ct)
    {
        var entraObjectId = Route<string>("entraObjectId") ?? req.EntraObjectId;

        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            await SendFailureAsync(404, "User not found.", "user_not_found", "User not found.", ct);
            return;
        }

        user.DepartmentId = req.DepartmentId;
        user.FacultyId = req.FacultyId;
        user.UpdatedUtc = DateTime.UtcNow;

        await userRepository.SaveChangesAsync(ct);

        await SendSuccessAsync(user.EntraObjectId, ct);
    }
}
