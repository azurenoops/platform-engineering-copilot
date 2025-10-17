using Xunit;
using Moq;
using Moq.Protected;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Cost;

public class AzurePricingServiceTests
{
    private readonly Mock<ILogger<AzurePricingService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly AzurePricingService _service;

    public AzurePricingServiceTests()
    {
        _mockLogger = new Mock<ILogger<AzurePricingService>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        
        var httpClient = new HttpClient(_mockHttpHandler.Object);
        
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
        
        _service = new AzurePricingService(_mockLogger.Object, _mockHttpClientFactory.Object);
    }

    #region GetPricingAsync Tests

    [Fact]
    public async Task GetPricingAsync_WithValidParameters_ReturnsPriceItemsAsync()
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 2,
            ""Items"": [
                {
                    ""currencyCode"": ""USD"",
                    ""unitPrice"": 0.096,
                    ""retailPrice"": 0.096,
                    ""unitOfMeasure"": ""1 Hour"",
                    ""serviceName"": ""Virtual Machines"",
                    ""serviceFamily"": ""Compute"",
                    ""productName"": ""Virtual Machines Dv3 Series"",
                    ""skuName"": ""D2 v3"",
                    ""meterName"": ""D2 v3"",
                    ""armRegionName"": ""eastus"",
                    ""location"": ""US East"",
                    ""effectiveStartDate"": ""2024-01-01T00:00:00Z"",
                    ""type"": ""Consumption""
                },
                {
                    ""currencyCode"": ""USD"",
                    ""unitPrice"": 0.192,
                    ""retailPrice"": 0.192,
                    ""unitOfMeasure"": ""1 Hour"",
                    ""serviceName"": ""Virtual Machines"",
                    ""serviceFamily"": ""Compute"",
                    ""productName"": ""Virtual Machines Dv3 Series"",
                    ""skuName"": ""D4 v3"",
                    ""meterName"": ""D4 v3"",
                    ""armRegionName"": ""eastus"",
                    ""location"": ""US East"",
                    ""effectiveStartDate"": ""2024-01-01T00:00:00Z"",
                    ""type"": ""Consumption""
                }
            ]
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().StartsWith("https://prices.azure.com/api/retail/prices")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _service.GetPricingAsync("Virtual Machines", "eastus");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].ServiceName.Should().Be("Virtual Machines");
        result[0].ArmRegionName.Should().Be("eastus");
        result[0].RetailPrice.Should().Be(0.096m);
        result[1].RetailPrice.Should().Be(0.192m);
    }

    [Fact]
    public async Task GetPricingAsync_WithMeterName_FiltersCorrectlyAsync()
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 1,
            ""Items"": [
                {
                    ""currencyCode"": ""USD"",
                    ""unitPrice"": 0.096,
                    ""retailPrice"": 0.096,
                    ""unitOfMeasure"": ""1 Hour"",
                    ""serviceName"": ""Virtual Machines"",
                    ""serviceFamily"": ""Compute"",
                    ""productName"": ""Virtual Machines Dv3 Series"",
                    ""skuName"": ""D2 v3"",
                    ""meterName"": ""D2 v3"",
                    ""armRegionName"": ""eastus"",
                    ""location"": ""US East"",
                    ""effectiveStartDate"": ""2024-01-01T00:00:00Z"",
                    ""type"": ""Consumption""
                }
            ]
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("meterName")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _service.GetPricingAsync("Virtual Machines", "eastus", "D2 v3");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].MeterName.Should().Be("D2 v3");
    }

    [Fact]
    public async Task GetPricingAsync_WithNoResults_ReturnsEmptyListAsync()
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 0,
            ""Items"": []
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _service.GetPricingAsync("NonExistent", "eastus");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPricingAsync_WhenApiThrows_ThrowsExceptionAsync()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("API error"));

        // Act
        Func<Task> act = async () => await _service.GetPricingAsync("Virtual Machines", "eastus");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    #endregion

    #region CalculateMonthlyCostAsync Tests

    [Fact]
    public async Task CalculateMonthlyCostAsync_WithValidPricing_ReturnsCorrectCostAsync()
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 1,
            ""Items"": [
                {
                    ""currencyCode"": ""USD"",
                    ""unitPrice"": 0.10,
                    ""retailPrice"": 0.10,
                    ""unitOfMeasure"": ""1 Hour"",
                    ""serviceName"": ""Virtual Machines"",
                    ""serviceFamily"": ""Compute"",
                    ""productName"": ""Virtual Machines D Series"",
                    ""skuName"": ""D2"",
                    ""meterName"": ""D2"",
                    ""armRegionName"": ""eastus"",
                    ""location"": ""US East"",
                    ""effectiveStartDate"": ""2024-01-01T00:00:00Z"",
                    ""type"": ""Consumption""
                }
            ]
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var specs = new ResourceSpecification
        {
            ServiceFamily = "Compute",
            Quantity = 2,
            HoursPerMonth = 730
        };

        // Act
        var result = await _service.CalculateMonthlyCostAsync("Compute", "eastus", specs);

        // Assert
        // 0.10 (price/hour) * 2 (quantity) * 730 (hours) = 146.00
        result.Should().Be(146.00m);
    }

    [Fact]
    public async Task CalculateMonthlyCostAsync_WithMatchingSkuName_UsesCorrectPriceAsync()
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 2,
            ""Items"": [
                {
                    ""currencyCode"": ""USD"",
                    ""unitPrice"": 0.20,
                    ""retailPrice"": 0.20,
                    ""unitOfMeasure"": ""1 Hour"",
                    ""serviceName"": ""Virtual Machines"",
                    ""serviceFamily"": ""Compute"",
                    ""productName"": ""Virtual Machines D Series"",
                    ""skuName"": ""D4"",
                    ""meterName"": ""D4"",
                    ""armRegionName"": ""eastus"",
                    ""location"": ""US East"",
                    ""effectiveStartDate"": ""2024-01-01T00:00:00Z"",
                    ""type"": ""Consumption""
                },
                {
                    ""currencyCode"": ""USD"",
                    ""unitPrice"": 0.10,
                    ""retailPrice"": 0.10,
                    ""unitOfMeasure"": ""1 Hour"",
                    ""serviceName"": ""Virtual Machines"",
                    ""serviceFamily"": ""Compute"",
                    ""productName"": ""Virtual Machines D Series"",
                    ""skuName"": ""D2"",
                    ""meterName"": ""D2"",
                    ""armRegionName"": ""eastus"",
                    ""location"": ""US East"",
                    ""effectiveStartDate"": ""2024-01-01T00:00:00Z"",
                    ""type"": ""Consumption""
                }
            ]
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var specs = new ResourceSpecification
        {
            ServiceFamily = "Compute",
            SkuName = "D2",
            Quantity = 1,
            HoursPerMonth = 730
        };

        // Act
        var result = await _service.CalculateMonthlyCostAsync("Compute", "eastus", specs);

        // Assert
        // Should use D2 SKU at 0.10, not D4 at 0.20
        result.Should().Be(73.00m); // 0.10 * 1 * 730
    }

    [Fact]
    public async Task CalculateMonthlyCostAsync_WhenNoPricingFound_UsesFallbackEstimateAsync()
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 0,
            ""Items"": []
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var specs = new ResourceSpecification
        {
            ServiceFamily = "Compute",
            Quantity = 2,
            HoursPerMonth = 730
        };

        // Act
        var result = await _service.CalculateMonthlyCostAsync("Compute", "eastus", specs);

        // Assert
        // Fallback: Compute baseline is 100.00, quantity 2 = 200.00
        result.Should().Be(200.00m);
    }

    [Fact]
    public async Task CalculateMonthlyCostAsync_WhenApiThrows_ReturnsFallbackEstimateAsync()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("API error"));

        var specs = new ResourceSpecification
        {
            ServiceFamily = "Databases",
            Quantity = 1,
            HoursPerMonth = 730
        };

        // Act
        var result = await _service.CalculateMonthlyCostAsync("Databases", "eastus", specs);

        // Assert
        // Fallback: Databases baseline is 150.00, quantity 1 = 150.00
        result.Should().Be(150.00m);
    }

    [Theory]
    [InlineData("Compute", 1, 100.00)]
    [InlineData("Storage", 1, 20.00)]
    [InlineData("Networking", 1, 50.00)]
    [InlineData("Databases", 2, 300.00)]
    [InlineData("Containers", 3, 225.00)]
    [InlineData("Virtual Machines", 1, 100.00)]
    [InlineData("App Service", 2, 120.00)]
    [InlineData("Azure Kubernetes Service", 1, 150.00)]
    [InlineData("Storage Accounts", 4, 80.00)]
    [InlineData("SQL Database", 1, 150.00)]
    [InlineData("Key Vault", 5, 25.00)]
    [InlineData("Application Gateway", 1, 125.00)]
    [InlineData("Load Balancer", 2, 50.00)]
    [InlineData("Unknown Service", 1, 50.00)] // Default fallback
    public async Task CalculateMonthlyCostAsync_FallbackEstimates_CalculatesCorrectlyAsync(
        string serviceFamily, int quantity, decimal expectedCost)
    {
        // Arrange
        var responseJson = @"{
            ""Count"": 0,
            ""Items"": []
        }";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var specs = new ResourceSpecification
        {
            ServiceFamily = serviceFamily,
            Quantity = quantity,
            HoursPerMonth = 730
        };

        // Act
        var result = await _service.CalculateMonthlyCostAsync(serviceFamily, "eastus", specs);

        // Assert
        result.Should().Be(expectedCost);
    }

    #endregion

    #region GetRegionalPricingComparisonAsync Tests

    [Fact]
    public async Task GetRegionalPricingComparisonAsync_WithMultipleRegions_ReturnsComparisonAsync()
    {
        // Arrange
        var regions = new List<string> { "eastus", "westus", "northeurope" };
        
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("eastus")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""Count"": 1, ""Items"": [{
                    ""currencyCode"": ""USD"",
                    ""retailPrice"": 0.10,
                    ""serviceName"": ""Virtual Machines"",
                    ""armRegionName"": ""eastus""
                }]}")
            });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("westus")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""Count"": 1, ""Items"": [{
                    ""currencyCode"": ""USD"",
                    ""retailPrice"": 0.12,
                    ""serviceName"": ""Virtual Machines"",
                    ""armRegionName"": ""westus""
                }]}")
            });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("northeurope")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""Count"": 1, ""Items"": [{
                    ""currencyCode"": ""USD"",
                    ""retailPrice"": 0.11,
                    ""serviceName"": ""Virtual Machines"",
                    ""armRegionName"": ""northeurope""
                }]}")
            });

        // Act
        var result = await _service.GetRegionalPricingComparisonAsync(
            "Virtual Machines", "D2 v3", regions);

        // Assert
        result.Should().HaveCount(3);
        result["eastus"].Should().Be(0.10m);
        result["westus"].Should().Be(0.12m);
        result["northeurope"].Should().Be(0.11m);
    }

    [Fact]
    public async Task GetRegionalPricingComparisonAsync_WithNoResults_ReturnsZeroAsync()
    {
        // Arrange
        var regions = new List<string> { "eastus" };
        
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""Count"": 0, ""Items"": []}")
            });

        // Act
        var result = await _service.GetRegionalPricingComparisonAsync(
            "Virtual Machines", "D2 v3", regions);

        // Assert
        result.Should().HaveCount(1);
        result["eastus"].Should().Be(0m);
    }

    [Fact]
    public async Task GetRegionalPricingComparisonAsync_WhenOneFails_ContinuesWithOthersAsync()
    {
        // Arrange
        var regions = new List<string> { "eastus", "westus" };
        
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("eastus")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("API error"));

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("westus")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""Count"": 1, ""Items"": [{
                    ""currencyCode"": ""USD"",
                    ""retailPrice"": 0.12,
                    ""serviceName"": ""Virtual Machines"",
                    ""armRegionName"": ""westus""
                }]}")
            });

        // Act
        var result = await _service.GetRegionalPricingComparisonAsync(
            "Virtual Machines", "D2 v3", regions);

        // Assert
        result.Should().HaveCount(2);
        result["eastus"].Should().Be(0m); // Failed, returns 0
        result["westus"].Should().Be(0.12m); // Succeeded
    }

    [Fact]
    public async Task GetRegionalPricingComparisonAsync_WithMultiplePrices_ReturnsLowestAsync()
    {
        // Arrange
        var regions = new List<string> { "eastus" };
        
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""Count"": 3, ""Items"": [
                    {""currencyCode"": ""USD"", ""retailPrice"": 0.15, ""serviceName"": ""Virtual Machines""},
                    {""currencyCode"": ""USD"", ""retailPrice"": 0.10, ""serviceName"": ""Virtual Machines""},
                    {""currencyCode"": ""USD"", ""retailPrice"": 0.12, ""serviceName"": ""Virtual Machines""}
                ]}")
            });

        // Act
        var result = await _service.GetRegionalPricingComparisonAsync(
            "Virtual Machines", "D2 v3", regions);

        // Assert
        result["eastus"].Should().Be(0.10m); // Should return lowest price
    }

    #endregion
}
