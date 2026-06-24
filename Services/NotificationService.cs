using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class NotificationService(
    LmsDbContext dbContext,
    IAuditService auditService,
    IPushNotificationService pushNotificationService) : BaseService(auditService), INotificationService
{
    public async Task<ErrorOr<NotificationDto>> CreateAsync(CreateNotificationRequest request, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            RecipientId = request.RecipientId,
            SenderId = request.SenderId,
            Title = request.Title,
            Message = request.Message,
            NotificationType = request.NotificationType,
            RelatedUrl = request.RelatedUrl,
            IsRead = false,
            IsActive = true
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Notification", notification.Id.ToString(), $"Created notification: {notification.Title}", ct);

        // Fire-and-forget push notification to user's registered browser endpoints
        _ = Task.Run(async () =>
        {
            try
            {
                await pushNotificationService.SendNotificationAsync(
                    notification.RecipientId,
                    notification.Title,
                    notification.Message,
                    notification.RelatedUrl,
                    CancellationToken.None);
            }
            catch
            {
                // Fail silently in background fire-and-forget task
            }
        }, CancellationToken.None);

        var createdNotification = await dbContext.Notifications
            .Include(n => n.Recipient)
            .Include(n => n.Sender)
            .FirstOrDefaultAsync(n => n.Id == notification.Id, ct);

        return createdNotification!.ToDto();
    }

    public async Task<ErrorOr<List<NotificationDto>>> GetByRecipientIdAsync(Guid recipientId, CancellationToken ct = default)
    {
        var notifications = await dbContext.Notifications
            .Include(n => n.Recipient)
            .Include(n => n.Sender)
            .Where(n => n.RecipientId == recipientId && n.IsActive)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        return notifications.Select(n => n.ToDto()).ToList();
    }

    public async Task<ErrorOr<NotificationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await dbContext.Notifications
            .Include(n => n.Recipient)
            .Include(n => n.Sender)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

        if (notification is null)
            return DomainErrors.Notification.NotFound;

        return notification.ToDto();
    }

    public async Task<ErrorOr<NotificationDto>> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await dbContext.Notifications.FindAsync(id, ct);
        if (notification == null)
            return DomainErrors.Notification.NotFound;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Update", "Notification", id.ToString(), $"Marked notification as read: {notification.Title}", ct);

        return notification.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await dbContext.Notifications.FindAsync(id, ct);
        if (notification == null)
            return DomainErrors.Notification.NotFound;

        notification.IsActive = false;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Notification", id.ToString(), $"Deleted notification: {notification.Title}", ct);

        return Result.Deleted;
    }
}

public interface INotificationService
{
    Task<ErrorOr<NotificationDto>> CreateAsync(CreateNotificationRequest request, CancellationToken ct = default);
    Task<ErrorOr<List<NotificationDto>>> GetByRecipientIdAsync(Guid recipientId, CancellationToken ct = default);
    Task<ErrorOr<NotificationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<NotificationDto>> MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
}
