using System.Threading.Tasks;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IEmailService
{
    Task SendApplicationSubmittedEmailAsync(string toEmail, string studentName);
    Task SendAdmissionOfferEmailAsync(
        string toEmail,
        string studentName,
        string programName,
        byte[]? pdfAttachment = null,
        string? fileName = null);
    Task SendPaymentInstructionsEmailAsync(string toEmail, string studentName, decimal amountDue, string paymentPageUrl);
    Task SendStudentCredentialsEmailAsync(string toEmail, string studentName, string officialEmail, string temporaryPassword);
    Task SendOfferAcceptedConfirmationAsync(string toEmail, string studentName, string programName);
    Task SendExistingAccountNotificationAsync(string toEmail, string studentName, string officialEmail);
    Task SendTestEmailAsync(string toEmail, string subject, string message);
    Task SendApplicationReminderEmailAsync(string toEmail, string studentName, string applicationNumber, AdmissionStatus status);
    Task SendBulkApplicationRemindersAsync(IEnumerable<(string Email, string StudentName, string ApplicationNumber, AdmissionStatus Status)> recipients);
    Task SendGuardianCredentialsEmailAsync(string toEmail, string guardianName, string studentName, string loginEmail, bool isNewAccount);
}
