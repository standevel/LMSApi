using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

// Request models
public record SessionFilterRequest(
    Guid? CourseOfferingId = null,
    Guid? AcademicSessionId = null,
    Guid? LecturerId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    bool? IsCompleted = null,
    int Page = 1,
    int PageSize = 20);

public record UpdateSessionRequest(
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? VenueId,
    List<Guid> LecturerIds,
    string? Notes);

public record SaveAttendanceRequest(List<AttendanceRecord> Records);

public record AttendanceRecord(Guid StudentId, bool IsPresent);

public record UpdateNotesRequest(string Notes);

public record ToggleCompletionRequest(bool IsCompleted);

public record AddExternalLinkRequest(
    string Title,
    string Url,
    string? Description);

// Response models
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record SessionListItem(
    Guid Id,
    string CourseCode,
    string CourseName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? VenueName,
    List<string> LecturerNames,
    bool IsManuallyCreated,
    bool IsCompleted,
    int MaterialCount,
    bool HasAttendance);

public record SessionDetailsResponse(
    Guid Id,
    string CourseCode,
    string CourseName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? VenueName,
    List<LecturerInfo> Lecturers,
    bool IsManuallyCreated,
    bool IsCompleted,
    string? Notes,
    List<MaterialInfo> Materials,
    List<ExternalLinkInfo> ExternalLinks,
    AttendanceStatistics? AttendanceStats);

public record MaterialInfo(
    Guid Id,
    string FileName,
    string FileUrl,
    long FileSizeBytes,
    DateTime UploadedAt,
    string UploadedByName);

public record ExternalLinkInfo(
    Guid Id,
    string Title,
    string Url,
    string? Description,
    DateTime CreatedAt,
    string CreatedByName);

public record AttendanceStatistics(
    int TotalStudents,
    int PresentCount,
    int AbsentCount,
    decimal AttendancePercentage);

public record EnrolledStudent(
    Guid Id,
    string Name,
    string Email);
