using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LMS.Api.Data.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

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
            throw new InvalidOperationException("Brevo API Key is not configured. Cannot send emails.");
        }

        // Build payload dynamically so we never send a null attachment field (Brevo rejects it)
        var payloadDict = new Dictionary<string, object>
        {
            ["sender"] = new { name = _senderName, email = _senderEmail },
            ["to"] = new[] { new { email = toEmail } },
            ["subject"] = subject,
            ["htmlContent"] = htmlContent
        };

        if (attachment != null)
        {
            payloadDict["attachment"] = attachment;
        }

        var payload = (object)payloadDict;

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
                throw new InvalidOperationException($"Brevo API returned {response.StatusCode}: {error}");
            }
            logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while sending email to {Email}", toEmail);
            throw;
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

    public Task SendAdmissionOfferEmailAsync(
        string toEmail,
        string studentName,
        string programName,
        byte[]? pdfAttachment = null,
        string? fileName = null)
    {
        var subject = "Admission Offer - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Congratulations!</h2>
                <p>Dear {studentName},</p>
                <p>We are pleased to offer you admission into the <strong>{programName}</strong> program at Wigwe University!</p>
                <p>Please find attached your provisional admission letter.</p>
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

    public Task SendPaymentInstructionsEmailAsync(string toEmail, string studentName, decimal amountDue, string paymentPageUrl)
    {
        var subject = "Payment Instructions - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Next Step: Payment</h2>
                <p>Dear {studentName},</p>
                <p>Thank you for accepting your admission offer. To secure your spot, please proceed with the payment of your acceptance fee.</p>
                <p><strong>Amount Due:</strong> ₦{amountDue:N2}</p>
                <p>You can review the fee and complete payment from the page below:</p>
                <p><a href='{paymentPageUrl}' style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 22px; border-radius:12px; font-weight:700; display:inline-block;'>Pay Acceptance Fee</a></p>
                <p style='font-size: 12px; color: #666;'>If the button above does not work, copy and paste this link into your browser: {paymentPageUrl}</p>
                <p>Once payment is verified, your official student account will be created and your admission will be finalized.</p>
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

    public Task SendOfferAcceptedConfirmationAsync(string toEmail, string studentName, string programName)
    {
        var subject = "Offer Accepted - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Offer Accepted Successfully!</h2>
                <p>Dear {studentName},</p>
                <p>Congratulations on accepting your admission offer for the <strong>{programName}</strong> program at Wigwe University!</p>
                <p>Your acceptance has been recorded and is now being processed by the Admissions Office.</p>
                <p><strong>Next Steps:</strong></p>
                <ol>
                    <li>The Registrar will review and approve your admission</li>
                    <li>You will receive your official student credentials via email</li>
                    <li>You will be able to access the student portal to complete payment</li>
                </ol>
                <p>Please allow 1-2 business days for your account to be created.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University Admissions Office</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendExistingAccountNotificationAsync(string toEmail, string studentName, string officialEmail)
    {
        var subject = "Wigwe University - Your Student Account Information";
        var content = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <h2 style='color: #003366;'>Student Account Access</h2>
                <p>Dear {studentName},</p>
                <p>Your student account has been processed. We found that you already have an existing account in our system.</p>
                <div style='background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p><strong>Official Email:</strong> {officialEmail}</p>
                </div>
                <p>You can log in using your existing password. If you have forgotten your password, please use the 'Forgot Password' option on the login page or contact the IT Support team.</p>
                <p><a href='https://portal.wigweuniversity.edu.ng' style='background: #003366; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 10px;'>Access Student Portal</a></p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University IT Support<br>Email: support@wigweuniversity.edu.ng</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendTestEmailAsync(string toEmail, string subject, string message)
    {
        var encodedMessage = System.Net.WebUtility.HtmlEncode(message);
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Test Email</h2>
                <p>{encodedMessage}</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>This is a test email from Wigwe University LMS API.</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }

    public async Task SendApplicationReminderEmailAsync(string toEmail, string studentName, string applicationNumber, AdmissionStatus status)
    {
        var subject = "Action Required: Complete Your Admission Process - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #D4AF37;'>Important: Action Required</h2>
                <p>Dear {studentName},</p>
                <p>This is a reminder regarding your admission application <strong>{applicationNumber}</strong> to Wigwe University.</p>
                <p>Please ensure you have completed the following steps to receive your offer letter:</p>
                <ol style='line-height: 1.8;'>
                    <li><strong>JAMB CAPS Portal:</strong> Ensure Wigwe University is selected as your FIRST CHOICE in the JAMB CAPS portal.</li>
                    <li><strong>O'Level Results:</strong> Upload your O'Level results to JAMB to ensure you receive your offer letter.</li>
                </ol>
                <p style='background: #fff3cd; padding: 15px; border-left: 4px solid #D4AF37; margin: 20px 0;'>
                    <strong>Important:</strong> If these steps are not completed, JAMB may not be able to process your admission offer.
                </p>
                <p>If you have already completed these steps, please disregard this message.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University Admissions Office<br>
                Email: admissions@wigweuniversity.edu.ng</p>
            </div>";
        await SendEmailAsync(toEmail, subject, content);
    }

    public async Task SendBulkApplicationRemindersAsync(IEnumerable<(string Email, string StudentName, string ApplicationNumber, AdmissionStatus Status)> recipients)
    {
        foreach (var recipient in recipients)
        {
            try
            {
                await SendApplicationReminderEmailAsync(recipient.Email, recipient.StudentName, recipient.ApplicationNumber, recipient.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[EMAIL-BULK-ERROR] Failed to send reminder email to {Email} for application {ApplicationNumber}", 
                    recipient.Email, recipient.ApplicationNumber);
                throw;
            }
        }
    }

    public Task SendGuardianCredentialsEmailAsync(string toEmail, string guardianName, string studentName, string loginEmail, bool isNewAccount)
    {
        var subject = "Wigwe University - Parent/Guardian Portal Access";

        string body;
        if (isNewAccount)
        {
            body = $@"
                <p>An account has been created for you on the <strong>Wigwe University Parent/Guardian Portal</strong>.</p>
                <div style='background:#f9f9f9; padding:15px; border-radius:8px; margin:20px 0;'>
                    <p><strong>Login Email:</strong> {loginEmail}</p>
                    <p style='margin:0;'><strong>Password:</strong> Use the <em>Forgot Password</em> link on the portal to set your password.</p>
                </div>
                <p>
                    <a href='https://portal.wigweuniversity.edu.ng/parent'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 22px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Access Parent Portal
                    </a>
                </p>
                <p style='font-size:12px; color:#666;'>
                    If the button above does not work, copy and paste this link:<br>
                    https://portal.wigweuniversity.edu.ng/parent
                </p>";
        }
        else
        {
            body = $@"
                <p>Your existing Wigwe University account has been linked to <strong>{studentName}'s</strong> student profile.</p>
                <p>You can now monitor your ward's academic progress on the Parent/Guardian Portal using your existing credentials.</p>
                <p>
                    <a href='https://portal.wigweuniversity.edu.ng/parent'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 22px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Access Parent Portal
                    </a>
                </p>";
        }

        var content = $@"
            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #eee; border-radius:10px;'>
                <h2 style='color:#006B62;'>Parent/Guardian Portal</h2>
                <p>Dear {guardianName},</p>
                <p>You have been registered as the parent/guardian of <strong>{studentName}</strong> at Wigwe University.</p>
                {body}
                <hr style='border:0; border-top:1px solid #eee; margin:20px 0;'>
                <p style='font-size:12px; color:#666;'>
                    Wigwe University Registry<br>
                    Email: registry@wigweuniversity.edu.ng
                </p>
            </div>";

        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendCourseAssignmentEmailAsync(string toEmail, string lecturerName, string courseCode, string courseTitle, string sessionName)
    {
        var subject = $"Course Assignment: {courseCode} - Wigwe University";
        var content = $@"
            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #eee; border-radius:10px;'>
                <h2 style='color:#006B62;'>New Course Assignment</h2>
                <p>Dear {lecturerName},</p>
                <p>You have been assigned to teach the following course for the <strong>{sessionName}</strong> academic session:</p>
                <div style='background:#f9f9f9; padding:15px; border-radius:8px; margin:20px 0;'>
                    <p><strong>Course Code:</strong> {courseCode}</p>
                    <p><strong>Course Title:</strong> {courseTitle}</p>
                </div>
                <p>You can view your course details and manage materials via the LMS portal.</p>
                <p>
                    <a href='https://lms.wigweuniversity.edu.ng/lecturer/courses'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 22px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Go to My Courses
                    </a>
                </p>
                <hr style='border:0; border-top:1px solid #eee; margin:20px 0;'>
                <p style='font-size:12px; color:#666;'>
                    Wigwe University Academic Registry
                </p>
            </div>";

        return SendEmailAsync(toEmail, subject, content);
    }
}
