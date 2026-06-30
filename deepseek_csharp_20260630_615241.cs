// CacheLoggerService.cs
using System.Text.Json;

public class CacheLoggerService
{
    private readonly ILogger<CacheLoggerService> _logger;
    private readonly ConcurrentQueue<CacheLogEntry> _logQueue = new();
    private readonly Timer _flushTimer;
    private readonly object _lock = new();

    public CacheLoggerService(ILogger<CacheLoggerService> logger)
    {
        _logger = logger;
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    // 记录缓存命中
    public void LogCacheHit(string path, string cacheKey, string source, int durationSeconds)
    {
        var entry = new CacheLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Information,
            Category = "Cache",
            Event = "CacheHit",
            Path = path,
            CacheKey = cacheKey,
            Source = source,
            DurationSeconds = durationSeconds,
            Message = $"缓存命中: {path} (来源: {source}, 时长: {durationSeconds}s)"
        };
        EnqueueLog(entry);
        
        // 立即记录到日志
        _logger.LogInformation("缓存命中 [路径: {Path}, 来源: {Source}, 时长: {Duration}s, Key: {CacheKey}]", 
            path, source, durationSeconds, cacheKey);
    }

    // 记录缓存写入
    public void LogCacheWrite(string path, string cacheKey, string source, int durationSeconds, bool isDegradation = false)
    {
        var entry = new CacheLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Information,
            Category = isDegradation ? "DegradationCache" : "Cache",
            Event = isDegradation ? "DegradationWrite" : "CacheWrite",
            Path = path,
            CacheKey = cacheKey,
            Source = source,
            DurationSeconds = durationSeconds,
            Message = $"缓存写入: {path} (来源: {source}, 时长: {durationSeconds}s, 类型: {(isDegradation ? "降级缓存" : "正常缓存")})"
        };
        EnqueueLog(entry);
        
        _logger.LogInformation("缓存写入 [路径: {Path}, 来源: {Source}, 时长: {Duration}s, 类型: {Type}]", 
            path, source, durationSeconds, isDegradation ? "降级缓存" : "正常缓存");
    }

    // 记录异常
    public void LogException(string path, string method, Exception exception, string cacheKey = null, bool hasDegradation = false)
    {
        var entry = new CacheLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Error,
            Category = "Exception",
            Event = hasDegradation ? "ExceptionWithDegradation" : "Exception",
            Path = path,
            Method = method,
            CacheKey = cacheKey,
            Exception = exception,
            HasDegradation = hasDegradation,
            Message = $"请求异常: {method} {path} - {exception.Message} (有降级缓存: {hasDegradation})"
        };
        EnqueueLog(entry);
        
        _logger.LogError(exception, "请求异常 [路径: {Path}, 方法: {Method}, 有降级缓存: {HasDegradation}]", 
            path, method, hasDegradation);
    }

    // 记录降级使用
    public void LogDegradationUse(string path, string cacheKey, string reason, int failureCount = 0)
    {
        var entry = new CacheLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Warning,
            Category = "DegradationCache",
            Event = "DegradationUsed",
            Path = path,
            CacheKey = cacheKey,
            FailureCount = failureCount,
            Message = $"使用降级缓存: {path} (原因: {reason}, 连续失败次数: {failureCount})"
        };
        EnqueueLog(entry);
        
        _logger.LogWarning("使用降级缓存 [路径: {Path}, 原因: {Reason}, 连续失败次数: {FailCount}]", 
            path, reason, failureCount);
    }

    // 记录缓存清除
    public void LogCacheClear(string path, string cacheKey, string reason = "手动清除")
    {
        var entry = new CacheLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Information,
            Category = "Cache",
            Event = "CacheClear",
            Path = path,
            CacheKey = cacheKey,
            Message = $"缓存清除: {path} (原因: {reason})"
        };
        EnqueueLog(entry);
        
        _logger.LogInformation("缓存清除 [路径: {Path}, Key: {CacheKey}, 原因: {Reason}]", path, cacheKey, reason);
    }

    // 记录缓存跳过
    public void LogCacheSkip(string path, string reason)
    {
        var entry = new CacheLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Debug,
            Category = "Cache",
            Event = "CacheSkip",
            Path = path,
            Message = $"跳过缓存: {path} (原因: {reason})"
        };
        EnqueueLog(entry);
        
        _logger.LogDebug("跳过缓存 [路径: {Path}, 原因: {Reason}]", path, reason);
    }

    private void EnqueueLog(CacheLogEntry entry)
    {
        _logQueue.Enqueue(entry);
        
        // 如果队列太大，触发刷新
        if (_logQueue.Count > 100)
        {
            FlushLogs(null);
        }
    }

    private void FlushLogs(object state)
    {
        lock (_lock)
        {
            var count = 0;
            while (_logQueue.TryDequeue(out var entry) && count < 50)
            {
                // 可以批量写入数据库或文件
                // 这里只记录到结构化日志
                count++;
            }
            
            if (count > 0)
            {
                _logger.LogDebug("刷新了 {Count} 条缓存日志", count);
            }
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushLogs(null);
    }

    public List<CacheLogEntry> GetRecentLogs(int count = 100)
    {
        return _logQueue.Take(count).ToList();
    }
}

// 日志条目类
public class CacheLogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; }
    public string Event { get; set; }
    public string Path { get; set; }
    public string Method { get; set; }
    public string CacheKey { get; set; }
    public string Source { get; set; }
    public int DurationSeconds { get; set; }
    public int FailureCount { get; set; }
    public bool HasDegradation { get; set; }
    public Exception Exception { get; set; }
    public string Message { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}