using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Services;

namespace LMS.Api.Services;

public class CacheService : BaseService, ICacheService
{
    private readonly IDictionary<string, CacheItem> _cache;
    private readonly object _lock = new object();

    public CacheService(IAuditService auditService) : base(auditService)
    {
        _cache = new Dictionary<string, CacheItem>();
    }

    public Task<ErrorOr<T>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<ErrorOr<T>>(Error.Validation("InvalidInput", "Cache key is required."));
        }

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var cacheItem))
            {
                return Task.FromResult<ErrorOr<T>>(Error.NotFound("CacheMiss", $"Key '{key}' not found in cache."));
            }

            if (cacheItem.ExpiresAt.HasValue && cacheItem.ExpiresAt.Value <= DateTime.UtcNow)
            {
                // Remove expired item
                _cache.Remove(key);
                return Task.FromResult<ErrorOr<T>>(Error.NotFound("CacheExpired", $"Key '{key}' has expired."));
            }

            return Task.FromResult<ErrorOr<T>>((T)cacheItem.Value);
        }
    }

    public async Task<ErrorOr<Deleted>> SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Error.Validation("InvalidInput", "Cache key is required.");
        }

        if (value == null)
        {
            return Error.Validation("InvalidInput", "Cache value cannot be null.");
        }

        lock (_lock)
        {
            var cacheItem = new CacheItem
            {
                Value = value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null
            };

            _cache[key] = cacheItem;
        }

        await LogActionAsync("SetCacheItem", "CacheService", key, $"Cache item set with key: {key}", ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> RemoveAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Error.Validation("InvalidInput", "Cache key is required.");
        }

        bool removed;
        lock (_lock)
        {
            removed = _cache.Remove(key);
        }

        if (removed)
        {
            await LogActionAsync("RemoveCacheItem", "CacheService", key, $"Cache item removed with key: {key}", ct);
            return Result.Deleted;
        }
        else
        {
            return Error.NotFound("CacheMiss", $"Key '{key}' not found in cache.");
        }
    }

    public async Task<ErrorOr<bool>> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Error.Validation("InvalidInput", "Cache key is required.");
        }

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var cacheItem))
            {
                return false;
            }

            if (cacheItem.ExpiresAt.HasValue && cacheItem.ExpiresAt.Value <= DateTime.UtcNow)
            {
                // Remove expired item
                _cache.Remove(key);
                return false;
            }

            return true;
        }
    }

    private class CacheItem
    {
        public object Value { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}