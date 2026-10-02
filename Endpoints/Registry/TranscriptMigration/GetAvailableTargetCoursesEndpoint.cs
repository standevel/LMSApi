using System.Collections.Generic;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Registry.TranscriptMigration;

public sealed class GetAvailableTargetCoursesEndpoint(ITranscriptMigrationService migrationService)
    : ApiEndpointWithoutRequest<IEnumerable<TranscriptTargetCourseDto>>
{
    public override void Configure()
    {
        Get("registry/transcript-migration/courses");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Transcript Migration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var courses = await migrationService.GetAvailableTargetCoursesAsync(ct);
        await SendSuccessAsync(courses, ct);
    }
}
