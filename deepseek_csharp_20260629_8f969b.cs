// ApiCacheConfig.cs
public class ApiCacheConfig
{
    public int DefaultCacheSeconds { get; set; } = 60;
    public Dictionary<string, int> PathConfigs { get; set; } = new();
}