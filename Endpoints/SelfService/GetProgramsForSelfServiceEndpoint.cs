using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.SelfService;

/// <summary>
/// GET /self-service/programs
/// Returns all active academic programs — used by students to select a target program for switching.
/// </summary>
public sealed class GetProgramsForSelfServiceEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<List<AcademicProgramSummaryDto>>
{
    public override void Configure()
    {
        Get("self-service/programs");
        Roles("Student", "SuperAdmin");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("GetProgramsForSelfService")
            .WithSummary("Returns all active academic programs available for a program switch request."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var programs = await db.Programs
            .Include(p => p.Department)
                .ThenInclude(d => d.Faculty)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new AcademicProgramSummaryDto(
                p.Id,
                p.Name,
                p.Code,
                p.Department != null ? p.Department.Name : null,
                p.Department != null && p.Department.Faculty != null ? p.Department.Faculty.Name : null))
            .ToListAsync(ct);

        await SendSuccessAsync(programs, ct);
    }
}
