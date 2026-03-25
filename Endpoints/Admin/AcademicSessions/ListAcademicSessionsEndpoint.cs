using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public sealed class ListAcademicSessionsEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<EmptyRequest, List<AcademicSessionDto>>
{
    public override void Configure()
    {
        Get("/api/admin/sessions");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "List academic sessions";
            s.Description = "Retrieves a comprehensive list of all academic sessions, along with their start and end dates and active status.";
            s.Responses[200] = "Successfully retrieved the list of sessions.";
        });
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await sessionService.GetAllAsync(ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
