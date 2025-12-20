using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;

namespace Platform.Engineering.Copilot.Core.Services.TokenManagement;

/// <summary>
/// Implementation of cost tracking service
/// Stores and aggregates agent operation costs and optimization metrics
/// </summary>
public class CostTrackingService : ICostTrackingService
{
    private readonly ILogger<CostTrackingService> _logger;
    private readonly List<AgentCostMetrics> _costMetrics = new();
    private readonly object _lockObject = new();

    public CostTrackingService(ILogger<CostTrackingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Record a cost metric for an agent operation
    /// </summary>
    public async Task RecordAgentCostAsync(AgentCostMetrics metrics)
    {
        if (metrics == null)
            throw new ArgumentNullException(nameof(metrics));

        lock (_lockObject)
        {
            _costMetrics.Add(metrics);
        }

        _logger.LogInformation(
            "Cost metric recorded: Agent={Agent}, Tokens={Tokens}, Cost=${Cost:F4}, Saved={Saved}%",
            metrics.AgentType,
            metrics.TotalTokens,
            metrics.EstimatedCost,
            metrics.OptimizationPercentage);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get cost metrics for a specific agent
    /// </summary>
    public async Task<List<AgentCostMetrics>> GetAgentCostMetricsAsync(
        string agentType,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        lock (_lockObject)
        {
            var query = _costMetrics
                .Where(m => m.AgentType == agentType)
                .AsEnumerable();

            if (startDate.HasValue)
                query = query.Where(m => m.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(m => m.Timestamp <= endDate.Value);

            return query.OrderByDescending(m => m.Timestamp).ToList();
        }
    }

    /// <summary>
    /// Get aggregated optimization metrics
    /// </summary>
    public async Task<PromptOptimizationMetrics> GetOptimizationMetricsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        lock (_lockObject)
        {
            var query = _costMetrics.AsEnumerable();

            if (startDate.HasValue)
                query = query.Where(m => m.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(m => m.Timestamp <= endDate.Value);

            var metrics = query.ToList();

            if (metrics.Count == 0)
            {
                return new PromptOptimizationMetrics
                {
                    PeriodStart = startDate,
                    PeriodEnd = endDate
                };
            }

            var agentGroups = metrics.GroupBy(m => m.AgentType);
            var agentStats = new Dictionary<string, AgentOptimizationStats>();

            foreach (var group in agentGroups)
            {
                var groupMetrics = group.ToList();
                var optimizedCount = groupMetrics.Count(m => m.WasOptimized);

                agentStats[group.Key] = new AgentOptimizationStats
                {
                    AgentType = group.Key,
                    OperationCount = groupMetrics.Count,
                    OptimizedOperationCount = optimizedCount,
                    TokensSaved = groupMetrics.Sum(m => m.TokensSaved),
                    CostSaved = groupMetrics.Sum(m => m.CostSaved),
                    AverageOptimizationPercentage = optimizedCount > 0
                        ? groupMetrics.Where(m => m.WasOptimized).Average(m => m.OptimizationPercentage)
                        : 0,
                    PreferredStrategy = groupMetrics
                        .Where(m => !string.IsNullOrEmpty(m.OptimizationStrategy))
                        .GroupBy(m => m.OptimizationStrategy)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? "None"
                };
            }

            var totalTokensSaved = metrics.Sum(m => m.TokensSaved);
            var totalTokensProcessed = metrics.Sum(m => m.OriginalPromptTokens);
            var totalCostSaved = metrics.Sum(m => m.CostSaved);
            var totalCost = metrics.Sum(m => m.EstimatedCost);

            var result = new PromptOptimizationMetrics
            {
                TotalPromptsProcessed = metrics.Count,
                PromptsOptimized = metrics.Count(m => m.WasOptimized),
                TotalTokensProcessed = totalTokensProcessed,
                TotalTokensAfterOptimization = metrics.Sum(m => m.OptimizedPromptTokens),
                TotalTokensSaved = totalTokensSaved,
                AverageOptimizationPercentage = totalTokensProcessed > 0
                    ? (totalTokensSaved * 100.0 / totalTokensProcessed)
                    : 0,
                TotalCostSaved = totalCostSaved,
                TotalCostIncurred = totalCost,
                PeriodStart = startDate ?? metrics.Min(m => m.Timestamp),
                PeriodEnd = endDate ?? metrics.Max(m => m.Timestamp),
                AgentStats = agentStats,
                MostCommonStrategy = metrics
                    .Where(m => !string.IsNullOrEmpty(m.OptimizationStrategy))
                    .GroupBy(m => m.OptimizationStrategy)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "None"
            };

            return result;
        }
    }

    /// <summary>
    /// Get daily usage trend for an agent
    /// </summary>
    public async Task<List<TokenUsageRecord>> GetDailyUsageTrendAsync(string agentType, int days = 30)
    {
        lock (_lockObject)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var metrics = _costMetrics
                .Where(m => m.AgentType == agentType && m.Timestamp >= cutoffDate)
                .ToList();

            var dailyGroups = metrics
                .GroupBy(m => m.Timestamp.Date)
                .OrderBy(g => g.Key)
                .ToList();

            var records = new List<TokenUsageRecord>();

            foreach (var day in dailyGroups)
            {
                var dayMetrics = day.ToList();
                var optimizedCount = dayMetrics.Count(m => m.WasOptimized);

                records.Add(new TokenUsageRecord
                {
                    AgentType = agentType,
                    Date = day.Key,
                    TotalTokensUsed = dayMetrics.Sum(m => m.TotalTokens),
                    TotalTokensSaved = dayMetrics.Sum(m => m.TokensSaved),
                    OperationCount = dayMetrics.Count,
                    DailyEstimatedCost = dayMetrics.Sum(m => m.EstimatedCost),
                    DailyCostSaved = dayMetrics.Sum(m => m.CostSaved),
                    OptimizationRate = dayMetrics.Count > 0 ? (optimizedCount * 100.0 / dayMetrics.Count) : 0
                });
            }

            return records;
        }
    }

    /// <summary>
    /// Get cost savings summary for a date range
    /// </summary>
    public async Task<(double TotalCostSaved, long TotalTokensSaved, double PercentageSaved)> GetCostSavingsSummaryAsync(
        DateTime startDate,
        DateTime endDate)
    {
        lock (_lockObject)
        {
            var metrics = _costMetrics
                .Where(m => m.Timestamp >= startDate && m.Timestamp <= endDate)
                .ToList();

            if (metrics.Count == 0)
            {
                return (0, 0, 0);
            }

            var totalCostSaved = metrics.Sum(m => m.CostSaved);
            var totalTokensSaved = metrics.Sum(m => m.TokensSaved);
            var totalTokensProcessed = metrics.Sum(m => m.OriginalPromptTokens);

            var percentageSaved = totalTokensProcessed > 0
                ? (totalTokensSaved * 100.0 / totalTokensProcessed)
                : 0;

            return (totalCostSaved, totalTokensSaved, percentageSaved);
        }
    }

    /// <summary>
    /// Clear old metrics (retention policy)
    /// </summary>
    public async Task ClearOldMetricsAsync(int retentionDays = 90)
    {
        lock (_lockObject)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var beforeCount = _costMetrics.Count;

            _costMetrics.RemoveAll(m => m.Timestamp < cutoffDate);

            var removedCount = beforeCount - _costMetrics.Count;
            _logger.LogInformation("Cleared {Count} old cost metrics (older than {Days} days)", removedCount, retentionDays);
        }
    }

    /// <summary>
    /// Get total cost for all operations in a period
    /// </summary>
    public async Task<double> GetTotalCostAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        lock (_lockObject)
        {
            var query = _costMetrics.AsEnumerable();

            if (startDate.HasValue)
                query = query.Where(m => m.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(m => m.Timestamp <= endDate.Value);

            return query.Sum(m => m.EstimatedCost);
        }
    }
}
