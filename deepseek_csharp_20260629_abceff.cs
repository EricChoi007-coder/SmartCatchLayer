// ProductsController.cs
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    // 不需要额外加特性，因为已经在全局注册
    // 如果只想局部使用，可以用 [ServiceFilter(typeof(DynamicCacheFilter))]
    public async Task<IActionResult> GetProducts()
    {
        // 模拟耗时操作
        await Task.Delay(500);
        return Ok(new { products = new[] { "Product1", "Product2" } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        // 测试异常
        if (id < 0)
            throw new ArgumentException("ID不能为负数");
        
        await Task.Delay(100);
        return Ok(new { id, name = $"Product{id}" });
    }
}