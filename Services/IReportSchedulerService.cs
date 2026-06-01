using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;

namespace LMS.Api.Services;

public interface IReportSchedulerService
{
    Task<ErrorOr<bool>> IsCacheValidAsync(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId, CancellationToken ct = default);
    Task<ErrorOr<T?>> GetCachedDataAsync<T>(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> CacheDataAsync(ReportType reportType, object data, Guid? studentId, Guid? facultyId, Guid? departmentId, TimeSpan? expiration = null, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ClearCacheAsync(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ClearAllCacheAsync(CancellationToken ct = default);
}
