# API 缓存降级系统

## 📖 概述

一个基于 [ASP.NET](https://asp.net/) Core 的智能缓存降级系统，支持 **Attribute + 配置文件混合配置**、**异常自动降级**、**详细日志记录** 等功能。当后端服务出现异常时，系统会自动返回最近一次成功的缓存数据，确保 API 的高可用性。

------

## ✨ 核心特性

| 特性               | 说明                                                         |
| :----------------- | :----------------------------------------------------------- |
| 🎯 **混合配置**     | 同时支持 Attribute 和 appsettings.json 配置，Attribute 优先级更高 |
| 🛡️ **自动降级**     | Controller 抛出异常时，自动返回最近一次成功的缓存数据        |
| 📊 **连续失败检测** | 连续失败超过阈值（默认3次），强制使用降级缓存                |
| 🚀 **智能缓存键**   | 自动包含路由参数、Query 参数、Headers 等变化维度             |
| 📝 **详细日志**     | 完整记录缓存命中、写入、异常、降级等所有事件                 |
| 🔍 **可观测性**     | 提供 API 端点查看缓存状态、日志、配置信息                    |
| ⚡ **高性能**       | 基于 IMemoryCache，支持绝对/滑动过期时间                     |

------

## 🚀 快速开始

### 1. 安装依赖

bash

```bash
dotnet add package Microsoft.Extensions.Caching.Memory
```



### 2. 注册服务

csharp

```c#
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 注册缓存
builder.Services.AddMemoryCache();

// 注册服务
builder.Services.AddSingleton<HybridCacheConfigurationService>();
builder.Services.AddSingleton<EnhancedDegradationCacheService>();
builder.Services.AddSingleton<CacheLoggerService>();

// 注册过滤器
builder.Services.AddScoped<DetailedHybridCacheFilter>();
builder.Services.AddScoped<DetailedExceptionFilter>();

// 添加控制器并应用全局过滤器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DetailedHybridCacheFilter>();
    options.Filters.Add<DetailedExceptionFilter>();
});

var app = builder.Build();
app.MapControllers();
app.Run();
```



### 3. 配置 appsettings.json

json

```json
{
  "ApiCacheConfig": {
    "DefaultCacheSeconds": 60,
    "PathConfigs": {
      "/api/products": 300,
      "/api/categories": 180,
      "/api/orders/*": 120,
      "/api/products/{id}": 120
    }
  },
  "DegradationCache": {
    "MaxConsecutiveFailures": 3,
    "CacheExpirySeconds": 3600,
    "EnableLogging": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Cache": "Debug"
    }
  }
}
```



### 4. 在 Controller 中使用 Attribute

csharp

```csharp
[ApiController]
[Route("api/[controller]")]
[CacheDuration(300)] // Controller 级别配置
public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetProducts() 
    {
        // 自动缓存 300 秒
        return Ok(new[] { "Product1", "Product2" });
    }

    [HttpGet("{id}")]
    [CacheDuration(60)] // 覆盖 Controller 配置
    public IActionResult GetProduct(int id)
    {
        return Ok(new { id, name = $"Product{id}" });
    }

    [HttpGet("search")]
    [CacheProfile(120, VaryByQuery = true)]
    public IActionResult Search([FromQuery] string keyword)
    {
        // 根据 keyword 参数区分缓存
        return Ok(new { keyword, results = new[] { "Result1", "Result2" } });
    }

    [HttpGet("latest")]
    [NoCache(Reason = "需要实时数据")]
    public IActionResult GetLatest()
    {
        // 完全禁用缓存
        return Ok(new[] { "Latest1", "Latest2" });
    }
}
```



------

## 🏗️ 架构设计

### 系统组件

text

```
┌─────────────────────────────────────────────────────────────────┐
│                        请求流程                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Request → 2. HybridCacheFilter                             │
│                    ├─ 检查正常缓存 → 命中 → 返回               │
│                    ├─ 检查降级缓存 → 存在 → 返回               │
│                    └─ 执行 Action                              │
│                         ├─ 成功 → 保存缓存 + 降级缓存         │
│                         └─ 异常 → ExceptionFilter              │
│                                    ├─ 有降级缓存 → 返回降级数据 │
│                                    └─ 无降级缓存 → 返回 503   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```



### 核心组件说明

| 组件                                | 职责                                                  |
| :---------------------------------- | :---------------------------------------------------- |
| **HybridCacheConfigurationService** | 自动扫描 Controller/Action，加载 Attribute 和配置文件 |
| **EnhancedDegradationCacheService** | 管理降级缓存的保存、读取、统计                        |
| **CacheLoggerService**              | 统一日志记录，支持批量刷新                            |
| **DetailedHybridCacheFilter**       | 核心缓存逻辑：命中检查、缓存写入、降级处理            |
| **DetailedExceptionFilter**         | 异常捕获：降级缓存返回、错误响应生成                  |

------

## 📋 配置详解

### Attribute 配置

#### 1. `[CacheDuration]` - 基础缓存配置

csharp

```csharp
[CacheDuration(300)] // 缓存 300 秒
[CacheDuration(60, "custom_prefix")] // 自定义缓存键前缀
```



| 参数                | 类型     | 默认值 | 说明                |
| :------------------ | :------- | :----- | :------------------ |
| `DurationSeconds`   | int      | 必填   | 缓存时长（秒）      |
| `CacheKeyPrefix`    | string   | null   | 缓存键前缀          |
| `VaryByQueryParams` | bool     | true   | 是否区分 Query 参数 |
| `VaryByHeaders`     | bool     | false  | 是否区分 Headers    |
| `Headers`           | string[] | []     | 需要区分的 Headers  |

#### 2. `[CacheProfile]` - 精细缓存配置

csharp

```csharp
[CacheProfile(120, VaryByQuery = true, VaryByRouteParams = true)]
```



| 参数                 | 类型     | 默认值 | 说明                |
| :------------------- | :------- | :----- | :------------------ |
| `DurationSeconds`    | int      | 必填   | 缓存时长（秒）      |
| `VaryByQuery`        | bool     | true   | 是否区分 Query 参数 |
| `VaryByHeaders`      | bool     | false  | 是否区分 Headers    |
| `VaryByHeadersArray` | string[] | []     | 需要区分的 Headers  |
| `VaryByRouteParams`  | bool     | true   | 是否区分路由参数    |
| `ExcludeQueryParams` | string[] | []     | 排除的 Query 参数   |

#### 3. `[NoCache]` - 禁用缓存

csharp

```csharp
[NoCache(Reason = "实时数据，不缓存")]
```



### 配置文件

json

```json
{
  "ApiCacheConfig": {
    "DefaultCacheSeconds": 60,     // 默认缓存时长
    "PathConfigs": {
      // 精确路径匹配
      "/api/products": 300,
      
      // 控制器级别通配符
      "/api/orders/*": 120,
      
      // 带路由参数
      "/api/products/{id}": 120
    }
  }
}
```



------

## 🔄 缓存键生成规则

缓存键自动包含以下维度：

text

```text
{路径}|prefix:{前缀}|route:{路由参数}|query:{查询参数}|headers:{Header值}

示例：
/api/products/123|prefix:product_search|route:id=123|query:page=1&size=10|headers:Accept-Language=zh-CN
```



### 缓存键变化维度

| 维度           | 配置                                 | 示例                                    |
| :------------- | :----------------------------------- | :-------------------------------------- |
| **路由参数**   | `VaryByRouteParams = true`           | `/products/1` 和 `/products/2` 不同缓存 |
| **Query 参数** | `VaryByQueryParams = true`           | `?page=1` 和 `?page=2` 不同缓存         |
| **Headers**    | `VaryByHeaders = true`               | 不同 `Accept-Language` 不同缓存         |
| **排除参数**   | `ExcludeQueryParams = ["timestamp"]` | 忽略 `timestamp` 参数                   |

------

## 🛡️ 降级缓存机制

### 工作原理

text

```text
正常请求
    ↓
成功响应 → 保存降级缓存 (过期时间 = 正常缓存 × 2, 最少 1 小时)
    ↓
后续请求
    ↓
Controller 异常 → 检查降级缓存 → 存在 → 返回降级数据
                                  ↓ 不存在
                                返回 503 错误
```



### 连续失败检测

- 连续失败次数 ≥ `MaxConsecutiveFailures`（默认 3）
- 强制使用降级缓存，即使正常缓存存在
- 成功请求后自动重置失败计数

------

## 📝 日志功能

### 日志级别

| 级别            | 场景                          |
| :-------------- | :---------------------------- |
| **Error**       | Controller 异常、缓存操作失败 |
| **Warning**     | 使用降级缓存、连续失败        |
| **Information** | 缓存命中、缓存写入、请求开始  |
| **Debug**       | 缓存跳过、调试信息            |

### 日志示例

text

```text
[Information] 请求开始 [RequestId: 0HLVMP4E1K3B4, Path: /api/products, Method: GET]
[Information] 缓存命中 [路径: /api/products, 来源: Action级别Attribute, 时长: 300s, Key: /api/products]
[Information] 请求完成 [RequestId: 0HLVMP4E1K3B4, Path: /api/products, 耗时: 2ms]

[Error] 请求异常 [RequestId: 0HLVMP4E1K3B5, Path: /api/products, 方法: GET, 有降级缓存: True]
System.Exception: 数据库连接超时
[Warning] 使用降级缓存 [路径: /api/products, 原因: 异常降级, 连续失败次数: 1]
```



------

## 🔍 可观测性 API

系统内置了调试端点，方便监控和管理：

### 1. 查看缓存配置

bash

```bash
GET /api/cache/configs
```



响应：

json

```json
{
  "Total": 5,
  "Timestamp": "2024-01-15T10:30:00Z",
  "Configs": [
    {
      "Path": "/api/products",
      "Source": "Action级别Attribute",
      "Duration": 300,
      "IsDisabled": false,
      "Priority": 30,
      "VaryByQueryParams": true,
      "VaryByHeaders": false
    }
  ]
}
```



### 2. 查看降级缓存状态

bash

```
GET /api/cache/degradation/status
```



响应：

json

```json
{
  "Statistics": {
    "TotalEndpoints": 3,
    "Endpoints": [
      {
        "Endpoint": "/api/products",
        "LastSuccessTime": "2024-01-15T10:28:00Z",
        "FailureCount": 2,
        "FailureHistory": ["2024-01-15T10:29:00Z", "2024-01-15T10:28:30Z"],
        "HasDegradationCache": true
      }
    ]
  }
}
```



### 3. 查看最近日志

bash

```bash
GET /api/logs/recent
```



响应：

json

```json
{
  "Total": 50,
  "Logs": [
    {
      "Timestamp": "2024-01-15T10:30:00Z",
      "Level": "Information",
      "Category": "Cache",
      "Event": "CacheHit",
      "Path": "/api/products",
      "Message": "缓存命中: /api/products",
      "Exception": null
    }
  ]
}
```



### 4. 清除降级缓存

bash

```bash
DELETE /api/cache/degradation/{path}
```



示例：

bash

```bash
DELETE /api/cache/degradation/api/products
```



------

## 📊 优先级规则

| 优先级 | 配置来源                       | 说明                     |
| :----- | :----------------------------- | :----------------------- |
| 1      | `[NoCache]`                    | 最高优先级，完全禁用缓存 |
| 2      | `[CacheProfile]` (Action)      | Action 级别精细配置      |
| 3      | `[CacheDuration]` (Action)     | Action 级别基础配置      |
| 4      | `[CacheProfile]` (Controller)  | Controller 级别精细配置  |
| 5      | `[CacheDuration]` (Controller) | Controller 级别基础配置  |
| 6      | 配置文件精确匹配               | 如 `/api/products`       |
| 7      | 配置文件通配符                 | 如 `/api/orders/*`       |
| 8      | 默认配置                       | `DefaultCacheSeconds`    |

------

## 🎯 最佳实践

### 1. 选择合适的缓存时长

csharp

```csharp
// 高频访问、数据变化慢 → 长缓存
[CacheDuration(600)] // 10 分钟

// 数据变化较快 → 短缓存
[CacheDuration(30)] // 30 秒

// 实时数据 → 禁用缓存
[NoCache]
```



### 2. 合理使用缓存维度

csharp

```csharp
// 不同用户不同数据
[CacheProfile(60, VaryByQuery = true)]

// 不同语言不同内容
[CacheProfile(300, VaryByHeaders = true, VaryByHeadersArray = new[] { "Accept-Language" })]

// 排除时间戳参数
[CacheProfile(120, ExcludeQueryParams = new[] { "timestamp", "_" })]
```



### 3. 降级缓存配置建议

json

```json
{
  "DegradationCache": {
    "MaxConsecutiveFailures": 3,    // 连续失败 3 次触发降级
    "CacheExpirySeconds": 7200      // 降级缓存保留 2 小时
  }
}
```



### 4. 监控告警建议

- 监控 `X-Cache-Status: Degradation` 响应头比例
- 监控 `CacheLoggerService` 中的 Warning 级别日志
- 设置异常率告警阈值

------

## 🧪 测试示例

### 测试降级缓存

csharp

```csharp
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private static int _counter = 0;

    [HttpGet("degradation-test")]
    [CacheDuration(300)]
    public IActionResult TestDegradation()
    {
        _counter++;
        if (_counter % 3 == 0) // 每 3 次请求失败 1 次
        {
            throw new Exception("模拟异常");
        }
        return Ok(new { 
            Message = "成功响应", 
            RequestCount = _counter,
            Timestamp = DateTime.UtcNow 
        });
    }
}
```



测试步骤：

1. 连续请求 2 次 → 成功，缓存写入
2. 第 3 次请求 → 异常，返回降级缓存
3. 查看响应头 `X-Cache-Status: Degradation`

------

## 📦 项目结构

text

```
├── Program.cs                          # 应用入口，服务注册
├── Attributes/
│   ├── CacheDurationAttribute.cs       # 基础缓存配置
│   ├── CacheProfileAttribute.cs        # 精细缓存配置
│   └── NoCacheAttribute.cs             # 禁用缓存
├── Services/
│   ├── HybridCacheConfigurationService.cs    # 混合配置服务
│   ├── EnhancedDegradationCacheService.cs    # 降级缓存服务
│   └── CacheLoggerService.cs                 # 日志服务
├── Filters/
│   ├── DetailedHybridCacheFilter.cs    # 缓存过滤器
│   └── DetailedExceptionFilter.cs      # 异常过滤器
└── Models/
    ├── CacheConfiguration.cs           # 缓存配置模型
    └── CacheLogEntry.cs                # 日志条目模型
```



------

## ⚠️ 注意事项

1. **内存限制**：IMemoryCache 存储在内存中，大量缓存可能消耗内存，建议设置缓存大小限制
2. **分布式环境**：IMemoryCache 是进程内缓存，分布式部署建议替换为 IDistributedCache（Redis）
3. **敏感数据**：注意不要缓存包含敏感信息的响应
4. **降级数据时效性**：降级缓存可能返回过时数据，建议在响应头中标识

------

## 🔧 扩展建议

### 使用 Redis 替代 MemoryCache

csharp

```
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
```



### 添加缓存大小限制

csharp

```
builder.Services.Configure<MemoryCacheOptions>(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB
});
```



------

## 📄 License

MIT

------

## 🤝 贡献

欢迎提交 Issue 和 Pull Request。

------

## 📧 联系方式

如有问题，请提交 Issue 或联系项目维护者。