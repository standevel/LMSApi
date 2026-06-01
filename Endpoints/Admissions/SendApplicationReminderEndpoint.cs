using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed record SendReminderRequest(Guid Id);

public sealed record SendReminderResponse(
    bool Success,
    Guid ApplicationId,
    string? ErrorMessage,
    string? StudentEmail,
    string? StudentName);

public sealed class SendApplicationReminderEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<SendReminderRequest, SendReminderResponse>
{
    public override void Configure()
    {
        Post("admissions/applications/{Id}/reminder");
        Policies(LmsPolicies.Management);
        Tags("Admissions");
        Description(d => d
            .WithName("Send Application Reminder")
            .WithTags("Admissions")
            .WithSummary("Send a reminder email to an applicant to complete JAMB CAPS and O'Level steps"));
    }

    public override async Task HandleAsync(SendReminderRequest req, CancellationToken ct)
    {
        try
        {
            var result = await admissionService.SendReminderAsync(req.Id, ct);
            var response = new SendReminderResponse(
                result.Success,
                result.ApplicationId,
                result.ErrorMessage,
                result.StudentEmail,
                result.StudentName);
            await SendSuccessAsync(response, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Send Failed", "send_failed", ex.Message, ct);
        }
    }
}