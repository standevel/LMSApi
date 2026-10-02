using System;
using System.Collections.Generic;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Registry.TranscriptMigration;

public sealed class GetStudentMigratedCoursesEndpoint(ITranscriptMigrationService migrationService)
    : ApiEndpointWithoutRequest<IEnumerable<MigratedCourseResultDto>>
{
    public override void Configure()
    {
        Get("registry/transcript-migration/students/{id:guid}");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Transcript Migration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = Route<Guid>("id");
        var results = await migrationService.GetStudentMigratedCoursesAsync(studentId, ct);
        await SendSuccessAsync(results, ct);
    }
}
