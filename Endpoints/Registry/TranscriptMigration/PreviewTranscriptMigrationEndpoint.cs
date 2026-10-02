using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Registry.TranscriptMigration;

public sealed class PreviewTranscriptMigrationEndpoint(ITranscriptMigrationService migrationService)
    : ApiEndpoint<PreviewTranscriptMigrationRequest, PreviewTranscriptMigrationResponse>
{
    public override void Configure()
    {
        Post("registry/transcript-migration/preview");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Transcript Migration");
    }

    public override async Task HandleAsync(PreviewTranscriptMigrationRequest req, CancellationToken ct)
    {
        try
        {
            var response = await migrationService.PreviewMigrationAsync(req, ct);
            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException knf)
        {
            await SendNotFoundAsync(knf.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(400, ex.Message, "PREVIEW_FAILED", ex.Message, ct);
        }
    }
}
