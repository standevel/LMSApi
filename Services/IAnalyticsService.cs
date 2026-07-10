using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAnalyticsService
{
    Task<ErrorOr<EnrollmentAnalyticsDto>> GetEnrollmentAnalyticsAsync(Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<GraduationRatesDto>> GetGraduationRatesAsync(CancellationToken ct = default);
    Task<ErrorOr<DashboardSummaryDto>> GetDashboardSummaryAsync(Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<FacultyDashboardDto>> GetFacultyDashboardAsync(Guid facultyId, Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<DepartmentDashboardDto>> GetDepartmentDashboardAsync(Guid departmentId, Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<TreasuryAnalyticsDto>> GetTreasuryAnalyticsAsync(Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<FeeLedgerResponseDto>> GetFeeLedgerAsync(FeeLedgerRequestDto request, CancellationToken ct = default);
    Task<ErrorOr<DebtorsReportResponseDto>> GetDebtorsReportAsync(DebtorsReportRequestDto request, CancellationToken ct = default);
    Task<ErrorOr<List<RevenueByCategoryDto>>> GetRevenueByCategoryAsync(Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<List<ScholarshipImpactDto>>> GetScholarshipImpactAsync(Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<FeeReminderResult>> SendFeeRemindersAsync(Guid? academicSessionId, CancellationToken ct = default);
}
