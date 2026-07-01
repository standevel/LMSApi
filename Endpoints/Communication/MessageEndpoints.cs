using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Communication;

public sealed class CreateMessageEndpoint(IMessageService messageService)
    : ApiEndpoint<CreateMessageRequest, MessageDto>
{
    public override void Configure()
    {
        Post("messages");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CreateMessageRequest req, CancellationToken ct)
    {
        var result = await messageService.CreateAsync(req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class SendCourseLecturerMessageEndpoint(
    LmsDbContext dbContext,
    IMessageService messageService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<SendCourseLecturerMessageEndpoint.SendCourseLecturerMessageEndpointRequest, MessageDto>
{
    public sealed class SendCourseLecturerMessageEndpointRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid CourseOfferingId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public override void Configure()
    {
        Post("student/courses/{CourseOfferingId:guid}/lecturer/messages");
        Roles("Student");
        Tags("Communication", "Student");
    }

    public override async Task HandleAsync(SendCourseLecturerMessageEndpointRequest req, CancellationToken ct)
    {
        var content = req.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            await SendFailureAsync(400, "Message content is required.", "VALIDATION_ERROR", "Message content is required.", ct);
            return;
        }

        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var isEnrolled = await dbContext.CourseEnrollments
            .AsNoTracking()
            .AnyAsync(e =>
                e.StudentId == userId.Value &&
                e.CourseOfferingId == req.CourseOfferingId &&
                e.Status == "Registered", ct);

        if (!isEnrolled)
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var lecturerId = await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(o => o.Id == req.CourseOfferingId)
            .Select(o => o.LecturerId)
            .FirstOrDefaultAsync(ct);

        if (!lecturerId.HasValue)
        {
            await SendFailureAsync(404, "No lecturer is assigned to this course.", "LECTURER_NOT_FOUND", "No lecturer is assigned to this course.", ct);
            return;
        }

        if (lecturerId.Value == userId.Value)
        {
            await SendFailureAsync(400, "You cannot message yourself for this course.", "INVALID_RECIPIENT", "The resolved lecturer matches the current user.", ct);
            return;
        }

        var result = await messageService.CreateAsync(new CreateMessageRequest(
            userId.Value,
            lecturerId.Value,
            content), ct);

        await SendAsync(result, ct);
    }
}

public sealed class GetUserMessagesEndpoint(IMessageService messageService)
    : ApiEndpointWithoutRequest<List<MessageDto>>
{
    public override void Configure()
    {
        Get("messages");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Get the current user id from the context
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;
        if (userId == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        // We'll get messages where the user is the recipient (inbox)
        var result = await messageService.GetByRecipientIdAsync(userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetMessageByIdEndpoint(IMessageService messageService)
    : ApiEndpoint<GetMessageByIdEndpoint.GetMessageByIdRequest, MessageDto>
{
    public class GetMessageByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("messages/{Id:guid}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(GetMessageByIdRequest req, CancellationToken ct)
    {
        var result = await messageService.GetByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class MarkMessageAsReadEndpoint(IMessageService messageService)
    : ApiEndpoint<MarkMessageAsReadEndpoint.MarkMessageAsReadRequest, MessageDto>
{
    public class MarkMessageAsReadRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Put("messages/{Id:guid}/read");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(MarkMessageAsReadRequest req, CancellationToken ct)
    {
        var result = await messageService.MarkAsReadAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteMessageEndpoint(IMessageService messageService)
    : ApiEndpoint<DeleteMessageEndpoint.DeleteMessageRequest, bool>
{
    public class DeleteMessageRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("messages/{Id:guid}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(DeleteMessageRequest req, CancellationToken ct)
    {
        var result = await messageService.DeleteAsync(req.Id, ct);
        await SendAsync(result.Match(
            deleted => true,
            errors => false), ct);
    }
}

public sealed record AdminRecipientDto(Guid AdminId);

public sealed class GetAdminRecipientEndpoint(LmsDbContext dbContext, IConfiguration configuration)
    : ApiEndpointWithoutRequest<AdminRecipientDto>
{
    public override void Configure()
    {
        Get("messages/admin-recipient");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bootstrapAdminEmail = configuration["BootstrapAdmin:Email"];
        if (string.IsNullOrWhiteSpace(bootstrapAdminEmail))
        {
            await SendFailureAsync(404, "Not Found", "NOT_FOUND", "Bootstrap admin email is not configured", ct);
            return;
        }

        var adminUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == bootstrapAdminEmail, ct);

        if (adminUser != null)
        {
            await SendSuccessAsync(new AdminRecipientDto(adminUser.Id), ct);
            return;
        }

        // If the bootstrap admin hasn't logged in yet, provision them dynamically
        var superAdminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == LmsRoles.SuperAdmin, ct);
        if (superAdminRole == null)
        {
            await SendFailureAsync(404, "Not Found", "NOT_FOUND", "SuperAdmin role not found", ct);
            return;
        }

        var newAdmin = new AppUser
        {
            Id = Guid.NewGuid(),
            EntraObjectId = Guid.NewGuid().ToString(),
            Email = bootstrapAdminEmail,
            DisplayName = "System Administrator",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            IsActive = true
        };

        dbContext.Users.Add(newAdmin);
        dbContext.UserRoles.Add(new UserRole { UserId = newAdmin.Id, RoleId = superAdminRole.Id, AssignedUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new AdminRecipientDto(newAdmin.Id), ct);
    }
}

public sealed record AllowedRecipientDto(
    Guid Id,
    string? DisplayName,
    string? Email,
    string[] Roles);

public sealed class GetAllowedRecipientsEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<List<AllowedRecipientDto>>
{
    public override void Configure()
    {
        Get("messages/allowed-recipients");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student", "Parent");
        Tags("Communication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var search = Query<string>("search", isRequired: false);
        
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;
        if (userId == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var currentUser = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (currentUser == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var isStudent = currentUser.UserRoles.Any(ur => ur.Role.Name == LmsRoles.Student);

        IQueryable<AppUser> query = dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .Where(u => u.IsActive);

        if (isStudent)
        {
            var enrolledOfferingIds = await dbContext.CourseEnrollments
                .Where(e => e.StudentId == userId.Value && e.Status == "Registered")
                .Select(e => e.CourseOfferingId)
                .ToListAsync(ct);

            var lecturerIds = await dbContext.CourseOfferings
                .Where(o => enrolledOfferingIds.Contains(o.Id) && o.LecturerId != null)
                .Select(o => o.LecturerId!.Value)
                .ToListAsync(ct);

            query = query.Where(u => 
                u.Id == userId.Value || 
                lecturerIds.Contains(u.Id) || 
                u.UserRoles.Any(ur => ur.Role.Name == LmsRoles.SuperAdmin));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => 
                (u.DisplayName != null && u.DisplayName.Contains(s)) ||
                (u.Email != null && u.Email.Contains(s)) ||
                (u.Username != null && u.Username.Contains(s)));
        }

        var users = await query.ToListAsync(ct);

        var result = users.Select(u => new AllowedRecipientDto(
            u.Id,
            u.DisplayName ?? u.Username,
            u.Email,
            u.UserRoles.Select(ur => ur.Role.Name).ToArray()
        )).ToList();

        await SendSuccessAsync(result, ct);
    }
}
