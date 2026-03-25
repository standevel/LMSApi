using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IFacultyService
{
    Task<ErrorOr<FacultyDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<FacultyDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<FacultyDto>> CreateAsync(CreateFacultyRequest request, CancellationToken ct = default);
    Task<ErrorOr<FacultyDto>> UpdateAsync(Guid id, UpdateFacultyRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
}
