// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 注册缓存
builder.Services.AddMemoryCache();

// 注册配置服务
builder.Services.AddSingleton<HybridCacheConfigurationService>();

// 注册过滤器
builder.Services.AddScoped<HybridCacheFilter>();
builder.Services.AddScoped<CustomExceptionFilter>();

// 添加控制器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<HybridCacheFilter>();
    options.Filters.Add<CustomExceptionFilter>();
});

var app = builder.Build();

// 调试端点
app.MapGet("/api/cache/configs", async (HybridCacheConfigurationService configService) =>
{
    var configs = configService.GetAllCacheConfigs();
    return Results.Json(new
    {
        Total = configs.Count,
        Configs = configs.Select(kvp => new
        {
            Path = kvp.Key,
            Source = kvp.Value.SourceDescription,
            Duration = kvp.Value.Config.DurationSeconds,
            IsDisabled = kvp.Value.Config.IsDisabled,
            Priority = kvp.Value.Config.Priority
        })
    });
});

app.MapControllers();
app.Run();