using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IDepartmentService
{
    Task<ErrorOr<DepartmentDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<DepartmentDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<List<DepartmentDto>>> GetByFacultyIdAsync(Guid facultyId, CancellationToken ct = default);
    Task<ErrorOr<DepartmentDto>> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default);
    Task<ErrorOr<DepartmentDto>> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
}
