using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LMS.Api.Services;

public interface IGuardianProvisioningService
{
    Task<SystemParentPortalConfiguration> GetConfigurationAsync(CancellationToken ct = default);
    Task<SystemParentPortalConfigurationDto> UpdateConfigurationAsync(UpdateSystemParentPortalConfigurationRequest request, Guid? updatedById, CancellationToken ct = default);
    Task<ProvisionGuardianResultDto> ProvisionForStudentAsync(Student student, string? relationship = null, bool? sendInvitationEmail = null, CancellationToken ct = default);
    Task<ProvisionGuardianResultDto> ProvisionForStudentAsync(Guid studentId, bool? sendInvitationEmail = null, CancellationToken ct = default);
    Task<ProvisionGuardianBatchResponse> ProvisionBatchAsync(ProvisionGuardianBatchRequest request, CancellationToken ct = default);
    Task<bool> AutoCreateGuardianAccountsEnabledAsync(CancellationToken ct = default);
    Task<ResendGuardianCredentialsResponse> ResendCredentialsAsync(Guid parentGuardianId, Guid? requestedById = null, CancellationToken ct = default);
    Task<ResendGuardianCredentialsResponse> ResendCredentialsForStudentAsync(Guid studentId, Guid? requestedById = null, CancellationToken ct = default);
}

