namespace CacheDegradationSystem.Models;

public class EndpointMetadata
{
    public string Path { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsFromRoute { get; set; }
    public bool IsFromQuery { get; set; }
    public bool IsFromBody { get; set; }
}