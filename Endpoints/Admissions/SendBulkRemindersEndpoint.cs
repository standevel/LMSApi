using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed record SendBulkRemindersRequest(IEnumerable<Guid> ApplicationIds);

public sealed record SendBulkRemindersResponse(
    int TotalCount,
    int SentCount,
    int FailedCount,
    IEnumerable<ReminderResult> Results);

public sealed record ReminderResult(
    bool Success,
    Guid ApplicationId,
    string? ErrorMessage,
    string? StudentEmail,
    string? StudentName);

public sealed class SendBulkRemindersEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<SendBulkRemindersRequest, SendBulkRemindersResponse>
{
    public override void Configure()
    {
        Post("admissions/applications/reminders/bulk");
        Policies(LmsPolicies.AdmissionsManagement);
        Tags("Admissions");
        Description(d => d
            .WithName("Send Bulk Reminders")
            .WithTags("Admissions")
            .WithSummary("Send reminder emails to multiple applicants"));
    }

    public override async Task HandleAsync(SendBulkRemindersRequest req, CancellationToken ct)
    {
        try
        {
            var result = await admissionService.SendBulkRemindersAsync(req.ApplicationIds, ct);
            var response = new SendBulkRemindersResponse(
                result.TotalCount,
                result.SentCount,
                result.FailedCount,
                result.Results.Select(r => new ReminderResult(
                    r.Success,
                    r.ApplicationId,
                    r.ErrorMessage,
                    r.StudentEmail,
                    r.StudentName)));
            await SendSuccessAsync(response, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Bulk Send Failed", "bulk_send_failed", ex.Message, ct);
        }
    }
}