using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAnalyticsService
{
    Task<ErrorOr<EnrollmentAnalyticsDto>> GetEnrollmentAnalyticsAsync(CancellationToken ct = default);
    Task<ErrorOr<GraduationRatesDto>> GetGraduationRatesAsync(CancellationToken ct = default);
    Task<ErrorOr<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken ct = default);
    Task<ErrorOr<FacultyDashboardDto>> GetFacultyDashboardAsync(Guid facultyId, CancellationToken ct = default);
    Task<ErrorOr<DepartmentDashboardDto>> GetDepartmentDashboardAsync(Guid departmentId, CancellationToken ct = default);
}
