using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public sealed class CreateAcademicSessionEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<CreateAcademicSessionRequest, AcademicSessionDto>
{
    public override void Configure()
    {
        Post("/api/admin/sessions");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Create an academic session";
            s.Description = "Creates a new academic session (e.g., 2024/2025). The session name must be unique.";
            s.Responses[200] = "Successfully created the academic session.";
            s.Responses[400] = "Validation error, typically due to a duplicate session name.";
        });
    }

    public override async Task HandleAsync(CreateAcademicSessionRequest req, CancellationToken ct)
    {
        var result = await sessionService.CreateAsync(req, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
