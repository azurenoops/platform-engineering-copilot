using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance;

/// <summary>
/// Unit tests for FindingAutoRemediationService
/// Tests auto-remediation eligibility determination, complexity assessment, and action generation
/// </summary>
public class FindingAutoRemediationServiceTests
{
    public FindingAutoRemediationServiceTests()
    {
        // Set up logger for static methods
        var loggerMock = new Mock<ILogger>();
        FindingAutoRemediationService.SetLogger(loggerMock.Object);
    }

    #region IsAutoRemediable Tests - Null/Empty Cases

    [Fact]
    public void IsAutoRemediable_WithNullFinding_ReturnsFalse()
    {
        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAutoRemediable_WithEmptyTitle_ReturnsFalse()
    {
        // Arrange
        var finding = new AtoFinding { Title = "" };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAutoRemediable_WithNullTitle_ReturnsFalse()
    {
        // Arrange
        var finding = new AtoFinding { Title = null! };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsAutoRemediable Tests - Grouped Findings (ResourceType = "Multiple")

    [Theory]
    [InlineData("TLS 1.0/1.1 enabled on multiple resources")]
    [InlineData("HTTPS not enforced on storage accounts")]
    [InlineData("Resources with TLS configuration issues")]
    public void IsAutoRemediable_GroupedFinding_TlsHttps_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Diagnostic settings disabled on multiple resources")]
    [InlineData("Diagnostic logging not enabled")]
    public void IsAutoRemediable_GroupedFinding_Diagnostics_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Encryption disabled on storage accounts")]
    [InlineData("Encryption not enabled for databases")]
    public void IsAutoRemediable_GroupedFinding_Encryption_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Logging disabled on multiple resources")]
    [InlineData("Log Analytics not configured")]
    [InlineData("Monitoring not enabled")]
    public void IsAutoRemediable_GroupedFinding_LoggingMonitoring_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Backup not configured on VMs")]
    [InlineData("Disaster recovery disabled")]
    [InlineData("Recovery services not enabled")]
    public void IsAutoRemediable_GroupedFinding_BackupRecovery_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("MFA not enabled for users")]
    [InlineData("Multi-factor authentication disabled")]
    public void IsAutoRemediable_GroupedFinding_Mfa_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Network security group rules overly permissive")]
    [InlineData("Firewall rules not configured")]
    [InlineData("NSG missing on subnets")]
    public void IsAutoRemediable_GroupedFinding_NetworkSecurity_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Access assignment requires review")]
    [InlineData("Role access assignment needs approval")]
    public void IsAutoRemediable_GroupedFinding_AccessAssignment_ReturnsFalse(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("Data classification required for sensitive data")]
    [InlineData("Sensitivity label not applied")]
    public void IsAutoRemediable_GroupedFinding_DataClassification_ReturnsFalse(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsAutoRemediable Tests - By FindingType

    [Fact]
    public void IsAutoRemediable_EncryptionFindingType_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Storage encryption issue",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_NetworkSecurityFindingType_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "NSG configuration issue",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("SC-28")]
    [InlineData("SC-13")]
    public void IsAutoRemediable_ConfigurationFinding_EncryptionControls_ReturnsTrue(string controlId)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Configuration issue",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { controlId }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("AU-2")]
    [InlineData("AU-3")]
    [InlineData("AU-12")]
    public void IsAutoRemediable_ConfigurationFinding_AuditControls_ReturnsTrue(string controlId)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Audit logging configuration",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { controlId }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("CP-9")]
    [InlineData("CP-10")]
    public void IsAutoRemediable_ConfigurationFinding_ContingencyControls_ReturnsTrue(string controlId)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Backup configuration",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { controlId }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_AccessControlFindingType_ReturnsFalse()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "RBAC configuration issue",
            FindingType = AtoFindingType.AccessControl
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAutoRemediable_SecurityFinding_DiagnosticRelated_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Diagnostic settings not configured",
            FindingType = AtoFindingType.Security
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_SecurityFinding_MonitoringRelated_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Authentication monitoring not enabled",
            FindingType = AtoFindingType.Security
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - By Resource Type (Storage)

    [Fact]
    public void IsAutoRemediable_StorageAccount_EncryptionDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Encryption disabled on storage account",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_StorageAccount_HttpsRequired_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "HTTPS not required for storage account",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_StorageAccount_TlsVersion_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "TLS version 1.0 enabled",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_StorageAccount_PublicAccess_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Public blob access enabled",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - By Resource Type (VM)

    [Fact]
    public void IsAutoRemediable_VirtualMachine_DiskEncryption_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Disk encryption not enabled",
            ResourceType = "Microsoft.Compute/virtualMachines",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_VirtualMachine_DiagnosticsDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Diagnostic settings not enabled",
            ResourceType = "Microsoft.Compute/virtualMachines",
            FindingType = AtoFindingType.Monitoring
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - By Resource Type (NSG)

    [Theory]
    [InlineData("Port 22 open to internet")]
    [InlineData("SSH port unrestricted")]
    [InlineData("Port 3389 open to all")]
    [InlineData("RDP access unrestricted")]
    [InlineData("Management port open")]
    public void IsAutoRemediable_NetworkSecurityGroup_OpenPorts_ReturnsTrue(string title)
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Microsoft.Network/networkSecurityGroups",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - By Resource Type (Key Vault)

    [Fact]
    public void IsAutoRemediable_KeyVault_SoftDeleteDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Soft delete disabled on Key Vault",
            ResourceType = "Microsoft.KeyVault/vaults",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "SC-28" }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_KeyVault_PurgeProtectionDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Purge protection not enabled",
            ResourceType = "Microsoft.KeyVault/vaults",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "SC-28" }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_KeyVault_RbacNotEnabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "RBAC authorization not enabled",
            ResourceType = "Microsoft.KeyVault/vaults",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "SC-28" }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - By Resource Type (SQL)

