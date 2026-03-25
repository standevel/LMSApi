using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public sealed class BrevoEmailService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<BrevoEmailService> logger) : IEmailService
{
    private readonly string _apiKey = configuration["Brevo:ApiKey"] ?? string.Empty;
    private readonly string _senderEmail = configuration["Brevo:SenderEmail"] ?? "no-reply@wigweuniversity.edu.ng";
    private readonly string _senderName = configuration["Brevo:SenderName"] ?? "Wigwe University Admissions";

    private async Task SendEmailAsync(string toEmail, string subject, string htmlContent, object? attachment = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogWarning("Brevo API Key is missing. Email to {Email} was not sent.", toEmail);
            return;
        }

        var payload = new
        {
            sender = new { name = _senderName, email = _senderEmail },
            to = new[] { new { email = toEmail } },
            subject = subject,
            htmlContent = htmlContent,
            attachment = attachment
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", _apiKey);
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to send email via Brevo. Status: {Status}, Error: {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while sending email to {Email}", toEmail);
        }
    }

    public Task SendApplicationSubmittedEmailAsync(string toEmail, string studentName)
    {
        var subject = "Application Received - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Application Received!</h2>
                <p>Dear {studentName},</p>
                <p>Thank you for applying to Wigwe University. We have received your application and it is currently under review.</p>
                <p>We will notify you of the next steps shortly.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>This is an automated message, please do not reply.</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendAdmissionOfferEmailAsync(string toEmail, string studentName, string programName, byte[]? pdfAttachment = null, string? fileName = null)
    {
        var subject = "Admission Offer - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Congratulations!</h2>
                <p>Dear {studentName},</p>
                <p>We are pleased to offer you admission into the <strong>{programName}</strong> program at Wigwe University!</p>
                <p>Please find attached your official admission letter.</p>
                <p>To accept this offer, please log in to the applicant portal and click 'Accept Offer'.</p>
                <p>After acceptance, you will receive instructions on how to pay your commitment fee.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University Admissions Office</p>
            </div>";

        object? attachment = null;
        if (pdfAttachment != null && !string.IsNullOrEmpty(fileName))
        {
            attachment = new[]
            {
                new { content = Convert.ToBase64String(pdfAttachment), name = fileName }
            };
        }

        return SendEmailAsync(toEmail, subject, content, attachment);
    }

    public Task SendPaymentInstructionsEmailAsync(string toEmail, string studentName)
    {
        var subject = "Payment Instructions - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Next Step: Payment</h2>
                <p>Dear {studentName},</p>
                <p>Thank you for accepting your admission offer. To secure your spot, please proceed with the payment of your commitment fee.</p>
                <p>Payment can be made via the applicant portal or via direct bank transfer using the details provided there.</p>
                <p>Once payment is verified, your official student account will be created.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University Bursary</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendStudentCredentialsEmailAsync(string toEmail, string studentName, string officialEmail, string temporaryPassword)
    {
        var subject = "Welcome to Wigwe University - Your Official Credentials";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Welcome to the Tribe!</h2>
                <p>Dear {studentName},</p>
                <p>Your payment has been verified, and your official student account has been created.</p>
                <div style='background: #f9f9f9; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                    <p><strong>Official Email:</strong> {officialEmail}</p>
                    <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
                </div>
                <p>Please log in to <a href='https://portal.wigweuniversity.edu.ng'>the portal</a> using these credentials. You will be prompted to change your password upon first login.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University ICT Department</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }
}
