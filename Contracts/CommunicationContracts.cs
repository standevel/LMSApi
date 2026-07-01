using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public sealed record AnnouncementDto(
    Guid Id,
    string Title,
    string? Content,
    Guid? AuthorId,
    string? AuthorName,
    bool IsGlobal,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsActive,
    DateTime? ExpiresAt
);

public sealed record CreateAnnouncementRequest(
    string Title,
    string? Content,
    Guid? AuthorId,
    bool IsGlobal,
    DateTime? ExpiresAt
);

public sealed record UpdateAnnouncementRequest(
    string Title,
    string? Content,
    bool IsGlobal,
    DateTime? ExpiresAt
);

public sealed record DiscussionThreadDto(
    Guid Id,
    string Title,
    Guid? AuthorId,
    string? AuthorName,
    Guid? CourseOfferingId,
    string? CourseOfferingName,
    bool IsPinned,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsActive,
    int PostCount
);

public sealed record DiscussionPostDto(
    Guid Id,
    Guid DiscussionThreadId,
    Guid? AuthorId,
    string? AuthorName,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsActive
);

public sealed record CreateDiscussionRequest(
    string Title,
    Guid? AuthorId,
    Guid? CourseOfferingId
);

public sealed record UpdateDiscussionRequest(
    string Title,
    bool IsPinned,
    bool IsLocked
);

public sealed record MessageDto(
    Guid Id,
    Guid SenderId,
    string? SenderName,
    Guid RecipientId,
    string? RecipientName,
    string Content,
    DateTime SentAt,
    bool IsRead,
    DateTime? ReadAt
);

public sealed record CreateMessageRequest(
    Guid SenderId,
    Guid RecipientId,
    string Content
);

public sealed record SendCourseLecturerMessageRequest(
    string Content
);

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientId,
    string? RecipientName,
    Guid? SenderId,
    string? SenderName,
    string Title,
    string Message,
    string? NotificationType,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? RelatedUrl
);

public sealed record CreateNotificationRequest(
    Guid RecipientId,
    Guid? SenderId,
    string Title,
    string Message,
    string? NotificationType,
    string? RelatedUrl
);

public sealed record UpdateNotificationRequest(
    bool IsRead
);
