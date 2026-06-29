// HybridCacheConfigurationService.cs
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        
        _appSettingsConfig = _configuration.GetSection("ApiCacheConfig").Get<ApiCacheConfig>() 
                             ?? new ApiCacheConfig();
        
        DiscoverAllConfigurations();
    }

    public Dictionary<string, CacheConfigurationResult> GetAllCacheConfigs() => _cacheConfigs;
    public Dictionary<string, EndpointMetadata> GetAllEndpoints() => _endpoints;

    public CacheConfigurationResult GetCacheConfig(string path)
    {
        path = path.ToLowerInvariant();
        
        // 1. 精确匹配
        if (_cacheConfigs.TryGetValue(path, out var config))
            return config;

        // 2. 匹配路由参数模板
        foreach (var cachedPath in _cacheConfigs.Keys)
        {
            if (IsPathMatch(cachedPath, path))
                return _cacheConfigs[cachedPath];
        }

        // 3. 匹配Controller通配符
        var controllerKey = GetControllerKey(path);
        if (controllerKey != null && _cacheConfigs.TryGetValue(controllerKey, out config))
            return config;

        // 4. 返回默认配置
        return new CacheConfigurationResult
        {
            Config = new CacheConfiguration
            {
                DurationSeconds = _appSettingsConfig.DefaultCacheSeconds,
                VaryByQueryParams = true
            },
            Source = CacheConfigurationSource.Default
        };
    }

    private void DiscoverAllConfigurations()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetTypes().Any(t => t.IsSubclassOf(typeof(ControllerBase))));

        foreach (var assembly in assemblies)
        {
            var controllers = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ControllerBase)));

            foreach (var controller in controllers)
            {
                ProcessController(controller);
            }
        }

        MergeAppSettingsConfigurations();

        _logger.LogInformation($"混合缓存配置加载完成: {_cacheConfigs.Count} 个端点配置");
    }

    private void ProcessController(Type controllerType)
    {
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template ?? "[controller]";
        var controllerName = controllerType.Name.Replace("Controller", "");
        
        var controllerCacheAttr = controllerType.GetCustomAttribute<CacheDurationAttribute>();
        var controllerCacheProfile = controllerType.GetCustomAttribute<CacheProfileAttribute>();
        var controllerNoCache = controllerType.GetCustomAttribute<NoCacheAttribute>();

        CacheConfiguration controllerConfig = null;
        if (controllerCacheAttr != null && controllerNoCache == null)
        {
            controllerConfig = new CacheConfiguration
            {
                DurationSeconds = controllerCacheAttr.DurationSeconds,
                CacheKeyPrefix = controllerCacheAttr.CacheKeyPrefix ?? controllerName,
                VaryByQueryParams = controllerCacheAttr.VaryByQueryParams,
                VaryByHeaders = controllerCacheAttr.VaryByHeaders,
                Headers = controllerCacheAttr.Headers,
                IsControllerLevel = true,
                Priority = 20
            };
            
            var controllerKey = $"/{controllerName}/*";
            _cacheConfigs[controllerKey.ToLowerInvariant()] = new CacheConfigurationResult
            {
                Config = controllerConfig,
                Source = CacheConfigurationSource.ControllerAttribute
            };
        }
        else if (controllerCacheProfile != null && controllerNoCache == null)
        {
            controllerConfig = new CacheConfiguration
            {
                DurationSeconds = controllerCacheProfile.DurationSeconds,
                VaryByQueryParams = controllerCacheProfile.VaryByQuery,
                VaryByHeaders = controllerCacheProfile.VaryByHeaders,
                Headers = controllerCacheProfile.VaryByHeadersArray,
                IsControllerLevel = true,
                VaryByRouteParams = controllerCacheProfile.VaryByRouteParams,
                ExcludeQueryParams = controllerCacheProfile.ExcludeQueryParams,
                Priority = 20
            };
            
            var controllerKey = $"/{controllerName}/*";
            _cacheConfigs[controllerKey.ToLowerInvariant()] = new CacheConfigurationResult
            {
                Config = controllerConfig,
                Source = CacheConfigurationSource.ControllerAttribute
            };
        }

        var actions = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsPublic && !m.IsSpecialName);

        foreach (var action in actions)
        {
            ProcessAction(controllerRoute, action, controllerConfig, controllerNoCache);
        }
    }

    private void ProcessAction(string controllerRoute, MethodInfo action, 
        CacheConfiguration controllerConfig, NoCacheAttribute controllerNoCache)
    {
        var actionCacheAttr = action.GetCustomAttribute<CacheDurationAttribute>();
        var actionCacheProfile = action.GetCustomAttribute<CacheProfileAttribute>();
        var actionNoCache = action.GetCustomAttribute<NoCacheAttribute>();

        var httpMethodAttr = action.GetCustomAttributes()
            .FirstOrDefault(a => a is HttpMethodAttribute) as HttpMethodAttribute;

        var httpMethod = httpMethodAttr?.Method ?? "GET";
        var actionTemplate = httpMethodAttr?.Template ?? action.Name;

        var fullPath = BuildFullPath(controllerRoute, actionTemplate, action);
        
        var endpoint = new EndpointMetadata
        {
            Path = fullPath,
            HttpMethod = httpMethod,
            ControllerName = action.DeclaringType.Name.Replace("Controller", ""),
            ActionName = action.Name,
            Parameters = action.GetParameters().Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.ParameterType.Name,
                IsFromRoute = p.GetCustomAttributes().Any(a => a is FromRouteAttribute),
                IsFromQuery = p.GetCustomAttributes().Any(a => a is FromQueryAttribute),
                IsFromBody = p.GetCustomAttributes().Any(a => a is FromBodyAttribute)
            }).ToList()
        };
        
        _endpoints[fullPath] = endpoint;

        CacheConfiguration actionConfig = null;
        CacheConfigurationSource source = CacheConfigurationSource.Default;

        if (actionNoCache != null || controllerNoCache != null)
        {
            actionConfig = new CacheConfiguration
            {
                IsDisabled = true,
                DisableReason = actionNoCache?.Reason ?? controllerNoCache?.Reason ?? "Disabled by attribute",
                Priority = 100
            };
            source = CacheConfigurationSource.ActionAttribute;
        }
        else if (actionCacheProfile != null)
        {
            actionConfig = new CacheConfiguration
            {
                DurationSeconds = actionCacheProfile.DurationSeconds,
                VaryByQueryParams = actionCacheProfile.VaryByQuery,
                VaryByHeaders = actionCacheProfile.VaryByHeaders,
                Headers = actionCacheProfile.VaryByHeadersArray,
                VaryByRouteParams = actionCacheProfile.VaryByRouteParams,
                ExcludeQueryParams = actionCacheProfile.ExcludeQueryParams,
                Priority = 30
            };
            source = CacheConfigurationSource.ActionAttribute;
        }
        else if (actionCacheAttr != null)
        {
            actionConfig = new CacheConfiguration
            {
                DurationSeconds = actionCacheAttr.DurationSeconds,
                CacheKeyPrefix = actionCacheAttr.CacheKeyPrefix,
                VaryByQueryParams = actionCacheAttr.VaryByQueryParams,
                VaryByHeaders = actionCacheAttr.VaryByHeaders,
                Headers = actionCacheAttr.Headers,
                Priority = 30
            };
            source = CacheConfigurationSource.ActionAttribute;
        }
        else if (controllerConfig != null && !controllerConfig.IsDisabled)
        {
            actionConfig = new CacheConfiguration
            {
                DurationSeconds = controllerConfig.DurationSeconds,
                CacheKeyPrefix = controllerConfig.CacheKeyPrefix,
                VaryByQueryParams = controllerConfig.VaryByQueryParams,
                VaryByHeaders = controllerConfig.VaryByHeaders,
                Headers = controllerConfig.Headers,
                VaryByRouteParams = controllerConfig.VaryByRouteParams,
                ExcludeQueryParams = controllerConfig.ExcludeQueryParams,
                Priority = 20,
                IsControllerLevel = true
            };
            source = CacheConfigurationSource.ControllerAttribute;
        }

        if (actionConfig != null)
        {
            _cacheConfigs[fullPath] = new CacheConfigurationResult
            {
                Config = actionConfig,
                Source = source
            };
        }
    }

    private void MergeAppSettingsConfigurations()
    {
        if (_appSettingsConfig?.PathConfigs == null) return;

        foreach (var kvp in _appSettingsConfig.PathConfigs)
        {
            var path = kvp.Key.ToLowerInvariant();
            var duration = kvp.Value;

            if (_cacheConfigs.TryGetValue(path, out var existing))
            {
                if (duration > 0 && existing.Config.DurationSeconds <= 0)
                {
                    existing.Config.DurationSeconds = duration;
                    existing.Config.Priority = Math.Max(existing.Config.Priority ?? 0, 10);
                }
            }
            else
            {
                _cacheConfigs[path] = new CacheConfigurationResult
                {
                    Config = new CacheConfiguration
                    {
                        DurationSeconds = duration,
                        VaryByQueryParams = true,
                        Priority = 10
                    },
                    Source = CacheConfigurationSource.AppSettings
                };
            }
        }

        foreach (var kvp in _appSettingsConfig.PathConfigs)
        {
            if (kvp.Key.EndsWith("/*"))
            {
                var controllerKey = kvp.Key.ToLowerInvariant();
                if (!_cacheConfigs.ContainsKey(controllerKey))
                {
                    _cacheConfigs[controllerKey] = new CacheConfigurationResult
                    {
                        Config = new CacheConfiguration
                        {
                            DurationSeconds = kvp.Value,
                            VaryByQueryParams = true,
                            Priority = 10,
                            IsControllerLevel = true
                        },
                        Source = CacheConfigurationSource.AppSettings
                    };
                }
            }
        }
    }

    private string BuildFullPath(string controllerRoute, string actionTemplate, MethodInfo action)
    {
        if (actionTemplate.StartsWith("/"))
            return actionTemplate.ToLowerInvariant();

        if (controllerRoute.Contains("[controller]"))
        {
            var controllerName = action.DeclaringType.Name.Replace("Controller", "");
            controllerRoute = controllerRoute.Replace("[controller]", controllerName);
        }

        var fullPath = $"{controllerRoute}/{actionTemplate}".Replace("//", "/");
        if (!fullPath.StartsWith("/"))
            fullPath = "/" + fullPath;

        return fullPath.ToLowerInvariant();
    }

    private bool IsPathMatch(string templatePath, string actualPath)
    {
        var templateParts = templatePath.Split('/');
        var actualParts = actualPath.Split('/');

        if (templatePath.EndsWith("/*"))
        {
            var prefix = templatePath.Replace("/*", "");
            return actualPath.StartsWith(prefix);
        }

        if (templateParts.Length != actualParts.Length)
            return false;

        for (int i = 0; i < templateParts.Length; i++)
        {
            if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
                continue;
            
            if (!string.Equals(templateParts[i], actualParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private string GetControllerKey(string path)
    {
        var parts = path.Split('/');
        if (parts.Length >= 2)
        {
            var controller = parts[1];
            return $"/{controller}/*";
        }
        return null;
    }
}

public class ApiCacheConfig
{
    public int DefaultCacheSeconds { get; set; } = 60;
    public Dictionary<string, int> PathConfigs { get; set; } = new();
}

public class EndpointMetadata
{
    public string Path { get; set; }
    public string HttpMethod { get; set; }
    public string ControllerName { get; set; }
    public string ActionName { get; set; }
    public List<ParameterInfo> Parameters { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsFromRoute { get; set; }
    public bool IsFromQuery { get; set; }
    public bool IsFromBody { get; set; }
}