    [Fact]
    public void IsAutoRemediable_SqlServer_TdeDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "TDE not enabled on database",
            ResourceType = "Microsoft.Sql/servers",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_SqlServer_AuditingDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Auditing disabled on SQL server",
            ResourceType = "Microsoft.Sql/servers",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "AU-2" }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_SqlServer_ThreatDetectionDisabled_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Advanced threat detection disabled",
            ResourceType = "Microsoft.Sql/servers",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "SC-7" }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_SqlServer_FirewallAllowAll_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Firewall rule allows 0.0.0.0",
            ResourceType = "Microsoft.Sql/servers",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - By Resource Type (App Service)

    [Fact]
    public void IsAutoRemediable_AppService_HttpsOnly_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "HTTPS only not enforced",
            ResourceType = "Microsoft.Web/sites",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_AppService_TlsVersion_ReturnsTrue()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Minimum TLS version not set",
            ResourceType = "Microsoft.Web/sites",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsAutoRemediable Tests - Tags and Backup

    [Fact]
    public void IsAutoRemediable_TagMissing_ReturnsTrue()
    {
        // Arrange - Tag findings are grouped findings on Multiple resources
        var finding = new AtoFinding
        {
            Title = "Required tag missing on resource",
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRemediable_BackupNotConfigured_VirtualMachine_ReturnsTrue()
    {
        // Arrange - Backup is contingency planning control
        var finding = new AtoFinding
        {
            Title = "Backup not configured for VM",
            ResourceType = "Microsoft.Compute/virtualMachines",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "CP-9" }
        };

        // Act
        var result = FindingAutoRemediationService.IsAutoRemediable(finding);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetRemediationComplexity Tests

    [Fact]
    public void GetRemediationComplexity_NonRemediableFinding_ReturnsComplex()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Manual review required for access control",
            FindingType = AtoFindingType.AccessControl
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationComplexity(finding);

        // Assert
        result.Should().Be(AtoRemediationComplexity.Complex);
    }

    [Theory]
    [InlineData("Missing required tag")]
    [InlineData("HTTPS only not enabled")]
    [InlineData("TLS version 1.0 detected")]
    [InlineData("Soft delete disabled")]
    [InlineData("Purge protection not enabled")]
    [InlineData("Diagnostic settings not configured")]
    public void GetRemediationComplexity_SimpleFinding_ReturnsSimple(string title)
    {
        // Arrange - Use Multiple resource type for grouped findings which are auto-remediable
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationComplexity(finding);

        // Assert
        result.Should().Be(AtoRemediationComplexity.Simple);
    }

    [Theory]
    [InlineData("Encryption not enabled")]
    [InlineData("Firewall rules overly permissive")]
    [InlineData("NSG rules need updating")]
    [InlineData("Backup not configured")]
    public void GetRemediationComplexity_ModerateFinding_ReturnsModerate(string title)
    {
        // Arrange - Use Multiple resource type for grouped findings which are auto-remediable
        var finding = new AtoFinding
        {
            Title = title,
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationComplexity(finding);

        // Assert
        result.Should().Be(AtoRemediationComplexity.Moderate);
    }

    #endregion

    #region GetEstimatedDuration Tests

    [Fact]
    public void GetEstimatedDuration_SimpleFinding_Returns5Minutes()
    {
        // Arrange - Use TLS version finding which is classified as Simple
        var finding = new AtoFinding
        {
            Title = "TLS version 1.0 detected on resource",
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.GetEstimatedDuration(finding);

        // Assert
        result.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GetEstimatedDuration_ModerateFinding_Returns15Minutes()
    {
        // Arrange - Use encryption finding which is classified as Moderate
        var finding = new AtoFinding
        {
            Title = "Encryption not enabled on storage",
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.GetEstimatedDuration(finding);

        // Assert
        result.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void GetEstimatedDuration_ComplexFinding_Returns1Hour()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Access control needs manual review",
            FindingType = AtoFindingType.AccessControl
        };

        // Act
        var result = FindingAutoRemediationService.GetEstimatedDuration(finding);

        // Assert
        result.Should().Be(TimeSpan.FromHours(1));
    }

    #endregion

    #region GetRemediationActions Tests

    [Fact]
    public void GetRemediationActions_NonRemediableFinding_ReturnsManualAction()
    {
        // Arrange
        var finding = new AtoFinding
        {
            Title = "Manual review required",
            FindingType = AtoFindingType.AccessControl,
            Recommendation = "Review access permissions manually"
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("Manual Review Required");
        result[0].ActionType.Should().Be(AtoRemediationActionType.ManualAction);
        result[0].Complexity.Should().Be(AtoRemediationComplexity.Complex);
    }

    [Fact]
    public void GetRemediationActions_StorageEncryption_ReturnsEnableEncryptionAction()
    {
        // Arrange - Use Encryption FindingType to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Encryption disabled on storage",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Enable Storage Encryption");
        var action = result.First(a => a.Name == "Enable Storage Encryption");
        action.ActionType.Should().Be(AtoRemediationActionType.ConfigurationChange);
        action.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void GetRemediationActions_VmDiskEncryption_ReturnsEnableDiskEncryptionAction()
    {
        // Arrange - Use Encryption FindingType to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Disk encryption not enabled",
            ResourceType = "Microsoft.Compute/virtualMachines",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Enable Azure Disk Encryption");
        var action = result.First(a => a.Name == "Enable Azure Disk Encryption");
        action.RequiresApproval.Should().BeTrue(); // VM restart may be needed
    }

    [Fact]
    public void GetRemediationActions_NsgPort_ReturnsUpdateNsgRulesAction()
    {
        // Arrange - Use NetworkSecurity FindingType to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Port 22 open to internet",
            ResourceType = "Microsoft.Network/networkSecurityGroups",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Update NSG Rules");
        var action = result.First(a => a.Name == "Update NSG Rules");
        action.RequiresApproval.Should().BeTrue(); // May impact connectivity
    }

    [Fact]
    public void GetRemediationActions_KeyVaultSoftDelete_ReturnsEnableSoftDeleteAction()
    {
        // Arrange - Use Configuration FindingType with SC-28 control to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Soft delete disabled",
            ResourceType = "Microsoft.KeyVault/vaults",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "SC-28" }
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Enable Soft Delete");
        var action = result.First(a => a.Name == "Enable Soft Delete");
        action.Complexity.Should().Be(AtoRemediationComplexity.Simple);
        action.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void GetRemediationActions_KeyVaultPurgeProtection_ReturnsEnablePurgeProtectionAction()
    {
        // Arrange - Use Configuration FindingType with SC-28 control to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Purge protection not enabled",
            ResourceType = "Microsoft.KeyVault/vaults",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "SC-28" }
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Enable Purge Protection");
        var action = result.First(a => a.Name == "Enable Purge Protection");
        action.RequiresApproval.Should().BeTrue(); // Irreversible
    }

    [Fact]
    public void GetRemediationActions_SqlTde_ReturnsEnableTdeAction()
    {
        // Arrange - Use Encryption FindingType to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "TDE not enabled",
            ResourceType = "Microsoft.Sql/servers",
            FindingType = AtoFindingType.Encryption
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Enable Transparent Data Encryption");
    }

    [Fact]
    public void GetRemediationActions_Diagnostics_ReturnsEnableDiagnosticsAction()
    {
        // Arrange - Diagnostic findings contain "diagnostic" in title, use Security type
        var finding = new AtoFinding
        {
            Title = "Diagnostic settings not configured",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.Security
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Enable Diagnostic Settings");
        var action = result.First(a => a.Name == "Enable Diagnostic Settings");
        action.ToolCommand.Should().Be("ENABLE_DIAGNOSTIC_SETTINGS");
    }

    [Fact]
    public void GetRemediationActions_Tls_ReturnsUpdateTlsVersionAction()
    {
        // Arrange - TLS is NetworkSecurity
        var finding = new AtoFinding
        {
            Title = "TLS 1.0 enabled",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Update Minimum TLS Version");
        var action = result.First(a => a.Name == "Update Minimum TLS Version");
        action.Parameters.Should().ContainKey("minimumTlsVersion");
        action.Parameters["minimumTlsVersion"].Should().Be("1.2");
    }

    [Fact]
    public void GetRemediationActions_Https_ReturnsRequireHttpsAction()
    {
        // Arrange - HTTPS is NetworkSecurity
        var finding = new AtoFinding
        {
            Title = "HTTPS not enforced",
            ResourceType = "Microsoft.Storage/storageAccounts",
            FindingType = AtoFindingType.NetworkSecurity
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Require HTTPS Only");
        var action = result.First(a => a.Name == "Require HTTPS Only");
        action.ToolCommand.Should().Be("ENABLE_HTTPS");
    }

    [Fact]
    public void GetRemediationActions_Tag_ReturnsApplyTagsAction()
    {
        // Arrange - Tags are grouped findings on Multiple resources
        var finding = new AtoFinding
        {
            Title = "Required tag missing",
            ResourceType = "Multiple"
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Apply Required Tags");
        var action = result.First(a => a.Name == "Apply Required Tags");
        action.Complexity.Should().Be(AtoRemediationComplexity.Simple);
        action.EstimatedDuration.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void GetRemediationActions_AlertRules_ReturnsConfigureAlertRulesAction()
    {
        // Arrange - Use Configuration FindingType with AU-2 control to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Audit review alert rules not configured",
            ResourceType = "Microsoft.Insights/scheduledQueryRules",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "AU-2" }
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Configure Audit Alert Rules");
        var action = result.First(a => a.Name == "Configure Audit Alert Rules");
        action.ToolCommand.Should().Be("CONFIGURE_ALERT_RULES");
    }

    [Fact]
    public void GetRemediationActions_LogRetention_ReturnsConfigureRetentionAction()
    {
        // Arrange - Use Configuration FindingType with AU-11 control to make it auto-remediable
        var finding = new AtoFinding
        {
            Title = "Log retention period insufficient",
            ResourceType = "Microsoft.OperationalInsights/workspaces",
            FindingType = AtoFindingType.Configuration,
            AffectedNistControls = new List<string> { "AU-11" }
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().Contain(a => a.Name == "Configure Log Retention");
        var action = result.First(a => a.Name == "Configure Log Retention");
        action.Parameters.Should().ContainKey("retentionDays");
        action.Parameters["retentionDays"].Should().Be("90");
    }

    [Fact]
    public void GetRemediationActions_UnmatchedAutoRemediable_ReturnsGenericAction()
    {
        // Arrange - An auto-remediable finding that doesn't match specific patterns
        var finding = new AtoFinding
        {
            Title = "Configuration baseline not applied",
            ResourceType = "Multiple",
            Recommendation = "Apply security baseline configuration"
        };

        // Act
        var result = FindingAutoRemediationService.GetRemediationActions(finding);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(a => a.Name == "Auto-Configure Compliance Setting" || a.ActionType == AtoRemediationActionType.ConfigurationChange);
    }

    #endregion
}
