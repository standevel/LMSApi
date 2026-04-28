using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public class SessionManagementService : ISessionManagementService
{
    private readonly LmsDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<SessionManagementService> _logger;

    public SessionManagementService(
        LmsDbContext context,
        IFileStorageService fileStorageService,
        ILogger<SessionManagementService> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<PagedResult<SessionListItem>> GetSessionsAsync(
        SessionFilterRequest filters,
        Guid userId,
        bool isAdmin)
    {
        var query = _context.LectureSessions
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.Venue)
            .Include(s => s.SessionLecturers)
                .ThenInclude(sl => sl.Lecturer)
            .Include(s => s.Materials)
            .Include(s => s.Attendance)
            .AsQueryable();

        // Role-based filtering
        if (!isAdmin)
        {
            query = query.Where(s => s.SessionLecturers.Any(sl => sl.LecturerId == userId));
        }

        // Apply filters
        if (filters.CourseOfferingId.HasValue)
        {
            query = query.Where(s => s.CourseOfferingId == filters.CourseOfferingId.Value);
        }

        if (filters.AcademicSessionId.HasValue)
        {
            query = query.Where(s => s.CourseOffering.AcademicSessionId == filters.AcademicSessionId.Value);
        }

        if (filters.LecturerId.HasValue)
        {
            query = query.Where(s => s.SessionLecturers.Any(sl => sl.LecturerId == filters.LecturerId.Value));
        }

        if (filters.StartDate.HasValue)
        {
            query = query.Where(s => s.SessionDate >= filters.StartDate.Value);
        }

        if (filters.EndDate.HasValue)
        {
            query = query.Where(s => s.SessionDate <= filters.EndDate.Value);
        }

        if (filters.IsCompleted.HasValue)
        {
            query = query.Where(s => s.IsCompleted == filters.IsCompleted.Value);
        }

        // Sort by date ascending
        query = query.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime);

        // Pagination
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(s => new SessionListItem(
                s.Id,
                s.CourseOffering.Course.Code,
                s.CourseOffering.Course.Title,
                s.SessionDate,
                s.StartTime,
                s.EndTime,
                s.Venue != null ? s.Venue.Name : null,
                s.SessionLecturers.Select(sl => sl.Lecturer.DisplayName ?? sl.Lecturer.Email ?? "Unknown").ToList(),
                s.IsManuallyCreated,
                s.IsCompleted,
                s.Materials.Count,
                s.Attendance.Any()))
            .ToListAsync();

        return new PagedResult<SessionListItem>(items, totalCount, filters.Page, filters.PageSize);
    }

    public async Task<SessionDetailsResponse> GetSessionDetailsAsync(
        Guid sessionId,
        Guid userId,
        bool isAdmin)
    {
        var session = await _context.LectureSessions
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.Venue)
            .Include(s => s.SessionLecturers)
                .ThenInclude(sl => sl.Lecturer)
            .Include(s => s.Materials)
                .ThenInclude(m => m.UploadedByUser)
            .Include(s => s.ExternalLinks)
                .ThenInclude(el => el.CreatedByUser)
            .Include(s => s.Attendance)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!isAdmin && !session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to access this session");
        }

        var lecturers = session.SessionLecturers.Select(sl => new LecturerInfo(
            sl.LecturerId,
            sl.Lecturer.DisplayName ?? sl.Lecturer.Email ?? "Unknown",
            sl.Lecturer.Email ?? "")).ToList();

        var materials = session.Materials.Select(m => new MaterialInfo(
            m.Id,
            m.FileName,
            m.FileUrl,
            m.FileSizeBytes,
            m.UploadedAt,
            m.UploadedByUser.DisplayName ?? m.UploadedByUser.Email ?? "Unknown")).ToList();

        var externalLinks = session.ExternalLinks.Select(el => new ExternalLinkInfo(
            el.Id,
            el.Title,
            el.Url,
            el.Description,
            el.CreatedAt,
            el.CreatedByUser.DisplayName ?? el.CreatedByUser.Email ?? "Unknown")).ToList();

        AttendanceStatistics? attendanceStats = null;
        if (session.Attendance.Any())
        {
            attendanceStats = await GetAttendanceStatisticsAsync(sessionId);
        }

        return new SessionDetailsResponse(
            session.Id,
            session.CourseOffering.Course.Code,
            session.CourseOffering.Course.Title,
            session.SessionDate,
            session.StartTime,
            session.EndTime,
            session.Venue?.Name,
            lecturers,
            session.IsManuallyCreated,
            session.IsCompleted,
            session.Notes,
            materials,
            externalLinks,
            attendanceStats);
    }

    public async Task<LectureSession> UpdateSessionAsync(
        Guid sessionId,
        UpdateSessionRequest request,
        Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Validate time range
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("End time must be after start time");
        }

        // Validate date within academic session
        if (request.SessionDate < DateOnly.FromDateTime(session.CourseOffering.AcademicSession.StartDate) ||
            request.SessionDate > DateOnly.FromDateTime(session.CourseOffering.AcademicSession.EndDate))
        {
            throw new InvalidOperationException("Session date must fall within the academic session period");
        }

        // Update session properties
        session.SessionDate = request.SessionDate;
        session.StartTime = request.StartTime;
        session.EndTime = request.EndTime;
        session.VenueId = request.VenueId;
        session.Notes = request.Notes;

        // Update lecturers
        _context.LectureSessionLecturers.RemoveRange(session.SessionLecturers);
        foreach (var lecturerId in request.LecturerIds.Distinct())
        {
            session.SessionLecturers.Add(new LectureSessionLecturer
            {
                LectureSessionId = session.Id,
                LecturerId = lecturerId
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated lecture session {SessionId}", sessionId);

        return session;
    }

    public async Task DeleteSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.Materials)
            .Include(s => s.Attendance)
            .Include(s => s.SessionLecturers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Delete materials from storage
        foreach (var material in session.Materials)
        {
            try
            {
                await _fileStorageService.DeleteFileAsync(material.FileUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file {FileUrl} for material {MaterialId}", material.FileUrl, material.Id);
            }
        }

        _context.LectureSessions.Remove(session);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted lecture session {SessionId}", sessionId);
    }

    public async Task<SessionMaterial> UploadMaterialAsync(
        Guid sessionId,
        IFormFile file,
        Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to upload materials for this session");
        }

        // Validate file type
        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("File type not supported. Allowed: PDF, DOC, DOCX, PPT, PPTX");
        }

        // Validate file size (50MB)
        if (file.Length > 50 * 1024 * 1024)
        {
            throw new InvalidOperationException("File size exceeds maximum allowed size of 50MB");
        }

        // Generate unique filename
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fileUrl = await _fileStorageService.UploadFileAsync(file, "session-materials", fileName);

        var material = new SessionMaterial
        {
            LectureSessionId = sessionId,
            FileName = file.FileName,
            FileUrl = fileUrl,
            FileSizeBytes = file.Length,
            ContentType = file.ContentType,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = userId
        };

        _context.SessionMaterials.Add(material);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Uploaded material {MaterialId} for session {SessionId}", material.Id, sessionId);

        return material;
    }

    public async Task DeleteMaterialAsync(Guid materialId, Guid userId)
    {
        var material = await _context.SessionMaterials
            .Include(m => m.LectureSession)
                .ThenInclude(s => s.SessionLecturers)
            .FirstOrDefaultAsync(m => m.Id == materialId);

        if (material == null)
        {
            throw new InvalidOperationException("Material not found");
        }

        // Check authorization
        if (!material.LectureSession.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this material");
        }

        try
        {
            await _fileStorageService.DeleteFileAsync(material.FileUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {FileUrl} for material {MaterialId}", material.FileUrl, materialId);
        }

        _context.SessionMaterials.Remove(material);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted material {MaterialId}", materialId);
    }

    public async Task<AttendanceStatistics> SaveAttendanceAsync(
        Guid sessionId,
        List<AttendanceRecord> records,
        Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .Include(s => s.Attendance)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to take attendance for this session");
        }

        foreach (var record in records)
        {
            var existing = session.Attendance.FirstOrDefault(a => a.StudentId == record.StudentId);
            if (existing != null)
            {
                // Update existing
                existing.IsPresent = record.IsPresent;
                existing.ModifiedAt = DateTime.UtcNow;
                existing.ModifiedBy = userId;
            }
            else
            {
                // Create new
                var attendance = new SessionAttendance
                {
                    LectureSessionId = sessionId,
                    StudentId = record.StudentId,
                    IsPresent = record.IsPresent,
                    RecordedAt = DateTime.UtcNow,
                    RecordedBy = userId
                };
                _context.SessionAttendances.Add(attendance);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved attendance for session {SessionId}", sessionId);

        // Return attendance statistics
        return await GetAttendanceStatisticsAsync(sessionId);
    }

    public async Task<LectureSession> UpdateNotesAsync(
        Guid sessionId,
        string notes,
        Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to update notes for this session");
        }

        // Validate notes length
        if (notes.Length > 2000)
        {
            throw new InvalidOperationException("Notes cannot exceed 2000 characters");
        }

        session.Notes = notes;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated notes for session {SessionId}", sessionId);

        return session;
    }

    public async Task<LectureSession> ToggleCompletionAsync(
        Guid sessionId,
        bool isCompleted,
        Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to update completion status for this session");
        }

        session.IsCompleted = isCompleted;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Toggled completion for session {SessionId} to {IsCompleted}", sessionId, isCompleted);

        return session;
    }

    public async Task<List<EnrolledStudent>> GetEnrolledStudentsAsync(
        Guid courseOfferingId)
    {
        var courseOffering = await _context.CourseOfferings
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId);

        if (courseOffering == null)
        {
            throw new InvalidOperationException("Course offering not found");
        }

        return await _context.Enrollments
            .Where(e => e.ProgramId == courseOffering.ProgramId 
                && e.LevelId == courseOffering.LevelId 
                && e.AcademicSessionId == courseOffering.AcademicSessionId)
            .Select(e => new EnrolledStudent(
                e.UserId,
                e.User.DisplayName ?? e.User.Email ?? "Unknown",
                e.User.Email ?? ""))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<List<EnrolledStudent>> GetEnrolledStudentsForSessionAsync(
        Guid sessionId,
        Guid userId,
        bool isAdmin)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!isAdmin && !session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to access this session");
        }

        return await GetEnrolledStudentsAsync(session.CourseOfferingId);
    }

    public async Task<AttendanceStatistics> GetAttendanceStatisticsAsync(
        Guid sessionId)
    {
        var attendance = await _context.SessionAttendances
            .Where(a => a.LectureSessionId == sessionId)
            .ToListAsync();

        var totalStudents = attendance.Count;
        var presentCount = attendance.Count(a => a.IsPresent);
        var absentCount = totalStudents - presentCount;
        var percentage = totalStudents > 0 ? (decimal)presentCount / totalStudents * 100 : 0;

        return new AttendanceStatistics(totalStudents, presentCount, absentCount, Math.Round(percentage, 2));
    }

    public async Task<ExternalLinkInfo> AddExternalLinkAsync(
        Guid sessionId,
        AddExternalLinkRequest request,
        Guid userId)
    {
        var session = await _context.LectureSessions
            .Include(s => s.SessionLecturers)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new InvalidOperationException("Lecture session not found");
        }

        // Check authorization
        if (!session.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to add external links for this session");
        }

        // Validate title length
        if (request.Title.Length > 200)
        {
            throw new InvalidOperationException("Title cannot exceed 200 characters");
        }

        // Validate description length
        if (request.Description?.Length > 500)
        {
            throw new InvalidOperationException("Description cannot exceed 500 characters");
        }

        var externalLink = new SessionExternalLink
        {
            LectureSessionId = sessionId,
            Title = request.Title,
            Url = request.Url,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.SessionExternalLinks.Add(externalLink);
        await _context.SaveChangesAsync();

        // Load the created by user for response
        await _context.Entry(externalLink)
            .Reference(el => el.CreatedByUser)
            .LoadAsync();

        _logger.LogInformation("Added external link {LinkId} for session {SessionId}", externalLink.Id, sessionId);

        return new ExternalLinkInfo(
            externalLink.Id,
            externalLink.Title,
            externalLink.Url,
            externalLink.Description,
            externalLink.CreatedAt,
            externalLink.CreatedByUser.DisplayName ?? externalLink.CreatedByUser.Email ?? "Unknown");
    }

    public async Task DeleteExternalLinkAsync(Guid linkId, Guid userId)
    {
        var externalLink = await _context.SessionExternalLinks
            .Include(el => el.LectureSession)
                .ThenInclude(s => s.SessionLecturers)
            .FirstOrDefaultAsync(el => el.Id == linkId);

        if (externalLink == null)
        {
            throw new InvalidOperationException("External link not found");
        }

        // Check authorization
        if (!externalLink.LectureSession.SessionLecturers.Any(sl => sl.LecturerId == userId))
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this external link");
        }

        _context.SessionExternalLinks.Remove(externalLink);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted external link {LinkId}", linkId);
    }
}
