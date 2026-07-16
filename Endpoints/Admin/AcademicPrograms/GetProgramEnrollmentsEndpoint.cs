using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Endpoints.Admin;
using Microsoft.EntityFrameworkCore;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class GetProgramEnrollmentsEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<GetProgramEnrollmentsRequest, List<EnrollmentDto>>
{
    public override void Configure()
    {
        Get("admin/programs/{id}/enrollments");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "List program enrollments";
            s.Description = "Retrieves a list of all student enrollments associated with a specific academic program.";
            s.Responses[200] = "Successfully retrieved the list of enrollments.";
        });
    }

    public override async Task HandleAsync(GetProgramEnrollmentsRequest req, CancellationToken ct)
    {
        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        var hasSession = Guid.TryParse(sessionIdStr, out var sessionId) && sessionId != Guid.Empty;

        var query = dbContext.Enrollments
            .Include(x => x.Level)
            .Include(x => x.User)
            .Include(x => x.AcademicSession)
            .Include(x => x.Curriculum)
            .Where(x => x.ProgramId == req.Id);

        // Only filter by session when an explicit session is requested.
        // Without a filter, ALL enrollments (across all sessions) are returned
        // so the list matches what the delete-guard checks.
        if (hasSession)
            query = query.Where(x => x.AcademicSessionId == sessionId);

        var enrollments = await query
            .OrderByDescending(e => e.AcademicSession.StartDate)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.ProgramId,
                "", // Program name not needed when listing for a specific program
                e.LevelId,
                e.Level.Name,
                e.UserId,
                e.User.DisplayName ?? e.User.Username ?? "Unknown",
                e.AcademicSessionId,
                e.AcademicSession.Name,
                e.CurriculumId,
                e.Curriculum.Name,
                e.EnrolledAtUtc))
            .ToListAsync(ct);

        await SendSuccessAsync(enrollments, ct);
    }
}
