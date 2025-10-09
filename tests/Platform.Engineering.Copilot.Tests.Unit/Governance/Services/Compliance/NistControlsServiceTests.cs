using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Polly;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Governance.Configuration;
using Platform.Engineering.Copilot.Governance.Services;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Governance.Services.Compliance;

/// <summary>
/// Comprehensive unit tests for NistControlsService
/// Tests caching, HTTP retry policies, fallback mechanisms, and NIST controls operations
/// </summary>
public class NistControlsServiceTests
{
    private readonly Mock<ILogger<NistControlsService>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly ComplianceMetricsService _metricsService;
    private readonly IMemoryCache _memoryCache;
    private readonly NistControlsOptions _options;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    public NistControlsServiceTests()
    {
        _mockLogger = new Mock<ILogger<NistControlsService>>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        
        // Use a real ComplianceMetricsService instead of a mock
        var metricsLogger = new Mock<ILogger<ComplianceMetricsService>>();
        _metricsService = new ComplianceMetricsService(metricsLogger.Object);
        
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        _options = new NistControlsOptions
        {
            BaseUrl = "https://example.com",
            CacheDurationHours = 4,  // Must be >= 4 to avoid SlidingExpiration becoming 0
            EnableOfflineFallback = false,
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 2,
            TimeoutSeconds = 30
        };

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesService()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new NistControlsService(
            null!,
            _memoryCache,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            Options.Create(_options),
            _metricsService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new NistControlsService(
            _httpClient,
            null!,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            Options.Create(_options),
            _metricsService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new NistControlsService(
            _httpClient,
            _memoryCache,
            null!,
            _mockHostEnvironment.Object,
            Options.Create(_options),
            _metricsService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region GetCatalogAsync Tests

    [Fact]
    public async Task GetCatalogAsync_WithCachedCatalog_ReturnsCachedValue()
    {
        // Arrange
        var service = CreateService();
        var testCatalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest"; // TargetVersion is null, so it becomes "latest"
        _memoryCache.Set(cacheKey, testCatalog);

        // Act
        var result = await service.GetCatalogAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(testCatalog);
    }

    [Fact]
    public async Task GetCatalogAsync_WithoutCache_FetchesFromRemote()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        var catalogRoot = new NistCatalogRoot { Catalog = catalog };
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var jsonResponse = JsonSerializer.Serialize(catalogRoot, jsonOptions);

        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
        
        var service = CreateService();

        // Act
        var result = await service.GetCatalogAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Groups.Should().HaveCount(1);
    }    [Fact]
    public async Task GetCatalogAsync_OnRemoteFailure_WithFallbackDisabled_ReturnsNull()
    {
        // Arrange
        _options.EnableOfflineFallback = false;
        var service = CreateService();

        SetupHttpResponse(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await service.GetCatalogAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCatalogAsync_OnSuccess_CachesCatalog()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var catalogRoot = new NistCatalogRoot { Catalog = catalog };
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var jsonResponse = JsonSerializer.Serialize(catalogRoot, jsonOptions);

        SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

        // Act
        await service.GetCatalogAsync();

        // Assert
        var cacheKey = "nist_catalog_latest";
        _memoryCache.TryGetValue(cacheKey, out NistCatalog? cachedCatalog).Should().BeTrue();
        cachedCatalog.Should().NotBeNull();
    }    [Fact]
    public async Task GetCatalogAsync_OnHttpException_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await service.GetCatalogAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetControlAsync Tests

    [Fact]
    public async Task GetControlAsync_WithValidControlId_ReturnsControl()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.GetControlAsync("AC-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("AC-1");
        result.Title.Should().Be("Access Control Policy and Procedures");
    }

    [Fact]
    public async Task GetControlAsync_WithInvalidControlId_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.GetControlAsync("INVALID-99");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetControlAsync_WithNoCatalog_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await service.GetControlAsync("AC-1");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetControlsByFamilyAsync Tests

    [Fact]
    public async Task GetControlsByFamilyAsync_WithValidFamily_ReturnsControls()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.GetControlsByFamilyAsync("AC");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(c => c.Id.StartsWith("AC-")).Should().BeTrue();
    }

    [Fact]
    public async Task GetControlsByFamilyAsync_WithInvalidFamily_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.GetControlsByFamilyAsync("INVALID");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetControlsByFamilyAsync_WithNoCatalog_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await service.GetControlsByFamilyAsync("AC");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region SearchControlsAsync Tests

    [Fact]
    public async Task SearchControlsAsync_WithMatchingTerm_ReturnsControls()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.SearchControlsAsync("access");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.All(c => c.Title.Contains("Access", StringComparison.OrdinalIgnoreCase) ||
                       c.Id.Contains("Access", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task SearchControlsAsync_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.SearchControlsAsync("nonexistentterm12345");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchControlsAsync_IsCaseInsensitive()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var resultLower = await service.SearchControlsAsync("access");
        var resultUpper = await service.SearchControlsAsync("ACCESS");

        // Assert
        resultLower.Should().HaveCount(resultUpper.Count);
    }

    [Fact]
    public async Task SearchControlsAsync_WithNoCatalog_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await service.SearchControlsAsync("access");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetVersionAsync Tests

    [Fact]
    public async Task GetVersionAsync_ReturnsTargetVersion()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.GetVersionAsync();

        // Assert
        result.Should().Be("5.1");
    }

    [Fact]
    public async Task GetVersionAsync_WithNullTargetVersion_ReturnsLatest()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.GetVersionAsync();

        // Assert
        result.Should().Be("5.1");
    }

    #endregion

    #region ValidateControlIdAsync Tests

    [Fact]
    public async Task ValidateControlIdAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.ValidateControlIdAsync("AC-1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateControlIdAsync_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var catalog = CreateTestCatalog();
        var cacheKey = "nist_catalog_latest";
        _memoryCache.Set(cacheKey, catalog);

        // Act
        var result = await service.ValidateControlIdAsync("INVALID-99");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateControlIdAsync_WithNoCatalog_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await service.ValidateControlIdAsync("AC-1");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private NistControlsService CreateService()
    {
        return new NistControlsService(
            _httpClient,
            _memoryCache,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            Options.Create(_options),
            _metricsService);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private NistCatalog CreateTestCatalog()
    {
        return new NistCatalog
        {
            Metadata = new CatalogMetadata
            {
                Title = "NIST SP 800-53 Rev. 5",
                LastModified = DateTime.UtcNow,
                Version = "5.1",
                OscalVersion = "1.0.0"
            },
            Groups = new List<ControlGroup>
            {
                new ControlGroup
                {
                    Id = "ac",
                    Title = "Access Control",
                    Controls = new List<NistControl>
                    {
                        new NistControl
                        {
                            Id = "AC-1",
                            Title = "Access Control Policy and Procedures",
                            Props = new List<ControlProperty>
                            {
                                new ControlProperty { Name = "label", Value = "AC-1", Class = "SP800-53" }
                            }
                        },
                        new NistControl
                        {
                            Id = "AC-2",
                            Title = "Account Management",
                            Props = new List<ControlProperty>
                            {
                                new ControlProperty { Name = "label", Value = "AC-2", Class = "SP800-53" }
                            }
                        }
                    }
                }
            }
        };
    }

    #endregion
}
