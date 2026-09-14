using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Endpoints.Reporting;

public sealed class DownloadTranscriptCoverLetterRequest
{
    public Guid Id { get; set; }
}

public sealed class DownloadTranscriptCoverLetterEndpoint : Endpoint<DownloadTranscriptCoverLetterRequest>
{
    private readonly ITranscriptCoverLetterPdfService _coverLetterPdfService;
    private readonly LmsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DownloadTranscriptCoverLetterEndpoint> _logger;

    public DownloadTranscriptCoverLetterEndpoint(
        ITranscriptCoverLetterPdfService coverLetterPdfService,
        LmsDbContext dbContext,
        ICurrentUserContext currentUserContext,
        ILogger<DownloadTranscriptCoverLetterEndpoint> logger)
    {
        _coverLetterPdfService = coverLetterPdfService;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("reports/transcript-requests/{Id}/cover-letter");
        Policies(LmsPolicies.Management);
        Tags("Reporting");
        Description(d => d
            .WithName("Download Transcript Cover Letter")
            .WithTags("Reporting")
            .WithSummary("Generate and download the official cover letter for an academic transcript request."));
    }

    public override async Task HandleAsync(DownloadTranscriptCoverLetterRequest req, CancellationToken ct)
    {
        var request = await _dbContext.TranscriptRequests
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == req.Id, ct);

        if (request == null)
        {
            HttpContext.Response.StatusCode = 404;
            await HttpContext.Response.WriteAsync("Transcript request not found.", ct);
            return;
        }

        try
        {
            var pdfBytes = await _coverLetterPdfService.GenerateCoverLetterPdfAsync(request);
            var studentName = !string.IsNullOrWhiteSpace(request.Student?.DisplayName)
                ? request.Student.DisplayName.Replace(" ", "_")
                : "Student";
            var fileName = $"Transcript_Cover_Letter_{studentName}_{request.Id.ToString()[..8]}.pdf";

            HttpContext.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            HttpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            HttpContext.Response.ContentType = "application/pdf";
            await HttpContext.Response.Body.WriteAsync(pdfBytes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate transcript cover letter for request {RequestId}", req.Id);
            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.WriteAsync($"Failed to generate cover letter: {ex.Message}", ct);
        }
    }
}
