using CacheDegradationSystem.Attributes;
using CacheDegradationSystem.Models;
using CacheDegradationSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace CacheDegradationSystem.Filters;

/// <summary>
/// 核心缓存过滤器：实现缓存命中、写入、降级逻辑
/// </summary>
public class DetailedHybridCacheFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly HybridCacheConfigurationService _configService;
    private readonly EnhancedDegradationCacheService _degradationService;
    private readonly CacheLoggerService _cacheLogger;
    private readonly ILogger<DetailedHybridCacheFilter> _logger;

    public DetailedHybridCacheFilter(
        IMemoryCache cache,
        HybridCacheConfigurationService configService,
        EnhancedDegradationCacheService degradationService,
        CacheLoggerService cacheLogger,
        ILogger<DetailedHybridCacheFilter> logger)
    {
        _cache = cache;
        _configService = configService;
        _degradationService = degradationService;
        _cacheLogger = cacheLogger;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.ToString().ToLowerInvariant();
        var method = context.HttpContext.Request.Method;
        var requestId = context.HttpContext.TraceIdentifier;

        _logger.LogInformation("请求开始 [RequestId: {RequestId}, Path: {Path}, Method: {Method}]",
            requestId, path, method);

        // 只处理 GET 请求
        if (!HttpMethods.IsGet(method))
        {
            _cacheLogger.LogCacheSkip(path, "非GET请求");
            await next();
            return;
        }

        var configResult = _configService.GetCacheConfig(path);
        var config = configResult.Config;

        // 生成缓存键
        var cacheKey = GenerateCacheKey(context, config);
        _logger.LogDebug("缓存键生成 [RequestId: {RequestId}, Key: {CacheKey}]", requestId, cacheKey);

        // 1. 尝试从正常缓存获取
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            _cacheLogger.LogCacheHit(path, cacheKey, configResult.SourceDescription, config.DurationSeconds);

            // 检查是否强制使用降级缓存（连续失败过高）
            if (_degradationService.ShouldUseDegradation(cacheKey, path))
            {
                var (hasDegradation, degradationResponse) = _degradationService.GetDegradationResponse(cacheKey, path);
                if (hasDegradation && degradationResponse != null)
                {
                    context.Result = new ObjectResult(degradationResponse)
                    {
                        StatusCode = 200,
                        DeclaredType = degradationResponse.GetType()
                    };
                    context.HttpContext.Response.Headers.Add("X-Cache-Status", "Degradation-Forced");
                    _logger.LogWarning("强制使用降级缓存 [RequestId: {RequestId}, Path: {Path}]", requestId, path);
                    return;
                }
            }

            context.Result = (IActionResult)cachedResult;
            context.HttpContext.Response.Headers.Add("X-Cache-Status", "Hit");
            _logger.LogInformation("正常缓存命中 [RequestId: {RequestId}, Path: {Path}]", requestId, path);
            return;
        }

        // 2. 检查降级缓存
        var (hasDegradationCache, degradationData) = _degradationService.GetDegradationResponse(cacheKey, path);
        if (hasDegradationCache && degradationData != null)
        {
            context.Result = new ObjectResult(degradationData)
            {
                StatusCode = 200,
                DeclaredType = degradationData.GetType()
            };
            context.HttpContext.Response.Headers.Add("X-Cache-Status", "Degradation");
            _cacheLogger.LogDegradationUse(path, cacheKey, "首次请求降级");
            _logger.LogWarning("降级缓存命中 [RequestId: {RequestId}, Path: {Path}]", requestId, path);
            return;
        }

        // 3. 执行 Action
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("执行Action [RequestId: {RequestId}, Path: {Path}]", requestId, path);

        try
        {
            var executedContext = await next();
            var executionTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("Action执行完成 [RequestId: {RequestId}, Path: {Path}, 耗时: {ExecutionTime}ms]",
                requestId, path, executionTime.TotalMilliseconds);

            if (executedContext.Exception == null &&
                executedContext.Result != null &&
                executedContext.HttpContext.Response.StatusCode == 200)
            {
                // 保存正常缓存
                if (config.DurationSeconds > 0)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(config.DurationSeconds));

                    _cache.Set(cacheKey, executedContext.Result, cacheOptions);
                    _cacheLogger.LogCacheWrite(path, cacheKey, configResult.SourceDescription, config.DurationSeconds);

                    // 保存降级缓存（过期时间更长）
                    var degradationExpiry = Math.Max(config.DurationSeconds * 2, 3600);
                    _degradationService.SaveSuccessResponse(
                        cacheKey,
                        executedContext.Result,
                        TimeSpan.FromSeconds(degradationExpiry),
                        path
                    );

                    _logger.LogInformation("缓存保存成功 [RequestId: {RequestId}, Path: {Path}, Key: {CacheKey}]",
                        requestId, path, cacheKey);
                }
            }
            else if (executedContext.Exception != null)
            {
                _logger.LogError(executedContext.Exception, "Action执行异常 [RequestId: {RequestId}, Path: {Path}]",
                    requestId, path);
                throw executedContext.Exception;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行Action时发生异常 [RequestId: {RequestId}, Path: {Path}]", requestId, path);

            // 尝试使用降级缓存
            var (hasDegradation, degradationData) = _degradationService.GetDegradationResponse(cacheKey, path);
            if (hasDegradation && degradationData != null)
            {
                context.Result = new ObjectResult(degradationData)
                {
                    StatusCode = 200,
                    DeclaredType = degradationData.GetType()
                };
                context.HttpContext.Response.Headers.Add("X-Cache-Status", "Degradation-On-Error");
                _cacheLogger.LogDegradationUse(path, cacheKey, "执行异常降级");
                _logger.LogWarning("执行异常，使用降级缓存 [RequestId: {RequestId}, Path: {Path}]", requestId, path);
                return;
            }

            // 没有降级缓存，重新抛出
            throw;
        }
    }

    private string GenerateCacheKey(ActionExecutingContext context, CacheConfiguration config)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(context.HttpContext.Request.Path);

        if (!string.IsNullOrEmpty(config.CacheKeyPrefix))
            keyBuilder.Append($"|prefix:{config.CacheKeyPrefix}");

        if (config.VaryByRouteParams && context.RouteData.Values.Any())
        {
            var routeValues = context.RouteData.Values
                .Where(kv => !string.Equals(kv.Key, "controller", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(kv.Key, "action", StringComparison.OrdinalIgnoreCase))
                .Select(kv => $"{kv.Key}={kv.Value}");
            if (routeValues.Any())
                keyBuilder.Append($"|route:{string.Join("&", routeValues)}");
        }

        if (config.VaryByQueryParams)
        {
            var queryParams = context.HttpContext.Request.Query
                .Where(kv => !config.ExcludeQueryParams.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}={kv.Value}");
            if (queryParams.Any())
                keyBuilder.Append($"|query:{string.Join("&", queryParams)}");
        }

        if (config.VaryByHeaders && config.Headers.Any())
        {
            var headerValues = config.Headers
                .Select(h => $"{h}={context.HttpContext.Request.Headers[h].ToString()}")
                .Where(h => !string.IsNullOrEmpty(h.Split('=')[1]));
            if (headerValues.Any())
                keyBuilder.Append($"|headers:{string.Join("&", headerValues)}");
        }

        return keyBuilder.ToString();
    }
}