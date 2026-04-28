using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace LMS.Api.Security;

public sealed class UserProvisioningMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, LmsDbContext dbContext, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Be extremely aggressive in finding ANY identifier
            var entraObjectId = context.User.FindFirstValue("oid")
                ?? context.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
            if (string.IsNullOrWhiteSpace(entraObjectId))
            {
                entraObjectId = null;
            }

            var subjectId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.User.FindFirstValue("sub")
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                subjectId = null;
            }

            var email = context.User.FindFirstValue("preferred_username")
                ?? context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.FindFirstValue("email")
                ?? context.User.FindFirstValue("emails")
                ?? context.User.FindFirstValue("upn")
                ?? context.User.FindFirstValue("unique_name")
                ?? context.User.FindFirstValue(ClaimTypes.Upn);

            if (!string.IsNullOrWhiteSpace(entraObjectId) || !string.IsNullOrWhiteSpace(subjectId) || !string.IsNullOrWhiteSpace(email))
            {
                var displayName = context.User.FindFirstValue("name") ?? context.User.Identity?.Name;
                var now = DateTime.UtcNow;

                // DIAGNOSTIC LOGGING
                Console.WriteLine($"[Auth-Diagnostic] Attempting to provision: Email={email}, OID={entraObjectId}, Subject={subjectId}");

                var user = await dbContext.Users.FirstOrDefaultAsync(x => x.EntraObjectId == entraObjectId)
                    ?? (Guid.TryParse(subjectId, out var subjectGuid)
                        ? await dbContext.Users.FirstOrDefaultAsync(x => x.Id == subjectGuid)
                        : null)
                    ?? (string.IsNullOrWhiteSpace(email) ? null : await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email));

                if (user is null)
                {
                    user = new AppUser
                    {
                        EntraObjectId = entraObjectId ?? Guid.NewGuid().ToString(),
                        Email = email,
                        DisplayName = displayName ?? email ?? "Unknown User",
                        CreatedUtc = now,
                        UpdatedUtc = now,
                        IsActive = true
                    };
                    dbContext.Users.Add(user);
                    Console.WriteLine($"[Auth-Diagnostic] Created new user record for {email}");
                }
                else
                {
                    user.EntraObjectId = entraObjectId ?? user.EntraObjectId;
                    user.Email = email ?? user.Email;
                    user.DisplayName = displayName ?? user.DisplayName;
                    user.UpdatedUtc = now;
                    user.IsActive = true;
                }

                // Handle Bootstrap Admin
                var bootstrapAdminEmail = configuration["BootstrapAdmin:Email"];
                var isBootstrapAdmin = !string.IsNullOrWhiteSpace(bootstrapAdminEmail) && !string.IsNullOrWhiteSpace(email)
                    && (string.Equals(email, bootstrapAdminEmail, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(email, bootstrapAdminEmail.Replace("@wigweuniversity.edu.ng", "@wigweuniversity.onmicrosoft.com"), StringComparison.OrdinalIgnoreCase));

                if (isBootstrapAdmin)
                {
                    Console.WriteLine($"[Auth-Diagnostic] User {email} identified as Bootstrap Admin.");
                    var superAdminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == LmsRoles.SuperAdmin);
                    if (superAdminRole is not null)
                    {
                        var hasSuperAdmin = await dbContext.UserRoles
                            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == superAdminRole.Id);

                        if (!hasSuperAdmin)
                        {
                            dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = superAdminRole.Id, AssignedUtc = now });
                            Console.WriteLine($"[Auth-Diagnostic] Assigned SuperAdmin role to bootstrap admin.");
                        }
                    }
                }

                // Handle Student Role - Check if user has a Student record
                var isStudent = await dbContext.Students.AnyAsync(s => 
                    (!string.IsNullOrWhiteSpace(entraObjectId) && s.EntraObjectId == entraObjectId) ||
                    (!string.IsNullOrWhiteSpace(email) && (s.OfficialEmail == email || s.PersonalEmail == email)), cancellationToken: default);

                if (isStudent)
                {
                    Console.WriteLine($"[Auth-Diagnostic] User {email} identified as Student.");
                    var studentRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == LmsRoles.Student);
                    if (studentRole is not null)
                    {
                        var hasStudentRole = await dbContext.UserRoles
                            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == studentRole.Id);

                        if (!hasStudentRole)
                        {
                            dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = studentRole.Id, AssignedUtc = now });
                            Console.WriteLine($"[Auth-Diagnostic] Assigned Student role to user.");
                        }
                    }
                }

                await dbContext.SaveChangesAsync();
                context.Items["CurrentUserId"] = user.Id;

                // Load roles from DB
                var roleNames = await (
                    from userRole in dbContext.UserRoles.AsNoTracking()
                    join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name
                ).ToListAsync();

                if (isBootstrapAdmin && !roleNames.Contains(LmsRoles.SuperAdmin))
                {
                    roleNames.Add(LmsRoles.SuperAdmin);
                }

                // INJECT ROLES INTO ALL IDENTITIES
                foreach (var identity in context.User.Identities.OfType<ClaimsIdentity>())
                {
                    var roleClaimType = identity.RoleClaimType ?? "roles";
                    foreach (var roleName in roleNames)
                    {
                        // 1. Standard ClaimTypes.Role URI
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));

                        // 2. Short "roles" claim (common in OIDC/Entra)
                        if (!identity.HasClaim(c => c.Type == "roles" && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                            identity.AddClaim(new Claim("roles", roleName));

                        // 3. Identity-defined RoleClaimType
                        if (roleClaimType != "roles" && roleClaimType != ClaimTypes.Role)
                        {
                            if (!identity.HasClaim(c => c.Type == roleClaimType && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                                identity.AddClaim(new Claim(roleClaimType, roleName));
                        }
                    }
                }

                Console.WriteLine($"[Auth-Diagnostic] Injected roles for {email}: {string.Join(", ", roleNames)}");
            }
        }

        await next(context);
    }
}
