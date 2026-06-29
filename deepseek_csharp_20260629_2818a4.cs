// appsettings.json
{
  "ApiCacheConfig": {
    "DefaultCacheSeconds": 60,
    "PathConfigs": {
      "/api/products": 300,
      "/api/categories": 180,
      "/api/orders/*": 120,
      "/api/reports/*": 600,
      "/api/products/{id}": 120
    }
  }
}