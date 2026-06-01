using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IGpaCalculationService
{
    Task<ErrorOr<GpaDto>> GetStudentGpaAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<List<SessionGpaDto>>> GetStudentSessionGpasAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<GpaDto>> CalculateGpaForStudentAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default);
}
