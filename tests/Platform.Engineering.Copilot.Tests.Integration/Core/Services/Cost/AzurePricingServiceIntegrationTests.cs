using Xunit;
using Moq;
using Moq.Protected;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using Platform.Engineering.Copilot.Core.Services.Cost;

namespace Platform.Engineering.Copilot.Tests.Integration.Core.Services.Cost;

public class AzurePricingServiceIntegrationTests
{
    private readonly Mock<ILogger<AzurePricingService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly AzurePricingService _service;

    public AzurePricingServiceIntegrationTests()
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

    [Fact]
    public async Task CompleteWorkflow_CalculateCostUsesCorrectPricing()
    {
        // Arrange - This test verifies cost calculation logic, not HTTP mocking
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
            SkuName = "D2 v3",
            Quantity = 3,
            HoursPerMonth = 730
        };

        // Act
        var monthlyCost = await _service.CalculateMonthlyCostAsync("Compute", "eastus", specs);

        // Assert - Verify calculation: 0.096 * 3 * 730 = 210.24
        monthlyCost.Should().Be(210.24m);
    }
}
