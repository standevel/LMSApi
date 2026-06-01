using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListCourseEquivalenciesEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<CourseEquivalencyDto>>
{
    public override void Configure()
    {
        Get("admin/admission-config/course-equivalencies");
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var equivalencies = await dbContext.CourseEquivalencies
            .Include(e => e.TargetCourse)
            .OrderBy(e => e.SourceInstitution)
            .ThenBy(e => e.SourceCourseCode)
            .ToListAsync(ct);

        var response = equivalencies.Select(e => new CourseEquivalencyDto(
            e.Id, e.SourceInstitution, e.SourceCourseCode, e.SourceCourseName,
            e.SourceCredits, e.TargetCourseId, e.TargetCourse?.Code, e.TargetCourse?.Title,
            e.TargetCredits, e.Description, e.MappingNotes, e.IsActive, e.CreatedAt, e.UpdatedAt));

        await SendSuccessAsync(response, ct);
    }
}

public record CourseEquivalencyDto(
    Guid Id,
    string SourceInstitution,
    string SourceCourseCode,
    string SourceCourseName,
    decimal SourceCredits,
    Guid? TargetCourseId,
    string? TargetCourseCode,
    string? TargetCourseTitle,
    decimal TargetCredits,
    string? Description,
    string? MappingNotes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateCourseEquivalencyEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateCourseEquivalencyRequest, CourseEquivalencyDto>
{
    public override void Configure()
    {
        Post("admin/admission-config/course-equivalencies");
    }

    public override async Task HandleAsync(CreateCourseEquivalencyRequest req, CancellationToken ct)
    {
        var equivalency = new CourseEquivalency
        {
            SourceInstitution = req.SourceInstitution,
            SourceCourseCode = req.SourceCourseCode,
            SourceCourseName = req.SourceCourseName,
            SourceCredits = req.SourceCredits,
            TargetCourseId = req.TargetCourseId,
            TargetCredits = req.TargetCredits,
            Description = req.Description,
            MappingNotes = req.MappingNotes,
            IsActive = req.IsActive
        };
        dbContext.CourseEquivalencies.Add(equivalency);
        await dbContext.SaveChangesAsync(ct);

        var dto = new CourseEquivalencyDto(
            equivalency.Id, equivalency.SourceInstitution, equivalency.SourceCourseCode,
            equivalency.SourceCourseName, equivalency.SourceCredits, equivalency.TargetCourseId,
            equivalency.TargetCourse?.Code, equivalency.TargetCourse?.Title,
            equivalency.TargetCredits, equivalency.Description, equivalency.MappingNotes,
            equivalency.IsActive, equivalency.CreatedAt, equivalency.UpdatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class UpdateCourseEquivalencyEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateCourseEquivalencyRequest, CourseEquivalencyDto>
{
    public override void Configure()
    {
        Patch("admin/admission-config/course-equivalencies/{Id}");
    }

    public override async Task HandleAsync(UpdateCourseEquivalencyRequest req, CancellationToken ct)
    {
        var equivalency = await dbContext.CourseEquivalencies.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Course equivalency not found");

        equivalency.SourceInstitution = req.SourceInstitution ?? equivalency.SourceInstitution;
        equivalency.SourceCourseCode = req.SourceCourseCode ?? equivalency.SourceCourseCode;
        equivalency.SourceCourseName = req.SourceCourseName ?? equivalency.SourceCourseName;
        equivalency.SourceCredits = req.SourceCredits ?? equivalency.SourceCredits;
        equivalency.TargetCourseId = req.TargetCourseId ?? equivalency.TargetCourseId;
        equivalency.TargetCredits = req.TargetCredits ?? equivalency.TargetCredits;
        equivalency.Description = req.Description ?? equivalency.Description;
        equivalency.MappingNotes = req.MappingNotes ?? equivalency.MappingNotes;
        equivalency.IsActive = req.IsActive ?? equivalency.IsActive;
        equivalency.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var dto = new CourseEquivalencyDto(
            equivalency.Id, equivalency.SourceInstitution, equivalency.SourceCourseCode,
            equivalency.SourceCourseName, equivalency.SourceCredits, equivalency.TargetCourseId,
            equivalency.TargetCourse?.Code, equivalency.TargetCourse?.Title,
            equivalency.TargetCredits, equivalency.Description, equivalency.MappingNotes,
            equivalency.IsActive, equivalency.CreatedAt, equivalency.UpdatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class DeleteCourseEquivalencyEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteCourseEquivalencyRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/admission-config/course-equivalencies/{Id}");
    }

    public override async Task HandleAsync(DeleteCourseEquivalencyRequest req, CancellationToken ct)
    {
        var equivalency = await dbContext.CourseEquivalencies.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Course equivalency not found");

        equivalency.IsActive = false;
        equivalency.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}

public record CreateCourseEquivalencyRequest(
    string SourceInstitution,
    string SourceCourseCode,
    string SourceCourseName,
    decimal SourceCredits,
    Guid? TargetCourseId,
    decimal TargetCredits,
    string? Description,
    string? MappingNotes,
    bool IsActive = true);

public record UpdateCourseEquivalencyRequest(
    Guid Id,
    string? SourceInstitution,
    string? SourceCourseCode,
    string? SourceCourseName,
    decimal? SourceCredits,
    Guid? TargetCourseId,
    decimal? TargetCredits,
    string? Description,
    string? MappingNotes,
    bool? IsActive);

public record DeleteCourseEquivalencyRequest(Guid Id);
