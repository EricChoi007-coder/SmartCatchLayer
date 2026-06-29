// 修改 GetCacheDuration 方法
private int GetCacheDuration(ActionExecutingContext context)
{
    var path = context.HttpContext.Request.Path.ToString();
    var user = context.HttpContext.User;
    
    // 管理员获得更长的缓存时间
    if (user.IsInRole("Admin"))
    {
        return _config.PathConfigs.TryGetValue(path, out var duration) 
            ? duration * 2 
            : _config.DefaultCacheSeconds * 2;
    }
    
    return _config.PathConfigs.TryGetValue(path, out duration) 
        ? duration 
        : _config.DefaultCacheSeconds;
}