using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Registry.TranscriptMigration;

public sealed class UploadTranscriptMigrationRequest
{
    public Guid StudentId { get; set; }
    public IFormFile File { get; set; } = null!;
}

public sealed class UploadAndPreviewTranscriptMigrationEndpoint(ITranscriptMigrationService migrationService)
    : ApiEndpoint<UploadTranscriptMigrationRequest, PreviewTranscriptMigrationResponse>
{
    public override void Configure()
    {
        Post("registry/transcript-migration/upload-preview");
        AllowFileUploads();
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Transcript Migration");
    }

    public override async Task HandleAsync(UploadTranscriptMigrationRequest req, CancellationToken ct)
    {
        if (req.File == null || req.File.Length == 0)
        {
            await SendFailureAsync(400, "Please upload a valid transcript file (.xlsx or .csv).", "FILE_REQUIRED", "No file uploaded.", ct);
            return;
        }

        try
        {
            using var stream = req.File.OpenReadStream();
            var parsedCourses = migrationService.ParseTranscriptFile(stream, req.File.FileName);

            if (parsedCourses.Count == 0)
            {
                await SendFailureAsync(400, "No courses could be parsed from the uploaded file. Please check the file formatting.", "EMPTY_TRANSCRIPT", "Empty transcript.", ct);
                return;
            }

            var previewReq = new PreviewTranscriptMigrationRequest
            {
                StudentId = req.StudentId,
                Courses = parsedCourses
            };

            var response = await migrationService.PreviewMigrationAsync(previewReq, ct);
            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException knf)
        {
            await SendNotFoundAsync(knf.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(400, ex.Message, "UPLOAD_PARSE_FAILED", ex.Message, ct);
        }
    }
}
