using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// Tool for modeling cost scenarios and simulating policy impacts.
/// Consolidates: model_cost_scenario, simulate_policy_impact.
/// </summary>
public class CostScenarioTool : BaseTool
{
    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly ConfigService _configService;

    public override string Name => "model_cost_scenario";

    public override string Description =>
        "Model cost scenarios including infrastructure changes, policy impacts, and what-if analysis. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Simulate adding/removing resources, changing tiers, or applying cost policies.";

    public CostScenarioTool(
        ILogger<CostScenarioTool> logger,
        CostManagementStateAccessors stateAccessors,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID for scenario modeling. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "scenarioType",
            description: "Type of scenario: 'add-resources', 'remove-resources', 'change-tier', 'policy-impact', 'growth-projection' (required)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "resourceType",
            description: "Resource type for the scenario (e.g., 'Microsoft.Compute/virtualMachines')",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceCount",
            description: "Number of resources to add/remove (default: 1)",
            required: false,
            type: "integer"));

        Parameters.Add(new ToolParameter(
            name: "skuTier",
            description: "SKU tier for pricing (e.g., 'Standard_D2s_v3', 'Premium')",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "policyName",
            description: "Policy name for policy-impact scenarios (e.g., 'auto-shutdown', 'reserved-instances')",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var scenarioType = GetOptionalString(arguments, "scenarioType");
        var resourceType = GetOptionalString(arguments, "resourceType") ?? "Microsoft.Compute/virtualMachines";
        var resourceCount = GetOptionalInt(arguments, "resourceCount") ?? 1;
        var skuTier = GetOptionalString(arguments, "skuTier");
        var policyName = GetOptionalString(arguments, "policyName");

        // Auto-fill subscription from configuration if not provided
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                Logger.LogInformation("Using configured default subscription: {SubscriptionId}", subscriptionId);
            }
        }

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { 
                success = false, 
                error = "Subscription ID is required. Either provide subscriptionId parameter or set a default using 'Set my subscription to <id>'" 
            });
        }

        if (string.IsNullOrWhiteSpace(scenarioType))
        {
            return ToJson(new { success = false, error = "Scenario type is required" });
        }

        Logger.LogInformation("Modeling cost scenario for {SubscriptionId}: {ScenarioType}",
            subscriptionId, scenarioType);

        try
        {
            // TODO: Integrate with Azure Pricing Calculator API
            Logger.LogWarning("Cost scenario modeling requires Azure Pricing API integration. Returning sample data.");

            var currentMonthlyCost = 2450.00m;
            decimal costImpact;
            string impactDescription;
            var recommendations = new List<string>();

            switch (scenarioType.ToLowerInvariant())
            {
                case "add-resources":
                    var unitCost = GetResourceUnitCost(resourceType, skuTier);
                    costImpact = unitCost * resourceCount;
                    impactDescription = $"Adding {resourceCount} x {GetResourceTypeName(resourceType)} ({skuTier ?? "default"})";
                    recommendations.Add("Consider reserved instances for long-term resources (up to 72% savings)");
                    recommendations.Add("Use auto-shutdown for dev/test workloads");
                    break;

                case "remove-resources":
                    unitCost = GetResourceUnitCost(resourceType, skuTier);
                    costImpact = -(unitCost * resourceCount);
                    impactDescription = $"Removing {resourceCount} x {GetResourceTypeName(resourceType)}";
                    recommendations.Add("Verify no dependencies on resources before removal");
                    break;

                case "change-tier":
                    var currentTierCost = GetResourceUnitCost(resourceType, "Standard");
                    var newTierCost = GetResourceUnitCost(resourceType, skuTier ?? "Premium");
                    costImpact = (newTierCost - currentTierCost) * resourceCount;
                    impactDescription = $"Changing tier from Standard to {skuTier ?? "Premium"} for {resourceCount} resources";
                    if (costImpact > 0)
                        recommendations.Add("Evaluate if premium features are required");
                    else
                        recommendations.Add("Verify performance requirements are still met");
                    break;

                case "policy-impact":
                    (costImpact, impactDescription, recommendations) = SimulatePolicyImpact(policyName ?? "auto-shutdown", currentMonthlyCost);
                    break;

                case "growth-projection":
                    var growthRate = 0.15m; // 15% growth
                    costImpact = currentMonthlyCost * growthRate;
                    impactDescription = $"Projected 15% infrastructure growth over next quarter";
                    recommendations.Add("Plan budget increase to accommodate growth");
                    recommendations.Add("Evaluate reserved instance commitments for stable workloads");
                    break;

                default:
                    return ToJson(new { success = false, error = $"Unknown scenario type: {scenarioType}" });
            }

            var projectedMonthlyCost = currentMonthlyCost + costImpact;

            await Task.CompletedTask;

            return ToJson(new
            {
                success = true,
                subscriptionId,
                scenario = new
                {
                    type = scenarioType,
                    description = impactDescription,
                    resourceType,
                    resourceCount,
                    skuTier,
                    policyName
                },
                impact = new
                {
                    currentMonthlyCost,
                    costImpact,
                    projectedMonthlyCost,
                    changePercentage = Math.Round(costImpact / currentMonthlyCost * 100, 1),
                    annualImpact = costImpact * 12
                },
                recommendations,
                breakdown = new
                {
                    compute = scenarioType == "add-resources" && resourceType.Contains("Compute") ? costImpact * 0.6m : 0,
                    storage = scenarioType == "add-resources" && resourceType.Contains("Storage") ? costImpact : 0,
                    networking = costImpact * 0.05m
                },
                note = "This is estimated data. Actual costs may vary. Use Azure Pricing Calculator for precise estimates."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error modeling cost scenario");
            return ToJson(new { success = false, error = $"Failed to model scenario: {ex.Message}" });
        }
    }

    private static decimal GetResourceUnitCost(string resourceType, string? sku)
    {
        // Sample pricing - would come from Azure Pricing API
        var baseCosts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "Microsoft.Compute/virtualMachines", 150.00m },
            { "Microsoft.Storage/storageAccounts", 50.00m },
            { "Microsoft.Sql/servers", 200.00m },
            { "Microsoft.ContainerService/managedClusters", 500.00m },
            { "Microsoft.CognitiveServices/accounts", 100.00m }
        };

        var baseCost = baseCosts.GetValueOrDefault(resourceType, 100.00m);

        // SKU multipliers
        return sku?.ToLowerInvariant() switch
        {
            "standard_d2s_v3" => baseCost * 0.5m,
            "standard_d4s_v3" => baseCost,
            "standard_d8s_v3" => baseCost * 2,
            "premium" => baseCost * 2.5m,
            "basic" => baseCost * 0.3m,
            _ => baseCost
        };
    }

    private static string GetResourceTypeName(string resourceType)
    {
        return resourceType.Split('/').LastOrDefault() ?? resourceType;
    }

    private static (decimal costImpact, string description, List<string> recommendations) SimulatePolicyImpact(
        string policyName, decimal currentCost)
    {
        return policyName.ToLowerInvariant() switch
        {
            "auto-shutdown" => (
                -currentCost * 0.15m,
                "Auto-shutdown policy for dev/test VMs (12 hours/day)",
                new List<string>
                {
                    "Ensure critical workloads are excluded from auto-shutdown",
                    "Configure startup schedules for business hours"
                }),
            "reserved-instances" => (
                -currentCost * 0.35m,
                "1-year reserved instances for stable workloads",
                new List<string>
                {
                    "Requires 1-year commitment",
                    "Apply to resources with consistent 24/7 usage"
                }),
            "spot-instances" => (
                -currentCost * 0.60m,
                "Spot instances for fault-tolerant workloads",
                new List<string>
                {
                    "Only suitable for interruptible workloads",
                    "Implement checkpointing for long-running tasks"
                }),
            "right-sizing" => (
                -currentCost * 0.20m,
                "Right-sizing policy based on utilization metrics",
                new List<string>
                {
                    "Review recommendations monthly",
                    "Validate performance after resizing"
                }),
            _ => (
                0,
                $"Unknown policy: {policyName}",
                new List<string> { "Valid policies: auto-shutdown, reserved-instances, spot-instances, right-sizing" })
        };
    }
}
