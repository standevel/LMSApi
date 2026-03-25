using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAcademicSessionService
{
    Task<ErrorOr<AcademicSessionDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<AcademicSessionDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<AcademicSessionDto>> CreateAsync(CreateAcademicSessionRequest request, CancellationToken ct = default);
    Task<ErrorOr<AcademicSessionDto>> UpdateAsync(Guid id, UpdateAcademicSessionRequest request, CancellationToken ct = default);
    Task<ErrorOr<AcademicSessionDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default);
}
