// ProductsController.cs - 纯Attribute配置
[ApiController]
[Route("api/[controller]")]
[CacheDuration(300)] // 所有Action缓存5分钟
public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(new[] { "Product1", "Product2" });

    [HttpGet("{id}")]
    [CacheDuration(60)] // 覆盖为1分钟
    public IActionResult GetById(int id) => Ok($"产品 {id}");

    [HttpGet("search")]
    [CacheProfile(120, VaryByQuery = true)] // 根据Query参数区分缓存
    public IActionResult Search([FromQuery] string keyword, [FromQuery] int page = 1)
        => Ok($"搜索: {keyword}, 第{page}页");

    [HttpGet("latest")]
    [NoCache(Reason = "需要实时数据")]
    public IActionResult GetLatest() => Ok("最新产品");
}

// OrdersController.cs - 混合使用
[ApiController]
[Route("api/[controller]")]
// 使用配置文件中的 /api/orders/*: 120秒
public class OrdersController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok("所有订单");

    [HttpGet("{id}")]
    [CacheDuration(30)] // Attribute覆盖配置文件
    public IActionResult GetById(int id) => Ok($"订单 {id}");

    [HttpGet("status")]
    [NoCache] // 禁用缓存
    public IActionResult GetStatus() => Ok("订单状态");
}