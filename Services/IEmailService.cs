using System.Threading.Tasks;

namespace LMS.Api.Services;

public interface IEmailService
{
    Task SendApplicationSubmittedEmailAsync(string toEmail, string studentName);
    Task SendAdmissionOfferEmailAsync(string toEmail, string studentName, string programName, byte[]? pdfAttachment = null, string? fileName = null);
    Task SendPaymentInstructionsEmailAsync(string toEmail, string studentName);
    Task SendStudentCredentialsEmailAsync(string toEmail, string studentName, string officialEmail, string temporaryPassword);
}
