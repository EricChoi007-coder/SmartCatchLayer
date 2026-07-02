using CacheDegradationSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace CacheDegradationSystem.Filters;

/// <summary>
/// 异常过滤器：捕获 Controller 异常，返回降级缓存或错误响应
/// </summary>
public class DetailedExceptionFilter : IExceptionFilter
{
    private readonly ILogger<DetailedExceptionFilter> _logger;
    private readonly EnhancedDegradationCacheService _degradationService;
    private readonly CacheLoggerService _cacheLogger;

    public DetailedExceptionFilter(
        ILogger<DetailedExceptionFilter> logger,
        EnhancedDegradationCacheService degradationService,
        CacheLoggerService cacheLogger)
    {
        _logger = logger;
        _degradationService = degradationService;
        _cacheLogger = cacheLogger;
    }

    public void OnException(ExceptionContext context)
    {
        var path = context.HttpContext.Request.Path.ToString().ToLowerInvariant();
        var method = context.HttpContext.Request.Method;
        var exception = context.Exception;
        var requestId = context.HttpContext.TraceIdentifier;

        // 记录详细异常信息
        _logger.LogError(exception,
            "请求异常详情 [RequestId: {RequestId}, Path: {Path}, Method: {Method}, ClientIP: {ClientIP}]",
            requestId, path, method,
            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");

        // 生成缓存键（简单路径+查询串）
        var cacheKey = $"{path}{context.HttpContext.Request.QueryString}";

        // 尝试获取降级缓存
        var (hasDegradation, degradationResponse) = _degradationService.GetDegradationResponse(cacheKey, path);

        if (hasDegradation && degradationResponse != null)
        {
            _cacheLogger.LogDegradationUse(path, cacheKey, "异常降级");
            _cacheLogger.LogException(path, method, exception, cacheKey, true);

            context.Result = new ObjectResult(degradationResponse)
            {
                StatusCode = 200,
                DeclaredType = degradationResponse.GetType()
            };

            context.HttpContext.Response.Headers.Add("X-Cache-Status", "Degradation");
            context.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
            context.HttpContext.Response.Headers.Add("X-Error-Handled", "true");

            _logger.LogWarning("返回降级缓存 [RequestId: {RequestId}, Path: {Path}]", requestId, path);
            context.ExceptionHandled = true;
            return;
        }

        // 没有降级缓存，记录异常并返回错误响应
        _cacheLogger.LogException(path, method, exception, cacheKey, false);

        var errorResponse = new
        {
            Success = false,
            Message = "服务暂时不可用，请稍后重试",
            Path = path,
            Timestamp = DateTime.UtcNow,
            RequestId = requestId,
            ErrorType = exception.GetType().Name
        };

        _logger.LogInformation("返回错误响应 [RequestId: {RequestId}, Path: {Path}, ErrorType: {ErrorType}]",
            requestId, path, exception.GetType().Name);

        context.Result = new ObjectResult(errorResponse)
        {
            StatusCode = 503
        };

        context.ExceptionHandled = true;
    }
}