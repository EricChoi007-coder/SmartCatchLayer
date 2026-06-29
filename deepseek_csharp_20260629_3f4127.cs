// DynamicCacheFilter.cs
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public class DynamicCacheFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly ApiCacheConfig _config;
    private readonly ILogger<DynamicCacheFilter> _logger;

    public DynamicCacheFilter(
        IMemoryCache cache,
        IOptions<ApiCacheConfig> options,
        ILogger<DynamicCacheFilter> logger)
    {
        _cache = cache;
        _config = options.Value;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 获取请求路径
        var path = context.HttpContext.Request.Path.ToString();
        
        // 获取该路径的缓存时长（秒），如果未配置则使用默认值
        var cacheDuration = GetCacheDuration(path);
        
        // 构造缓存键（包含路径和查询参数）
        var cacheKey = GenerateCacheKey(context);
        
        // 尝试从缓存中获取结果
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            // 如果命中缓存，直接返回结果
            context.Result = (IActionResult)cachedResult;
            _logger.LogInformation($"缓存命中: {path}, 缓存时长: {cacheDuration}秒");
            return;
        }

        // 执行 Action（未命中缓存）
        var executedContext = await next();
        
        // 如果执行成功且有结果，存入缓存
        if (executedContext.Result != null && 
            executedContext.Exception == null &&
            executedContext.HttpContext.Response.StatusCode == 200)
        {
            // 只缓存成功的GET请求
            if (HttpMethods.IsGet(context.HttpContext.Request.Method))
            {
                _cache.Set(cacheKey, executedContext.Result, TimeSpan.FromSeconds(cacheDuration));
                _logger.LogInformation($"缓存写入: {path}, 缓存时长: {cacheDuration}秒");
            }
        }
    }

    private int GetCacheDuration(string path)
    {
        // 精确匹配
        if (_config.PathConfigs.TryGetValue(path, out var duration))
            return duration;
        
        // 支持前缀匹配（可选）
        foreach (var configPath in _config.PathConfigs.Keys)
        {
            if (path.StartsWith(configPath))
                return _config.PathConfigs[configPath];
        }
        
        return _config.DefaultCacheSeconds;
    }

    private string GenerateCacheKey(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path;
        var queryString = context.HttpContext.Request.QueryString.ToString();
        return $"{path}{queryString}";
    }
}