// Program.cs (.NET 6+)
var builder = WebApplication.CreateBuilder(args);

// 1. 注册缓存服务
builder.Services.AddMemoryCache();

// 2. 注册配置
builder.Services.Configure<ApiCacheConfig>(
    builder.Configuration.GetSection("ApiCacheConfig"));

// 3. 注册自定义Filter（作为全局过滤器）
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DynamicCacheFilter>();    // 添加缓存过滤器
    options.Filters.Add<CustomExceptionFilter>();  // 添加异常过滤器
});

// 4. 或者作为局部Filter使用（在Controller或Action上加特性）
// 需要先注册为服务
builder.Services.AddScoped<DynamicCacheFilter>();
builder.Services.AddScoped<CustomExceptionFilter>();

var app = builder.Build();

app.MapControllers();
app.Run();