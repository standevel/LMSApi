using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using LMS.Api.Hubs;

namespace LMS.Api.Services;

public sealed class AnnouncementService(
    LmsDbContext dbContext,
    IAuditService auditService,
    IHubContext<NotificationHub, INotificationClient> hubContext) : BaseService(auditService), IAnnouncementService
{
    public async Task<ErrorOr<AnnouncementDto>> CreateAsync(CreateAnnouncementRequest request, CancellationToken ct = default)
    {
        var announcement = new Announcement
        {
            Title = request.Title,
            Content = request.Content,
            AuthorId = request.AuthorId,
            IsGlobal = request.IsGlobal,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        dbContext.Announcements.Add(announcement);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Announcement", announcement.Id.ToString(), $"Created announcement: {announcement.Title}", ct);

        var createdAnnouncement = await dbContext.Announcements
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == announcement.Id, ct);

        var dto = createdAnnouncement!.ToDto();

        try
        {
            await hubContext.Clients.All.ReceiveAnnouncement(dto);
        }
        catch { }

        return dto;
    }

    public async Task<ErrorOr<AnnouncementDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var announcement = await dbContext.Announcements
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (announcement is null)
            return Error.NotFound("Announcement.NotFound", "Announcement not found.");

        return announcement.ToDto();
    }

    public async Task<ErrorOr<List<AnnouncementDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var announcements = await dbContext.Announcements
            .Include(a => a.Author)
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return announcements.Select(a => a.ToDto()).ToList();
    }

    public async Task<ErrorOr<AnnouncementDto>> UpdateAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken ct = default)
    {
        var announcement = await dbContext.Announcements
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (announcement == null)
            return Error.NotFound("Announcement.NotFound", "Announcement not found.");

        announcement.Title = request.Title;
        announcement.Content = request.Content;
        announcement.IsGlobal = request.IsGlobal;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Update", "Announcement", id.ToString(), $"Updated announcement: {announcement.Title}", ct);

        return announcement.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var announcement = await dbContext.Announcements.FindAsync(id, ct);
        if (announcement == null)
            return Error.NotFound("Announcement.NotFound", "Announcement not found.");

        announcement.IsActive = false; // Soft delete
        announcement.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Announcement", id.ToString(), $"Deleted announcement: {announcement.Title}", ct);

        return Result.Deleted;
    }
}
