using System.Text;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Api.Endpoints.BulkOperations;

public sealed class StudentImportTemplateEndpoint(
    IBulkOperationService bulkOperationService,
    ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("bulk-operations/students/template");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var csvContent = GenerateStudentImportTemplate();

        HttpContext.Response.ContentType = "text/csv";
        HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=student_import_template.csv";
        await HttpContext.Response.WriteAsync(csvContent, ct);
    }

    private static string GenerateStudentImportTemplate()
    {
        var headers = new[]
        {
            "Start time",
            "Completion time",
            "Email",
            "Name",
            "Matric Number",
            "First Name",
            "Last Name(Surname)",
            "Phone Number",
            "Personal Email Address",
            "Guardian Phone",
            "Guardian Email",
            "Level",
            "Academic Program",
            "Sponsor",
            "JAMB Number",
            "JAMB Score"
        };

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(h =>
        {
            // Escape headers containing commas
            if (h.Contains(','))
                return $"\"{h}\"";
            return h;
        })));

        // Add a sample row
        var sampleValues = new[]
        {
            "2026-09-01",
            "2030-06-30",
            "student@example.com",
            "John Doe",
            "LUM/2026/001",
            "John",
            "Doe",
            "+2348000000000",
            "john@personal.com",
            "+2348000000001",
            "guardian@example.com",
            "100 Level",
            "Computer Science",
            "Federal Government",
            "JAMB/2026/123456",
            "280"
        };
        sb.AppendLine(string.Join(",", sampleValues.Select(v =>
        {
            if (v.Contains(','))
                return $"\"{v}\"";
            return v;
        })));

        return sb.ToString();
    }
}
