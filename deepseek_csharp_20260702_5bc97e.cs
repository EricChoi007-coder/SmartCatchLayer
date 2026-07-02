using CacheDegradationSystem.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace CacheDegradationSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[CacheDuration(300)] // Controller 级别：所有 Action 缓存 300 秒
public class ProductsController : ControllerBase
{
    private static readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = "Product1", Price = 100 },
        new Product { Id = 2, Name = "Product2", Price = 200 },
        new Product { Id = 3, Name = "Product3", Price = 300 }
    };

    // 使用 Controller 缓存配置 (300秒)
    [HttpGet]
    public IActionResult GetAll() => Ok(_products);

    // 覆盖为 60 秒
    [HttpGet("{id}")]
    [CacheDuration(60)]
    public IActionResult GetById(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        return product == null ? NotFound() : Ok(product);
    }

    // 精细控制：根据 Query 参数区分缓存
    [HttpGet("search")]
    [CacheProfile(120, VaryByQuery = true)]
    public IActionResult Search([FromQuery] string? keyword, [FromQuery] int page = 1)
    {
        var results = _products.Where(p => string.IsNullOrEmpty(keyword) || p.Name.Contains(keyword));
        return Ok(new { keyword, page, results });
    }

    // 禁用缓存
    [HttpGet("latest")]
    [NoCache(Reason = "需要实时数据")]
    public IActionResult GetLatest() => Ok(_products.LastOrDefault());

    // 模拟异常，测试降级
    [HttpGet("test-error")]
    public IActionResult TestError()
    {
        throw new InvalidOperationException("模拟业务异常，测试降级缓存");
    }

    // 测试连续失败（每3次失败1次）
    private static int _counter = 0;
    [HttpGet("degradation-test")]
    [CacheDuration(300)]
    public IActionResult TestDegradation()
    {
        _counter++;
        if (_counter % 3 == 0)
            throw new Exception("模拟数据库超时");
        return Ok(new
        {
            Message = "成功响应",
            RequestCount = _counter,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}