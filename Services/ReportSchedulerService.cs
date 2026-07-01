using System.Text.Json;
using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class ReportSchedulerService : BaseService, IReportSchedulerService
{
    private readonly LmsDbContext _dbContext;

    public ReportSchedulerService(LmsDbContext dbContext, IAuditService auditService) : base(auditService)
    {
        _dbContext = dbContext;
    }

    public async Task<ErrorOr<bool>> IsCacheValidAsync(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId, CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(reportType, studentId, facultyId, departmentId);
        var cacheEntry = await _dbContext.ReportCaches
            .FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);

        if (cacheEntry == null) return false;
        return cacheEntry.IsValid();
    }

    public async Task<ErrorOr<T?>> GetCachedDataAsync<T>(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId, CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(reportType, studentId, facultyId, departmentId);
        var cacheEntry = await _dbContext.ReportCaches
            .FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);

        if (cacheEntry == null || !cacheEntry.IsValid())
            return default;

        try
        {
            return string.IsNullOrWhiteSpace(cacheEntry.CachedData)
                ? default
                : JsonSerializer.Deserialize<T>(cacheEntry.CachedData);
        }
        catch
        {
            return default;
        }
    }

    public async Task<ErrorOr<Deleted>> CacheDataAsync(ReportType reportType, object data, Guid? studentId, Guid? facultyId, Guid? departmentId, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(reportType, studentId, facultyId, departmentId);
        var expiresAt = DateTime.UtcNow + (expiration ?? TimeSpan.FromHours(1));

        var existingCache = await _dbContext.ReportCaches
            .FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);

        var jsonData = JsonSerializer.Serialize(data);

        if (existingCache != null)
        {
            existingCache.ReportType = reportType;
            existingCache.StudentId = studentId;
            existingCache.FacultyId = facultyId;
            existingCache.DepartmentId = departmentId;
            existingCache.CachedData = jsonData;
            existingCache.ExpiresAt = expiresAt;
            existingCache.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            var cacheEntry = new ReportCache
            {
                ReportType = reportType,
                StudentId = studentId,
                FacultyId = facultyId,
                DepartmentId = departmentId,
                CacheKey = cacheKey,
                CachedData = jsonData,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };
            _dbContext.ReportCaches.Add(cacheEntry);
        }

        await _dbContext.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> ClearCacheAsync(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId, CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(reportType, studentId, facultyId, departmentId);
        var cacheEntry = await _dbContext.ReportCaches
            .FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);

        if (cacheEntry != null)
        {
            _dbContext.ReportCaches.Remove(cacheEntry);
            await _dbContext.SaveChangesAsync(ct);
        }

        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> ClearAllCacheAsync(CancellationToken ct = default)
    {
        var allEntries = await _dbContext.ReportCaches.ToListAsync(ct);
        _dbContext.ReportCaches.RemoveRange(allEntries);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    private string GenerateCacheKey(ReportType reportType, Guid? studentId, Guid? facultyId, Guid? departmentId)
    {
        return $"{reportType}_{studentId}_{facultyId}_{departmentId}";
    }
}
