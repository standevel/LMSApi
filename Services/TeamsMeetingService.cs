using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using ErrorOr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace LMS.Api.Services;

public class TeamsMeetingService : BaseService, ITeamsMeetingService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<TeamsMeetingService> _logger;

    public TeamsMeetingService(
        IConfiguration configuration,
        ILogger<TeamsMeetingService> logger,
        IAuditService auditService) : base(auditService)
    {
        _logger = logger;

        var tenantId = configuration["AzureAd:TenantId"];
        var clientId = configuration["AzureAd:ClientId"];
        var clientSecret = configuration["AzureAd:ClientSecret"];

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("AzureAd TenantId, ClientId, or ClientSecret is not configured");
        }

        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        _graphClient = new GraphServiceClient(clientSecretCredential);
    }

    public async Task<ErrorOr<OnlineMeeting>> CreateTeamsMeetingAsync(
        string subject,
        DateTime startDateTime,
        DateTime endDateTime,
        string lecturerEntraObjectId,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lecturerEntraObjectId))
            {
                return Error.Validation("InvalidLecturerEntraId", "Lecturer Entra Object ID is required.");
            }

            var onlineMeeting = new OnlineMeeting
            {
                StartDateTime = new DateTimeOffset(startDateTime, TimeSpan.Zero),
                EndDateTime = new DateTimeOffset(endDateTime, TimeSpan.Zero),
                Subject = subject,
                LobbyBypassSettings = new LobbyBypassSettings
                {
                    Scope = LobbyBypassScope.Organization
                }
            };

            _logger.LogInformation("Creating Teams Online Meeting for Lecturer UPN/Id: {LecturerId}, Subject: {Subject}", lecturerEntraObjectId, subject);
            
            var meeting = await _graphClient.Users[lecturerEntraObjectId].OnlineMeetings.PostAsync(onlineMeeting, cancellationToken: ct);
            if (meeting == null)
            {
                return Error.Failure("TeamsMeetingCreationFailed", "Microsoft Graph returned a null meeting object.");
            }

            await LogActionAsync("CreateTeamsMeeting", "OnlineMeeting", meeting.Id ?? string.Empty, 
                $"Created Teams Online Meeting: {subject} (JoinUrl: {meeting.JoinWebUrl})", ct);

            return meeting;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Teams meeting for Lecturer: {LecturerId}", lecturerEntraObjectId);
            return Error.Failure("TeamsMeetingCreationFailed", $"Error creating Teams online meeting: {ex.Message}");
        }
    }

    public async Task<ErrorOr<OnlineMeeting>> UpdateTeamsMeetingAsync(
        string meetingId,
        string subject,
        DateTime startDateTime,
        DateTime endDateTime,
        string lecturerEntraObjectId,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(meetingId))
            {
                return Error.Validation("InvalidMeetingId", "Meeting ID is required.");
            }
            if (string.IsNullOrWhiteSpace(lecturerEntraObjectId))
            {
                return Error.Validation("InvalidLecturerEntraId", "Lecturer Entra Object ID is required.");
            }

            var onlineMeeting = new OnlineMeeting
            {
                StartDateTime = new DateTimeOffset(startDateTime, TimeSpan.Zero),
                EndDateTime = new DateTimeOffset(endDateTime, TimeSpan.Zero),
                Subject = subject
            };

            _logger.LogInformation("Updating Teams Online Meeting: {MeetingId} for Lecturer: {LecturerId}", meetingId, lecturerEntraObjectId);
            
            var meeting = await _graphClient.Users[lecturerEntraObjectId].OnlineMeetings[meetingId].PatchAsync(onlineMeeting, cancellationToken: ct);
            if (meeting == null)
            {
                return Error.Failure("TeamsMeetingUpdateFailed", "Microsoft Graph returned a null meeting object on update.");
            }

            await LogActionAsync("UpdateTeamsMeeting", "OnlineMeeting", meetingId, 
                $"Updated Teams Online Meeting: {subject}", ct);

            return meeting;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Teams meeting: {MeetingId} for Lecturer: {LecturerId}", meetingId, lecturerEntraObjectId);
            return Error.Failure("TeamsMeetingUpdateFailed", $"Error updating Teams online meeting: {ex.Message}");
        }
    }

    public async Task<ErrorOr<ErrorOr.Deleted>> DeleteTeamsMeetingAsync(
        string meetingId,
        string lecturerEntraObjectId,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(meetingId))
            {
                return Error.Validation("InvalidMeetingId", "Meeting ID is required.");
            }
            if (string.IsNullOrWhiteSpace(lecturerEntraObjectId))
            {
                return Error.Validation("InvalidLecturerEntraId", "Lecturer Entra Object ID is required.");
            }

            _logger.LogInformation("Deleting Teams Online Meeting: {MeetingId} for Lecturer: {LecturerId}", meetingId, lecturerEntraObjectId);
            
            await _graphClient.Users[lecturerEntraObjectId].OnlineMeetings[meetingId].DeleteAsync(cancellationToken: ct);

            await LogActionAsync("DeleteTeamsMeeting", "OnlineMeeting", meetingId, 
                $"Deleted Teams Online Meeting: {meetingId}", ct);

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Teams meeting: {MeetingId} for Lecturer: {LecturerId}", meetingId, lecturerEntraObjectId);
            return Error.Failure("TeamsMeetingDeletionFailed", $"Error deleting Teams online meeting: {ex.Message}");
        }
    }
}
