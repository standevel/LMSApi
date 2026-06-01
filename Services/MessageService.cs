using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class MessageService(
    LmsDbContext dbContext,
    IUserRepository userRepository,
    IAuditService auditService) : BaseService(auditService), IMessageService
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

        // Optionally, create a notification for the recipient
        // We can do that here or let the caller handle it. For now, we'll do it.
        try
        {
            var recipient = await userRepository.GetByIdAsync(request.RecipientId, ct);
            if (recipient != null)
            {
                // We would use the NotificationService, but we don't have a reference.
                // We could inject it, but to avoid circular dependencies, we might use an event or do it in the endpoint.
                // For simplicity, we'll skip the notification in the service and let the endpoint handle it.
                // Alternatively, we can inject the NotificationService as well.
                // Let's inject the NotificationService in the constructor.
                // But we don't want to complicate now. We'll leave it to the endpoint.
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
        var messages = await dbContext.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.RecipientId == recipientId && m.IsActive)
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