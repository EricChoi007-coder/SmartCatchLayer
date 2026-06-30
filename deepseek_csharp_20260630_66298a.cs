// DetailedExceptionFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

public class DetailedExceptionFilter : IExceptionFilter
{
    private readonly ILogger<DetailedExceptionFilter> _logger;
    private readonly EnhancedDegradationCacheService _degradationService;
    private readonly HybridCacheConfigurationService _configService;
    private readonly CacheLoggerService _cacheLogger;

    public DetailedExceptionFilter(
        ILogger<DetailedExceptionFilter> logger,
        EnhancedDegradationCacheService degradationService,
        HybridCacheConfigurationService configService,
        CacheLoggerService cacheLogger)
    {
        _logger = logger;
        _degradationService = degradationService;
        _configService = configService;
        _cacheLogger = cacheLogger;
    }

    public void OnException(ExceptionContext context)
    {
        var path = context.HttpContext.Request.Path.ToString().ToLowerInvariant();
        var method = context.HttpContext.Request.Method;
        var exception = context.Exception;
        var queryString = context.HttpContext.Request.QueryString.ToString();

        // 记录完整的异常信息
        var requestId = context.HttpContext.TraceIdentifier;
        var userAgent = context.HttpContext.Request.Headers["User-Agent"].ToString();
        var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        _logger.LogError(exception, 
            "请求异常详情 [RequestId: {RequestId}, Path: {Path}, Method: {Method}, ClientIP: {ClientIP}, UserAgent: {UserAgent}]", 
            requestId, path, method, clientIp, userAgent);

        // 记录请求体（如果有）
        if (context.HttpContext.Request.ContentLength > 0)
        {
            try
            {
                context.HttpContext.Request.EnableBuffering();
                using var reader = new StreamReader(context.HttpContext.Request.Body, leaveOpen: true);
                var body = reader.ReadToEndAsync().Result;
                context.HttpContext.Request.Body.Position = 0;
                
                if (!string.IsNullOrEmpty(body))
                {
                    _logger.LogInformation("请求体: {Body}", body.Length > 1000 ? body.Substring(0, 1000) + "..." : body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("读取请求体失败: {Error}", ex.Message);
            }
        }

        // 生成缓存键
        var cacheKey = GenerateCacheKey(context);
        
        // 检查降级缓存
        var (hasDegradation, degradationResponse) = _degradationService.GetDegradationResponse(cacheKey, path);

        if (hasDegradation && degradationResponse != null)
        {
            // 记录降级使用
            _cacheLogger.LogDegradationUse(path, cacheKey, "异常降级");
            _cacheLogger.LogException(path, method, exception, cacheKey, true);
            
            // 返回降级数据
            context.Result = new ObjectResult(degradationResponse)
            {
                StatusCode = 200,
                DeclaredType = degradationResponse.GetType()
            };
            
            // 添加响应头
            context.HttpContext.Response.Headers.Add("X-Cache-Status", "Degradation");
            context.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
            context.HttpContext.Response.Headers.Add("X-Error-Handled", "true");
            
            // 记录降级事件到应用日志
            _logger.LogWarning("返回降级缓存 [RequestId: {RequestId}, Path: {Path}]", requestId, path);
            
            context.ExceptionHandled = true;
            return;
        }

        // 没有降级缓存，记录异常
        _cacheLogger.LogException(path, method, exception, cacheKey, false);

        // 构建错误响应
        var errorResponse = new
        {
            Success = false,
            Message = "服务暂时不可用，请稍后重试",
            Path = path,
            Timestamp = DateTime.UtcNow,
            RequestId = requestId,
            ErrorType = exception.GetType().Name
        };

        // 记录错误响应
        _logger.LogInformation("返回错误响应 [RequestId: {RequestId}, Path: {Path}, ErrorType: {ErrorType}]", 
            requestId, path, exception.GetType().Name);

        context.Result = new ObjectResult(errorResponse)
        {
            StatusCode = 503
        };

        context.ExceptionHandled = true;
    }

    private string GenerateCacheKey(ExceptionContext context)
    {
        var path = context.HttpContext.Request.Path.ToString();
        var queryString = context.HttpContext.Request.QueryString.ToString();
        return $"{path}{queryString}";
    }
}