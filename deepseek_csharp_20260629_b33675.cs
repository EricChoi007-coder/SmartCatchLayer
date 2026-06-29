// CustomExceptionFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public class CustomExceptionFilter : IExceptionFilter
{
    private readonly ILogger<CustomExceptionFilter> _logger;
    private readonly IMemoryCache _cache;

    public CustomExceptionFilter(ILogger<CustomExceptionFilter> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public void OnException(ExceptionContext context)
    {
        // 获取请求路径
        var path = context.HttpContext.Request.Path.ToString();
        var method = context.HttpContext.Request.Method;
        
        // 记录异常信息
        _logger.LogError($"请求异常: {method} {path}, 错误: {context.Exception.Message}", context.Exception);

        // 获取请求内容（用于排查问题）
        var requestBody = GetRequestBody(context).Result;
        _logger.LogInformation($"请求内容: {requestBody}");

        // 记录到缓存（用于监控/统计）
        var errorKey = $"Error_{path}_{DateTime.Now:yyyyMMddHH}";
        var errorCount = _cache.Get<int>(errorKey);
        _cache.Set(errorKey, errorCount + 1, TimeSpan.FromHours(1));
        
        // 返回友好的错误响应
        context.Result = new ObjectResult(new 
        { 
            Success = false,
            Message = "服务器处理请求时发生错误",
            Path = path,
            Timestamp = DateTime.UtcNow
        })
        {
            StatusCode = 500
        };
        
        context.ExceptionHandled = true;
    }

    private async Task<string> GetRequestBody(ExceptionContext context)
    {
        try
        {
            context.HttpContext.Request.EnableBuffering();
            var body = await new StreamReader(context.HttpContext.Request.Body).ReadToEndAsync();
            context.HttpContext.Request.Body.Position = 0;
            return body.Length > 1000 ? body.Substring(0, 1000) + "..." : body;
        }
        catch
        {
            return "无法读取请求内容";
        }
    }
}