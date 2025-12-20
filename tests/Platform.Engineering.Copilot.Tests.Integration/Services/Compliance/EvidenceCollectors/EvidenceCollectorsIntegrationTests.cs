using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Services.Compliance.EvidenceCollectors;

/// <summary>
/// Integration tests for all evidence collectors
/// Tests evidence collector behavior with realistic configurations and verifies proper output generation
/// </summary>
public class EvidenceCollectorsIntegrationTests : IClassFixture<EvidenceCollectorTestFixture>
{
    private readonly EvidenceCollectorTestFixture _fixture;

    public EvidenceCollectorsIntegrationTests(EvidenceCollectorTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region DefaultEvidenceCollector Integration Tests

    [Fact]
    public async Task DefaultEvidenceCollector_CollectAllEvidenceTypes_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<DefaultEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
        var logEvidence = await collector.CollectLogEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
        var metricEvidence = await collector.CollectMetricEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
        var policyEvidence = await collector.CollectPolicyEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
        var accessEvidence = await collector.CollectAccessControlEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        logEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        metricEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        policyEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        accessEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region AccessControlEvidenceCollector Integration Tests

    [Fact]
    public async Task AccessControlEvidenceCollector_CollectAllEvidenceTypes_ForACFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<AccessControlEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
        var logEvidence = await collector.CollectLogEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
        var accessEvidence = await collector.CollectAccessControlEvidenceAsync(
            _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        logEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        accessEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region AuditEvidenceCollector Integration Tests

    [Fact]
    public async Task AuditEvidenceCollector_CollectAllEvidenceTypes_ForAUFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<AuditEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "AU", _fixture.TestCollectedBy);
        var logEvidence = await collector.CollectLogEvidenceAsync(
            _fixture.TestSubscriptionId, "AU", _fixture.TestCollectedBy);
        var metricEvidence = await collector.CollectMetricEvidenceAsync(
            _fixture.TestSubscriptionId, "AU", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        logEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        metricEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region ConfigurationManagementEvidenceCollector Integration Tests

    [Fact]
    public async Task ConfigurationManagementEvidenceCollector_CollectAllEvidenceTypes_ForCMFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<ConfigurationManagementEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "CM", _fixture.TestCollectedBy);
        var policyEvidence = await collector.CollectPolicyEvidenceAsync(
            _fixture.TestSubscriptionId, "CM", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        policyEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region ContingencyPlanningEvidenceCollector Integration Tests

    [Fact]
    public async Task ContingencyPlanningEvidenceCollector_CollectAllEvidenceTypes_ForCPFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<ContingencyPlanningEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "CP", _fixture.TestCollectedBy);
        var metricEvidence = await collector.CollectMetricEvidenceAsync(
            _fixture.TestSubscriptionId, "CP", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        metricEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region IdentificationAuthenticationEvidenceCollector Integration Tests

    [Fact]
    public async Task IdentificationAuthenticationEvidenceCollector_CollectAllEvidenceTypes_ForIAFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<IdentificationAuthenticationEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "IA", _fixture.TestCollectedBy);
        var accessEvidence = await collector.CollectAccessControlEvidenceAsync(
            _fixture.TestSubscriptionId, "IA", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        accessEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region IncidentResponseEvidenceCollector Integration Tests

    [Fact]
    public async Task IncidentResponseEvidenceCollector_CollectAllEvidenceTypes_ForIRFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<IncidentResponseEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "IR", _fixture.TestCollectedBy);
        var logEvidence = await collector.CollectLogEvidenceAsync(
            _fixture.TestSubscriptionId, "IR", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        logEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region RiskAssessmentEvidenceCollector Integration Tests

    [Fact]
    public async Task RiskAssessmentEvidenceCollector_CollectAllEvidenceTypes_ForRAFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<RiskAssessmentEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "RA", _fixture.TestCollectedBy);
        var metricEvidence = await collector.CollectMetricEvidenceAsync(
            _fixture.TestSubscriptionId, "RA", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        metricEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region SecurityAssessmentEvidenceCollector Integration Tests

    [Fact]
    public async Task SecurityAssessmentEvidenceCollector_CollectAllEvidenceTypes_ForCAFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<SecurityAssessmentEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "CA", _fixture.TestCollectedBy);
        var policyEvidence = await collector.CollectPolicyEvidenceAsync(
            _fixture.TestSubscriptionId, "CA", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        policyEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region SecurityEvidenceCollector Integration Tests

    [Fact]
    public async Task SecurityEvidenceCollector_CollectAllEvidenceTypes_ForSCFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<SecurityEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "SC", _fixture.TestCollectedBy);
        var policyEvidence = await collector.CollectPolicyEvidenceAsync(
            _fixture.TestSubscriptionId, "SC", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        policyEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region SystemIntegrityEvidenceCollector Integration Tests

    [Fact]
    public async Task SystemIntegrityEvidenceCollector_CollectAllEvidenceTypes_ForSIFamily_ReturnsValidLists()
    {
        // Arrange
        var collector = _fixture.GetCollector<SystemIntegrityEvidenceCollector>();

        // Act
        var configEvidence = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, "SI", _fixture.TestCollectedBy);
        var metricEvidence = await collector.CollectMetricEvidenceAsync(
            _fixture.TestSubscriptionId, "SI", _fixture.TestCollectedBy);
        var logEvidence = await collector.CollectLogEvidenceAsync(
            _fixture.TestSubscriptionId, "SI", _fixture.TestCollectedBy);

        // Assert
        configEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        metricEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
        logEvidence.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>();
    }

    #endregion

    #region Multi-Collector Workflow Tests

    [Fact]
    public async Task AllCollectors_CollectConfigurationEvidence_ProduceConsistentResultTypes()
    {
        // Arrange
        var collectorTypes = new[]
        {
            typeof(DefaultEvidenceCollector),
            typeof(AccessControlEvidenceCollector),
            typeof(AuditEvidenceCollector),
            typeof(ConfigurationManagementEvidenceCollector),
            typeof(ContingencyPlanningEvidenceCollector),
            typeof(IdentificationAuthenticationEvidenceCollector),
            typeof(IncidentResponseEvidenceCollector),
            typeof(RiskAssessmentEvidenceCollector),
            typeof(SecurityAssessmentEvidenceCollector),
            typeof(SecurityEvidenceCollector),
            typeof(SystemIntegrityEvidenceCollector)
        };

        // Act & Assert
        foreach (var collectorType in collectorTypes)
        {
            var collector = _fixture.GetCollector(collectorType);
            var result = await collector.CollectConfigurationEvidenceAsync(
                _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy);
            
            result.Should().NotBeNull($"Collector {collectorType.Name} should return non-null results");
            result.Should().BeOfType<List<ComplianceEvidence>>($"Collector {collectorType.Name} should return List<ComplianceEvidence>");
        }
    }

    [Fact]
    public async Task AllCollectors_WithCancellation_HandleGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        
        var collectorTypes = new[]
        {
            typeof(DefaultEvidenceCollector),
            typeof(AccessControlEvidenceCollector),
            typeof(ConfigurationManagementEvidenceCollector),
            typeof(SystemIntegrityEvidenceCollector)
        };

        // Act & Assert - Should complete successfully before cancellation
        foreach (var collectorType in collectorTypes)
        {
            var collector = _fixture.GetCollector(collectorType);
            var result = await collector.CollectConfigurationEvidenceAsync(
                _fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy, cts.Token);
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AllCollectors_CanHandleConcurrentCollection()
    {
        // Arrange
        var controlFamilies = new[] { "AC", "AU", "CM", "SC", "SI" };
        var collector = _fixture.GetCollector<DefaultEvidenceCollector>();

        // Act - Run collections concurrently
        var tasks = controlFamilies.Select(cf => 
            collector.CollectConfigurationEvidenceAsync(_fixture.TestSubscriptionId, cf, _fixture.TestCollectedBy));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    [Fact]
    public async Task AllCollectors_CollectAllEvidenceTypes_ForSameFamily_ReturnConsistentResults()
    {
        // Arrange
        var collector = _fixture.GetCollector<DefaultEvidenceCollector>();
        var controlFamily = "AC";

        // Act
        var configTask = collector.CollectConfigurationEvidenceAsync(_fixture.TestSubscriptionId, controlFamily, _fixture.TestCollectedBy);
        var logTask = collector.CollectLogEvidenceAsync(_fixture.TestSubscriptionId, controlFamily, _fixture.TestCollectedBy);
        var metricTask = collector.CollectMetricEvidenceAsync(_fixture.TestSubscriptionId, controlFamily, _fixture.TestCollectedBy);
        var policyTask = collector.CollectPolicyEvidenceAsync(_fixture.TestSubscriptionId, controlFamily, _fixture.TestCollectedBy);
        var accessTask = collector.CollectAccessControlEvidenceAsync(_fixture.TestSubscriptionId, controlFamily, _fixture.TestCollectedBy);

        var results = await Task.WhenAll(configTask, logTask, metricTask, policyTask, accessTask);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r => r.Should().NotBeNull().And.BeOfType<List<ComplianceEvidence>>());
    }

    [Fact]
    public async Task MultipleCollectors_CollectingInParallel_DoNotInterfere()
    {
        // Arrange
        var collectors = new IEvidenceCollector[]
        {
            _fixture.GetCollector<DefaultEvidenceCollector>(),
            _fixture.GetCollector<AccessControlEvidenceCollector>(),
            _fixture.GetCollector<AuditEvidenceCollector>(),
            _fixture.GetCollector<ConfigurationManagementEvidenceCollector>()
        };

        // Act - Run all collectors in parallel
        var tasks = collectors.Select(c => 
            c.CollectConfigurationEvidenceAsync(_fixture.TestSubscriptionId, "AC", _fixture.TestCollectedBy));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(4);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region Control Family Coverage Tests

    [Theory]
    [InlineData("AC", "Access Control")]
    [InlineData("AU", "Audit and Accountability")]
    [InlineData("CM", "Configuration Management")]
    [InlineData("CP", "Contingency Planning")]
    [InlineData("IA", "Identification and Authentication")]
    [InlineData("IR", "Incident Response")]
    [InlineData("RA", "Risk Assessment")]
    [InlineData("CA", "Security Assessment and Authorization")]
    [InlineData("SC", "System and Communications Protection")]
    [InlineData("SI", "System and Information Integrity")]
    public async Task DefaultCollector_CollectsEvidence_ForAllControlFamilies(string familyId, string familyName)
    {
        // Arrange
        var collector = _fixture.GetCollector<DefaultEvidenceCollector>();

        // Act
        var result = await collector.CollectConfigurationEvidenceAsync(
            _fixture.TestSubscriptionId, familyId, _fixture.TestCollectedBy);

        // Assert
        result.Should().NotBeNull($"Should collect evidence for {familyName} ({familyId})");
    }

    #endregion
}

/// <summary>
/// Test fixture providing evidence collector instances with properly configured dependencies
/// </summary>
public class EvidenceCollectorTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    public string TestSubscriptionId { get; } = "test-subscription-00000000-0000-0000-0000-000000000000";
    public string TestCollectedBy { get; } = "integration-test-collector";

    public EvidenceCollectorTestFixture()
    {
        var services = new ServiceCollection();
        
        // Add logging with factory and register non-generic ILogger
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        
        // Register non-generic ILogger for collectors that use ILogger instead of ILogger<T>
        services.AddSingleton<ILogger>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("EvidenceCollector"));

        // Configure mocked Azure service
        var azureServiceMock = new Mock<IAzureResourceService>();
        services.AddSingleton(azureServiceMock.Object);

        // Configure mocked Defender service
        var defenderServiceMock = new Mock<IDefenderForCloudService>();
        services.AddSingleton(defenderServiceMock.Object);

        // Register all evidence collectors
        services.AddTransient<DefaultEvidenceCollector>();
        services.AddTransient<AccessControlEvidenceCollector>();
        services.AddTransient<AuditEvidenceCollector>();
        services.AddTransient<ConfigurationManagementEvidenceCollector>();
        services.AddTransient<ContingencyPlanningEvidenceCollector>();
        services.AddTransient<IdentificationAuthenticationEvidenceCollector>();
        services.AddTransient<IncidentResponseEvidenceCollector>();
        services.AddTransient<RiskAssessmentEvidenceCollector>();
        services.AddTransient<SecurityAssessmentEvidenceCollector>();
        services.AddTransient<SecurityEvidenceCollector>();
        services.AddTransient<SystemIntegrityEvidenceCollector>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public T GetCollector<T>() where T : class, IEvidenceCollector
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public IEvidenceCollector GetCollector(Type collectorType)
    {
        return (IEvidenceCollector)_serviceProvider.GetRequiredService(collectorType);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
