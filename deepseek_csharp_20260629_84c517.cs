// CacheDurationAttribute.cs
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class CacheDurationAttribute : Attribute
{
    public int DurationSeconds { get; set; }
    public string CacheKeyPrefix { get; set; }
    public bool VaryByQueryParams { get; set; } = true;
    public bool VaryByHeaders { get; set; } = false;
    public string[] Headers { get; set; } = Array.Empty<string>();

    public CacheDurationAttribute(int durationSeconds)
    {
        DurationSeconds = durationSeconds;
    }

    public CacheDurationAttribute(int durationSeconds, string cacheKeyPrefix)
    {
        DurationSeconds = durationSeconds;
        CacheKeyPrefix = cacheKeyPrefix;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class CacheProfileAttribute : Attribute
{
    public int DurationSeconds { get; set; }
    public bool VaryByQuery { get; set; } = true;
    public bool VaryByHeaders { get; set; } = false;
    public string[] VaryByHeadersArray { get; set; } = Array.Empty<string>();
    public bool VaryByRouteParams { get; set; } = true;
    public string[] ExcludeQueryParams { get; set; } = Array.Empty<string>();

    public CacheProfileAttribute(int durationSeconds)
    {
        DurationSeconds = durationSeconds;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class NoCacheAttribute : Attribute
{
    public string Reason { get; set; } = "Cache disabled by attribute";
}