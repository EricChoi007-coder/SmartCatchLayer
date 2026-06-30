// EnhancedDegradationCacheService.cs
public class EnhancedDegradationCacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheLoggerService _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastSuccessTime = new();
    private readonly ConcurrentDictionary<string, int> _failureCount = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _failureHistory = new();
    
    private const int MaxConsecutiveFailures = 3;
    private const int DegradationCacheExpirySeconds = 3600;

    public EnhancedDegradationCacheService(
        IMemoryCache cache,
        CacheLoggerService logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void SaveSuccessResponse(string cacheKey, object response, TimeSpan? customExpiry = null, string path = "")
    {
        try
        {
            var expiry = customExpiry ?? TimeSpan.FromSeconds(DegradationCacheExpirySeconds);
            var degradationKey = GetDegradationKey(cacheKey);
            
            _cache.Set(degradationKey, response, expiry);
            _lastSuccessTime[cacheKey] = DateTime.UtcNow;
            _failureCount[cacheKey] = 0;
            
            // 清除失败历史
            _failureHistory.TryRemove(cacheKey, out _);
            
            _logger.LogCacheWrite(path, cacheKey, "降级缓存", (int)expiry.TotalSeconds, true);
        }
        catch (Exception ex)
        {
            _logger.LogException(path, "SAVE", ex, cacheKey);
            throw;
        }
    }

    public (bool Exists, object Response) GetDegradationResponse(string cacheKey, string path = "")
    {
        try
        {
            var degradationKey = GetDegradationKey(cacheKey);
            
            if (_cache.TryGetValue(degradationKey, out var response))
            {
                // 更新失败计数
                var failures = _failureCount.AddOrUpdate(cacheKey, 1, (key, count) => count + 1);
                
                // 记录失败历史
                _failureHistory.AddOrUpdate(cacheKey, 
                    new List<DateTime> { DateTime.UtcNow },
                    (key, list) => { list.Add(DateTime.UtcNow); return list; });
                
                _logger.LogDegradationUse(path, cacheKey, "异常降级", failures);
                return (true, response);
            }
            
            _logger.LogCacheSkip(path, "无降级缓存可用");
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogException(path, "GET_DEGRADATION", ex, cacheKey);
            return (false, null);
        }
    }

    public bool ShouldUseDegradation(string cacheKey, string path = "")
    {
        var failures = _failureCount.GetValueOrDefault(cacheKey, 0);
        
        if (failures >= MaxConsecutiveFailures)
        {
            _logger.LogDegradationUse(path, cacheKey, $"连续失败超过阈值({failures}/{MaxConsecutiveFailures})", failures);
            return true;
        }
        
        // 检查是否有降级缓存可用
        var hasCache = _cache.TryGetValue(GetDegradationKey(cacheKey), out _);
        if (!hasCache)
        {
            _logger.LogCacheSkip(path, "降级缓存不存在");
            return false;
        }
        
        return false;
    }

    public void ClearDegradation(string cacheKey, string path = "")
    {
        try
        {
            var degradationKey = GetDegradationKey(cacheKey);
            _cache.Remove(degradationKey);
            _failureCount.TryRemove(cacheKey, out _);
            _lastSuccessTime.TryRemove(cacheKey, out _);
            _failureHistory.TryRemove(cacheKey, out _);
            
            _logger.LogCacheClear(path, cacheKey, "手动清除降级缓存");
        }
        catch (Exception ex)
        {
            _logger.LogException(path, "CLEAR", ex, cacheKey);
        }
    }

    private string GetDegradationKey(string cacheKey)
    {
        return $"degradation_{cacheKey}";
    }

    public object GetStatistics(string path = "")
    {
        try
        {
            return new
            {
                TotalEndpoints = _lastSuccessTime.Keys.Count,
                Endpoints = _lastSuccessTime.Select(kvp => new
                {
                    Endpoint = kvp.Key,
                    LastSuccessTime = kvp.Value,
                    FailureCount = _failureCount.GetValueOrDefault(kvp.Key, 0),
                    FailureHistory = _failureHistory.GetValueOrDefault(kvp.Key, new List<DateTime>())
                        .OrderByDescending(d => d)
                        .Take(10)
                        .ToList(),
                    HasDegradationCache = _cache.TryGetValue(GetDegradationKey(kvp.Key), out _)
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogException(path, "GET_STATS", ex);
            return new { Error = "获取统计信息失败" };
        }
    }
}