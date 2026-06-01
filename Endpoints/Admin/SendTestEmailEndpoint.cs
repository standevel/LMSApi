using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin;

public sealed record SendTestEmailResponse(string Message);

public sealed class SendTestEmailEndpoint(IEmailService emailService)
    : ApiEndpointWithoutRequest<SendTestEmailResponse>
{
    public override void Configure()
    {
        Post("admin/send-test-email");
        AllowAnonymous();
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await emailService.SendTestEmailAsync(
                "standevcode@gmail.com",
                "Test Email from LMS API",
                "This is a test email triggered from the LMS API backend using the Brevo email service.");

            await SendSuccessAsync(new SendTestEmailResponse("Email sent successfully to standevcode@gmail.com"), ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Failed to send email", "email_error", ex.Message, ct);
        }
    }
}