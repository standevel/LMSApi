using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public sealed class UpdateAcademicSessionEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<UpdateAcademicSessionRequestWrapper, AcademicSessionDto>
{
    public override void Configure()
    {
        Put("/api/admin/sessions/{id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Update an academic session";
            s.Description = "Updates the definitions of an academic session via its unique ID. Can rename the session and shift its operational dates.";
            s.Responses[200] = "Successfully updated the academic session details.";
            s.Responses[404] = "The specified session ID was not found.";
        });
    }

    public override async Task HandleAsync(UpdateAcademicSessionRequestWrapper req, CancellationToken ct)
    {
        var request = new UpdateAcademicSessionRequest
        {
            Name = req.Name,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            IsActive = req.IsActive
        };

        var result = await sessionService.UpdateAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class UpdateAcademicSessionRequestWrapper
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}
