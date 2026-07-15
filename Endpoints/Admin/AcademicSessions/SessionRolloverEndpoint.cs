using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;
using ErrorOr;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public sealed class SessionRolloverEndpoint(ISessionRolloverService rolloverService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<SessionRolloverRequest, SessionRolloverResultDto>
{
    public override void Configure()
    {
        Post("admin/sessions/rollover");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Execute academic session rollover";
            s.Description = "Bulk-copies courses, lecturer assignments, timetables, fee setups, student scholarships, and promotes active students to the next level in a target session.";
            s.Responses[200] = "Successfully completed the rollover operation.";
            s.Responses[400] = "Validation or business logic error during rollover.";
        });
    }

    public override async Task HandleAsync(SessionRolloverRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (userId == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await rolloverService.RolloverSessionAsync(req, userId.Value, ct);
        
        await result.Match(
            data => SendSuccessAsync(data, ct, "Rollover completed successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
