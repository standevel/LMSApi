using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Services;

public sealed class FacultyService(
    IFacultyRepository facultyRepository,
    IAuditService auditService) : BaseService(auditService), IFacultyService
{
    public async Task<ErrorOr<FacultyDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var faculty = await facultyRepository.GetByIdAsync(id, ct);
        if (faculty is null) return DomainErrors.Faculty.NotFound;

        return faculty.ToDto();
    }

    public async Task<ErrorOr<List<FacultyDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var faculties = await facultyRepository.GetAllAsync(ct);
        return faculties.Select(f => f.ToDto()).ToList();
    }

    public async Task<ErrorOr<FacultyDto>> CreateAsync(CreateFacultyRequest request, CancellationToken ct = default)
    {
        var faculty = new Faculty
        {
            Name = request.Name,
            Label = request.Label
        };

        await facultyRepository.AddAsync(faculty, ct);
        await facultyRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Faculty", faculty.Id.ToString(), $"Created faculty: {faculty.Name} ({faculty.Label})", ct);

        return faculty.ToDto();
    }

    public async Task<ErrorOr<FacultyDto>> UpdateAsync(Guid id, UpdateFacultyRequest request, CancellationToken ct = default)
    {
        var faculty = await facultyRepository.GetByIdAsync(id, ct);
        if (faculty is null) return DomainErrors.Faculty.NotFound;

        faculty.Name = request.Name;
        faculty.Label = request.Label;

        await facultyRepository.UpdateAsync(faculty, ct);
        await facultyRepository.SaveChangesAsync(ct);

        await LogActionAsync("Update", "Faculty", id.ToString(), $"Updated faculty: {faculty.Name}", ct);

        return faculty.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var faculty = await facultyRepository.GetByIdAsync(id, ct);
        if (faculty is null) return DomainErrors.Faculty.NotFound;

        // Optionally check if there are programs associated with this faculty
        // if (faculty.Programs.Any()) return DomainErrors.Faculty.HasPrograms;

        await facultyRepository.DeleteAsync(faculty, ct);
        await facultyRepository.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Faculty", id.ToString(), $"Deleted faculty: {faculty.Name}", ct);

        return Result.Deleted;
    }
}
