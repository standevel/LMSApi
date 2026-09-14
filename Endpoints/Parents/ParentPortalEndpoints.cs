using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace LMS.Api.Endpoints.Parents;

public sealed class GetLinkedStudentsEndpoint(
    IParentPortalService parentPortalService, 
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext)
    : ApiEndpoint<GetLinkedStudentsEndpoint.GetLinkedStudentsRequest, List<ParentStudentLinkDto>>
{
    public class GetLinkedStudentsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public string ParentId { get; set; } = string.Empty;
    }

    public override void Configure()
    {
        Get("parents/{ParentId}/students");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(GetLinkedStudentsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        Guid parentGuid;
        if (req.ParentId == "current-parent-id" || req.ParentId == "me" || !Guid.TryParse(req.ParentId, out parentGuid))
        {
            var parentGuardian = await dbContext.ParentGuardians
                .FirstOrDefaultAsync(pg => pg.UserId == userId.Value, ct);

            if (parentGuardian == null)
            {
                await SendSuccessAsync(new List<ParentStudentLinkDto>(), ct);
                return;
            }
            parentGuid = parentGuardian.Id;
        }

        var result = await parentPortalService.GetLinkedStudentsAsync(parentGuid, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetStudentProgressEndpoint(
    IParentPortalService parentPortalService,
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext,
    IParentAuthorizationService authService)
    : ApiEndpoint<GetStudentProgressEndpoint.GetStudentProgressRequest, StudentProgressDto>
{
    public class GetStudentProgressRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Get("parents/students/{StudentId}/progress");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(GetStudentProgressRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var guardian = await dbContext.ParentGuardians.FirstOrDefaultAsync(pg => pg.UserId == userId.Value, ct);
        if (guardian == null)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Parent is not linked to this student.", ct);
            return;
        }

        var access = await authService.CanAccessAsync(guardian.Id, req.StudentId, ParentAccessCategory.AcademicResults, ct);
        if (access.IsError)
        {
            await SendFailureAsync(403, "Forbidden", access.FirstError.Code, access.FirstError.Description, ct);
            return;
        }

        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        Guid.TryParse(sessionIdStr, out var sessionId);
        var academicSessionId = sessionId == Guid.Empty ? (Guid?)null : sessionId;

        var result = await parentPortalService.GetStudentProgressAsync(req.StudentId, academicSessionId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetStudentGradesEndpoint(
    IParentPortalService parentPortalService,
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext,
    IParentAuthorizationService authService)
    : ApiEndpoint<GetStudentGradesEndpoint.GetStudentGradesRequest, StudentGradesDto>
{
    public class GetStudentGradesRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Get("parents/students/{StudentId}/grades");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(GetStudentGradesRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var guardian = await dbContext.ParentGuardians.FirstOrDefaultAsync(pg => pg.UserId == userId.Value, ct);
        if (guardian == null)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Parent is not linked to this student.", ct);
            return;
        }

        var access = await authService.CanAccessAsync(guardian.Id, req.StudentId, ParentAccessCategory.AcademicResults, ct);
        if (access.IsError)
        {
            await SendFailureAsync(403, "Forbidden", access.FirstError.Code, access.FirstError.Description, ct);
            return;
        }

        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        Guid.TryParse(sessionIdStr, out var sessionId);
        var academicSessionId = sessionId == Guid.Empty ? (Guid?)null : sessionId;

        var result = await parentPortalService.GetStudentGradesAsync(req.StudentId, academicSessionId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class SendMessageToStudentEndpoint(
    IParentPortalService parentPortalService,
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext,
    IParentAuthorizationService authService)
    : ApiEndpoint<SendMessageToStudentEndpoint.SendMessageToStudentRequest, bool>
{
    public class SendMessageToStudentRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public override void Configure()
    {
        Post("parents/students/{StudentId}/messages");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(SendMessageToStudentRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var guardian = await dbContext.ParentGuardians.FirstOrDefaultAsync(pg => pg.UserId == userId.Value, ct);
        if (guardian == null)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Parent is not linked to this student.", ct);
            return;
        }

        var access = await authService.CanAccessAsync(guardian.Id, req.StudentId, ParentAccessCategory.PrivateMessages, ct);
        if (access.IsError)
        {
            await SendFailureAsync(403, "Forbidden", access.FirstError.Code, access.FirstError.Description, ct);
            return;
        }

        var content = string.IsNullOrWhiteSpace(req.Subject)
            ? req.Content
            : $"{req.Subject.Trim()}\n\n{req.Content}";
        var result = await parentPortalService.SendMessageToStudentAsync(req.StudentId, userId.Value, content, ct);
        await SendAsync(result, ct);
    }
}

public sealed class CreateParentGuardianEndpoint(LmsDbContext dbContext, IPasswordHasher<AppUser> passwordHasher)
    : ApiEndpoint<CreateParentGuardianRequest, ParentGuardianDto>
{
    public override void Configure()
    {
        Post("parents");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Parents");
    }

    public override async Task HandleAsync(CreateParentGuardianRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
        {
            await SendFailureAsync(400, "Email is required.", "INVALID_REQUEST", "Email is required.", ct);
            return;
        }

        var now = DateTime.UtcNow;
        var email = req.Email.Trim();
        var parts = (req.Name ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.ElementAtOrDefault(0) ?? email.Split('@')[0];
        var lastName = parts.ElementAtOrDefault(1) ?? string.Empty;

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email || u.Username == email, ct);
        if (user == null)
        {
            user = new Data.Entities.AppUser
            {
                Id = Guid.NewGuid(),
                EntraObjectId = $"parent:{Guid.NewGuid()}",
                Username = email,
                Email = email,
                DisplayName = $"{firstName} {lastName}".Trim(),
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            var tempPass = "Parent@" + Guid.NewGuid().ToString("N")[..6] + "!";
            user.PasswordHash = passwordHasher.HashPassword(user, tempPass);
            dbContext.Users.Add(user);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                var tempPass = "Parent@" + Guid.NewGuid().ToString("N")[..6] + "!";
                user.PasswordHash = passwordHasher.HashPassword(user, tempPass);
                user.UpdatedUtc = now;
            }
        }

        var parentRoleId = await dbContext.Roles
            .Where(r => r.Name == LmsRoles.Parent)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (parentRoleId.HasValue && !await dbContext.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == parentRoleId.Value, ct))
        {
            dbContext.UserRoles.Add(new Data.Entities.UserRole { UserId = user.Id, RoleId = parentRoleId.Value, AssignedUtc = now });
        }

        var guardian = await dbContext.ParentGuardians.FirstOrDefaultAsync(pg => pg.UserId == user.Id || pg.Email == email, ct);
        if (guardian == null)
        {
            guardian = new Data.Entities.ParentGuardian
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = req.Phone?.Trim() ?? string.Empty,
                Email = email,
                DateAddedUtc = now
            };
            dbContext.ParentGuardians.Add(guardian);
        }
        else
        {
            guardian.UserId = user.Id;
            guardian.FirstName = string.IsNullOrWhiteSpace(guardian.FirstName) ? firstName : guardian.FirstName;
            guardian.LastName = string.IsNullOrWhiteSpace(guardian.LastName) ? lastName : guardian.LastName;
            guardian.PhoneNumber = string.IsNullOrWhiteSpace(guardian.PhoneNumber) ? (req.Phone?.Trim() ?? string.Empty) : guardian.PhoneNumber;
            guardian.Email = string.IsNullOrWhiteSpace(guardian.Email) ? email : guardian.Email;
        }

        if (!await dbContext.FamilyCommunicationPreferences.AnyAsync(p => p.ParentGuardianId == guardian.Id, ct))
        {
            dbContext.FamilyCommunicationPreferences.Add(new Data.Entities.FamilyCommunicationPreference
            {
                Id = Guid.NewGuid(),
                ParentGuardianId = guardian.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new ParentGuardianDto(
            guardian.Id,
            $"{guardian.FirstName} {guardian.LastName}".Trim(),
            guardian.Email,
            guardian.PhoneNumber,
            req.Relationship,
            guardian.DateAddedUtc), ct);
    }
}

public sealed class CreateParentStudentLinkEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateParentStudentLinkRequest, ParentStudentLinkDto>
{
    public override void Configure()
    {
        Post("parents/links");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Parents");
    }

    public override async Task HandleAsync(CreateParentStudentLinkRequest req, CancellationToken ct)
    {
        var guardian = await dbContext.ParentGuardians.FirstOrDefaultAsync(pg => pg.Id == req.ParentGuardianId, ct);
        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.Id == req.StudentId, ct);
        if (guardian == null || student == null)
        {
            await SendFailureAsync(404, "Guardian or student not found.", "NOT_FOUND", "Guardian or student not found.", ct);
            return;
        }

        var link = await dbContext.ParentStudentLinks
            .FirstOrDefaultAsync(l => l.ParentGuardianId == guardian.Id && l.StudentId == student.Id, ct);
        if (link == null)
        {
            link = new Data.Entities.ParentStudentLink
            {
                Id = Guid.NewGuid(),
                ParentGuardianId = guardian.Id,
                StudentId = student.Id,
                RelationshipType = "Guardian",
                LinkedAtUtc = DateTime.UtcNow
            };
            dbContext.ParentStudentLinks.Add(link);
            await dbContext.SaveChangesAsync(ct);
        }

        await SendSuccessAsync(new ParentStudentLinkDto(
            link.Id,
            guardian.Id,
            student.Id,
            student.StudentNumber,
            $"{student.FirstName} {student.LastName}".Trim(),
            student.OfficialEmail,
            student.Status == StudentStatus.Active,
            link.LinkedAtUtc), ct);
    }
}

public sealed class UpdateFamilyCommunicationPreferenceEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<FamilyCommunicationPreferenceDto, FamilyCommunicationPreferenceDto>
{
    public override void Configure()
    {
        Put("parents/communication-preference");
        Roles("Admin", "SuperAdmin", "Registrar", "Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(FamilyCommunicationPreferenceDto req, CancellationToken ct)
    {
        var preference = await dbContext.FamilyCommunicationPreferences
            .FirstOrDefaultAsync(p => p.ParentGuardianId == req.ParentGuardianId, ct);

        if (preference == null)
        {
            preference = new Data.Entities.FamilyCommunicationPreference
            {
                Id = Guid.NewGuid(),
                ParentGuardianId = req.ParentGuardianId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.FamilyCommunicationPreferences.Add(preference);
        }

        preference.EmailNotifications = req.EmailNotifications;
        preference.SmsNotifications = req.SmsNotifications;
        preference.AllowMessageSending = req.AllowMessageSending;
        preference.ReceiveAcademicUpdates = req.ReceiveAcademicUpdates;
        preference.ReceiveAttendanceAlerts = req.ReceiveAttendanceAlerts;
        preference.ReceiveGradeUpdates = req.ReceiveGradeUpdates;
        preference.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new FamilyCommunicationPreferenceDto(
            preference.Id,
            preference.ParentGuardianId,
            preference.EmailNotifications,
            preference.SmsNotifications,
            preference.AllowMessageSending,
            preference.ReceiveAcademicUpdates,
            preference.ReceiveAttendanceAlerts,
            preference.ReceiveGradeUpdates), ct);
    }
}

public sealed class GetParentAccessPolicyEndpoint(
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext,
    IParentAuthorizationService authService)
    : ApiEndpointWithoutRequest<List<ParentAccessPolicyEntry>>
{
    public override void Configure()
    {
        Get("parents/students/{StudentId:guid}/access-policy");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var studentId = Route<Guid>("StudentId");
        var guardian = await dbContext.ParentGuardians.FirstOrDefaultAsync(pg => pg.UserId == userId.Value, ct);
        if (guardian == null)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Parent is not linked to this student.", ct);
            return;
        }

        var categories = Enum.GetValues<ParentAccessCategory>().Cast<ParentAccessCategory>().ToList();
        var policy = new List<ParentAccessPolicyEntry>();
        foreach (var category in categories)
        {
            var access = await authService.CanAccessAsync(guardian.Id, studentId, category, ct);
            policy.Add(new ParentAccessPolicyEntry((int)category, access.IsError ? false : access.Value));
        }

        await SendSuccessAsync(policy, ct);
    }
}

public sealed class SetStudentConsentEndpoint(LmsDbContext dbContext, ICurrentUserContext currentUserContext)
    : ApiEndpoint<SetStudentConsentEndpoint.SetStudentConsentRequest, ParentAccessPolicyEntry>
{
    public class SetStudentConsentRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        public int Category { get; set; }
        public bool IsAllowed { get; set; }
    }

    public override void Configure()
    {
        Post("students/{StudentId:guid}/parent-access-consent");
        Roles("Student", "Admin", "SuperAdmin", "Registrar");
        Tags("Students");
    }

    public override async Task HandleAsync(SetStudentConsentRequest req, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(ParentAccessCategory), req.Category))
        {
            await SendFailureAsync(400, "Invalid category", "INVALID_CATEGORY", "Unknown access category.", ct);
            return;
        }

        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.Id == req.StudentId, ct);
        if (student == null)
        {
            await SendFailureAsync(404, "Student not found.", "NOT_FOUND", "Student not found.", ct);
            return;
        }

        var isStudentSelf = await dbContext.Users.AnyAsync(u =>
            u.Id == userId.Value && (
                (!string.IsNullOrWhiteSpace(student.EntraObjectId) && u.EntraObjectId == student.EntraObjectId) ||
                u.EntraObjectId == $"student:{student.Id}" ||
                (!string.IsNullOrWhiteSpace(student.OfficialEmail) && u.Email == student.OfficialEmail)),
            ct);

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("Registrar");
        if (!isStudentSelf && !isAdmin)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Only the student can set their own consent.", ct);
            return;
        }

        var existing = await dbContext.ParentStudentConsents
            .FirstOrDefaultAsync(c => c.StudentId == req.StudentId && c.Category == req.Category, ct);

        if (existing == null)
        {
            existing = new ParentStudentConsent
            {
                Id = Guid.NewGuid(),
                StudentId = req.StudentId,
                Category = req.Category,
                SetById = userId.Value
            };
            dbContext.ParentStudentConsents.Add(existing);
        }

        existing.IsAllowed = req.IsAllowed;
        existing.SetAtUtc = DateTime.UtcNow;
        existing.SetById = userId.Value;

        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new ParentAccessPolicyEntry(req.Category, req.IsAllowed), ct);
    }
}

internal static class ParentPortalAuthorization
{
    public static Task<bool> IsParentLinkedToStudentAsync(LmsDbContext dbContext, Guid parentUserId, Guid studentId, CancellationToken ct)
    {
        return dbContext.ParentStudentLinks
            .AnyAsync(psl => psl.StudentId == studentId && psl.ParentGuardian!.UserId == parentUserId, ct);
    }
}
