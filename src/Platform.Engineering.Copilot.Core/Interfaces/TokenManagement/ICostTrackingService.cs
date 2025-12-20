using Platform.Engineering.Copilot.Core.Models.TokenManagement;

namespace Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;

/// <summary>
/// Service for tracking and recording agent operation costs
/// Monitors token usage, cost savings, and optimization effectiveness
/// </summary>
public interface ICostTrackingService
{
    /// <summary>
    /// Record a cost metric for an agent operation
    /// </summary>
    Task RecordAgentCostAsync(AgentCostMetrics metrics);

    /// <summary>
    /// Get cost metrics for a specific agent
    /// </summary>
    Task<List<AgentCostMetrics>> GetAgentCostMetricsAsync(string agentType, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Get aggregated optimization metrics
    /// </summary>
    Task<PromptOptimizationMetrics> GetOptimizationMetricsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Get daily usage trend for an agent
    /// </summary>
    Task<List<TokenUsageRecord>> GetDailyUsageTrendAsync(string agentType, int days = 30);

    /// <summary>
    /// Get cost savings summary for a date range
    /// </summary>
    Task<(double TotalCostSaved, long TotalTokensSaved, double PercentageSaved)> GetCostSavingsSummaryAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Clear old metrics (retention policy)
    /// </summary>
    Task ClearOldMetricsAsync(int retentionDays = 90);

    /// <summary>
    /// Get total cost for all operations in a period
    /// </summary>
    Task<double> GetTotalCostAsync(DateTime? startDate = null, DateTime? endDate = null);
}
