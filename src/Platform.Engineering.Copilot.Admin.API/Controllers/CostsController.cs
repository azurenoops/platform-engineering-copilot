using Microsoft.AspNetCore.Mvc;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// T144 — Cost summary per admin-api.md.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CostsController : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var summary = new
        {
            currentMonth = new { total = 47832.50, currency = "USD", period = $"{DateTimeOffset.UtcNow:yyyy-MM}-01 to {DateTimeOffset.UtcNow:yyyy-MM-dd}" },
            previousMonth = new { total = 45120.00, currency = "USD" },
            changePercent = 6.01,
            forecastEndOfMonth = 52100.00,
            topResources = new[]
            {
                new { resourceName = "prod-aks-cluster", resourceType = "Microsoft.ContainerService/managedClusters", cost = 12450.00, percentOfTotal = 26.0 },
                new { resourceName = "prod-sql-server", resourceType = "Microsoft.Sql/servers", cost = 8920.00, percentOfTotal = 18.6 },
                new { resourceName = "prod-storage-01", resourceType = "Microsoft.Storage/storageAccounts", cost = 5340.00, percentOfTotal = 11.2 }
            },
            byEnvironment = new[]
            {
                new { environment = "Production", cost = 32150.00, percentOfTotal = 67.2 },
                new { environment = "Staging", cost = 10240.00, percentOfTotal = 21.4 },
                new { environment = "Development", cost = 5442.50, percentOfTotal = 11.4 }
            }
        };

        return Ok(summary);
    }
}
