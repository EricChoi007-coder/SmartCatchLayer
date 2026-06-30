// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. 注册日志服务
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
    config.AddConfiguration(builder.Configuration.GetSection("Logging"));
});

// 2. 注册缓存
builder.Services.AddMemoryCache();

// 3. 注册服务
builder.Services.AddSingleton<HybridCacheConfigurationService>();
builder.Services.AddSingleton<EnhancedDegradationCacheService>();
builder.Services.AddSingleton<CacheLoggerService>();

// 4. 注册过滤器
builder.Services.AddScoped<DetailedHybridCacheFilter>();
builder.Services.AddScoped<DetailedExceptionFilter>();

// 5. 添加控制器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DetailedHybridCacheFilter>();
    options.Filters.Add<DetailedExceptionFilter>();
});

var app = builder.Build();

// 6. 日志端点 - 查看最近日志
app.MapGet("/api/logs/recent", async (CacheLoggerService logger) =>
{
    var logs = logger.GetRecentLogs(100);
    return Results.Json(new
    {
        Total = logs.Count,
        Logs = logs.OrderByDescending(l => l.Timestamp).Select(l => new
        {
            l.Timestamp,
            Level = l.Level.ToString(),
            l.Category,
            l.Event,
            l.Path,
            l.Message,
            l.FailureCount,
            l.HasDegradation,
            Exception = l.Exception?.Message
        })
    });
});

// 7. 降级缓存统计
app.MapGet("/api/cache/degradation/status", async (EnhancedDegradationCacheService degradationService, CacheLoggerService logger) =>
{
    var stats = degradationService.GetStatistics();
    logger.LogCacheHit("/api/cache/degradation/status", "stats", "API", 0);
    return Results.Json(new
    {
        Statistics = stats,
        Timestamp = DateTime.UtcNow
    });
});

// 8. 手动清除降级缓存
app.MapDelete("/api/cache/degradation/{**path}", async (string path, EnhancedDegradationCacheService degradationService, CacheLoggerService logger) =>
{
    var cacheKey = "/" + path;
    degradationService.ClearDegradation(cacheKey, path);
    logger.LogCacheClear(path, cacheKey, "API手动清除");
    return Results.Ok(new 
    { 
        Message = $"降级缓存已清除: {cacheKey}",
        Timestamp = DateTime.UtcNow
    });
});

// 9. 查看所有缓存配置
app.MapGet("/api/cache/configs", async (HybridCacheConfigurationService configService, CacheLoggerService logger) =>
{
    var configs = configService.GetAllCacheConfigs();
    logger.LogCacheHit("/api/cache/configs", "configs", "API", 0);
    return Results.Json(new
    {
        Total = configs.Count,
        Timestamp = DateTime.UtcNow,
        Configs = configs.Select(kvp => new
        {
            Path = kvp.Key,
            Source = kvp.Value.SourceDescription,
            Duration = kvp.Value.Config.DurationSeconds,
            IsDisabled = kvp.Value.Config.IsDisabled,
            Priority = kvp.Value.Config.Priority,
            VaryByQueryParams = kvp.Value.Config.VaryByQueryParams,
            VaryByHeaders = kvp.Value.Config.VaryByHeaders
        })
    });
});

app.MapControllers();
app.Run();