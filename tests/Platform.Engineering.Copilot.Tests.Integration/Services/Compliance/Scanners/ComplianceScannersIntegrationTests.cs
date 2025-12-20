using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Services.Compliance.Scanners;

/// <summary>
/// Integration tests for all compliance scanners
/// Tests scanner behavior with realistic configurations and verifies proper output generation
/// </summary>
public class ComplianceScannersIntegrationTests : IClassFixture<ComplianceScannerTestFixture>
{
    private readonly ComplianceScannerTestFixture _fixture;

    public ComplianceScannersIntegrationTests(ComplianceScannerTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region DefaultComplianceScanner Integration Tests

    [Fact]
    public async Task DefaultComplianceScanner_ScanControlAsync_ReturnsValidFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<DefaultComplianceScanner>();
        var control = new NistControl { Id = "AC-1", Title = "Access Control Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task DefaultComplianceScanner_ScanControlWithResourceGroup_ReturnsValidFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<DefaultComplianceScanner>();
        var control = new NistControl { Id = "AC-1", Title = "Access Control Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, _fixture.TestResourceGroup, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    #endregion

    #region AccessControlScanner Integration Tests

    [Fact]
    public async Task AccessControlScanner_ScanControlAsync_ForACControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<AccessControlScanner>();
        var control = new NistControl { Id = "AC-2", Title = "Account Management" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task AccessControlScanner_ScanControlAsync_ForMultipleControls_ReturnsConsistentResults()
    {
        // Arrange
        var scanner = _fixture.GetScanner<AccessControlScanner>();
        var controls = new[]
        {
            new NistControl { Id = "AC-1", Title = "Access Control Policy and Procedures" },
            new NistControl { Id = "AC-2", Title = "Account Management" },
            new NistControl { Id = "AC-3", Title = "Access Enforcement" },
            new NistControl { Id = "AC-4", Title = "Information Flow Enforcement" },
            new NistControl { Id = "AC-5", Title = "Separation of Duties" }
        };

        // Act
        var results = new List<List<AtoFinding>>();
        foreach (var control in controls)
        {
            var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region AuditScanner Integration Tests

    [Fact]
    public async Task AuditScanner_ScanControlAsync_ForAUControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<AuditScanner>();
        var control = new NistControl { Id = "AU-1", Title = "Audit and Accountability Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task AuditScanner_ScanControlWithResourceGroup_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<AuditScanner>();
        var control = new NistControl { Id = "AU-2", Title = "Audit Events" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, _fixture.TestResourceGroup, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    #endregion

    #region ConfigurationManagementScanner Integration Tests

    [Fact]
    public async Task ConfigurationManagementScanner_ScanControlAsync_ForCMControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<ConfigurationManagementScanner>();
        var control = new NistControl { Id = "CM-1", Title = "Configuration Management Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task ConfigurationManagementScanner_ScanControlAsync_ForBaselineConfiguration_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<ConfigurationManagementScanner>();
        var control = new NistControl { Id = "CM-2", Title = "Baseline Configuration" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region ContingencyPlanningScanner Integration Tests

    [Fact]
    public async Task ContingencyPlanningScanner_ScanControlAsync_ForCPControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<ContingencyPlanningScanner>();
        var control = new NistControl { Id = "CP-1", Title = "Contingency Planning Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task ContingencyPlanningScanner_ScanControlAsync_ForBackupControls_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<ContingencyPlanningScanner>();
        var control = new NistControl { Id = "CP-9", Title = "System Backup" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region IdentificationAuthenticationScanner Integration Tests

    [Fact]
    public async Task IdentificationAuthenticationScanner_ScanControlAsync_ForIAControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<IdentificationAuthenticationScanner>();
        var control = new NistControl { Id = "IA-1", Title = "Identification and Authentication Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task IdentificationAuthenticationScanner_ScanControlAsync_ForMFAControls_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<IdentificationAuthenticationScanner>();
        var control = new NistControl { Id = "IA-2", Title = "Identification and Authentication (Organizational Users)" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region IncidentResponseScanner Integration Tests

    [Fact]
    public async Task IncidentResponseScanner_ScanControlAsync_ForIRControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<IncidentResponseScanner>();
        var control = new NistControl { Id = "IR-1", Title = "Incident Response Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task IncidentResponseScanner_ScanControlAsync_ForIncidentMonitoring_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<IncidentResponseScanner>();
        var control = new NistControl { Id = "IR-5", Title = "Incident Monitoring" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region RiskAssessmentScanner Integration Tests

    [Fact]
    public async Task RiskAssessmentScanner_ScanControlAsync_ForRAControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<RiskAssessmentScanner>();
        var control = new NistControl { Id = "RA-1", Title = "Risk Assessment Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task RiskAssessmentScanner_ScanControlAsync_ForVulnerabilityScanning_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<RiskAssessmentScanner>();
        var control = new NistControl { Id = "RA-5", Title = "Vulnerability Monitoring and Scanning" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region SecurityAssessmentScanner Integration Tests

    [Fact]
    public async Task SecurityAssessmentScanner_ScanControlAsync_ForCAControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<SecurityAssessmentScanner>();
        var control = new NistControl { Id = "CA-1", Title = "Security Assessment and Authorization Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task SecurityAssessmentScanner_ScanControlAsync_ForContinuousMonitoring_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<SecurityAssessmentScanner>();
        var control = new NistControl { Id = "CA-7", Title = "Continuous Monitoring" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region SystemCommunicationScanner Integration Tests

    [Fact]
    public async Task SystemCommunicationScanner_ScanControlAsync_ForSCControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<SystemCommunicationScanner>();
        var control = new NistControl { Id = "SC-1", Title = "System and Communications Protection Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task SystemCommunicationScanner_ScanControlAsync_ForEncryptionControls_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<SystemCommunicationScanner>();
        var control = new NistControl { Id = "SC-13", Title = "Cryptographic Protection" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region SystemIntegrityScanner Integration Tests

    [Fact]
    public async Task SystemIntegrityScanner_ScanControlAsync_ForSIControlFamily_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<SystemIntegrityScanner>();
        var control = new NistControl { Id = "SI-1", Title = "System and Information Integrity Policy and Procedures" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Fact]
    public async Task SystemIntegrityScanner_ScanControlAsync_ForMalwareProtection_ReturnsFindings()
    {
        // Arrange
        var scanner = _fixture.GetScanner<SystemIntegrityScanner>();
        var control = new NistControl { Id = "SI-3", Title = "Malicious Code Protection" };

        // Act
        var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Multi-Scanner Workflow Tests

    [Fact]
    public async Task AllScanners_ScanSameControl_ProduceConsistentResultTypes()
    {
        // Arrange
        var control = new NistControl { Id = "AC-1", Title = "Access Control Policy and Procedures" };
        var scannerTypes = new[]
        {
            typeof(DefaultComplianceScanner),
            typeof(AccessControlScanner),
            typeof(AuditScanner),
            typeof(ConfigurationManagementScanner),
            typeof(ContingencyPlanningScanner),
            typeof(IdentificationAuthenticationScanner),
            typeof(IncidentResponseScanner),
            typeof(RiskAssessmentScanner),
            typeof(SecurityAssessmentScanner),
            typeof(SystemCommunicationScanner),
            typeof(SystemIntegrityScanner)
        };

        // Act & Assert
        foreach (var scannerType in scannerTypes)
        {
            var scanner = _fixture.GetScanner(scannerType);
            var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control);
            
            result.Should().NotBeNull($"Scanner {scannerType.Name} should return non-null results");
            result.Should().BeOfType<List<AtoFinding>>($"Scanner {scannerType.Name} should return List<AtoFinding>");
        }
    }

    [Fact]
    public async Task AllScanners_WithCancellation_HandleGracefully()
    {
        // Arrange
        var control = new NistControl { Id = "AC-1", Title = "Access Control Policy and Procedures" };
        var cts = new CancellationTokenSource();
        
        var scannerTypes = new[]
        {
            typeof(DefaultComplianceScanner),
            typeof(AccessControlScanner),
            typeof(ConfigurationManagementScanner),
            typeof(SystemIntegrityScanner)
        };

        // Act & Assert - Should complete successfully before cancellation
        foreach (var scannerType in scannerTypes)
        {
            var scanner = _fixture.GetScanner(scannerType);
            var result = await scanner.ScanControlAsync(_fixture.TestSubscriptionId, control, cts.Token);
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AllScanners_CanHandleConcurrentScans()
    {
        // Arrange
        var controls = new[]
        {
            new NistControl { Id = "AC-1", Title = "Access Control Policy and Procedures" },
            new NistControl { Id = "AU-1", Title = "Audit and Accountability Policy and Procedures" },
            new NistControl { Id = "CM-1", Title = "Configuration Management Policy and Procedures" },
            new NistControl { Id = "SC-1", Title = "System and Communications Protection Policy and Procedures" }
        };

        var scanner = _fixture.GetScanner<DefaultComplianceScanner>();

        // Act - Run scans concurrently
        var tasks = controls.Select(c => scanner.ScanControlAsync(_fixture.TestSubscriptionId, c));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(4);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion
}

/// <summary>
/// Test fixture providing scanner instances with properly configured dependencies
/// </summary>
public class ComplianceScannerTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    public string TestSubscriptionId { get; } = "test-subscription-00000000-0000-0000-0000-000000000000";
    public string TestResourceGroup { get; } = "test-resource-group";

    public ComplianceScannerTestFixture()
    {
        var services = new ServiceCollection();
        
        // Add logging with factory and register non-generic ILogger
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        
        // Register non-generic ILogger for scanners that use ILogger instead of ILogger<T>
        services.AddSingleton<ILogger>(sp => 
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("ComplianceScanner"));

        // Configure mocked Azure service
        var azureServiceMock = new Mock<IAzureResourceService>();
        services.AddSingleton(azureServiceMock.Object);

        // Configure mocked Defender service
        var defenderServiceMock = new Mock<IDefenderForCloudService>();
        services.AddSingleton(defenderServiceMock.Object);

        // Configure GatewayOptions
        var gatewayOptions = Options.Create(new GatewayOptions());
        services.AddSingleton(gatewayOptions);

        // Register all scanners
        services.AddTransient<DefaultComplianceScanner>();
        services.AddTransient<AccessControlScanner>();
        services.AddTransient<AuditScanner>();
        services.AddTransient<ConfigurationManagementScanner>();
        services.AddTransient<ContingencyPlanningScanner>();
        services.AddTransient<IdentificationAuthenticationScanner>();
        services.AddTransient<IncidentResponseScanner>();
        services.AddTransient<RiskAssessmentScanner>();
        services.AddTransient<SecurityAssessmentScanner>();
        services.AddTransient<SystemCommunicationScanner>();
        services.AddTransient<SystemIntegrityScanner>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public T GetScanner<T>() where T : class, IComplianceScanner
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public IComplianceScanner GetScanner(Type scannerType)
    {
        return (IComplianceScanner)_serviceProvider.GetRequiredService(scannerType);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
