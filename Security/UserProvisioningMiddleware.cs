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
            try
            {
                // Be extremely aggressive in finding ANY identifier
                // 'oid' may not be present in resource access tokens (e.g., user_impersonation scope)
                // so fall back to 'sub' which is always available.
                var oidClaim = context.User.FindFirstValue("oid")
                    ?? context.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

                var subjectId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? context.User.FindFirstValue("sub")
                    ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(subjectId))
                {
                    subjectId = null;
                }

                // Use 'oid' if available; otherwise fall back to 'sub'.
                var entraObjectId = !string.IsNullOrWhiteSpace(oidClaim)
                    ? oidClaim
                    : (string.IsNullOrWhiteSpace(subjectId) ? null : subjectId);

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
                    var student = await dbContext.Students.FirstOrDefaultAsync(s =>
                        (!string.IsNullOrWhiteSpace(entraObjectId) && s.EntraObjectId == entraObjectId) ||
                        (!string.IsNullOrWhiteSpace(email) && (s.OfficialEmail == email || s.PersonalEmail == email)), cancellationToken: default);

                    if (student is not null)
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

                        // Auto-provision ProgramEnrollment for the active academic session if it doesn't exist
                        var activeSession = await dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive);
                        if (activeSession is not null && student.AcademicProgramId.HasValue && student.LevelId.HasValue)
                        {
                            var hasEnrollment = await dbContext.Enrollments.AnyAsync(e =>
                                e.UserId == user.Id && e.AcademicSessionId == activeSession.Id);

                            if (!hasEnrollment)
                            {
                                // Find curriculum for this program
                                var curriculum = await dbContext.Curricula.FirstOrDefaultAsync(c => c.ProgramId == student.AcademicProgramId.Value);
                                if (curriculum is not null)
                                {
                                    dbContext.Enrollments.Add(new ProgramEnrollment
                                    {
                                        ProgramId = student.AcademicProgramId.Value,
                                        LevelId = student.LevelId.Value,
                                        UserId = user.Id,
                                        AcademicSessionId = activeSession.Id,
                                        CurriculumId = curriculum.Id,
                                        EnrolledAtUtc = now
                                    });
                                    Console.WriteLine($"[Auth-Diagnostic] Auto-provisioned ProgramEnrollment for user {email} in active session {activeSession.Name}");
                                }
                                else
                                {
                                    Console.WriteLine($"[Auth-Diagnostic] Skipped ProgramEnrollment for {email}: No Curriculum found for Program {student.AcademicProgramId.Value}");
                                }
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
                        // Add AppUser.Id as NameIdentifier for SignalR and other standard components
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
                        }
                        else 
                        {
                            var existingClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
                            if (existingClaim != null && existingClaim.Value != user.Id.ToString())
                            {
                                identity.RemoveClaim(existingClaim);
                                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
                            }
                        }

                        var roleClaimType = identity.RoleClaimType ?? "roles";
                        foreach (var roleName in roleNames)
                        {
                            // 1. Standard ClaimTypes.Role URI
                            if (!identity.HasClaim(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                            }

                            // 2. Short "roles" claim (common in OIDC/Entra)
                            if (!identity.HasClaim(c => c.Type == "roles" && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                            {
                                identity.AddClaim(new Claim("roles", roleName));
                            }

                            // 3. Identity-defined RoleClaimType
                            if (roleClaimType != "roles" && roleClaimType != ClaimTypes.Role
                                && !identity.HasClaim(c => c.Type == roleClaimType && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                            {
                                identity.AddClaim(new Claim(roleClaimType, roleName));
                            }
                        }
                    }

                    Console.WriteLine($"[Auth-Diagnostic] Injected roles for {email}: {string.Join(", ", roleNames)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth-Diagnostic] User provisioning skipped: {ex.Message}");
            }
        }

        await next(context);
    }
}