public sealed class GuardianProvisioningService(
    LmsDbContext context,
    IEmailService emailService,
    IPasswordHasher<AppUser> passwordHasher,
    IConfiguration configuration) : IGuardianProvisioningService
{
    public async Task<SystemParentPortalConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = await context.SystemParentPortalConfigurations.FirstOrDefaultAsync(ct);
        if (config != null)
            return config;

        config = new SystemParentPortalConfiguration();
        context.SystemParentPortalConfigurations.Add(config);
        await context.SaveChangesAsync(ct);
        return config;
    }

    public async Task<SystemParentPortalConfigurationDto> UpdateConfigurationAsync(
        UpdateSystemParentPortalConfigurationRequest request,
        Guid? updatedById,
        CancellationToken ct = default)
    {
        var relationship = string.IsNullOrWhiteSpace(request.DefaultRelationship)
            ? "Guardian"
            : request.DefaultRelationship.Trim();

        if (relationship.Length > 100)
            relationship = relationship[..100];

        var config = await GetConfigurationAsync(ct);
        config.AutoCreateGuardianAccountsOnStudentCreation = request.AutoCreateGuardianAccountsOnStudentCreation;
        config.SendGuardianInvitationEmail = request.SendGuardianInvitationEmail;
        config.DefaultRelationship = relationship;

        if (request.AllowedCategories != null)
            config.AllowedCategoriesJson = System.Text.Json.JsonSerializer.Serialize(request.AllowedCategories);
        if (request.RequireStudentConsentForSensitive.HasValue)
            config.RequireStudentConsentForSensitive = request.RequireStudentConsentForSensitive.Value;
        if (request.TreatAdultStudentsAsRestricted.HasValue)
            config.TreatAdultStudentsAsRestricted = request.TreatAdultStudentsAsRestricted.Value;

        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedById = updatedById;

        await context.SaveChangesAsync(ct);
        return MapConfiguration(config);
    }

    public async Task<bool> AutoCreateGuardianAccountsEnabledAsync(CancellationToken ct = default)
    {
        var config = await GetConfigurationAsync(ct);
        return config.AutoCreateGuardianAccountsOnStudentCreation;
    }

    public async Task<ProvisionGuardianResultDto> ProvisionForStudentAsync(
        Guid studentId,
        bool? sendInvitationEmail = null,
        CancellationToken ct = default)
    {
        var student = await context.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
        {
            return new ProvisionGuardianResultDto(
                studentId,
                string.Empty,
                null,
                null,
                "Failed",
                "Student not found.",
                null,
                null,
                false,
                false,
                false);
        }

        return await ProvisionForStudentAsync(student, null, sendInvitationEmail, ct);
    }

    public async Task<ProvisionGuardianResultDto> ProvisionForStudentAsync(
        Student student,
        string? relationship = null,
        bool? sendInvitationEmail = null,
        CancellationToken ct = default)
    {
        var studentName = $"{student.FirstName} {student.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(student.EmergencyContactEmail))
        {
            return Result(
                student,
                "SkippedMissingGuardianEmail",
                "Student has no guardian email.",
                null,
                null,
                false,
                false,
                false);
        }

        var email = student.EmergencyContactEmail.Trim();
        if (!IsValidEmail(email))
        {
            return Result(
                student,
                "Failed",
                "Guardian email is invalid.",
                null,
                null,
                false,
                false,
                false);
        }

        var config = await GetConfigurationAsync(ct);
        var now = DateTime.UtcNow;
        var firstName = string.Empty;
        var lastName = string.Empty;
        if (!string.IsNullOrWhiteSpace(student.EmergencyContactName))
        {
            var parts = student.EmergencyContactName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts.ElementAtOrDefault(0) ?? string.Empty;
            lastName = parts.ElementAtOrDefault(1) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            firstName = email.Split('@')[0];

        var displayName = $"{firstName} {lastName}".Trim();
        var phone = student.EmergencyContactPhone?.Trim() ?? string.Empty;
        var relationshipType = !string.IsNullOrWhiteSpace(relationship)
            ? relationship.Trim()
            : config.DefaultRelationship;

        var guardianUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == email || u.Username == email, ct);

        var createdUser = false;
        string? generatedTempPassword = null;

        if (guardianUser == null)
        {
            generatedTempPassword = GenerateTemporaryPassword();
            guardianUser = new AppUser
            {
                Id = Guid.NewGuid(),
                EntraObjectId = $"parent:{Guid.NewGuid()}",
                Username = email,
                Email = email,
                DisplayName = displayName,
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            guardianUser.PasswordHash = passwordHasher.HashPassword(guardianUser, generatedTempPassword);
            context.Users.Add(guardianUser);
            createdUser = true;
        }
        else
        {
            guardianUser.Email ??= email;
            guardianUser.Username ??= email;
            guardianUser.DisplayName = string.IsNullOrWhiteSpace(guardianUser.DisplayName) ? displayName : guardianUser.DisplayName;
            guardianUser.IsActive = true;
            guardianUser.UpdatedUtc = now;

            if (string.IsNullOrWhiteSpace(guardianUser.PasswordHash))
            {
                generatedTempPassword = GenerateTemporaryPassword();
                guardianUser.PasswordHash = passwordHasher.HashPassword(guardianUser, generatedTempPassword);
                createdUser = true;
            }
        }

        var parentRoleId = await context.Roles
            .Where(r => r.Name == LmsRoles.Parent)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (parentRoleId.HasValue)
        {
            var hasParentRole = await context.UserRoles
                .AnyAsync(ur => ur.UserId == guardianUser.Id && ur.RoleId == parentRoleId.Value, ct);

            if (!hasParentRole)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = guardianUser.Id,
                    RoleId = parentRoleId.Value,
                    AssignedUtc = now
                });
            }
        }

        var guardian = await context.ParentGuardians
            .FirstOrDefaultAsync(pg => pg.UserId == guardianUser.Id || pg.Email == email, ct);

        var createdGuardian = false;
        if (guardian == null)
        {
            guardian = new ParentGuardian
            {
                Id = Guid.NewGuid(),
                UserId = guardianUser.Id,
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = phone,
                Email = email,
                DateAddedUtc = now
            };
            context.ParentGuardians.Add(guardian);
            createdGuardian = true;
        }
        else
        {
            guardian.UserId = guardianUser.Id;
            guardian.FirstName = string.IsNullOrWhiteSpace(guardian.FirstName) ? firstName : guardian.FirstName;
            guardian.LastName = string.IsNullOrWhiteSpace(guardian.LastName) ? lastName : guardian.LastName;
            guardian.PhoneNumber = string.IsNullOrWhiteSpace(guardian.PhoneNumber) ? phone : guardian.PhoneNumber;
            guardian.Email = string.IsNullOrWhiteSpace(guardian.Email) ? email : guardian.Email;
        }

        var existingLink = await context.ParentStudentLinks
            .FirstOrDefaultAsync(l => l.ParentGuardianId == guardian.Id && l.StudentId == student.Id, ct);

        var createdLink = false;
        if (existingLink == null)
        {
            existingLink = new ParentStudentLink
            {
                Id = Guid.NewGuid(),
                ParentGuardianId = guardian.Id,
                StudentId = student.Id,
                RelationshipType = relationshipType,
                LinkedAtUtc = now
            };
            context.ParentStudentLinks.Add(existingLink);
            createdLink = true;
        }

        var hasPreference = await context.FamilyCommunicationPreferences
            .AnyAsync(p => p.ParentGuardianId == guardian.Id, ct);
        if (!hasPreference)
        {
            context.FamilyCommunicationPreferences.Add(new FamilyCommunicationPreference
            {
                Id = Guid.NewGuid(),
                ParentGuardianId = guardian.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync(ct);

        var shouldSendEmail = sendInvitationEmail ?? config.SendGuardianInvitationEmail;
        if (shouldSendEmail && (createdUser || createdLink))
        {
            try
            {
                var clientBaseUrl = GetClientBaseUrl();
                var loginUrl = $"{clientBaseUrl}/auth/login?mode=password&email={Uri.EscapeDataString(email)}";

                await emailService.SendGuardianCredentialsEmailAsync(
                    email,
                    string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                    string.IsNullOrWhiteSpace(studentName) ? (student.OfficialEmail ?? "your ward") : studentName,
                    email,
                    generatedTempPassword,
                    createdUser,
                    loginUrl);
            }
            catch
            {
                // Email delivery must not roll back successful provisioning.
            }
        }

        var status = !createdLink
            ? "AlreadyLinked"
            : createdUser
                ? "CreatedAccount"
                : "LinkedExistingAccount";

        var message = status switch
        {
            "AlreadyLinked" => "Guardian is already linked to this student.",
            "CreatedAccount" => "Guardian account created and linked.",
            _ => "Existing guardian account linked."
        };

        return Result(student, status, message, guardian.Id, existingLink.Id, createdUser, createdGuardian, createdLink);
    }

    public async Task<ResendGuardianCredentialsResponse> ResendCredentialsAsync(
        Guid parentGuardianId,
        Guid? requestedById = null,
        CancellationToken ct = default)
    {
        var guardian = await context.ParentGuardians
            .Include(pg => pg.AppUser)
            .FirstOrDefaultAsync(pg => pg.Id == parentGuardianId, ct);

        if (guardian == null)
        {
            return new ResendGuardianCredentialsResponse(
                false,
                "Guardian record not found.",
                null,
                null,
                DateTime.UtcNow);
        }

        if (string.IsNullOrWhiteSpace(guardian.Email))
        {
            return new ResendGuardianCredentialsResponse(
                false,
                "Guardian record has no email address configured.",
                null,
                $"{guardian.FirstName} {guardian.LastName}".Trim(),
                DateTime.UtcNow);
        }

        var now = DateTime.UtcNow;
        var email = guardian.Email.Trim();
        var displayName = $"{guardian.FirstName} {guardian.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = email.Split('@')[0];

        var user = guardian.AppUser;
        if (user == null)
        {
            user = await context.Users.FirstOrDefaultAsync(u => u.Email == email || u.Username == email, ct);
            if (user == null)
            {
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    EntraObjectId = $"parent:{Guid.NewGuid()}",
                    Username = email,
                    Email = email,
                    DisplayName = displayName,
                    IsActive = true,
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                context.Users.Add(user);
            }
            guardian.UserId = user.Id;
            guardian.AppUser = user;
        }

        // Ensure user is active and has parent role
        user.IsActive = true;
        user.Email ??= email;
        user.Username ??= email;
        user.DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? displayName : user.DisplayName;

        var parentRoleId = await context.Roles
            .Where(r => r.Name == LmsRoles.Parent)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (parentRoleId.HasValue)
        {
            var hasParentRole = await context.UserRoles
                .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == parentRoleId.Value, ct);

            if (!hasParentRole)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = parentRoleId.Value,
                    AssignedUtc = now
                });
            }
        }

        // Generate fresh temporary password and set password hash
        var tempPassword = GenerateTemporaryPassword();
        user.PasswordHash = passwordHasher.HashPassword(user, tempPassword);
        user.UpdatedUtc = now;

        // Fetch student links
        var studentLinks = await context.ParentStudentLinks
            .Include(psl => psl.Student)
            .Where(psl => psl.ParentGuardianId == guardian.Id)
            .ToListAsync(ct);

        // Build list of student ward names
        var studentNamesList = studentLinks
            .Where(l => l.Student != null)
            .Select(l => $"{l.Student!.FirstName} {l.Student.LastName}".Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();

        var studentNames = studentNamesList.Count > 0
            ? string.Join(", ", studentNamesList)
            : "your ward";

        // Audit log
        context.AuditLogs.Add(new AuditLog
        {
            Action = "Resend Guardian Credentials",
            EntityName = nameof(ParentGuardian),
            EntityId = guardian.Id.ToString(),
            Changes = $"Generated new temporary password and resent credentials email to {email}",
            UserId = requestedById,
            Timestamp = now
        });

        await context.SaveChangesAsync(ct);

        // Send credentials email
        var clientBaseUrl = GetClientBaseUrl();
        var loginUrl = $"{clientBaseUrl}/auth/login?mode=password&email={Uri.EscapeDataString(email)}";

        try
        {
            await emailService.SendGuardianCredentialsResentEmailAsync(
                email,
                displayName,
                studentNames,
                email,
                tempPassword,
                loginUrl);
        }
        catch (Exception ex)
        {
            return new ResendGuardianCredentialsResponse(
                true,
                $"Credentials updated for {email}, but email sending returned a notice: {ex.Message}",
                email,
                displayName,
                DateTime.UtcNow);
        }

        return new ResendGuardianCredentialsResponse(
            true,
            $"Credentials and temporary password successfully sent to {email}",
            email,
            displayName,
            DateTime.UtcNow);
    }

    public async Task<ResendGuardianCredentialsResponse> ResendCredentialsForStudentAsync(
        Guid studentId,
        Guid? requestedById = null,
        CancellationToken ct = default)
    {
        var student = await context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            return new ResendGuardianCredentialsResponse(
                false,
                "Student not found.",
                null,
                null,
                DateTime.UtcNow);
        }

        // Check if there is an existing link
        var link = await context.ParentStudentLinks
            .Include(psl => psl.ParentGuardian)
            .FirstOrDefaultAsync(psl => psl.StudentId == studentId && psl.ParentGuardian != null, ct);

        if (link?.ParentGuardian != null)
        {
            return await ResendCredentialsAsync(link.ParentGuardian.Id, requestedById, ct);
        }

        // If no link, check emergency contact email
        if (string.IsNullOrWhiteSpace(student.EmergencyContactEmail))
        {
            return new ResendGuardianCredentialsResponse(
                false,
                "Student has no emergency contact email on record to send credentials to.",
                null,
                null,
                DateTime.UtcNow);
        }

        // Provision guardian for student first
        var provResult = await ProvisionForStudentAsync(student, sendInvitationEmail: false, ct: ct);
        if (provResult.ParentGuardianId.HasValue)
        {
            return await ResendCredentialsAsync(provResult.ParentGuardianId.Value, requestedById, ct);
        }

        return new ResendGuardianCredentialsResponse(
            false,
            $"Unable to provision guardian: {provResult.Message}",
            student.EmergencyContactEmail,
            student.EmergencyContactName,
            DateTime.UtcNow);
    }

    private string GetClientBaseUrl()
    {
        var clientBaseUrl = configuration["ClientApp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(clientBaseUrl) ||
            clientBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
            clientBaseUrl.Contains("YOUR_FRONTEND_DOMAIN", StringComparison.OrdinalIgnoreCase))
        {
            return "https://portal.wigweuniversity.edu.ng";
        }
        return clientBaseUrl.TrimEnd('/');
    }

    private static string GenerateTemporaryPassword()
    {
        return "Parent@" + Guid.NewGuid().ToString("N")[..6] + "!";
    }

    public async Task<ProvisionGuardianBatchResponse> ProvisionBatchAsync(
        ProvisionGuardianBatchRequest request,
        CancellationToken ct = default)
    {
        var query = context.Students.AsQueryable();

        if (request.AllEligible)
        {
            query = query.Where(s => s.EmergencyContactEmail != null && s.EmergencyContactEmail != string.Empty);

            if (request.SessionId.HasValue)
                query = query.Where(s => s.AcademicSessionId == request.SessionId.Value);
            if (request.ProgramId.HasValue)
                query = query.Where(s => s.AcademicProgramId == request.ProgramId.Value);
            if (request.LevelId.HasValue)
                query = query.Where(s => s.LevelId == request.LevelId.Value);
            if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<StudentStatus>(request.Status, true, out var status))
                query = query.Where(s => s.Status == status);
        }
        else
        {
            var ids = request.StudentIds?.Distinct().ToList() ?? [];
            if (ids.Count == 0)
            {
                return new ProvisionGuardianBatchResponse(0, 0, 0, 0, 0, 0, []);
            }

            query = query.Where(s => ids.Contains(s.Id));
        }

        var students = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);
        var results = new List<ProvisionGuardianResultDto>();

        foreach (var student in students)
        {
            try
            {
                results.Add(await ProvisionForStudentAsync(student, null, request.SendInvitationEmail, ct));
            }
            catch (Exception ex)
            {
                results.Add(Result(student, "Failed", ex.Message, null, null, false, false, false));
            }
        }

        return new ProvisionGuardianBatchResponse(
            results.Count,
            results.Count(r => r.Status == "CreatedAccount"),
            results.Count(r => r.Status == "LinkedExistingAccount"),
            results.Count(r => r.Status == "AlreadyLinked"),
            results.Count(r => r.Status.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase)),
            results.Count(r => r.Status == "Failed"),
            results);
    }
    private static SystemParentPortalConfigurationDto MapConfiguration(SystemParentPortalConfiguration config)
        => new(
            config.AutoCreateGuardianAccountsOnStudentCreation,
            config.SendGuardianInvitationEmail,
            config.DefaultRelationship,
            string.IsNullOrWhiteSpace(config.AllowedCategoriesJson) || config.AllowedCategoriesJson == "[]"
                ? new List<int>()
                : System.Text.Json.JsonSerializer.Deserialize<List<int>>(config.AllowedCategoriesJson) ?? new List<int>(),
            config.RequireStudentConsentForSensitive,
            config.TreatAdultStudentsAsRestricted);

    private static ProvisionGuardianResultDto Result(
        Student student,
        string status,
        string message,
        Guid? parentGuardianId,
        Guid? parentStudentLinkId,
        bool createdUser,
        bool createdGuardian,
        bool createdLink)
    {
        return new ProvisionGuardianResultDto(
            student.Id,
            $"{student.FirstName} {student.LastName}".Trim(),
            student.StudentNumber,
            student.EmergencyContactEmail,
            status,
            message,
            parentGuardianId,
            parentStudentLinkId,
            createdUser,
            createdGuardian,
            createdLink);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
