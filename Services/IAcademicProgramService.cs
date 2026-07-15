using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAcademicProgramService
{
    Task<ErrorOr<AcademicProgramDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<AcademicProgramDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<AcademicProgramDto>> CreateAsync(CreateAcademicProgramRequest request, CancellationToken ct = default);
    Task<ErrorOr<AcademicProgramDto>> UpdateAsync(Guid id, UpdateAcademicProgramRequest request, CancellationToken ct = default);
    Task<ErrorOr<AcademicProgramDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<AcademicProgramDto>> AddLevelAsync(Guid programId, AddAcademicLevelRequest request, CancellationToken ct = default);
    Task<ErrorOr<AcademicProgramDto>> UpdateLevelAsync(Guid programId, Guid levelId, UpdateAcademicLevelRequest request, CancellationToken ct = default);
    Task<ErrorOr<AcademicProgramDto>> DeleteLevelAsync(Guid programId, Guid levelId, CancellationToken ct = default);
}
