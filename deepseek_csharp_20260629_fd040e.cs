// 在应用启动时预缓存热点数据
public class CacheWarmupService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ApiCacheConfig _config;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var controller = scope.ServiceProvider.GetRequiredService<ProductsController>();
        
        // 预热关键路径
        foreach (var path in _config.PathConfigs.Keys)
        {
            await controller.GetProducts(); // 触发缓存
        }
    }
}