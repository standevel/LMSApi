using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class RateLimitingService : IRateLimitingService
{
    private readonly LmsDbContext _dbContext;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private const int DefaultLimit = 100;

    public RateLimitingService(LmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ErrorOr<ApiRateLimitDto>> GetRateLimitStatusAsync(string clientId, string endpoint, string method, CancellationToken ct = default)
    {
        var entry = await GetOrCreateEntryAsync(clientId, endpoint, method, ct);
        return Map(entry);
    }

    public async Task<ErrorOr<Deleted>> IncrementRequestCountAsync(string clientId, string endpoint, string method, CancellationToken ct = default)
    {
        var entry = await GetOrCreateEntryAsync(clientId, endpoint, method, ct);

        if (entry.RequestCount >= entry.Limit)
        {
            return Error.Validation("rate_limit_exceeded", "Rate limit exceeded.");
        }

        entry.RequestCount++;
        entry.Remaining = Math.Max(0, entry.Limit - entry.RequestCount);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Deleted;
    }

    public async Task<bool> IsRateLimitExceededAsync(string clientId, string endpoint, string method, CancellationToken ct = default)
    {
        var entry = await GetOrCreateEntryAsync(clientId, endpoint, method, ct);
        return entry.RequestCount >= entry.Limit;
    }

    private async Task<ApiRateLimit> GetOrCreateEntryAsync(string clientId, string endpoint, string method, CancellationToken ct)
    {
        var normalizedClientId = clientId?.Trim() ?? string.Empty;
        var normalizedEndpoint = endpoint?.Trim() ?? string.Empty;
        var normalizedMethod = method?.Trim().ToUpperInvariant() ?? string.Empty;
        var now = DateTime.UtcNow;

        var entry = await _dbContext.ApiRateLimits
            .FirstOrDefaultAsync(x => x.ClientId == normalizedClientId
                && x.Endpoint == normalizedEndpoint
                && x.Method == normalizedMethod, ct);

        if (entry == null)
        {
            entry = new ApiRateLimit
            {
                ClientId = normalizedClientId,
                Endpoint = normalizedEndpoint,
                Method = normalizedMethod,
                WindowStartUtc = now,
                ResetTimeUtc = now.Add(RateLimitWindow),
                RequestCount = 0,
                Limit = DefaultLimit,
                Remaining = DefaultLimit
            };

            _dbContext.ApiRateLimits.Add(entry);
            await _dbContext.SaveChangesAsync(ct);
            return entry;
        }

        if (!entry.ResetTimeUtc.HasValue || entry.ResetTimeUtc.Value <= now)
        {
            entry.WindowStartUtc = now;
            entry.ResetTimeUtc = now.Add(RateLimitWindow);
            entry.RequestCount = 0;
            entry.Remaining = entry.Limit;
            await _dbContext.SaveChangesAsync(ct);
        }

        return entry;
    }

    private static ApiRateLimitDto Map(ApiRateLimit entry)
        => new(
            entry.ClientId,
            entry.Endpoint,
            entry.Method,
            entry.RequestCount,
            entry.Limit,
            entry.Remaining,
            entry.WindowStartUtc,
            entry.ResetTimeUtc);
}
