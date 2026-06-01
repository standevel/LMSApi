using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IDegreeAuditService
{
    Task<ErrorOr<DegreeAuditDto>> GetDegreeAuditAsync(Guid auditId, CancellationToken ct = default);
    Task<ErrorOr<DegreeAuditDto>> CreateDegreeAuditAsync(Guid studentId, CreateDegreeAuditRequest request, Guid createdBy, CancellationToken ct = default);
    Task<ErrorOr<List<DegreeAuditDto>>> GetStudentDegreeAuditsAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<DegreeRequirementDto>> GetDegreeRequirementAsync(Guid requirementId, CancellationToken ct = default);
    Task<ErrorOr<List<DegreeRequirementDto>>> GetProgramDegreeRequirementsAsync(Guid programId, CancellationToken ct = default);
    Task<ErrorOr<DegreeRequirementDto>> CreateDegreeRequirementAsync(Guid programId, CreateDegreeRequirementRequest request, CancellationToken ct = default);
    Task<ErrorOr<DegreeRequirementDto>> UpdateDegreeRequirementAsync(Guid requirementId, UpdateDegreeRequirementRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteDegreeRequirementAsync(Guid requirementId, CancellationToken ct = default);
}
