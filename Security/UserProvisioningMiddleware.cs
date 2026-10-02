using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Api.Security;

public sealed class UserProvisioningMiddleware(RequestDelegate next)
{
    private sealed record CachedUserProvisioning(Guid UserId, List<string> Roles);

    public async Task InvokeAsync(HttpContext context, LmsDbContext dbContext, IConfiguration configuration, IMemoryCache cache)
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
                    var cacheKey = $"UserProv_{entraObjectId ?? subjectId ?? email}";
                    if (cache.TryGetValue<CachedUserProvisioning>(cacheKey, out var cached) && cached is not null)
                    {
                        context.Items["CurrentUserId"] = cached.UserId;
                        InjectRolesAndIdentity(context.User, cached.UserId, cached.Roles);
                        await next(context);
                        return;
                    }

                    var displayName = context.User.FindFirstValue("name") ?? context.User.Identity?.Name;
                    var now = DateTime.UtcNow;

                    Console.WriteLine($"[Auth-Diagnostic] Attempting to provision: Email={email}, OID={entraObjectId}, Subject={subjectId}");

                    Guid? parsedSubjectGuid = Guid.TryParse(subjectId, out var sGuid) ? sGuid : null;

                    var user = await dbContext.Users
                        .Include(x => x.UserRoles)
                            .ThenInclude(x => x.Role)
                        .FirstOrDefaultAsync(x =>
                            (!string.IsNullOrEmpty(entraObjectId) && x.EntraObjectId == entraObjectId) ||
                            (parsedSubjectGuid.HasValue && x.Id == parsedSubjectGuid.Value) ||
                            (!string.IsNullOrEmpty(email) && x.Email == email),
                            context.RequestAborted);

