using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for AtoComplianceEngine
/// Tests comprehensive ATO compliance assessment, evidence collection, risk assessment, and monitoring
/// </summary>
public class AtoComplianceEngineTests
{
    private readonly Mock<ILogger<AtoComplianceEngine>> _loggerMock;
    private readonly Mock<INistControlsService> _nistControlsServiceMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<ComplianceMetricsService> _metricsServiceMock;
    private readonly Mock<IAssessmentService> _assessmentServiceMock;
    private readonly Mock<IRmfKnowledgeService> _rmfKnowledgeServiceMock;
    private readonly Mock<IStigKnowledgeService> _stigKnowledgeServiceMock;
    private readonly Mock<IDoDInstructionService> _dodInstructionServiceMock;
    private readonly Mock<IDoDWorkflowService> _dodWorkflowServiceMock;
    private readonly Mock<IDefenderForCloudService> _defenderForCloudServiceMock;
    private readonly Mock<IEvidenceStorageService> _evidenceStorageServiceMock;
    private readonly Mock<IStigValidationService> _stigValidationServiceMock;
    private readonly IOptions<ComplianceAgentOptions> _options;
    private readonly AtoComplianceEngine _engine;

    public AtoComplianceEngineTests()
    {
        _loggerMock = new Mock<ILogger<AtoComplianceEngine>>();
        _nistControlsServiceMock = new Mock<INistControlsService>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
        _memoryCacheMock = new Mock<IMemoryCache>();
        _metricsServiceMock = new Mock<ComplianceMetricsService>(MockBehavior.Loose, new Mock<ILogger<ComplianceMetricsService>>().Object);
        _assessmentServiceMock = new Mock<IAssessmentService>();
        _rmfKnowledgeServiceMock = new Mock<IRmfKnowledgeService>();
        _stigKnowledgeServiceMock = new Mock<IStigKnowledgeService>();
        _dodInstructionServiceMock = new Mock<IDoDInstructionService>();
        _dodWorkflowServiceMock = new Mock<IDoDWorkflowService>();
        _defenderForCloudServiceMock = new Mock<IDefenderForCloudService>();
        _evidenceStorageServiceMock = new Mock<IEvidenceStorageService>();
        _stigValidationServiceMock = new Mock<IStigValidationService>();

        var complianceOptions = new ComplianceAgentOptions
        {
            EnableAutomatedRemediation = true
        };
        _options = Options.Create(complianceOptions);

        // Setup default mocks
        SetupDefaultMocks();

        _engine = new AtoComplianceEngine(
            _loggerMock.Object,
            _nistControlsServiceMock.Object,
            _azureResourceServiceMock.Object,
            _memoryCacheMock.Object,
            _metricsServiceMock.Object,
            _options,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);
    }

