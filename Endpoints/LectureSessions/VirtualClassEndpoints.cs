using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.LectureSessions;

public class CreateVirtualClassEndpoint(
    LmsDbContext dbContext,
    ITeamsMeetingService teamsMeetingService,
    ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<SessionDetailsResponse>
{
    public override void Configure()
    {
        Post("lecture-sessions/{id}/virtual-class");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Lecture Sessions");
        Description(d => d
            .WithName("CreateVirtualClass")
            .WithSummary("Creates a Microsoft Teams online meeting for a lecture session"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("id");
        var currentUserId = await currentUserContext.GetUserIdAsync(ct);

        if (!currentUserId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User identity could not be resolved.", ct);
            return;
        }

        var session = await dbContext.LectureSessions
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.SessionLecturers)
                .ThenInclude(sl => sl.Lecturer)
            .Include(s => s.Materials)
            .Include(s => s.ExternalLinks)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            await SendFailureAsync(404, "Not Found", "SESSION_NOT_FOUND", "Lecture session not found.", ct);
            return;
        }

        // Check if lecturer is assigned to this session (or is admin)
        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isAssignedLecturer = session.SessionLecturers.Any(sl => sl.LecturerId == currentUserId.Value);
        if (!isAdmin && !isAssignedLecturer)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "You are not authorized to create virtual meetings for this session.", ct);
            return;
        }

        if (!string.IsNullOrEmpty(session.OnlineMeetingId))
        {
            await SendFailureAsync(400, "Bad Request", "MEETING_ALREADY_EXISTS", "A virtual class has already been created for this session.", ct);
            return;
        }

        // Find the host lecturer entra ID
        // Fall back to current user if they are a lecturer, or the first session lecturer with an entra ID
        var hostUser = session.SessionLecturers
            .Select(sl => sl.Lecturer)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u.EntraObjectId));

        if (hostUser == null && !string.IsNullOrEmpty(session.CreatedByUser?.EntraObjectId))
        {
            hostUser = session.CreatedByUser;
        }

        if (hostUser == null)
        {
            // Try to load the current logged in user if they have an entra ID
            var currentAppUser = await dbContext.Users.FindAsync(new object[] { currentUserId.Value }, ct);
            if (currentAppUser != null && !string.IsNullOrEmpty(currentAppUser.EntraObjectId))
            {
                hostUser = currentAppUser;
            }
        }

        if (hostUser == null || string.IsNullOrEmpty(hostUser.EntraObjectId))
        {
            await SendFailureAsync(400, "Bad Request", "HOST_ENTRA_ID_MISSING", "No lecturer with a valid Microsoft Entra ID is associated with this session to host the Teams meeting.", ct);
            return;
        }

        // Subject format: CS101 - Intro to Computer Science Lecture
        var subject = $"{session.CourseOffering.Course.Code} - {session.CourseOffering.Course.Title} Virtual Class";
        
        // Meeting times
        var startDateTime = session.SessionDate.ToDateTime(session.StartTime);
        var endDateTime = session.SessionDate.ToDateTime(session.EndTime);

        var meetingResult = await teamsMeetingService.CreateTeamsMeetingAsync(
            subject,
            startDateTime,
            endDateTime,
            hostUser.EntraObjectId,
            ct);

        if (meetingResult.IsError)
        {
            await SendFailureAsync(500, "Graph API Error", "TEAMS_CREATION_FAILED", meetingResult.Errors.First().Description, ct);
            return;
        }

        var meeting = meetingResult.Value;
        session.OnlineMeetingId = meeting.Id;
        session.OnlineMeetingJoinUrl = meeting.JoinWebUrl;

        await dbContext.SaveChangesAsync(ct);

        // Map to return response
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
            m.UploadedByUser?.DisplayName ?? m.UploadedByUser?.Email ?? "Unknown")).ToList();

        var externalLinks = session.ExternalLinks.Select(el => new ExternalLinkInfo(
            el.Id,
            el.Title,
            el.Url,
            el.Description,
            el.CreatedAt,
            el.CreatedByUser?.DisplayName ?? el.CreatedByUser?.Email ?? "Unknown")).ToList();

        var response = new SessionDetailsResponse(
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
            null, // Stats
            session.OnlineMeetingId,
            session.OnlineMeetingJoinUrl);

        await SendSuccessAsync(response, ct, "Virtual class Teams meeting created successfully");
    }
}

