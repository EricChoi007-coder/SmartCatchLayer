namespace CacheDegradationSystem.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class NoCacheAttribute : Attribute
{
    public string Reason { get; set; } = "Cache disabled by attribute";
}