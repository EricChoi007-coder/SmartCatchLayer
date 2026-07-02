namespace CacheDegradationSystem.Models;

public class CacheConfiguration
{
    public int DurationSeconds { get; set; }
    public string? CacheKeyPrefix { get; set; }
    public bool VaryByQueryParams { get; set; } = true;
    public bool VaryByHeaders { get; set; } = false;
    public string[] Headers { get; set; } = Array.Empty<string>();
    public bool VaryByRouteParams { get; set; } = true;
    public string[] ExcludeQueryParams { get; set; } = Array.Empty<string>();
    public bool IsControllerLevel { get; set; } = false;
    public bool IsDisabled { get; set; } = false;
    public string? DisableReason { get; set; }
    public int? Priority { get; set; }
    public DateTime? AbsoluteExpiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
}

public enum CacheConfigurationSource
{
    Default,
    AppSettings,
    ControllerAttribute,
    ActionAttribute
}

public class CacheConfigurationResult
{
    public CacheConfiguration Config { get; set; } = new();
    public CacheConfigurationSource Source { get; set; }
    public string SourceDescription => Source switch
    {
        CacheConfigurationSource.ActionAttribute => "Action级别Attribute",
        CacheConfigurationSource.ControllerAttribute => "Controller级别Attribute",
        CacheConfigurationSource.AppSettings => "配置文件(appsettings.json)",
        CacheConfigurationSource.Default => "默认配置",
        _ => "未知来源"
    };
}

public class ApiCacheConfig
{
    public int DefaultCacheSeconds { get; set; } = 60;
    public Dictionary<string, int> PathConfigs { get; set; } = new();
}