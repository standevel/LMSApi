using FastEndpoints;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Timetable;

public class GetLecturersEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<List<LecturerResponse>>
{
    public override void Configure()
    {
        Get("/api/timetable/lecturers");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var lecturers = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name.Equals("Lecturer")))
            .Select(u => new LecturerResponse
            {
                id = u.Id.ToString(),
                displayName = u.DisplayName ?? u.Email ?? "Unknown",
                name = u.DisplayName ?? u.Email ?? "Unknown",
                email = u.Email ?? string.Empty
            })
            .ToListAsync(ct);

        await SendSuccessAsync(lecturers, ct);
    }
}

public class LecturerResponse
{
    public string id { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
}