    private void SetupDefaultMocks()
    {
        // Setup cache mock to return false for TryGetValue (cache miss)
        object? cachedValue = null;
        _memoryCacheMock
            .Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue!))
            .Returns(false);

        // Setup cache CreateEntry
        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(e => e.Value);
        cacheEntry.SetupProperty(e => e.AbsoluteExpiration);
        cacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(e => e.SlidingExpiration);
        cacheEntry.SetupProperty(e => e.Size);
        cacheEntry.Setup(e => e.ExpirationTokens).Returns(new List<Microsoft.Extensions.Primitives.IChangeToken>());
        cacheEntry.Setup(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());

        _memoryCacheMock
            .Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntry.Object);

        // Setup NIST controls service to return empty list by default
        _nistControlsServiceMock
            .Setup(x => x.GetControlsByFamilyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>());

        // Setup Azure resource service to return empty enumerable
        _azureResourceServiceMock
            .Setup(x => x.ListAllResourceGroupsInSubscriptionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<AzureResource>());

        // Setup STIG validation service to return empty list
        _stigValidationServiceMock
            .Setup(x => x.ValidateFamilyStigsAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AtoFinding>());
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Assert
        _engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoComplianceEngine(
            null!,
            _nistControlsServiceMock.Object,
            _azureResourceServiceMock.Object,
            _memoryCacheMock.Object,
            _metricsServiceMock.Object,
            _options,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullNistControlsService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoComplianceEngine(
            _loggerMock.Object,
            null!,
            _azureResourceServiceMock.Object,
            _memoryCacheMock.Object,
            _metricsServiceMock.Object,
            _options,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nistControlsService");
    }

    [Fact]
    public void Constructor_WithNullAzureResourceService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoComplianceEngine(
            _loggerMock.Object,
            _nistControlsServiceMock.Object,
            null!,
            _memoryCacheMock.Object,
            _metricsServiceMock.Object,
            _options,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("azureResourceService");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoComplianceEngine(
            _loggerMock.Object,
            _nistControlsServiceMock.Object,
            _azureResourceServiceMock.Object,
            null!,
            _metricsServiceMock.Object,
            _options,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullMetricsService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoComplianceEngine(
            _loggerMock.Object,
            _nistControlsServiceMock.Object,
            _azureResourceServiceMock.Object,
            _memoryCacheMock.Object,
            null!,
            _options,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metricsService");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoComplianceEngine(
            _loggerMock.Object,
            _nistControlsServiceMock.Object,
            _azureResourceServiceMock.Object,
            _memoryCacheMock.Object,
            _metricsServiceMock.Object,
            null!,
            _assessmentServiceMock.Object,
            _rmfKnowledgeServiceMock.Object,
            _stigKnowledgeServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _defenderForCloudServiceMock.Object,
            _evidenceStorageServiceMock.Object,
            _stigValidationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    #endregion

    #region RunComprehensiveAssessmentAsync Tests

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithValidSubscription_ReturnsAssessment()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.AssessmentId.Should().NotBeNullOrEmpty();
        result.ControlFamilyResults.Should().NotBeNull();
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithResourceGroupName_ScopesAssessment()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var resourceGroupName = "test-resource-group";

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId, resourceGroupName);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var progressReports = new List<AssessmentProgress>();
        var progress = new Progress<AssessmentProgress>(p => progressReports.Add(p));

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId, progress);

        // Assert
        result.Should().NotBeNull();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Message != null && p.Message.Contains("Starting"));
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _engine.RunComprehensiveAssessmentAsync(subscriptionId, null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithFindings_CalculatesComplianceScore()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controls = new List<NistControl>
        {
            new NistControl { Id = "AC-1", Title = "Access Control Policy" },
            new NistControl { Id = "AC-2", Title = "Account Management" }
        };

        _nistControlsServiceMock
            .Setup(x => x.GetControlsByFamilyAsync("AC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(controls);

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.OverallComplianceScore.Should().BeGreaterThanOrEqualTo(0);
        result.OverallComplianceScore.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_RecordsTimingMetrics()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().BeBefore(result.EndTime);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_GeneratesExecutiveSummary()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.ExecutiveSummary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_CalculatesRiskProfile()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.RiskProfile.Should().NotBeNull();
        result.RiskProfile!.RiskLevel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_CountsFindingsBySeverity()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.RunComprehensiveAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.CriticalFindings.Should().BeGreaterThanOrEqualTo(0);
        result.HighFindings.Should().BeGreaterThanOrEqualTo(0);
        result.MediumFindings.Should().BeGreaterThanOrEqualTo(0);
        result.LowFindings.Should().BeGreaterThanOrEqualTo(0);
        result.InformationalFindings.Should().BeGreaterThanOrEqualTo(0);
        result.TotalFindings.Should().Be(
            result.CriticalFindings + result.HighFindings + result.MediumFindings + 
            result.LowFindings + result.InformationalFindings);
    }

    #endregion

    #region GetContinuousComplianceStatusAsync Tests

    [Fact]
    public async Task GetContinuousComplianceStatusAsync_WithValidSubscription_ReturnsStatus()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        
        _assessmentServiceMock
            .Setup(x => x.GetMonitoredControlsAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitoredControl>());

        // Act
        var result = await _engine.GetContinuousComplianceStatusAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetContinuousComplianceStatusAsync_ReturnsMonitoringStatus()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        
        _assessmentServiceMock
            .Setup(x => x.GetMonitoredControlsAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitoredControl>());

        // Act
        var result = await _engine.GetContinuousComplianceStatusAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.MonitoringEnabled.Should().BeTrue();
        result.ControlStatuses.Should().NotBeNull();
    }

    [Fact]
    public async Task GetContinuousComplianceStatusAsync_CalculatesComplianceDrift()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        
        _assessmentServiceMock
            .Setup(x => x.GetMonitoredControlsAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitoredControl>());

        // Act
        var result = await _engine.GetContinuousComplianceStatusAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.ComplianceDriftPercentage.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region CollectComplianceEvidenceAsync Tests

    [Fact]
    public async Task CollectComplianceEvidenceAsync_WithValidInputs_ReturnsEvidencePackage()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "AC";
        var collectedBy = "test-user@example.com";

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, collectedBy);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.ControlFamily.Should().Be(controlFamily);
        result.CollectedBy.Should().Be(collectedBy);
        result.PackageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_WithAllControlFamily_CollectsFromAllFamilies()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "All";
        var collectedBy = "test-user@example.com";

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, collectedBy);

        // Assert
        result.Should().NotBeNull();
        result.ControlFamily.Should().Be(controlFamily);
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "AC";
        var collectedBy = "test-user@example.com";
        var progressReports = new List<EvidenceCollectionProgress>();
        var progress = new Progress<EvidenceCollectionProgress>(p => progressReports.Add(p));

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(
            subscriptionId, controlFamily, collectedBy, progress);

        // Assert
        result.Should().NotBeNull();
        progressReports.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_RecordsCollectionTiming()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "AC";
        var collectedBy = "test-user@example.com";

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, collectedBy);

        // Assert
        result.Should().NotBeNull();
        result.CollectionStartTime.Should().BeBefore(result.CollectionEndTime);
        result.CollectionDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_CalculatesCompletenessScore()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "AC";
        var collectedBy = "test-user@example.com";

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, collectedBy);

        // Assert
        result.Should().NotBeNull();
        result.CompletenessScore.Should().BeGreaterThanOrEqualTo(0);
        result.CompletenessScore.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_GeneratesAttestationStatement()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "AC";
        var collectedBy = "test-user@example.com";

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, collectedBy);

        // Assert
        result.Should().NotBeNull();
        result.AttestationStatement.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_WithUnknownFamily_UsesDefaultCollector()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var controlFamily = "UNKNOWN-FAMILY";
        var collectedBy = "test-user@example.com";

        // Act
        var result = await _engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, collectedBy);

        // Assert
        result.Should().NotBeNull();
        result.ControlFamily.Should().Be(controlFamily);
    }

    #endregion

    #region GetComplianceTimelineAsync Tests

    [Fact]
    public async Task GetComplianceTimelineAsync_WithValidDateRange_ReturnsTimeline()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetComplianceTimelineAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
    }

    [Fact]
    public async Task GetComplianceTimelineAsync_GeneratesDataPoints()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetComplianceTimelineAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.DataPoints.Should().NotBeNull();
    }

    [Fact]
    public async Task GetComplianceTimelineAsync_CalculatesTrends()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetComplianceTimelineAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Trends.Should().NotBeNull();
    }

    [Fact]
    public async Task GetComplianceTimelineAsync_IdentifiesSignificantEvents()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetComplianceTimelineAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.SignificantEvents.Should().NotBeNull();
    }

    [Fact]
    public async Task GetComplianceTimelineAsync_GeneratesInsights()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetComplianceTimelineAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Insights.Should().NotBeNull();
    }

    #endregion

    #region PerformRiskAssessmentAsync Tests

    [Fact]
    public async Task PerformRiskAssessmentAsync_WithValidSubscription_ReturnsRiskAssessment()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.AssessmentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PerformRiskAssessmentAsync_AssessesAllRiskCategories()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.RiskCategories.Should().NotBeEmpty();
        result.RiskCategories.Should().ContainKey("Data Protection");
        result.RiskCategories.Should().ContainKey("Access Control");
        result.RiskCategories.Should().ContainKey("Network Security");
    }

    [Fact]
    public async Task PerformRiskAssessmentAsync_CalculatesOverallRiskScore()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.OverallRiskScore.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PerformRiskAssessmentAsync_DeterminesRiskLevel()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevelString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PerformRiskAssessmentAsync_IdentifiesTopRisks()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.TopRisks.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformRiskAssessmentAsync_GeneratesMitigationRecommendations()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.MitigationRecommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformRiskAssessmentAsync_GeneratesExecutiveSummary()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.PerformRiskAssessmentAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.ExecutiveSummary.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GenerateComplianceCertificateAsync Tests

    [Fact]
    public async Task GenerateComplianceCertificateAsync_WithHighComplianceScore_ReturnsCertificate()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var latestAssessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            OverallComplianceScore = 95,
            ControlFamilyResults = new Dictionary<string, ControlFamilyAssessment>
            {
                ["AC"] = new ControlFamilyAssessment
                {
                    ControlFamily = "AC",
                    FamilyName = "Access Control",
                    ComplianceScore = 95,
                    Findings = new List<AtoFinding>()
                }
            }
        };

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestAssessment);

        // Act
        var result = await _engine.GenerateComplianceCertificateAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.CertificateId.Should().NotBeNullOrEmpty();
        result.ComplianceScore.Should().Be(95);
    }

    [Fact]
    public async Task GenerateComplianceCertificateAsync_WithLowComplianceScore_ThrowsInvalidOperationException()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var latestAssessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            OverallComplianceScore = 60, // Below 80% threshold
            ControlFamilyResults = new Dictionary<string, ControlFamilyAssessment>()
        };

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestAssessment);

        // Act
        var act = () => _engine.GenerateComplianceCertificateAsync(subscriptionId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Compliance score*below required 80%*");
    }

    [Fact]
    public async Task GenerateComplianceCertificateAsync_WithNoAssessment_ThrowsInvalidOperationException()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AtoComplianceAssessment?)null);

        // Act
        var act = () => _engine.GenerateComplianceCertificateAsync(subscriptionId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateComplianceCertificateAsync_SetsValidUntilDate()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var latestAssessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            OverallComplianceScore = 85,
            ControlFamilyResults = new Dictionary<string, ControlFamilyAssessment>()
        };

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestAssessment);

        // Act
        var result = await _engine.GenerateComplianceCertificateAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.ValidUntil.Should().BeAfter(DateTimeOffset.UtcNow);
        result.ValidUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMonths(6), TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task GenerateComplianceCertificateAsync_GeneratesVerificationHash()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var latestAssessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            OverallComplianceScore = 85,
            ControlFamilyResults = new Dictionary<string, ControlFamilyAssessment>()
        };

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestAssessment);

        // Act
        var result = await _engine.GenerateComplianceCertificateAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.VerificationHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateComplianceCertificateAsync_IncludesAttestations()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var latestAssessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            OverallComplianceScore = 90,
            ControlFamilyResults = new Dictionary<string, ControlFamilyAssessment>
            {
                ["AC"] = new ControlFamilyAssessment
                {
                    ControlFamily = "AC",
                    FamilyName = "Access Control",
                    ComplianceScore = 90,
                    Findings = new List<AtoFinding>()
                },
                ["AU"] = new ControlFamilyAssessment
                {
                    ControlFamily = "AU",
                    FamilyName = "Audit and Accountability",
                    ComplianceScore = 85,
                    Findings = new List<AtoFinding>()
                }
            }
        };

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestAssessment);

        // Act
        var result = await _engine.GenerateComplianceCertificateAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.Attestations.Should().NotBeEmpty();
        result.Attestations.Should().Contain(a => a.ControlFamily == "AC");
        result.Attestations.Should().Contain(a => a.ControlFamily == "AU");
    }

    #endregion

    #region GetLatestAssessmentAsync Tests

    [Fact]
    public async Task GetLatestAssessmentAsync_WithExistingAssessment_ReturnsAssessment()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var expectedAssessment = new AtoComplianceAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            OverallComplianceScore = 85
        };

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssessment);

        // Act
        var result = await _engine.GetLatestAssessmentAsync(subscriptionId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedAssessment);
    }

    [Fact]
    public async Task GetLatestAssessmentAsync_WithNoAssessment_ReturnsNull()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        _assessmentServiceMock
            .Setup(x => x.GetLatestCompletedAssessmentAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AtoComplianceAssessment?)null);

        // Act
        var result = await _engine.GetLatestAssessmentAsync(subscriptionId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
