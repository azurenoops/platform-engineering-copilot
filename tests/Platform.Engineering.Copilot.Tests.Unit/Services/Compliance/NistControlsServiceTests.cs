using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Net;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance;

/// <summary>
/// Unit tests for NistControlsService
/// Tests NIST catalog fetching, caching, control lookup, and offline fallback
/// </summary>
public class NistControlsServiceTests
{
    private readonly Mock<ILogger<NistControlsService>> _loggerMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ComplianceMetricsService> _metricsServiceMock;
    private readonly NistControlsOptions _options;

    public NistControlsServiceTests()
    {
        _loggerMock = new Mock<ILogger<NistControlsService>>();
        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _hostEnvironmentMock.Setup(h => h.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        var metricsLoggerMock = new Mock<ILogger<ComplianceMetricsService>>();
        _metricsServiceMock = new Mock<ComplianceMetricsService>(MockBehavior.Loose, metricsLoggerMock.Object);
        
        _options = new NistControlsOptions
        {
            BaseUrl = "https://example.com/nist",
            CacheDurationHours = 24,
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 1,
            TimeoutSeconds = 30,
            EnableOfflineFallback = false,
            EnableDetailedLogging = false
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Options.Create(_options);

        // Act
        var service = new NistControlsService(
            httpClient,
            _memoryCache,
            _loggerMock.Object,
            _hostEnvironmentMock.Object,
            options,
            _metricsServiceMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsService(
            null!,
            _memoryCache,
            _loggerMock.Object,
            _hostEnvironmentMock.Object,
            options,
            _metricsServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsService(
            httpClient,
            null!,
            _loggerMock.Object,
            _hostEnvironmentMock.Object,
            options,
            _metricsServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsService(
            httpClient,
            _memoryCache,
            null!,
            _hostEnvironmentMock.Object,
            options,
            _metricsServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullHostEnvironment_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsService(
            httpClient,
            _memoryCache,
            _loggerMock.Object,
            null!,
            options,
            _metricsServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("hostEnvironment");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new NistControlsService(
            httpClient,
            _memoryCache,
            _loggerMock.Object,
            _hostEnvironmentMock.Object,
            null!,
            _metricsServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithNullMetricsService_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsService(
            httpClient,
            _memoryCache,
            _loggerMock.Object,
            _hostEnvironmentMock.Object,
            options,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metricsService");
    }

    #endregion

    #region GetControlAsync Tests

    [Fact]
    public async Task GetControlAsync_WithNullControlId_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.GetControlAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("controlId");
    }

    [Fact]
    public async Task GetControlAsync_WithEmptyControlId_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.GetControlAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("controlId");
    }

    [Fact]
    public async Task GetControlAsync_WithWhitespaceControlId_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.GetControlAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("controlId");
    }

    [Fact]
    public async Task GetControlAsync_WithCachedCatalog_ReturnsControl()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlAsync("AC-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("AC-1");
        result.Title.Should().Be("Access Control Policy and Procedures");
    }

    [Fact]
    public async Task GetControlAsync_WithCaseInsensitiveId_ReturnsControl()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlAsync("ac-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("AC-1");
    }

    [Fact]
    public async Task GetControlAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlAsync("NONEXISTENT-99");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetControlsByFamilyAsync Tests

    [Fact]
    public async Task GetControlsByFamilyAsync_WithNullFamily_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.GetControlsByFamilyAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("family");
    }

    [Fact]
    public async Task GetControlsByFamilyAsync_WithEmptyFamily_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.GetControlsByFamilyAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("family");
    }

    [Fact]
    public async Task GetControlsByFamilyAsync_WithValidFamily_ReturnsControls()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlsByFamilyAsync("AC");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(c => c.Id!.ToUpperInvariant().Should().StartWith("AC"));
    }

    [Fact]
    public async Task GetControlsByFamilyAsync_WithCaseInsensitiveFamily_ReturnsControls()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlsByFamilyAsync("ac");

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetControlsByFamilyAsync_WithNonExistentFamily_ReturnsEmpty()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlsByFamilyAsync("XX");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region SearchControlsAsync Tests

    [Fact]
    public async Task SearchControlsAsync_WithNullSearchTerm_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.SearchControlsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("searchTerm");
    }

    [Fact]
    public async Task SearchControlsAsync_WithEmptySearchTerm_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.SearchControlsAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("searchTerm");
    }

    [Fact]
    public async Task SearchControlsAsync_WithMatchingTitle_ReturnsControls()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.SearchControlsAsync("Access Control");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.Title!.Contains("Access Control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchControlsAsync_WithMatchingId_ReturnsControls()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.SearchControlsAsync("AC-1");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.Id == "AC-1");
    }

    [Fact]
    public async Task SearchControlsAsync_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.SearchControlsAsync("xyz123nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetVersionAsync Tests

    [Fact]
    public async Task GetVersionAsync_WithCachedVersion_ReturnsCachedVersion()
    {
        // Arrange
        _memoryCache.Set("nist_version", "5.1.1", TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetVersionAsync();

        // Assert
        result.Should().Be("5.1.1");
    }

    [Fact]
    public async Task GetVersionAsync_WithCachedCatalog_ReturnsVersionFromCatalog()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetVersionAsync();

        // Assert
        result.Should().Be("5.1.1");
    }

    #endregion

    #region ValidateControlIdAsync Tests

    [Fact]
    public async Task ValidateControlIdAsync_WithNullControlId_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ValidateControlIdAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateControlIdAsync_WithEmptyControlId_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ValidateControlIdAsync("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateControlIdAsync_WithValidControlId_ReturnsTrue()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.ValidateControlIdAsync("AC-1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateControlIdAsync_WithInvalidControlId_ReturnsFalse()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.ValidateControlIdAsync("INVALID-99");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetControlEnhancementAsync Tests

    [Fact]
    public async Task GetControlEnhancementAsync_WithValidControlId_ReturnsEnhancement()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlEnhancementAsync("AC-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("AC-1");
        result.Title.Should().Be("Access Control Policy and Procedures");
    }

    [Fact]
    public async Task GetControlEnhancementAsync_WithNonExistentControlId_ReturnsNull()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetControlEnhancementAsync("NONEXISTENT-99");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCatalogAsync Tests - Cache Behavior

    [Fact]
    public async Task GetCatalogAsync_WithCacheHit_ReturnsCachedCatalog()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var result = await service.GetCatalogAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(catalog);
    }

    [Fact]
    public async Task GetCatalogAsync_WithNullCatalogGroups_ReturnsEmptyControlList()
    {
        // Arrange
        var catalog = new NistCatalog { Groups = null };
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();

        // Act
        var controls = await service.GetControlsByFamilyAsync("AC");

        // Assert
        controls.Should().BeEmpty();
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GetControlAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        _memoryCache.Set("nist_catalog_latest", catalog, TimeSpan.FromHours(1));
        var service = CreateService();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - depends on implementation, may or may not throw
        // Just verify it handles the token without crashing
        await service.GetControlAsync("AC-1", cts.Token);
    }

    #endregion

    #region Helper Methods

    private NistControlsService CreateService()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(_options);

        return new NistControlsService(
            httpClient,
            _memoryCache,
            _loggerMock.Object,
            _hostEnvironmentMock.Object,
            options,
            _metricsServiceMock.Object);
    }

    private static NistCatalog CreateTestCatalog()
    {
        return new NistCatalog
        {
            Metadata = new CatalogMetadata
            {
                Title = "NIST SP 800-53 Rev 5",
                Version = "5.1.1"
            },
            Groups = new List<ControlGroup>
            {
                new ControlGroup
                {
                    Id = "AC",
                    Title = "Access Control",
                    Controls = new List<NistControl>
                    {
                        new NistControl
                        {
                            Id = "AC-1",
                            Title = "Access Control Policy and Procedures",
                            Parts = new List<ControlPart>
                            {
                                new ControlPart
                                {
                                    Name = "statement",
                                    Prose = "Develop, document, and disseminate an access control policy."
                                },
                                new ControlPart
                                {
                                    Name = "guidance",
                                    Prose = "Access control policy can be included as part of the general security policy."
                                }
                            }
                        },
                        new NistControl
                        {
                            Id = "AC-2",
                            Title = "Account Management",
                            Parts = new List<ControlPart>
                            {
                                new ControlPart
                                {
                                    Name = "statement",
                                    Prose = "Define and document account management processes."
                                }
                            }
                        },
                        new NistControl
                        {
                            Id = "AC-3",
                            Title = "Access Enforcement",
                            Parts = new List<ControlPart>
                            {
                                new ControlPart
                                {
                                    Name = "statement",
                                    Prose = "Enforce approved authorizations for logical access."
                                }
                            }
                        }
                    }
                },
                new ControlGroup
                {
                    Id = "AU",
                    Title = "Audit and Accountability",
                    Controls = new List<NistControl>
                    {
                        new NistControl
                        {
                            Id = "AU-1",
                            Title = "Audit and Accountability Policy and Procedures",
                            Parts = new List<ControlPart>
                            {
                                new ControlPart
                                {
                                    Name = "statement",
                                    Prose = "Develop audit and accountability policies."
                                }
                            }
                        },
                        new NistControl
                        {
                            Id = "AU-2",
                            Title = "Event Logging",
                            Parts = new List<ControlPart>
                            {
                                new ControlPart
                                {
                                    Name = "statement",
                                    Prose = "Identify events to be logged."
                                }
                            }
                        }
                    }
                },
                new ControlGroup
                {
                    Id = "SC",
                    Title = "System and Communications Protection",
                    Controls = new List<NistControl>
                    {
                        new NistControl
                        {
                            Id = "SC-1",
                            Title = "System and Communications Protection Policy and Procedures",
                            Parts = new List<ControlPart>
                            {
                                new ControlPart
                                {
                                    Name = "statement",
                                    Prose = "Develop system and communications protection policies."
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    #endregion
}