public class UpdateVirtualClassEndpoint(
    LmsDbContext dbContext,
    ITeamsMeetingService teamsMeetingService,
    ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<SessionDetailsResponse>
{
    public override void Configure()
    {
        Put("lecture-sessions/{id}/virtual-class");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Lecture Sessions");
        Description(d => d
            .WithName("UpdateVirtualClass")
            .WithSummary("Updates scheduling details of a Teams online meeting for a lecture session"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("id");
        var currentUserId = await currentUserContext.GetUserIdAsync(ct);

        if (!currentUserId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User identity could not be resolved.", ct);
            return;
        }

        var session = await dbContext.LectureSessions
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.SessionLecturers)
                .ThenInclude(sl => sl.Lecturer)
            .Include(s => s.Materials)
            .Include(s => s.ExternalLinks)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            await SendFailureAsync(404, "Not Found", "SESSION_NOT_FOUND", "Lecture session not found.", ct);
            return;
        }

        if (string.IsNullOrEmpty(session.OnlineMeetingId))
        {
            await SendFailureAsync(400, "Bad Request", "NO_MEETING_EXISTS", "No virtual class exists for this session.", ct);
            return;
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isAssignedLecturer = session.SessionLecturers.Any(sl => sl.LecturerId == currentUserId.Value);
        if (!isAdmin && !isAssignedLecturer)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "You are not authorized to update virtual meetings for this session.", ct);
            return;
        }

        var hostUser = session.SessionLecturers
            .Select(sl => sl.Lecturer)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u.EntraObjectId));

        if (hostUser == null && !string.IsNullOrEmpty(session.CreatedByUser?.EntraObjectId))
        {
            hostUser = session.CreatedByUser;
        }

        if (hostUser == null)
        {
            var currentAppUser = await dbContext.Users.FindAsync(new object[] { currentUserId.Value }, ct);
            if (currentAppUser != null && !string.IsNullOrEmpty(currentAppUser.EntraObjectId))
            {
                hostUser = currentAppUser;
            }
        }

        if (hostUser == null || string.IsNullOrEmpty(hostUser.EntraObjectId))
        {
            await SendFailureAsync(400, "Bad Request", "HOST_ENTRA_ID_MISSING", "No lecturer with a valid Microsoft Entra ID is associated with this session.", ct);
            return;
        }

        var subject = $"{session.CourseOffering.Course.Code} - {session.CourseOffering.Course.Title} Virtual Class";
        var startDateTime = session.SessionDate.ToDateTime(session.StartTime);
        var endDateTime = session.SessionDate.ToDateTime(session.EndTime);

        var meetingResult = await teamsMeetingService.UpdateTeamsMeetingAsync(
            session.OnlineMeetingId,
            subject,
            startDateTime,
            endDateTime,
            hostUser.EntraObjectId,
            ct);

        if (meetingResult.IsError)
        {
            await SendFailureAsync(500, "Graph API Error", "TEAMS_UPDATE_FAILED", meetingResult.Errors.First().Description, ct);
            return;
        }

        var response = new SessionDetailsResponse(
            session.Id,
            session.CourseOffering.Course.Code,
            session.CourseOffering.Course.Title,
            session.SessionDate,
            session.StartTime,
            session.EndTime,
            session.Venue?.Name,
            session.SessionLecturers.Select(sl => new LecturerInfo(sl.LecturerId, sl.Lecturer.DisplayName ?? sl.Lecturer.Email ?? "Unknown", sl.Lecturer.Email ?? "")).ToList(),
            session.IsManuallyCreated,
            session.IsCompleted,
            session.Notes,
            session.Materials.Select(m => new MaterialInfo(m.Id, m.FileName, m.FileUrl, m.FileSizeBytes, m.UploadedAt, m.UploadedByUser?.DisplayName ?? m.UploadedByUser?.Email ?? "Unknown")).ToList(),
            session.ExternalLinks.Select(el => new ExternalLinkInfo(el.Id, el.Title, el.Url, el.Description, el.CreatedAt, el.CreatedByUser?.DisplayName ?? el.CreatedByUser?.Email ?? "Unknown")).ToList(),
            null,
            session.OnlineMeetingId,
            session.OnlineMeetingJoinUrl);

        await SendSuccessAsync(response, ct, "Virtual class Teams meeting updated successfully");
    }
}

