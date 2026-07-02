namespace CacheDegradationSystem.Models;

public class CacheLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? CacheKey { get; set; }
    public string? Source { get; set; }
    public int DurationSeconds { get; set; }
    public int FailureCount { get; set; }
    public bool HasDegradation { get; set; }
    public Exception? Exception { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}