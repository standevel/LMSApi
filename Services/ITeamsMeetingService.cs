using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using Microsoft.Graph.Models;

namespace LMS.Api.Services;

public interface ITeamsMeetingService
{
    Task<ErrorOr<OnlineMeeting>> CreateTeamsMeetingAsync(
        string subject,
        DateTime startDateTime,
        DateTime endDateTime,
        string lecturerEntraObjectId,
        CancellationToken ct = default);

    Task<ErrorOr<OnlineMeeting>> UpdateTeamsMeetingAsync(
        string meetingId,
        string subject,
        DateTime startDateTime,
        DateTime endDateTime,
        string lecturerEntraObjectId,
        CancellationToken ct = default);

    Task<ErrorOr<ErrorOr.Deleted>> DeleteTeamsMeetingAsync(
        string meetingId,
        string lecturerEntraObjectId,
        CancellationToken ct = default);
}
