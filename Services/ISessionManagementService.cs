using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Services;

public interface ISessionManagementService
{
    Task<PagedResult<SessionListItem>> GetSessionsAsync(
        SessionFilterRequest filters,
        Guid userId,
        bool isAdmin);

    Task<SessionDetailsResponse> GetSessionDetailsAsync(
        Guid sessionId,
        Guid userId,
        bool isAdmin);

    Task<LectureSession> UpdateSessionAsync(
        Guid sessionId,
        UpdateSessionRequest request,
        Guid userId);

    Task DeleteSessionAsync(Guid sessionId, Guid userId);

    Task<SessionMaterial> UploadMaterialAsync(
        Guid sessionId,
        IFormFile file,
        Guid userId);

    Task DeleteMaterialAsync(Guid materialId, Guid userId);

    Task<ExternalLinkInfo> AddExternalLinkAsync(
        Guid sessionId,
        AddExternalLinkRequest request,
        Guid userId);

    Task DeleteExternalLinkAsync(Guid linkId, Guid userId);

    Task<AttendanceStatistics> SaveAttendanceAsync(
        Guid sessionId,
        List<AttendanceRecord> records,
        Guid userId);

    Task<LectureSession> UpdateNotesAsync(
        Guid sessionId,
        string notes,
        Guid userId);

    Task<LectureSession> ToggleCompletionAsync(
        Guid sessionId,
        bool isCompleted,
        Guid userId);

    Task<List<EnrolledStudent>> GetEnrolledStudentsAsync(
        Guid courseOfferingId);

    Task<List<EnrolledStudent>> GetEnrolledStudentsForSessionAsync(
        Guid sessionId,
        Guid userId,
        bool isAdmin);

    Task<AttendanceStatistics> GetAttendanceStatisticsAsync(
        Guid sessionId);
}
