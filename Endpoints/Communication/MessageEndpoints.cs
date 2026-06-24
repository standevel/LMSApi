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
        Get("messages/{Id}");
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
        Put("messages/{Id}/read");
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
        Delete("messages/{Id}");
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
