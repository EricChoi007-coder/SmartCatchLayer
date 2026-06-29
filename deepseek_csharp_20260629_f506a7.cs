// HybridCacheFilter.cs
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

public class HybridCacheFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly HybridCacheConfigurationService _configService;
    private readonly ILogger<HybridCacheFilter> _logger;

    public HybridCacheFilter(
        IMemoryCache cache,
        HybridCacheConfigurationService configService,
        ILogger<HybridCacheFilter> logger)
    {
        _cache = cache;
        _configService = configService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.ToString().ToLowerInvariant();
        var method = context.HttpContext.Request.Method;

        var configResult = _configService.GetCacheConfig(path);
        var config = configResult.Config;

        // 只缓存GET请求
        if (config.IsDisabled || config.DurationSeconds <= 0 || !HttpMethods.IsGet(method))
        {
            await next();
            return;
        }

        var cacheKey = GenerateCacheKey(context, config);
        
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            context.Result = (IActionResult)cachedResult;
            _logger.LogInformation($"缓存命中 [来源: {configResult.SourceDescription}]: {path}");
            return;
        }

        var executedContext = await next();
        
        if (executedContext.Result != null && 
            executedContext.Exception == null &&
            executedContext.HttpContext.Response.StatusCode == 200)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(config.DurationSeconds));

            if (config.SlidingExpiration.HasValue)
            {
                cacheOptions.SetSlidingExpiration(config.SlidingExpiration.Value);
            }

            if (config.AbsoluteExpiration.HasValue)
            {
                cacheOptions.SetAbsoluteExpiration(config.AbsoluteExpiration.Value);
            }

            _cache.Set(cacheKey, executedContext.Result, cacheOptions);
            _logger.LogInformation($"缓存写入 [来源: {configResult.SourceDescription}]: {path}");
        }
    }

    private string GenerateCacheKey(ActionExecutingContext context, CacheConfiguration config)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(context.HttpContext.Request.Path);

        // 添加缓存前缀
        if (!string.IsNullOrEmpty(config.CacheKeyPrefix))
        {
            keyBuilder.Append($"|prefix:{config.CacheKeyPrefix}");
        }

        // 添加路由参数
        if (config.VaryByRouteParams && context.RouteData.Values.Any())
        {
            var routeValues = context.RouteData.Values
                .Where(kv => !string.Equals(kv.Key, "controller", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(kv.Key, "action", StringComparison.OrdinalIgnoreCase))
                .Select(kv => $"{kv.Key}={kv.Value}");
            
            if (routeValues.Any())
            {
                keyBuilder.Append($"|route:{string.Join("&", routeValues)}");
            }
        }

        // 添加查询参数
        if (config.VaryByQueryParams)
        {
            var queryParams = context.HttpContext.Request.Query
                .Where(kv => !config.ExcludeQueryParams.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}={kv.Value}");
            
            if (queryParams.Any())
            {
                keyBuilder.Append($"|query:{string.Join("&", queryParams)}");
            }
        }

        // 添加Headers
        if (config.VaryByHeaders && config.Headers.Any())
        {
            var headerValues = config.Headers
                .Select(h => $"{h}={context.HttpContext.Request.Headers[h].ToString()}")
                .Where(h => !string.IsNullOrEmpty(h.Split('=')[1]));
            
            if (headerValues.Any())
            {
                keyBuilder.Append($"|headers:{string.Join("&", headerValues)}");
            }
        }

        return keyBuilder.ToString();
    }
}