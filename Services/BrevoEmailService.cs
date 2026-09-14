using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LMS.Api.Data.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace LMS.Api.Services;

public sealed class BrevoEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _senderEmail;
    private readonly string _senderName;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = ResolveApiKey(configuration);
        _senderEmail = configuration["Brevo:SenderEmail"] ?? "no-reply@wigweuniversity.edu.ng";
        _senderName = configuration["Brevo:SenderName"] ?? "Wigwe University Admissions";

        var masked = _apiKey.Length > 12 ? $"{_apiKey[..12]}...{_apiKey[^4..]}" : (_apiKey.Length > 0 ? "CONFIGURED" : "EMPTY");
        _logger.LogInformation("[BREVO] Initialized. MaskedKey={MaskedKey}, HasKey={HasKey}, SenderEmail={SenderEmail}", masked, !string.IsNullOrEmpty(_apiKey), _senderEmail);
    }

    private static string ResolveApiKey(IConfiguration configuration)
    {
        var rawKey = (configuration["Brevo:ApiKey"] ?? string.Empty).Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(rawKey))
        {
            return rawKey;
        }

        var mcpKey = (configuration["Brevo:MCPKey"] ?? string.Empty).Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(mcpKey))
        {
            try
            {
                var bytes = Convert.FromBase64String(mcpKey);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                using var doc = System.Text.Json.JsonDocument.Parse(decoded);
                if (doc.RootElement.TryGetProperty("api_key", out var prop))
                {
                    var key = prop.GetString()?.Replace(" ", "").Trim();
                    if (!string.IsNullOrWhiteSpace(key)) return key;
                }
            }
            catch
            {
                if (mcpKey.StartsWith("xkeysib-", StringComparison.OrdinalIgnoreCase))
                {
                    return mcpKey;
                }
            }
        }

        return string.Empty;
    }

    private static string FormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlContent, object? attachment = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("Brevo API Key is not configured. Cannot send emails.");
        }

        var payload = (object)new Dictionary<string, object>
        {
            ["sender"] = new { name = _senderName, email = _senderEmail },
            ["to"] = new[] { new { email = toEmail } },
            ["subject"] = subject,
            ["htmlContent"] = htmlContent
        };

        if (attachment != null)
        {
            var payloadDict = new Dictionary<string, object>
            {
                ["sender"] = new { name = _senderName, email = _senderEmail },
                ["to"] = new[] { new { email = toEmail } },
                ["subject"] = subject,
                ["htmlContent"] = htmlContent,
                ["attachment"] = attachment
            };
            payload = payloadDict;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", _apiKey);
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("[BREVO] Response Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send email via Brevo. Status: {Status}, Error: {Error}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"Brevo API returned {response.StatusCode}: {responseBody}");
            }
            _logger.LogInformation("[BREVO] Email sent successfully to {Email}. KeyPrefix={KeyPrefix}", toEmail, _apiKey[..8]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BREVO] Exception occurred while sending email to {Email}", toEmail);
            throw;
        }
    }

    public Task SendApplicationSubmittedEmailAsync(string toEmail, string studentName)
    {
        studentName = FormatName(studentName);
        var subject = "Application Submission Confirmation - Wigwe University Admissions";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Admission Application Received</h2>
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
        string? fileName = null,
        byte[]? secondAttachment = null,
        string? secondFileName = null)
    {
        studentName = FormatName(studentName);
        var subject = $"Provisional Admission Offer: {programName} - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Provisional Offer of Admission</h2>
                <p>Dear {studentName},</p>
                <p>We are pleased to offer you admission into the <strong>{programName}</strong> program at Wigwe University!</p>
                <p>Please find attached your provisional admission letter.</p>
                <p style='color:#b45309; font-size:13px;'><strong>Note:</strong> A separate memorandum on advance tuition fee payments is also attached for your parent/guardian.</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>Wigwe University Admissions Office</p>
            </div>";

        object? attachment = null;
        var attachments = new List<object>();

        if (pdfAttachment != null && !string.IsNullOrEmpty(fileName))
            attachments.Add(new { content = Convert.ToBase64String(pdfAttachment), name = fileName });

        if (secondAttachment != null && !string.IsNullOrEmpty(secondFileName))
            attachments.Add(new { content = Convert.ToBase64String(secondAttachment), name = secondFileName });

        if (attachments.Count > 0)
            attachment = attachments.ToArray();

        return SendEmailAsync(toEmail, subject, content, attachment);
    }

    public Task SendPaymentInstructionsEmailAsync(string toEmail, string studentName, decimal amountDue, string paymentPageUrl)
    {
        studentName = FormatName(studentName);
        var subject = "Action Required: Acceptance Fee Payment Instructions - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Acceptance Fee Payment & Next Steps</h2>
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
        studentName = FormatName(studentName);
        var subject = "Official Student Portal Credentials & Account Activation - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Welcome to Wigwe University - Student Credentials</h2>
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
        studentName = FormatName(studentName);
        var subject = $"Admission Offer Acceptance Confirmation: {programName} - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>Admission Offer Acceptance Confirmed</h2>
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
        studentName = FormatName(studentName);
        var subject = "Student Portal Account Access & Status Update - Wigwe University";
        var content = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <h2 style='color: #003366;'>Existing Student Account Notification</h2>
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
        if (!string.IsNullOrWhiteSpace(subject) && !subject.Contains("Wigwe University", StringComparison.OrdinalIgnoreCase))
        {
            subject = $"[System Test] {subject} - Wigwe University LMS";
        }
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #006B62;'>LMS Email Verification Test</h2>
                <p>{encodedMessage}</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #666;'>This is a test email from Wigwe University LMS API.</p>
            </div>";
        return SendEmailAsync(toEmail, subject, content);
    }

    public async Task SendApplicationReminderEmailAsync(string toEmail, string studentName, string applicationNumber, AdmissionStatus status)
    {
        studentName = FormatName(studentName);
        var subject = $"Urgent Action Required: Complete Admission Requirements (App #{applicationNumber}) - Wigwe University";
        var content = $@"
            <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                <h2 style='color: #D4AF37;'>Admission Process Completion Reminder</h2>
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
                _logger.LogError(ex, "[EMAIL-BULK-ERROR] Failed to send reminder email to {Email} for application {ApplicationNumber}", 
                    recipient.Email, recipient.ApplicationNumber);
                throw;
            }
        }
    }

    public Task SendGuardianCredentialsEmailAsync(string toEmail, string guardianName, string studentName, string loginEmail, string? temporaryPassword, bool isNewAccount, string? portalUrl = null)
    {
        guardianName = FormatName(guardianName);
        studentName = FormatName(studentName);
        var subject = "Parent & Guardian Portal Access Credentials - Wigwe University";
        var loginLink = !string.IsNullOrWhiteSpace(portalUrl) && !portalUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) && !portalUrl.Contains("YOUR_FRONTEND_DOMAIN", StringComparison.OrdinalIgnoreCase)
            ? portalUrl
            : "https://portal.wigweuniversity.edu.ng/auth/login";

        string body;
        if (isNewAccount)
        {
            var passwordBlock = !string.IsNullOrWhiteSpace(temporaryPassword)
                ? $@"<p><strong>Temporary Password:</strong> <span style='font-family:monospace; background:#e2e8f0; padding:2px 8px; border-radius:4px; font-weight:700;'>{temporaryPassword}</span></p>
                    <p style='margin:0; font-size:13px; color:#4a5568;'><em>Please log in and change your password immediately upon first sign-in.</em></p>"
                : @"<p style='margin:0;'><strong>Password:</strong> Use the <em>Forgot Password</em> link on the login page to set your initial password.</p>";

            body = $@"
                <p>An official account has been created for you on the <strong>Wigwe University Parent & Guardian Portal</strong>.</p>
                <div style='background:#f8fafc; border:1px solid #e2e8f0; padding:18px; border-radius:12px; margin:20px 0;'>
                    <p style='margin-bottom:8px;'><strong>Login Email:</strong> {loginEmail}</p>
                    {passwordBlock}
                </div>
                <p style='margin-top:24px;'>
                    <a href='{loginLink}'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 24px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Sign In to Parent Portal
                    </a>
                </p>
                <p style='font-size:12px; color:#666; margin-top:16px;'>
                    If the button above does not work, copy and paste this link:<br>
                    <a href='{loginLink}' style='color:#006B62;'>{loginLink}</a>
                </p>";
        }
        else
        {
            body = $@"
                <p>Your existing Wigwe University account has been linked to <strong>{studentName}'s</strong> student profile.</p>
                <p>You can now monitor your ward's academic progress, view course grades, track attendance, and inspect fees on the Parent Portal using your existing account credentials.</p>
                <p style='margin-top:24px;'>
                    <a href='{loginLink}'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 24px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Access Parent Portal
                    </a>
                </p>";
        }

        var content = $@"
            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:24px; border:1px solid #e2e8f0; border-radius:16px; background:#ffffff;'>
                <div style='text-align:center; margin-bottom:20px;'>
                    <h2 style='color:#006B62; margin:0 0 6px 0;'>Wigwe University</h2>
                    <p style='color:#64748b; font-size:13px; margin:0;'>Parent & Guardian Portal</p>
                </div>
                <hr style='border:0; border-top:1px solid #f1f5f9; margin:16px 0 20px 0;'>
                <p style='font-size:15px;'>Dear <strong>{guardianName}</strong>,</p>
                <p>You have been registered as the parent/guardian of <strong>{studentName}</strong> at Wigwe University.</p>
                {body}
                <hr style='border:0; border-top:1px solid #e2e8f0; margin:24px 0 16px 0;'>
                <p style='font-size:12px; color:#64748b; margin:0;'>
                    Wigwe University Registry & ICT Support<br>
                    Email: registry@wigweuniversity.edu.ng
                </p>
            </div>";

        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendGuardianCredentialsResentEmailAsync(string toEmail, string guardianName, string studentName, string loginEmail, string temporaryPassword, string? portalUrl = null)
    {
        guardianName = FormatName(guardianName);
        studentName = FormatName(studentName);
        var subject = "Parent Portal Login Credentials Resent - Wigwe University";
        var loginLink = !string.IsNullOrWhiteSpace(portalUrl) && !portalUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) && !portalUrl.Contains("YOUR_FRONTEND_DOMAIN", StringComparison.OrdinalIgnoreCase)
            ? portalUrl
            : "https://portal.wigweuniversity.edu.ng/auth/login";

        var content = $@"
            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:24px; border:1px solid #e2e8f0; border-radius:16px; background:#ffffff;'>
                <div style='text-align:center; margin-bottom:20px;'>
                    <h2 style='color:#006B62; margin:0 0 6px 0;'>Wigwe University</h2>
                    <p style='color:#64748b; font-size:13px; margin:0;'>Parent & Guardian Portal</p>
                </div>
                <hr style='border:0; border-top:1px solid #f1f5f9; margin:16px 0 20px 0;'>
                <p style='font-size:15px;'>Dear <strong>{guardianName}</strong>,</p>
                <p>Your login credentials for the Wigwe University Parent Portal have been reset upon administrative request.</p>
                <p>You are connected as parent/guardian of: <strong>{studentName}</strong>.</p>
                <div style='background:#f8fafc; border:1px solid #e2e8f0; padding:18px; border-radius:12px; margin:20px 0;'>
                    <p style='margin-bottom:8px;'><strong>Login Email / Username:</strong> {loginEmail}</p>
                    <p style='margin-bottom:8px;'><strong>New Temporary Password:</strong> <span style='font-family:monospace; background:#e2e8f0; padding:2px 8px; border-radius:4px; font-weight:700;'>{temporaryPassword}</span></p>
                    <p style='margin:0; font-size:13px; color:#4a5568;'><em>Please sign in and change this password immediately.</em></p>
                </div>
                <p style='margin-top:24px;'>
                    <a href='{loginLink}'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 24px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Login to Parent Portal
                    </a>
                </p>
                <p style='font-size:12px; color:#666; margin-top:16px;'>
                    If the button above does not work, copy and paste this URL into your browser:<br>
                    <a href='{loginLink}' style='color:#006B62;'>{loginLink}</a>
                </p>
                <hr style='border:0; border-top:1px solid #e2e8f0; margin:24px 0 16px 0;'>
                <p style='font-size:12px; color:#64748b; margin:0;'>
                    Wigwe University Registry<br>
                    Email: registry@wigweuniversity.edu.ng
                </p>
            </div>";

        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetUrl)
    {
        userName = FormatName(userName);
        var subject = "Password Reset Request - Wigwe University LMS";

        if (string.IsNullOrWhiteSpace(resetUrl) ||
            resetUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
            resetUrl.Contains("YOUR_FRONTEND_DOMAIN", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(resetUrl, UriKind.Absolute, out var parsedUri))
            {
                resetUrl = "https://portal.wigweuniversity.edu.ng" + parsedUri.PathAndQuery;
            }
            else
            {
                resetUrl = "https://portal.wigweuniversity.edu.ng/auth/forgot-password";
            }
        }

        var content = $@"
            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:24px; border:1px solid #e2e8f0; border-radius:16px; background:#ffffff;'>
                <div style='text-align:center; margin-bottom:20px;'>
                    <h2 style='color:#006B62; margin:0 0 6px 0;'>Wigwe University LMS</h2>
                    <p style='color:#64748b; font-size:13px; margin:0;'>Account Security</p>
                </div>
                <hr style='border:0; border-top:1px solid #f1f5f9; margin:16px 0 20px 0;'>
                <p style='font-size:15px;'>Hello <strong>{userName}</strong>,</p>
                <p>We received a request to reset the password for your Wigwe University LMS account ({toEmail}).</p>
                <p>Click the button below to choose a new password. This link is valid for 24 hours.</p>
                <p style='margin-top:24px; margin-bottom:24px;'>
                    <a href='{resetUrl}'
                       style='background:#006B62; color:#ffffff; text-decoration:none; padding:14px 24px; border-radius:12px; font-weight:700; display:inline-block;'>
                        Reset My Password
                    </a>
                </p>
                <p style='font-size:12px; color:#666;'>
                    If the button above does not work, copy and paste this URL into your browser:<br>
                    <a href='{resetUrl}' style='color:#006B62;'>{resetUrl}</a>
                </p>
                <div style='background:#fffbeb; border-left:4px solid #f59e0b; padding:12px; margin-top:20px; border-radius:0 8px 8px 0;'>
                    <p style='font-size:12px; color:#92400e; margin:0;'>
                        If you did not request this password reset, please ignore this email or contact IT support if you have security concerns.
                    </p>
                </div>
                <hr style='border:0; border-top:1px solid #e2e8f0; margin:24px 0 16px 0;'>
                <p style='font-size:12px; color:#64748b; margin:0;'>
                    Wigwe University ICT Department<br>
                    Email: support@wigweuniversity.edu.ng
                </p>
            </div>";

        return SendEmailAsync(toEmail, subject, content);
    }

    public Task SendCourseAssignmentEmailAsync(string toEmail, string lecturerName, string courseCode, string courseTitle, string sessionName)
    {
        lecturerName = FormatName(lecturerName);
        var subject = $"Course Allocation Notice: {courseCode} - {courseTitle} ({sessionName}) - Wigwe University";
        var content = $@"
            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #eee; border-radius:10px;'>
                <h2 style='color:#006B62;'>Official Course Allocation & Teaching Assignment</h2>
                <p>Dear {lecturerName},</p>
                <p>You have been assigned to teach the following course for the <strong>{sessionName}</strong> academic session:</p>
                <div style='background:#f9f9f9; padding:15px; border-radius:8px; margin:20px 0;'>
                    <p><strong>Course Code:</strong> {courseCode}</p>
                    <p><strong>Course Title:</strong> {courseTitle}</p>
                </div>
                <p>You can view your course details and manage materials via the LMS portal.</p>
                <p>
                    <a href='https://portal.wigweuniversity.edu.ng/dashboard/lecturer/courses'
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
