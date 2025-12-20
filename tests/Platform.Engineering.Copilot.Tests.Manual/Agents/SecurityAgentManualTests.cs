using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Security Agent via MCP HTTP API.
/// Tests security posture assessment, vulnerability management, and identity review.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated with Security Reader permissions
/// </summary>
public class SecurityAgentManualTests : McpHttpTestBase
{
    private const string ExpectedIntent = "security";

    public SecurityAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Security Posture Assessment

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task AssessSecurityPosture_Overall_ShouldReturnScore()
    {
        // Arrange
        var message = "Assess my overall security posture and provide a security score with recommendations for improvement";

        // Act
        var response = await SendChatRequestAsync(message, "security-posture-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task AssessSecurityPosture_ByResourceGroup_ShouldReturnScore()
    {
        // Arrange
        var message = "Assess security posture for the production resource group and compare it to our baseline";

        // Act
        var response = await SendChatRequestAsync(message, "security-posture-rg-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task AssessSecurityPosture_Trends_ShouldReturnAnalysis()
    {
        // Arrange
        var message = "Show security posture trends over the last 30 days with improvement areas";

        // Act
        var response = await SendChatRequestAsync(message, "security-posture-trends-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Vulnerability Scanning

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task GetVulnerabilities_CriticalAndHigh_ShouldReturnFindings()
    {
        // Arrange
        var message = "Show me all critical and high severity vulnerabilities from Defender for Cloud with remediation steps";

        // Act
        var response = await SendChatRequestAsync(message, "security-vuln-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task GetVulnerabilities_ByResource_ShouldReturnFindings()
    {
        // Arrange
        var message = "Show vulnerabilities for virtual machines with CVE details and affected versions";

        // Act
        var response = await SendChatRequestAsync(message, "security-vuln-vm-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task GetVulnerabilities_Containers_ShouldReturnFindings()
    {
        // Arrange
        var message = "Show container image vulnerabilities from Azure Container Registry scanning";

        // Act
        var response = await SendChatRequestAsync(message, "security-vuln-container-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task GetVulnerabilities_SQLDatabases_ShouldReturnFindings()
    {
        // Arrange
        var message = "Show SQL vulnerability assessment results for all databases";

        // Act
        var response = await SendChatRequestAsync(message, "security-vuln-sql-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Identity and Access Review

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task ReviewIdentityAccess_PrivilegedAccounts_ShouldReturnFindings()
    {
        // Arrange
        var message = "Review privileged access assignments and identify any accounts with excessive permissions or stale access";

        // Act
        var response = await SendChatRequestAsync(message, "security-identity-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task ReviewIdentityAccess_ServicePrincipals_ShouldReturnFindings()
    {
        // Arrange
        var message = "Review service principal permissions and identify any with owner or contributor access";

        // Act
        var response = await SendChatRequestAsync(message, "security-identity-sp-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task ReviewIdentityAccess_GuestUsers_ShouldReturnFindings()
    {
        // Arrange
        var message = "List all guest users with access to Azure resources and their permission levels";

        // Act
        var response = await SendChatRequestAsync(message, "security-identity-guest-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task ReviewIdentityAccess_MFAStatus_ShouldReturnFindings()
    {
        // Arrange
        var message = "Show MFA enrollment status for all users with privileged roles";

        // Act
        var response = await SendChatRequestAsync(message, "security-identity-mfa-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Network Security Analysis

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task AnalyzeNetworkSecurity_NSGRules_ShouldReturnFindings()
    {
        // Arrange
        var message = "Analyze NSG rules across all subnets and identify overly permissive rules that allow traffic from the internet";

        // Act
        var response = await SendChatRequestAsync(message, "security-network-nsg-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task AnalyzeNetworkSecurity_PublicEndpoints_ShouldReturnFindings()
    {
        // Arrange
        var message = "Find all resources with public endpoints and evaluate their security configurations";

        // Act
        var response = await SendChatRequestAsync(message, "security-network-public-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task AnalyzeNetworkSecurity_PrivateEndpoints_ShouldReturnReport()
    {
        // Arrange
        var message = "Show private endpoint coverage and identify services that should use private endpoints but don't";

        // Act
        var response = await SendChatRequestAsync(message, "security-network-pe-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Encryption Status

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task CheckEncryptionStatus_AllResources_ShouldReturnReport()
    {
        // Arrange
        var message = "Check encryption status for all storage accounts and databases, identify any using Microsoft-managed keys instead of customer-managed keys";

        // Act
        var response = await SendChatRequestAsync(message, "security-encryption-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task CheckEncryptionStatus_TransitEncryption_ShouldReturnReport()
    {
        // Arrange
        var message = "Check TLS/SSL configuration for all web applications and identify any using outdated protocols";

        // Act
        var response = await SendChatRequestAsync(message, "security-encryption-tls-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task CheckEncryptionStatus_DiskEncryption_ShouldReturnReport()
    {
        // Arrange
        var message = "Verify disk encryption status for all virtual machines and identify any unencrypted disks";

        // Act
        var response = await SendChatRequestAsync(message, "security-encryption-disk-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Threat Detection

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task GetSecurityAlerts_Recent_ShouldReturnAlerts()
    {
        // Arrange
        var message = "Show recent security alerts from Defender for Cloud with severity and affected resources";

        // Act
        var response = await SendChatRequestAsync(message, "security-alerts-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Security")]
    public async Task GetSecurityAlerts_HighSeverity_ShouldReturnAlerts()
    {
        // Arrange
        var message = "List all high and critical security alerts that require immediate attention";

        // Act
        var response = await SendChatRequestAsync(message, "security-alerts-critical-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion
}