                    bool changed = false;

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
                        changed = true;
                        Console.WriteLine($"[Auth-Diagnostic] Created new user record for {email}");
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(entraObjectId) && user.EntraObjectId != entraObjectId)
                        {
                            user.EntraObjectId = entraObjectId;
                            changed = true;
                        }
                        if (!string.IsNullOrWhiteSpace(email) && user.Email != email)
                        {
                            user.Email = email;
                            changed = true;
                        }
                        if (!string.IsNullOrWhiteSpace(displayName) && user.DisplayName != displayName)
                        {
                            user.DisplayName = displayName;
                            changed = true;
                        }
                        if (!user.IsActive)
                        {
                            user.IsActive = true;
                            changed = true;
                        }
                        if (changed)
                        {
                            user.UpdatedUtc = now;
                        }
                    }

                    // Handle Bootstrap Admin
                    var bootstrapAdminEmail = configuration["BootstrapAdmin:Email"];
                    var isBootstrapAdmin = !string.IsNullOrWhiteSpace(bootstrapAdminEmail) && !string.IsNullOrWhiteSpace(email)
                        && (string.Equals(email, bootstrapAdminEmail, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(email, bootstrapAdminEmail.Replace("@wigweuniversity.edu.ng", "@wigweuniversity.onmicrosoft.com"), StringComparison.OrdinalIgnoreCase));

                    if (isBootstrapAdmin)
                    {
                        Console.WriteLine($"[Auth-Diagnostic] User {email} identified as Bootstrap Admin.");
                        var hasSuperAdmin = user.UserRoles.Any(ur => string.Equals(ur.Role?.Name, LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
                        if (!hasSuperAdmin)
                        {
                            var superAdminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == LmsRoles.SuperAdmin, context.RequestAborted);
                            if (superAdminRole is not null)
                            {
                                var newUr = new UserRole { UserId = user.Id, RoleId = superAdminRole.Id, AssignedUtc = now, Role = superAdminRole };
                                dbContext.UserRoles.Add(newUr);
                                user.UserRoles.Add(newUr);
                                changed = true;
                                Console.WriteLine($"[Auth-Diagnostic] Assigned SuperAdmin role to bootstrap admin.");
                            }
                        }
                    }

                    // Handle Student Role - Check if user has a Student record (only if not already bootstrap admin and not already assigned Student role)
                    var isStudentRoleAssigned = user.UserRoles.Any(ur => string.Equals(ur.Role?.Name, LmsRoles.Student, StringComparison.OrdinalIgnoreCase));
                    if (!isBootstrapAdmin && !isStudentRoleAssigned)
                    {
                        var student = await dbContext.Students.FirstOrDefaultAsync(s =>
                            (!string.IsNullOrWhiteSpace(entraObjectId) && s.EntraObjectId == entraObjectId) ||
                            (!string.IsNullOrWhiteSpace(email) && (s.OfficialEmail == email || s.PersonalEmail == email)),
                            context.RequestAborted);

                        if (student is not null)
                        {
                            Console.WriteLine($"[Auth-Diagnostic] User {email} identified as Student.");
                            var studentRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == LmsRoles.Student, context.RequestAborted);
                            if (studentRole is not null)
                            {
                                var newUr = new UserRole { UserId = user.Id, RoleId = studentRole.Id, AssignedUtc = now, Role = studentRole };
                                dbContext.UserRoles.Add(newUr);
                                user.UserRoles.Add(newUr);
                                changed = true;
                                Console.WriteLine($"[Auth-Diagnostic] Assigned Student role to user.");
                            }

                            // Auto-provision ProgramEnrollment for the active academic session if it doesn't exist
                            var activeSession = await dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, context.RequestAborted);
                            if (activeSession is not null && student.AcademicProgramId.HasValue && student.LevelId.HasValue)
                            {
                                var hasEnrollment = await dbContext.Enrollments.AnyAsync(e =>
                                    e.UserId == user.Id && e.AcademicSessionId == activeSession.Id,
                                    context.RequestAborted);

                                if (!hasEnrollment)
                                {
                                    // Find curriculum for this program
                                    var curriculum = await dbContext.Curricula.FirstOrDefaultAsync(c => c.ProgramId == student.AcademicProgramId.Value, context.RequestAborted);
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
                                        changed = true;
                                        Console.WriteLine($"[Auth-Diagnostic] Auto-provisioned ProgramEnrollment for user {email} in active session {activeSession.Name}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[Auth-Diagnostic] Skipped ProgramEnrollment for {email}: No Curriculum found for Program {student.AcademicProgramId.Value}");
                                    }
                                }
                            }
                        }
                    }

                    if (changed)
                    {
                        await dbContext.SaveChangesAsync(context.RequestAborted);
                    }

                    context.Items["CurrentUserId"] = user.Id;

                    // Extract existing token roles
                    var tokenRoles = context.User.Claims
                        .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles" || c.Type == "role")
                        .Select(c => c.Value)
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .ToList();

                    // Load roles from in-memory user entity and merge token roles
                    var roleNames = user.UserRoles
                        .Select(ur => ur.Role?.Name)
                        .Where(r => !string.IsNullOrEmpty(r))
                        .Select(r => r!)
                        .Union(tokenRoles, StringComparer.OrdinalIgnoreCase)
                        .Distinct()
                        .ToList();

                    if (isBootstrapAdmin && !roleNames.Contains(LmsRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
                    {
                        roleNames.Add(LmsRoles.SuperAdmin);
                    }

                    // Cache user provisioning result for 10 minutes to eliminate DB overhead on subsequent requests
                    cache.Set(cacheKey, new CachedUserProvisioning(user.Id, roleNames), TimeSpan.FromMinutes(10));

                    InjectRolesAndIdentity(context.User, user.Id, roleNames);
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

    private static void InjectRolesAndIdentity(ClaimsPrincipal principal, Guid userId, IEnumerable<string> roleNames)
    {
        foreach (var identity in principal.Identities.OfType<ClaimsIdentity>())
        {
            // Add AppUser.Id as NameIdentifier for SignalR and other standard components
            if (!identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            }
            else
            {
                var existingClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
                if (existingClaim != null && existingClaim.Value != userId.ToString())
                {
                    identity.RemoveClaim(existingClaim);
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
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

                // 3. Short "role" claim (singular)
                if (!identity.HasClaim(c => c.Type == "role" && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                {
                    identity.AddClaim(new Claim("role", roleName));
                }

                // 4. Identity-defined RoleClaimType
                if (roleClaimType != "roles" && roleClaimType != "role" && roleClaimType != ClaimTypes.Role
                    && !identity.HasClaim(c => c.Type == roleClaimType && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                {
                    identity.AddClaim(new Claim(roleClaimType, roleName));
                }
            }
        }
    }
}
