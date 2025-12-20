using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Environment Agent via MCP HTTP API.
/// Tests environment management, configuration drift detection, and promotion planning.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated
/// </summary>
public class EnvironmentAgentManualTests : McpHttpTestBase
{
    private const string ExpectedIntent = "environment";

    public EnvironmentAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Environment Status

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task GetEnvironmentStatus_All_ShouldReturnStatus()
    {
        // Arrange
        var message = "Show me the status of all environments (dev, staging, production) including resource health and any active alerts";

        // Act
        var response = await SendChatRequestAsync(message, "env-status-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task GetEnvironmentStatus_Production_ShouldReturnDetailedStatus()
    {
        // Arrange
        var message = "Show detailed status of the production environment including all resources, health checks, and recent deployments";

        // Act
        var response = await SendChatRequestAsync(message, "env-status-prod-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task GetEnvironmentStatus_WithMetrics_ShouldReturnMetrics()
    {
        // Arrange
        var message = "Show environment health metrics including CPU, memory, and availability for all environments";

        // Act
        var response = await SendChatRequestAsync(message, "env-status-metrics-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Configuration Drift Detection

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task DetectConfigurationDrift_ProdVsStaging_ShouldReturnDifferences()
    {
        // Arrange
        var message = "Compare the production and staging environments and identify any configuration drift between them";

        // Act
        var response = await SendChatRequestAsync(message, "env-drift-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task DetectConfigurationDrift_DevVsStaging_ShouldReturnDifferences()
    {
        // Arrange
        var message = "Identify configuration differences between dev and staging environments";

        // Act
        var response = await SendChatRequestAsync(message, "env-drift-dev-staging-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task DetectConfigurationDrift_FromBaseline_ShouldReturnDifferences()
    {
        // Arrange
        var message = "Check production environment configuration against our baseline template and identify any unauthorized changes";

        // Act
        var response = await SendChatRequestAsync(message, "env-drift-baseline-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task DetectConfigurationDrift_NetworkSettings_ShouldReturnDifferences()
    {
        // Arrange
        var message = "Compare network configurations between environments including NSG rules, firewall settings, and DNS";

        // Act
        var response = await SendChatRequestAsync(message, "env-drift-network-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Environment Promotion

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task CreatePromotionPlan_StagingToProduction_ShouldReturnPlan()
    {
        // Arrange
        var message = "Create a deployment plan for promoting the current staging environment to production including pre-checks and rollback steps";

        // Act
        var response = await SendChatRequestAsync(message, "env-promote-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task CreatePromotionPlan_DevToStaging_ShouldReturnPlan()
    {
        // Arrange
        var message = "Create a promotion plan from dev to staging environment with validation steps";

        // Act
        var response = await SendChatRequestAsync(message, "env-promote-dev-staging-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task ValidatePromotion_Readiness_ShouldReturnChecklist()
    {
        // Arrange
        var message = "Validate staging environment readiness for production promotion including health checks and compliance verification";

        // Act
        var response = await SendChatRequestAsync(message, "env-promote-validate-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Environment Synchronization

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task SynchronizeEnvironment_StagingFromProd_ShouldReturnPlan()
    {
        // Arrange
        var message = "Create a plan to synchronize staging environment configuration with production";

        // Act
        var response = await SendChatRequestAsync(message, "env-sync-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task SynchronizeEnvironment_RefreshDev_ShouldReturnPlan()
    {
        // Arrange
        var message = "Plan a dev environment refresh from production with data masking requirements";

        // Act
        var response = await SendChatRequestAsync(message, "env-sync-dev-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Environment Provisioning

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task ProvisionEnvironment_NewDev_ShouldReturnTemplate()
    {
        // Arrange
        var message = "Generate infrastructure templates to provision a new dev environment matching the production architecture";

        // Act
        var response = await SendChatRequestAsync(message, "env-provision-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task ProvisionEnvironment_Sandbox_ShouldReturnTemplate()
    {
        // Arrange
        var message = "Create a sandbox environment template with reduced resources for testing";

        // Act
        var response = await SendChatRequestAsync(message, "env-provision-sandbox-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Environment Cleanup

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task CleanupEnvironment_StaleResources_ShouldReturnPlan()
    {
        // Arrange
        var message = "Identify stale resources in dev environment that haven't been used in 30 days and create a cleanup plan";

        // Act
        var response = await SendChatRequestAsync(message, "env-cleanup-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Environment")]
    public async Task CleanupEnvironment_CostOptimization_ShouldReturnPlan()
    {
        // Arrange
        var message = "Recommend dev environment resources that can be shut down outside business hours to save costs";

        // Act
        var response = await SendChatRequestAsync(message, "env-cleanup-cost-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion
}
