using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Contracts;

public record ParentGuardianDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Relationship,
    DateTime CreatedAt);

public record CreateParentGuardianRequest(
    string Name,
    string Email,
    string Phone,
    string Relationship);

public record ParentStudentLinkDto(
    Guid Id,
    Guid ParentGuardianId,
    Guid StudentId,
    string? MatricNumber,
    string StudentName,
    string StudentEmail,
    bool IsActive,
    DateTime LinkedAt);

public record CreateParentStudentLinkRequest(
    Guid ParentGuardianId,
    Guid StudentId);

public record FamilyCommunicationPreferenceDto(
    Guid Id,
    Guid ParentGuardianId,
    bool EmailNotifications,
    bool SmsNotifications,
    bool AllowMessageSending,
    bool ReceiveAcademicUpdates,
    bool ReceiveAttendanceAlerts,
    bool ReceiveGradeUpdates);

public record WebhookSubscriptionDto(
    Guid Id,
    string Url,
    string EventTypes,
    bool IsActive,
    int RetryAttempts,
    int TimeoutSeconds,
    DateTime CreatedAt);

public record CreateWebhookSubscriptionRequest(
    string Url,
    string EventTypes,
    int RetryAttempts = 3,
    int TimeoutSeconds = 30);

public record WebhookDeliveryLogDto(
    Guid Id,
    Guid WebhookSubscriptionId,
    string EventType,
    DateTime SentAtUtc,
    int StatusCode,
    bool IsSuccess,
    int AttemptNumber);

public record BulkOperationDto(
    Guid Id,
    string OperationType,
    string FileName,
    BulkOperationStatus Status,
    int TotalRecords,
    int ProcessedRecords,
    int FailedRecords,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? ResultData = null);

public record CreateBulkOperationRequest(
    string OperationType,
    string FileName,
    string FileUrl);

public record ApiRateLimitDto(
    string ClientId,
    string Endpoint,
    string Method,
    int RequestCount,
    int Limit,
    int Remaining,
    DateTime WindowStartUtc,
    DateTime? ResetTimeUtc);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BulkOperationStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}