public class DeleteVirtualClassEndpoint(
    LmsDbContext dbContext,
    ITeamsMeetingService teamsMeetingService,
    ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<SessionDetailsResponse>
{
    public override void Configure()
    {
        Delete("lecture-sessions/{id}/virtual-class");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Lecture Sessions");
        Description(d => d
            .WithName("DeleteVirtualClass")
            .WithSummary("Cancels the Teams meeting and removes it from a lecture session"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("id");
        var currentUserId = await currentUserContext.GetUserIdAsync(ct);

        if (!currentUserId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User identity could not be resolved.", ct);
            return;
        }

        var session = await dbContext.LectureSessions
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.SessionLecturers)
                .ThenInclude(sl => sl.Lecturer)
            .Include(s => s.Materials)
            .Include(s => s.ExternalLinks)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            await SendFailureAsync(404, "Not Found", "SESSION_NOT_FOUND", "Lecture session not found.", ct);
            return;
        }

        if (string.IsNullOrEmpty(session.OnlineMeetingId))
        {
            await SendFailureAsync(400, "Bad Request", "NO_MEETING_EXISTS", "No virtual class exists for this session.", ct);
            return;
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isAssignedLecturer = session.SessionLecturers.Any(sl => sl.LecturerId == currentUserId.Value);
        if (!isAdmin && !isAssignedLecturer)
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "You are not authorized to delete virtual meetings for this session.", ct);
            return;
        }

        var hostUser = session.SessionLecturers
            .Select(sl => sl.Lecturer)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u.EntraObjectId));

        if (hostUser == null && !string.IsNullOrEmpty(session.CreatedByUser?.EntraObjectId))
        {
            hostUser = session.CreatedByUser;
        }

        if (hostUser == null)
        {
            var currentAppUser = await dbContext.Users.FindAsync(new object[] { currentUserId.Value }, ct);
            if (currentAppUser != null && !string.IsNullOrEmpty(currentAppUser.EntraObjectId))
            {
                hostUser = currentAppUser;
            }
        }

        if (hostUser == null || string.IsNullOrEmpty(hostUser.EntraObjectId))
        {
            await SendFailureAsync(400, "Bad Request", "HOST_ENTRA_ID_MISSING", "No lecturer with a valid Microsoft Entra ID is associated with this session.", ct);
            return;
        }

        var deleteResult = await teamsMeetingService.DeleteTeamsMeetingAsync(
            session.OnlineMeetingId,
            hostUser.EntraObjectId,
            ct);

        if (deleteResult.IsError)
        {
            await SendFailureAsync(500, "Graph API Error", "TEAMS_DELETION_FAILED", deleteResult.Errors.First().Description, ct);
            return;
        }

        session.OnlineMeetingId = null;
        session.OnlineMeetingJoinUrl = null;

        await dbContext.SaveChangesAsync(ct);

        var response = new SessionDetailsResponse(
            session.Id,
            session.CourseOffering.Course.Code,
            session.CourseOffering.Course.Title,
            session.SessionDate,
            session.StartTime,
            session.EndTime,
            session.Venue?.Name,
            session.SessionLecturers.Select(sl => new LecturerInfo(sl.LecturerId, sl.Lecturer.DisplayName ?? sl.Lecturer.Email ?? "Unknown", sl.Lecturer.Email ?? "")).ToList(),
            session.IsManuallyCreated,
            session.IsCompleted,
            session.Notes,
            session.Materials.Select(m => new MaterialInfo(m.Id, m.FileName, m.FileUrl, m.FileSizeBytes, m.UploadedAt, m.UploadedByUser?.DisplayName ?? m.UploadedByUser?.Email ?? "Unknown")).ToList(),
            session.ExternalLinks.Select(el => new ExternalLinkInfo(el.Id, el.Title, el.Url, el.Description, el.CreatedAt, el.CreatedByUser?.DisplayName ?? el.CreatedByUser?.Email ?? "Unknown")).ToList(),
            null,
            null,
            null);

        await SendSuccessAsync(response, ct, "Virtual class Teams meeting deleted successfully");
    }
}
