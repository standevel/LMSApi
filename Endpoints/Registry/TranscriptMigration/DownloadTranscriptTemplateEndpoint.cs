using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Registry.TranscriptMigration;

public sealed class DownloadTranscriptTemplateEndpoint(ITranscriptMigrationService migrationService)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("registry/transcript-migration/template");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Transcript Migration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bytes = migrationService.GenerateTranscriptTemplate();
        await Send.BytesAsync(
            bytes,
            fileName: "Transcript_Migration_Template.xlsx",
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
