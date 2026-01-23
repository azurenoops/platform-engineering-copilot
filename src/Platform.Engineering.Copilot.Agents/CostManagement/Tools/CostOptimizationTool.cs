using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// Tool for generating cost optimization recommendations.
/// Consolidates: get_cost_optimization_recommendations.
/// </summary>
public class CostOptimizationTool : BaseTool
{
    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly ConfigService _configService;

    public override string Name => "get_optimization_recommendations";

    public override string Description =>
        "Get cost optimization recommendations including rightsizing, reserved instances, " +
        "unused resources, and architectural improvements. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Returns prioritized recommendations with estimated savings.";

    public CostOptimizationTool(
        ILogger<CostOptimizationTool> logger,
        CostManagementStateAccessors stateAccessors,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID to analyze for optimization opportunities. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "minimumSavings",
            description: "Minimum monthly savings threshold to include (default: 100)",
            required: false,
            type: "number"));

        Parameters.Add(new ToolParameter(
            name: "category",
            description: "Filter by category: 'rightsizing', 'reserved-instances', 'unused-resources', 'all' (default: all)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var minimumSavings = GetOptionalDecimal(arguments, "minimumSavings") ?? 100m;
        var category = GetOptionalString(arguments, "category") ?? "all";

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

        Logger.LogInformation("Getting optimization recommendations for {SubscriptionId}, min savings: ${MinSavings}",
            subscriptionId, minimumSavings);

        try
        {
            // TODO: Integrate with Azure Advisor and Cost Management API
            Logger.LogWarning("Cost optimization requires Azure Advisor integration. Returning sample data.");

            var recommendations = new List<OptimizationRecommendation>
            {
                new()
                {
                    Id = "rec-001",
                    Category = "rightsizing",
                    Description = "Resize VM 'vm-prod-01' from D4s_v3 to D2s_v3 based on CPU utilization < 20%",
                    PotentialMonthlySavings = 180.00m,
                    Priority = "High",
                    Complexity = "Simple",
                    AffectedResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-prod-01",
                    AffectedResourceType = "Microsoft.Compute/virtualMachines"
                },
                new()
                {
                    Id = "rec-002",
                    Category = "unused-resources",
                    Description = "Delete unattached disk 'disk-backup-old' (not attached for 45 days)",
                    PotentialMonthlySavings = 95.00m,
                    Priority = "Medium",
                    Complexity = "Simple",
                    AffectedResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/rg-prod/providers/Microsoft.Compute/disks/disk-backup-old",
                    AffectedResourceType = "Microsoft.Compute/disks"
                },
                new()
                {
                    Id = "rec-003",
                    Category = "reserved-instances",
                    Description = "Purchase 1-year reserved instance for 'vm-prod-02' (consistent 24/7 usage)",
                    PotentialMonthlySavings = 145.00m,
                    Priority = "Medium",
                    Complexity = "Moderate",
                    AffectedResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-prod-02",
                    AffectedResourceType = "Microsoft.Compute/virtualMachines"
                },
                new()
                {
                    Id = "rec-004",
                    Category = "rightsizing",
                    Description = "Move storage account 'stprodlogs' from Premium to Standard tier (low IOPS usage)",
                    PotentialMonthlySavings = 65.00m,
                    Priority = "Low",
                    Complexity = "Simple",
                    AffectedResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/stprodlogs",
                    AffectedResourceType = "Microsoft.Storage/storageAccounts"
                }
            };

            // Filter by category if specified
            var filteredRecommendations = category.ToLowerInvariant() == "all"
                ? recommendations
                : recommendations.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

            // Filter by minimum savings
            filteredRecommendations = filteredRecommendations
                .Where(r => r.PotentialMonthlySavings >= minimumSavings)
                .OrderByDescending(r => r.PotentialMonthlySavings)
                .ToList();

            var summary = new OptimizationResultSummary
            {
                SubscriptionId = subscriptionId,
                TotalMonthlyCost = 2450.00m,
                TotalPotentialSavings = filteredRecommendations.Sum(r => r.PotentialMonthlySavings),
                RecommendationCount = filteredRecommendations.Count,
                TopRecommendations = filteredRecommendations.Take(10).ToList(),
                AnalyzedAt = DateTime.UtcNow
            };

            await Task.CompletedTask;

            return ToJson(new
            {
                success = true,
                subscriptionId,
                filters = new { minimumSavings, category },
                summary = new
                {
                    summary.TotalMonthlyCost,
                    summary.TotalPotentialSavings,
                    savingsPercentage = summary.TotalMonthlyCost > 0
                        ? Math.Round(summary.TotalPotentialSavings / summary.TotalMonthlyCost * 100, 1)
                        : 0,
                    summary.RecommendationCount
                },
                recommendations = filteredRecommendations,
                note = "This is sample data. Integrate with Azure Advisor API for real recommendations."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting optimization recommendations");
            return ToJson(new { success = false, error = $"Failed to get recommendations: {ex.Message}" });
        }
    }
}
