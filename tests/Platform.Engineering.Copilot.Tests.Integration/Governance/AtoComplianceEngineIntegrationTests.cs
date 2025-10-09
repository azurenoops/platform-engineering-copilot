using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Governance.Configuration;
using Platform.Engineering.Copilot.Governance.Services;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Governance;

/// <summary>
/// Integration tests for AtoComplianceEngine with real Azure service implementations
/// Tests the complete assessment flow including scanners and evidence collectors
/// </summary>
public class AtoComplianceEngineIntegrationTests
{
    private readonly Mock<ILogger<AtoComplianceEngine>> _mockLogger;
    private readonly Mock<INistControlsService> _mockNistControlsService;
    private readonly Mock<IAzureResourceService> _mockAzureResourceService;
    private readonly Mock<IAzureResourceHealthService> _mockAzureHealthService;
    private readonly Mock<IAzureCostManagementService> _mockCostService;
    private readonly Mock<ComplianceMetricsService> _mockMetricsService;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<GovernanceOptions> _options;

    public AtoComplianceEngineIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<AtoComplianceEngine>>();
        _mockNistControlsService = new Mock<INistControlsService>();
        _mockAzureResourceService = new Mock<IAzureResourceService>();
        _mockAzureHealthService = new Mock<IAzureResourceHealthService>();
        _mockCostService = new Mock<IAzureCostManagementService>();
        _mockMetricsService = new Mock<ComplianceMetricsService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        _options = Options.Create(new GovernanceOptions
        {
            EnforcePolicies = true,
            RequireApprovals = false
        });
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithRealAzureResources_GeneratesAccurateFindings()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var testResources = CreateTestAzureResources();
        
