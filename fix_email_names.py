import re

with open("/Users/mac/Apps/LMS APP/LMSApi/Services/BrevoEmailService.cs", "r") as f:
    content = f.read()

# Add the FormatName method
if "private static string FormatName" not in content:
    content = content.replace('private readonly string _senderName = configuration["Brevo:SenderName"] ?? "Wigwe University Admissions";',
'''private readonly string _senderName = configuration["Brevo:SenderName"] ?? "Wigwe University Admissions";

    private static string FormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }''')

methods = {
    "SendApplicationSubmittedEmailAsync": ["studentName"],
    "SendAdmissionOfferEmailAsync": ["studentName"],
    "SendPaymentInstructionsEmailAsync": ["studentName"],
    "SendStudentCredentialsEmailAsync": ["studentName"],
    "SendOfferAcceptedConfirmationAsync": ["studentName"],
    "SendExistingAccountNotificationAsync": ["studentName"],
    "SendApplicationReminderEmailAsync": ["studentName"],
    "SendGuardianCredentialsEmailAsync": ["guardianName", "studentName"],
    "SendCourseAssignmentEmailAsync": ["lecturerName"]
}

for method, vars in methods.items():
    # find the method signature and opening brace
    # pattern: public (async )?Task MethodName(...)\s*{
    pattern = r'(public (?:async )?Task ' + method + r'\([^)]*\)\s*\{)'
    match = re.search(pattern, content)
    if match:
        signature = match.group(1)
        additions = "".join([f"\n        {v} = FormatName({v});" for v in vars])
        if additions not in content[match.end():match.end()+200]:
            content = content[:match.end()] + additions + content[match.end():]

with open("/Users/mac/Apps/LMS APP/LMSApi/Services/BrevoEmailService.cs", "w") as f:
    f.write(content)
