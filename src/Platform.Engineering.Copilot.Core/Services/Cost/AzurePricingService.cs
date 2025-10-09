using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Services.Cost;

public interface IAzurePricingService
{
    /// <summary>
    /// Get pricing for a specific Azure service and region
    /// </summary>
    Task<List<AzurePriceItem>> GetPricingAsync(
        string serviceName,
        string region,
        string? meterName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate estimated monthly cost for resource specifications
    /// </summary>
    Task<decimal> CalculateMonthlyCostAsync(
        string serviceFamily,
        string region,
        ResourceSpecification specs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pricing comparison across multiple regions
    /// </summary>
    Task<Dictionary<string, decimal>> GetRegionalPricingComparisonAsync(
        string serviceName,
        string meterName,
        List<string> regions,
        CancellationToken cancellationToken = default);
}

public class ResourceSpecification
{
    public string? ServiceFamily { get; set; }
    public string? ProductName { get; set; }
    public string? SkuName { get; set; }
    public int Quantity { get; set; } = 1;
    public int HoursPerMonth { get; set; } = 730; // Default: 730 hours/month
    public Dictionary<string, string>? AdditionalAttributes { get; set; }
}

public class AzurePriceItem
{
    public string CurrencyCode { get; set; } = "USD";
    public decimal UnitPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceFamily { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string MeterName { get; set; } = string.Empty;
    public string ArmRegionName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime EffectiveStartDate { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class AzurePricingResponse
{
    public List<AzurePriceItem> Items { get; set; } = new();
    public string? NextPageLink { get; set; }
    public int Count { get; set; }
}

public class AzurePricingService : IAzurePricingService
{
    private readonly ILogger<AzurePricingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string PricingApiBaseUrl = "https://prices.azure.com/api/retail/prices";

    public AzurePricingService(
        ILogger<AzurePricingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<AzurePriceItem>> GetPricingAsync(
        string serviceName,
        string region,
        string? meterName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var filters = new List<string>
            {
                $"serviceName eq '{serviceName}'",
                $"armRegionName eq '{region}'"
            };

            if (!string.IsNullOrEmpty(meterName))
            {
                filters.Add($"meterName eq '{meterName}'");
            }

            // Add filter for current prices only
            filters.Add("priceType eq 'Consumption'");

            var filterQuery = string.Join(" and ", filters);
            var requestUri = $"{PricingApiBaseUrl}?$filter={HttpUtility.UrlEncode(filterQuery)}";

            _logger.LogInformation("Fetching pricing from Azure Retail Prices API: {Uri}", requestUri);

            var response = await httpClient.GetFromJsonAsync<AzurePricingResponse>(
                requestUri, 
                cancellationToken);

            if (response == null || response.Items.Count == 0)
            {
                _logger.LogWarning(
                    "No pricing data found for service={Service}, region={Region}, meter={Meter}",
                    serviceName, region, meterName);
                return new List<AzurePriceItem>();
            }

            _logger.LogInformation(
                "Retrieved {Count} price items for {Service} in {Region}",
                response.Items.Count, serviceName, region);

            return response.Items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error fetching pricing for service={Service}, region={Region}",
                serviceName, region);
            throw;
        }
    }

    public async Task<decimal> CalculateMonthlyCostAsync(
        string serviceFamily,
        string region,
        ResourceSpecification specs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prices = await GetPricingAsync(
                specs.ServiceFamily ?? serviceFamily,
                region,
                cancellationToken: cancellationToken);

            if (prices.Count == 0)
            {
                _logger.LogWarning(
                    "No pricing found for {ServiceFamily} in {Region}, using estimate",
                    serviceFamily, region);
                return CalculateFallbackEstimate(serviceFamily, specs);
            }

            // Find the most relevant price based on specs
            var relevantPrice = prices
                .Where(p => MatchesSpecification(p, specs))
                .OrderBy(p => p.RetailPrice)
                .FirstOrDefault();

            if (relevantPrice == null)
            {
                relevantPrice = prices.First(); // Use first available price
                _logger.LogWarning(
                    "No exact match found, using first available price: {Price}",
                    relevantPrice.RetailPrice);
            }

            var monthlyCost = relevantPrice.RetailPrice * specs.Quantity * specs.HoursPerMonth;

            _logger.LogInformation(
                "Calculated monthly cost: ${Cost:N2} ({Quantity}x {Hours}h @ ${Rate}/hr)",
                monthlyCost, specs.Quantity, specs.HoursPerMonth, relevantPrice.RetailPrice);

            return monthlyCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating monthly cost for {ServiceFamily}", serviceFamily);
            return CalculateFallbackEstimate(serviceFamily, specs);
        }
    }

    public async Task<Dictionary<string, decimal>> GetRegionalPricingComparisonAsync(
        string serviceName,
        string meterName,
        List<string> regions,
        CancellationToken cancellationToken = default)
    {
        var regionalPrices = new Dictionary<string, decimal>();

        foreach (var region in regions)
        {
            try
            {
                var prices = await GetPricingAsync(serviceName, region, meterName, cancellationToken);
                var lowestPrice = prices.Any() ? prices.Min(p => p.RetailPrice) : 0m;
                regionalPrices[region] = lowestPrice;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get pricing for region {Region}", region);
                regionalPrices[region] = 0m;
            }
        }

        return regionalPrices;
    }

    private bool MatchesSpecification(AzurePriceItem price, ResourceSpecification specs)
    {
        if (!string.IsNullOrEmpty(specs.SkuName) && 
            !price.SkuName.Contains(specs.SkuName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(specs.ProductName) && 
            !price.ProductName.Contains(specs.ProductName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private decimal CalculateFallbackEstimate(string serviceFamily, ResourceSpecification specs)
    {
        // Fallback baseline costs per service family (monthly)
        var baselineCosts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "Compute", 100.00m },
            { "Storage", 20.00m },
            { "Networking", 50.00m },
            { "Databases", 150.00m },
            { "Containers", 75.00m },
            { "Virtual Machines", 100.00m },
            { "App Service", 60.00m },
            { "Azure Kubernetes Service", 150.00m },
            { "Storage Accounts", 20.00m },
            { "SQL Database", 150.00m },
            { "Key Vault", 5.00m },
            { "Application Gateway", 125.00m },
            { "Load Balancer", 25.00m }
        };

        var baseCost = baselineCosts.ContainsKey(serviceFamily) 
            ? baselineCosts[serviceFamily] 
            : 50.00m;

        return baseCost * specs.Quantity;
    }
}
