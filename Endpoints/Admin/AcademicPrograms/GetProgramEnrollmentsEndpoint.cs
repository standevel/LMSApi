using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Endpoints.Admin;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class GetProgramEnrollmentsEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<GetProgramEnrollmentsRequest, List<EnrollmentDto>>
{
    public override void Configure()
    {
        Get("/api/admin/programs/{id}/enrollments");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "List program enrollments";
            s.Description = "Retrieves a list of all student enrollments associated with a specific academic program.";
            s.Responses[200] = "Successfully retrieved the list of enrollments.";
        });
    }

    public override async Task HandleAsync(GetProgramEnrollmentsRequest req, CancellationToken ct)
    {
        var enrollments = await dbContext.Enrollments
            .Include(x => x.Level)
            .Include(x => x.User)
            .Include(x => x.AcademicSession)
            .Where(x => x.ProgramId == req.Id)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.ProgramId,
                "", // Program name not needed if we are listing for a specific program
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
