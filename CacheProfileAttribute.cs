namespace CacheDegradationSystem.Attributes;

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