using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class DiscussionService(
    LmsDbContext dbContext,
    IAuditService auditService) : BaseService(auditService), IDiscussionService
{
    public async Task<ErrorOr<DiscussionThreadDto>> CreateThreadAsync(CreateDiscussionRequest request, CancellationToken ct = default)
    {
        var thread = new DiscussionThread
        {
            Title = request.Title,
            AuthorId = request.AuthorId,
            CourseOfferingId = request.CourseOfferingId,
            IsPinned = false,
            IsLocked = false,
            IsActive = true
        };

        dbContext.DiscussionThreads.Add(thread);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Create", "DiscussionThread", thread.Id.ToString(), $"Created discussion thread: {thread.Title}", ct);

        var createdThread = await dbContext.DiscussionThreads
            .Include(t => t.Author)
            .Include(t => t.CourseOffering)
            .FirstOrDefaultAsync(t => t.Id == thread.Id, ct);

        return createdThread!.ToDto();
    }

    public async Task<ErrorOr<DiscussionThreadDto>> GetThreadByIdAsync(Guid id, CancellationToken ct = default)
    {
        var thread = await dbContext.DiscussionThreads
            .Include(t => t.Author)
            .Include(t => t.CourseOffering)
            .Include(t => t.Posts)
                .ThenInclude(p => p.Author)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (thread is null)
            return DomainErrors.DiscussionThread.NotFound;

        return thread.ToDto();
    }

    public async Task<ErrorOr<List<DiscussionThreadDto>>> GetThreadsAsync(CancellationToken ct = default)
    {
        var threads = await dbContext.DiscussionThreads
            .Include(t => t.Author)
            .Include(t => t.CourseOffering)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return threads.Select(t => t.ToDto()).ToList();
    }

    public async Task<ErrorOr<DiscussionThreadDto>> UpdateThreadAsync(Guid id, UpdateDiscussionRequest request, CancellationToken ct = default)
    {
        var thread = await dbContext.DiscussionThreads
            .Include(t => t.Author)
            .Include(t => t.CourseOffering)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (thread == null)
            return DomainErrors.DiscussionThread.NotFound;

        thread.Title = request.Title;
        thread.IsPinned = request.IsPinned;
        thread.IsLocked = request.IsLocked;
        thread.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Update", "DiscussionThread", id.ToString(), $"Updated discussion thread: {thread.Title}", ct);

        return thread.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteThreadAsync(Guid id, CancellationToken ct = default)
    {
        var thread = await dbContext.DiscussionThreads.FindAsync(id, ct);
        if (thread == null)
            return DomainErrors.DiscussionThread.NotFound;

        thread.IsActive = false;
        thread.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "DiscussionThread", id.ToString(), $"Deleted discussion thread: {thread.Title}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<DiscussionPostDto>> CreatePostAsync(Guid threadId, Guid authorId, string content, CancellationToken ct = default)
    {
        var thread = await dbContext.DiscussionThreads.FindAsync(threadId, ct);
        if (thread == null)
            return DomainErrors.DiscussionThread.NotFound;

        var post = new DiscussionPost
        {
            DiscussionThreadId = threadId,
            AuthorId = authorId,
            Content = content,
            IsActive = true
        };

        dbContext.DiscussionPosts.Add(post);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Create", "DiscussionPost", post.Id.ToString(), $"Created discussion post in thread {threadId}", ct);

        var createdPost = await dbContext.DiscussionPosts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == post.Id, ct);

        return createdPost!.ToDto();
    }

    public async Task<ErrorOr<DiscussionPostDto>> GetPostByIdAsync(Guid id, CancellationToken ct = default)
    {
        var post = await dbContext.DiscussionPosts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (post is null)
            return DomainErrors.DiscussionPost.NotFound;

        return post.ToDto();
    }

    public async Task<ErrorOr<DiscussionPostDto>> UpdatePostAsync(Guid id, string content, CancellationToken ct = default)
    {
        var post = await dbContext.DiscussionPosts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (post == null)
            return DomainErrors.DiscussionPost.NotFound;

        post.Content = content;
        post.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Update", "DiscussionPost", id.ToString(), $"Updated discussion post", ct);

        return post.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeletePostAsync(Guid id, CancellationToken ct = default)
    {
        var post = await dbContext.DiscussionPosts.FindAsync(id, ct);
        if (post == null)
            return DomainErrors.DiscussionPost.NotFound;

        post.IsActive = false;
        post.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "DiscussionPost", id.ToString(), $"Deleted discussion post", ct);

        return Result.Deleted;
    }
}

public interface IDiscussionService
{
    Task<ErrorOr<DiscussionThreadDto>> CreateThreadAsync(CreateDiscussionRequest request, CancellationToken ct = default);
    Task<ErrorOr<DiscussionThreadDto>> GetThreadByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<DiscussionThreadDto>>> GetThreadsAsync(CancellationToken ct = default);
    Task<ErrorOr<DiscussionThreadDto>> UpdateThreadAsync(Guid id, UpdateDiscussionRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteThreadAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<DiscussionPostDto>> CreatePostAsync(Guid threadId, Guid authorId, string content, CancellationToken ct = default);
    Task<ErrorOr<DiscussionPostDto>> GetPostByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<DiscussionPostDto>> UpdatePostAsync(Guid id, string content, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeletePostAsync(Guid id, CancellationToken ct = default);
}
