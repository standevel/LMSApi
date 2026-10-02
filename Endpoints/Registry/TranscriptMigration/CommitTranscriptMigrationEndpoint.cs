using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Registry.TranscriptMigration;

public sealed class CommitTranscriptMigrationEndpoint(
    ITranscriptMigrationService migrationService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<CommitTranscriptMigrationRequest, CommitTranscriptMigrationResponse>
{
    public override void Configure()
    {
        Post("registry/transcript-migration/commit");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Transcript Migration");
    }

    public override async Task HandleAsync(CommitTranscriptMigrationRequest req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct) ?? Guid.Empty;
            var response = await migrationService.CommitMigrationAsync(req, userId, ct);
            await SendSuccessAsync(response, ct, response.Message);
        }
        catch (KeyNotFoundException knf)
        {
            await SendNotFoundAsync(knf.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(400, ex.Message, "COMMIT_FAILED", ex.Message, ct);
        }
    }
}
