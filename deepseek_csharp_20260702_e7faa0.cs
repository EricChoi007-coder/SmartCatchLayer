namespace CacheDegradationSystem.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class CacheDurationAttribute : Attribute
{
    public int DurationSeconds { get; set; }
    public string? CacheKeyPrefix { get; set; }
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