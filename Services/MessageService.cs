using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LMS.Api.Security;
using Microsoft.AspNetCore.SignalR;
using LMS.Api.Hubs;

namespace LMS.Api.Services;

public sealed class MessageService(
    LmsDbContext dbContext,
    IUserRepository userRepository,
    IAuditService auditService,
    IConfiguration configuration,
    INotificationService notificationService,
    IHubContext<NotificationHub, INotificationClient> hubContext) : BaseService(auditService), IMessageService
{
    public async Task<ErrorOr<MessageDto>> CreateAsync(CreateMessageRequest request, CancellationToken ct = default)
    {
        var message = new Message
        {
            SenderId = request.SenderId,
            RecipientId = request.RecipientId,
            Content = request.Content,
            IsRead = false,
            IsActive = true
        };

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Message", message.Id.ToString(), $"Created message from {request.SenderId} to {request.RecipientId}", ct);

        try
        {
            // Need to fetch them via DB query to include navigation props or map manually if we just want DTO.
            // But wait, the Message extension method expects the Message entity to just call ToDto().
            // Message.ToDto() maps sender/recipient properties if they exist, but if not it maps what it has.
            // Let's just retrieve the message with its includes.
            var createdMsg = await dbContext.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => m.Id == message.Id, ct);
            
            if (createdMsg != null)
            {
                var dto = createdMsg.ToDto();
                await hubContext.Clients.User(request.RecipientId.ToString()).ReceiveMessage(dto);
            }
        }
        catch { }

        // Create a notification for the recipient
        try
        {
            var recipient = await userRepository.GetByIdAsync(request.RecipientId, ct);
            if (recipient != null)
            {
                var sender = await userRepository.GetByIdAsync(request.SenderId, ct);
                var senderName = sender?.DisplayName ?? "Someone";

                await notificationService.CreateAsync(new CreateNotificationRequest(
                    request.RecipientId,
                    request.SenderId,
                    $"New Message from {senderName}",
                    "You have received a new message.",
                    "Message",
                    $"/dashboard/messages"
                ), ct);
            }
        }
        catch
        {
            // Ignore
        }

        var createdMessage = await dbContext.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == message.Id, ct);

        return createdMessage!.ToDto();
    }

    public async Task<ErrorOr<List<MessageDto>>> GetByRecipientIdAsync(Guid recipientId, CancellationToken ct = default)
    {
        var isAdmin = await dbContext.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == recipientId &&
                            (ur.Role.Name == LmsRoles.SuperAdmin || ur.Role.Name == LmsRoles.Admin), ct);

        Guid? bootstrapAdminId = null;
        if (isAdmin)
        {
            var bootstrapAdminEmail = configuration["BootstrapAdmin:Email"];
            if (!string.IsNullOrWhiteSpace(bootstrapAdminEmail))
            {
                var bootstrapAdmin = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == bootstrapAdminEmail, ct);
                if (bootstrapAdmin != null)
                {
                    bootstrapAdminId = bootstrapAdmin.Id;
                }
            }
        }

        var query = dbContext.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .AsNoTracking();

        if (isAdmin && bootstrapAdminId.HasValue)
        {
            query = query.Where(m => (m.RecipientId == recipientId || m.RecipientId == bootstrapAdminId.Value) && m.IsActive);
        }
        else
        {
            query = query.Where(m => m.RecipientId == recipientId && m.IsActive);
        }

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .ToListAsync(ct);

        return messages.Select(m => m.ToDto()).ToList();
    }

    public async Task<ErrorOr<MessageDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var message = await dbContext.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (message is null)
            return DomainErrors.Message.NotFound;

        return message.ToDto();
    }

    public async Task<ErrorOr<MessageDto>> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var message = await dbContext.Messages.FindAsync(id, ct);
        if (message == null)
            return DomainErrors.Message.NotFound;

        message.IsRead = true;
        message.ReadAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Update", "Message", id.ToString(), $"Marked message as read", ct);

        return message.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var message = await dbContext.Messages.FindAsync(id, ct);
        if (message == null)
            return DomainErrors.Message.NotFound;

        message.IsActive = false; // Soft delete

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Message", id.ToString(), $"Deleted message", ct);

        return Result.Deleted;
    }
}