        _mockAzureResourceService
            .Setup(x => x.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(testResources);

        SetupMockNistControls();

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.RunComprehensiveAssessmentAsync(subscriptionId, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AssessmentId.Should().NotBeNullOrEmpty();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.StartTime.Should().BeBefore(result.EndTime);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        
        // Verify control family assessments were run
        result.ControlFamilyResults.Should().NotBeEmpty();
        result.ControlFamilyResults.Should().ContainKey("AC"); // Access Control
        result.ControlFamilyResults.Should().ContainKey("AU"); // Audit
        result.ControlFamilyResults.Should().ContainKey("SC"); // Security
        
        // Verify findings structure
        result.TotalFindings.Should().BeGreaterThan(0);
        result.ExecutiveSummary.Should().NotBeNullOrEmpty();
        result.RiskProfile.Should().NotBeNull();
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting comprehensive ATO compliance assessment")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithNoNetworkSecurityGroups_GeneratesHighSeverityFinding()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var testResources = new List<AzureResource>
        {
            // VMs without NSGs - should trigger AC-3 finding
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1",
                Name = "test-vm-1",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm2",
                Name = "test-vm-2",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ResourceGroup = "rg1"
            }
            // No NSGs - should fail AC-3 (Access Enforcement)
        };
        
        _mockAzureResourceService
            .Setup(x => x.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(testResources);

        SetupMockNistControls();

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.RunComprehensiveAssessmentAsync(subscriptionId, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        // Should have AC family findings
        result.ControlFamilyResults["AC"].Findings.Should().NotBeEmpty();
        
        // Should have HIGH severity finding for missing NSGs
        var acFindings = result.ControlFamilyResults["AC"].Findings;
        acFindings.Should().Contain(f => 
            f.Severity == AtoFindingSeverity.High && 
            f.AffectedNistControls.Contains("AC-3"));
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithMissingAuditLogging_GeneratesAuditFindings()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var testResources = new List<AzureResource>
        {
            // Critical resources without audit logging infrastructure
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.KeyVault/vaults/kv1",
                Name = "test-keyvault",
                Type = "Microsoft.KeyVault/vaults",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Sql/servers/sql1",
                Name = "test-sql",
                Type = "Microsoft.Sql/servers",
                Location = "eastus",
                ResourceGroup = "rg1"
            }
            // No Log Analytics or Storage Accounts - should fail AU-2
        };
        
        _mockAzureResourceService
            .Setup(x => x.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(testResources);

        SetupMockNistControls();

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.RunComprehensiveAssessmentAsync(subscriptionId, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        // Should have AU family findings
        result.ControlFamilyResults["AU"].Findings.Should().NotBeEmpty();
        
        // Should have HIGH severity finding for missing audit logging
        var auFindings = result.ControlFamilyResults["AU"].Findings;
        auFindings.Should().Contain(f => 
            f.Severity == AtoFindingSeverity.High && 
            f.AffectedNistControls.Contains("AU-2"));
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithProperSecurityControls_GeneratesBetterComplianceScore()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var testResources = CreateCompliantAzureResources();
        
        _mockAzureResourceService
            .Setup(x => x.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(testResources);

        SetupMockNistControls();

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.RunComprehensiveAssessmentAsync(subscriptionId, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        // With proper security controls, critical findings should be minimal
        result.CriticalFindings.Should().Be(0);
        
        // Should have evidence of security controls
        result.ControlFamilyResults["AC"].Findings
            .Where(f => f.Severity == AtoFindingSeverity.Critical || f.Severity == AtoFindingSeverity.High)
            .Should().HaveCountLessThan(3);
        
        result.ControlFamilyResults["AU"].Findings
            .Where(f => f.Severity == AtoFindingSeverity.Critical || f.Severity == AtoFindingSeverity.High)
            .Should().HaveCountLessThan(3);
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_ForAccessControl_CapturesRealConfigurations()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var controlFamily = "AC";
        var testResources = CreateTestAzureResources();
        
        _mockAzureResourceService
            .Setup(x => x.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(testResources);

        SetupMockNistControls();

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PackageId.Should().NotBeNullOrEmpty();
        result.ControlFamily.Should().Be(controlFamily);
        result.Evidence.Should().NotBeEmpty();
        
        // Should have evidence with ConfigSnapshot (JSON)
        result.Evidence.Should().Contain(e => !string.IsNullOrEmpty(e.ConfigSnapshot));
        
        // Should have evidence for AC controls
        result.Evidence.Should().Contain(e => e.ControlId.StartsWith("AC-"));
        
        // Evidence completeness should be calculated
        result.CompletenessScore.Should().BeGreaterThan(0);
        result.Summary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_CalculatesRiskProfile_BasedOnFindings()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var testResources = CreateTestAzureResources();
        
        _mockAzureResourceService
            .Setup(x => x.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(testResources);

        SetupMockNistControls();

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.RunComprehensiveAssessmentAsync(subscriptionId, null, CancellationToken.None);

        // Assert
        result.RiskProfile.Should().NotBeNull();
        result.RiskProfile.RiskLevel.Should().NotBeNullOrEmpty();
        result.RiskProfile.RiskScore.Should().BeGreaterThan(0);
        result.RiskProfile.TopRisks.Should().NotBeNull();
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        
        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await engine.RunComprehensiveAssessmentAsync(subscriptionId, null, cts.Token));
    }

    [Fact]
    public async Task RunComprehensiveAssessmentAsync_WithProgressReporting_ReportsProgress()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var resources = CreateTestAzureResources();
        _mockAzureResourceService.Setup(s => s.ListAllResourcesAsync(It.IsAny<string>()))
            .ReturnsAsync(resources);

        var progressReports = new List<AssessmentProgress>();
        var progress = new Progress<AssessmentProgress>(p => progressReports.Add(p));

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.RunComprehensiveAssessmentAsync(subscriptionId, progress, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        progressReports.Should().NotBeEmpty("progress should be reported during assessment");
        
        // Should have initial report
        progressReports.Should().Contain(p => p.CurrentFamily == "Initialization", 
            "should report initialization");
        
        // Should have at least one family completion report
        progressReports.Should().Contain(p => p.CompletedFamilies > 0,
            "should report completed families");
        
        // Verify progress percentages are calculated correctly
        var completionReports = progressReports.Where(p => p.CompletedFamilies > 0).ToList();
        foreach (var report in completionReports)
        {
            var expectedPercent = Math.Round((double)report.CompletedFamilies / report.TotalFamilies * 100, 2);
            report.PercentComplete.Should().Be(expectedPercent, 
                $"progress percentage should be {expectedPercent} for {report.CompletedFamilies}/{report.TotalFamilies} families");
        }
        
        // Should report progress for multiple families
        var uniqueFamilies = progressReports.Select(p => p.CurrentFamily).Distinct().Count();
        uniqueFamilies.Should().BeGreaterThan(1, "should report progress for multiple control families");
    }

    [Fact]
    public async Task CollectComplianceEvidenceAsync_WithProgressReporting_ReportsProgress()
    {
        // Arrange
        var subscriptionId = "test-subscription-123";
        var controlFamily = "AC";
        var resources = CreateTestAzureResources();
        _mockAzureResourceService.Setup(s => s.ListAllResourcesAsync(It.IsAny<string>()))
            .ReturnsAsync(resources);

        var progressReports = new List<EvidenceCollectionProgress>();
        var progress = new Progress<EvidenceCollectionProgress>(p => progressReports.Add(p));

        var engine = new AtoComplianceEngine(
            _mockLogger.Object,
            _mockNistControlsService.Object,
            _mockAzureResourceService.Object,
            _mockAzureHealthService.Object,
            _mockCostService.Object,
            _memoryCache,
            _mockMetricsService.Object,
            _options);

        // Act
        var result = await engine.CollectComplianceEvidenceAsync(subscriptionId, controlFamily, progress, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ControlFamily.Should().Be(controlFamily);
        
        progressReports.Should().NotBeEmpty("progress should be reported during evidence collection");
        
        // Should report all evidence types
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Initialization");
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Configuration");
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Logs");
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Metrics");
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Policies");
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Access Control");
        progressReports.Should().Contain(p => p.CurrentEvidenceType == "Complete");
        
        // Verify progress percentages
        var completionReports = progressReports.Where(p => p.CollectedItems > 0).ToList();
        foreach (var report in completionReports)
        {
            var expectedPercent = Math.Round((double)report.CollectedItems / report.TotalItems * 100, 2);
            report.PercentComplete.Should().Be(expectedPercent,
                $"progress percentage should be {expectedPercent} for {report.CollectedItems}/{report.TotalItems} items");
        }
        
        // Final progress should show completion
        var finalReport = progressReports.Last();
        finalReport.PercentComplete.Should().Be(100, "final progress should be 100%");
        finalReport.CurrentEvidenceType.Should().Be("Complete");
    }

    #region Helper Methods

    private List<AzureResource> CreateTestAzureResources()
    {
        return new List<AzureResource>
        {
            // Network Security Groups
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Network/networkSecurityGroups/nsg1",
                Name = "test-nsg-1",
                Type = "Microsoft.Network/networkSecurityGroups",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            // Virtual Machines
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1",
                Name = "test-vm-1",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            // Key Vaults
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.KeyVault/vaults/kv1",
                Name = "test-keyvault",
                Type = "Microsoft.KeyVault/vaults",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            // Storage Accounts
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Storage/storageAccounts/sa1",
                Name = "teststorage",
                Type = "Microsoft.Storage/storageAccounts",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            // Log Analytics Workspace
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.OperationalInsights/workspaces/law1",
                Name = "test-workspace",
                Type = "Microsoft.OperationalInsights/workspaces",
                Location = "eastus",
                ResourceGroup = "rg1"
            }
        };
    }

    private List<AzureResource> CreateCompliantAzureResources()
    {
        var resources = CreateTestAzureResources();
        
        // Add additional security resources
        resources.AddRange(new List<AzureResource>
        {
            // Azure Firewall
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Network/azureFirewalls/fw1",
                Name = "test-firewall",
                Type = "Microsoft.Network/azureFirewalls",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            // Azure Sentinel
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.OperationsManagement/solutions/SecurityInsights",
                Name = "SecurityInsights",
                Type = "Microsoft.OperationsManagement/solutions",
                Location = "eastus",
                ResourceGroup = "rg1"
            },
            // Additional NSGs
            new AzureResource 
            { 
                Id = "/subscriptions/test/resourceGroups/rg1/providers/Microsoft.Network/networkSecurityGroups/nsg2",
                Name = "test-nsg-2",
                Type = "Microsoft.Network/networkSecurityGroups",
                Location = "eastus",
                ResourceGroup = "rg1"
            }
        });
        
        return resources;
    }

    private void SetupMockNistControls()
    {
        // Setup AC (Access Control) family controls
        _mockNistControlsService
            .Setup(x => x.GetControlsByFamilyAsync("AC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new NistControl { Id = "AC-2", Title = "Account Management" },
                new NistControl { Id = "AC-3", Title = "Access Enforcement" },
                new NistControl { Id = "AC-6", Title = "Least Privilege" },
                new NistControl { Id = "AC-7", Title = "Unsuccessful Logon Attempts" }
            });

        // Setup AU (Audit) family controls
        _mockNistControlsService
            .Setup(x => x.GetControlsByFamilyAsync("AU", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new NistControl { Id = "AU-2", Title = "Audit Events" },
                new NistControl { Id = "AU-6", Title = "Audit Review and Analysis" },
                new NistControl { Id = "AU-9", Title = "Protection of Audit Information" }
            });

        // Setup SC (Security) family controls
        _mockNistControlsService
            .Setup(x => x.GetControlsByFamilyAsync("SC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new NistControl { Id = "SC-7", Title = "Boundary Protection" },
                new NistControl { Id = "SC-8", Title = "Transmission Confidentiality" },
                new NistControl { Id = "SC-28", Title = "Protection of Information at Rest" }
            });

        // Setup other families with empty controls (they use default scanner)
        var otherFamilies = new[] { "SI", "CM", "CP", "IA", "IR", "MA", "MP", "PE", "PL", "PS", "RA", "SA", "CA", "AT", "PM" };
        foreach (var family in otherFamilies)
        {
            _mockNistControlsService
                .Setup(x => x.GetControlsByFamilyAsync(family, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NistControl>());
        }
    }

    #endregion
}
