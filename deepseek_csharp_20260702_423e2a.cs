// Services/HybridCacheConfigurationService.cs
public class HybridCacheConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HybridCacheConfigurationService> _logger;
    private readonly Dictionary<string, CacheConfigurationResult> _cacheConfigs = new();
    private readonly Dictionary<string, EndpointMetadata> _endpoints = new();
    private readonly ApiCacheConfig _appSettingsConfig;

    public HybridCacheConfigurationService(
        IConfiguration configuration,
        ILogger<HybridCacheConfigurationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // 加载配置文件
        _appSettingsConfig = _configuration.GetSection("ApiCacheConfig").Get<ApiCacheConfig>() 
                             ?? new ApiCacheConfig();
        
        // 自动发现所有配置
        DiscoverAllConfigurations();
    }

    // 获取路径的缓存配置
    public CacheConfigurationResult GetCacheConfig(string path)
    {
        // 1. 精确匹配
        // 2. 路由参数匹配
        // 3. Controller通配符匹配
        // 4. 返回默认配置
    }

    private void DiscoverAllConfigurations()
    {
        // 扫描所有程序集
        // 遍历所有 Controller
        // 读取 Attribute 配置
        // 合并配置文件
    }
}