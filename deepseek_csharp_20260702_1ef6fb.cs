using CacheDegradationSystem.Filters;
using CacheDegradationSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// 注册内存缓存
builder.Services.AddMemoryCache();

// 注册核心服务（单例）
builder.Services.AddSingleton<HybridCacheConfigurationService>();
builder.Services.AddSingleton<EnhancedDegradationCacheService>();
builder.Services.AddSingleton<CacheLoggerService>();

// 注册过滤器（作用域）
builder.Services.AddScoped<DetailedHybridCacheFilter>();
builder.Services.AddScoped<DetailedExceptionFilter>();

// 添加控制器并应用全局过滤器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DetailedHybridCacheFilter>();
    options.Filters.Add<DetailedExceptionFilter>();
});

// 启用详细错误页（开发环境）
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.MapControllers();

// ---- 调试端点 ----
// 查看缓存配置
app.MapGet("/api/cache/configs", async (HybridCacheConfigurationService configService) =>
{
    var configs = configService.GetAllCacheConfigs();
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

// 查看降级缓存状态
app.MapGet("/api/cache/degradation/status", async (EnhancedDegradationCacheService degradationService) =>
{
    var stats = degradationService.GetStatistics();
    return Results.Json(new
    {
        Statistics = stats,
        Timestamp = DateTime.UtcNow
    });
});

// 清除指定降级缓存
app.MapDelete("/api/cache/degradation/{**path}", async (string path, EnhancedDegradationCacheService degradationService) =>
{
    var cacheKey = "/" + path;
    degradationService.ClearDegradation(cacheKey, path);
    return Results.Ok(new
    {
        Message = $"降级缓存已清除: {cacheKey}",
        Timestamp = DateTime.UtcNow
    });
});

// 查看最近日志
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

app.Run();