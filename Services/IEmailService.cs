using System.Threading.Tasks;

namespace LMS.Api.Services;

public interface IEmailService
{
    Task SendApplicationSubmittedEmailAsync(string toEmail, string studentName);
    Task SendAdmissionOfferEmailAsync(
        string toEmail,
        string studentName,
        string programName,
        string acceptOfferUrl,
        string rejectOfferUrl,
        byte[]? pdfAttachment = null,
        string? fileName = null);
    Task SendPaymentInstructionsEmailAsync(string toEmail, string studentName, decimal amountDue, string paymentPageUrl);
    Task SendStudentCredentialsEmailAsync(string toEmail, string studentName, string officialEmail, string temporaryPassword);
    Task SendOfferAcceptedConfirmationAsync(string toEmail, string studentName, string programName);
    Task SendExistingAccountNotificationAsync(string toEmail, string studentName, string officialEmail);
}
