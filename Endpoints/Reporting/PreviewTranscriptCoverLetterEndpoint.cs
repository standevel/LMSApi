using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Endpoints.Reporting;

public sealed class PreviewTranscriptCoverLetterRequest
{
    public string? TemplateType { get; set; } = "TranscriptCoverLetter";
}

public sealed class PreviewTranscriptCoverLetterEndpoint : Endpoint<PreviewTranscriptCoverLetterRequest>
{
    private readonly ITranscriptCoverLetterPdfService _coverLetterPdfService;
    private readonly ILogger<PreviewTranscriptCoverLetterEndpoint> _logger;

    public PreviewTranscriptCoverLetterEndpoint(
        ITranscriptCoverLetterPdfService coverLetterPdfService,
        ILogger<PreviewTranscriptCoverLetterEndpoint> logger)
    {
        _coverLetterPdfService = coverLetterPdfService;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("reports/transcript-cover-letter/preview");
        Policies(LmsPolicies.Management);
        Tags("Reporting");
        Description(d => d
            .WithName("Preview Transcript Cover Letter")
            .WithTags("Reporting")
            .WithSummary("Generate a sample preview PDF of the transcript cover letter based on the current template."));
    }

    public override async Task HandleAsync(PreviewTranscriptCoverLetterRequest req, CancellationToken ct)
    {
        try
        {
            var pdfBytes = await _coverLetterPdfService.GenerateSampleCoverLetterPdfAsync(req.TemplateType);
            var fileName = $"Sample_Transcript_Cover_Letter_{req.TemplateType ?? "Default"}.pdf";

            HttpContext.Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
            HttpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            HttpContext.Response.ContentType = "application/pdf";
            await HttpContext.Response.Body.WriteAsync(pdfBytes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview sample transcript cover letter");
            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.WriteAsync($"Failed to preview cover letter: {ex.Message}", ct);
        }
    }
}
