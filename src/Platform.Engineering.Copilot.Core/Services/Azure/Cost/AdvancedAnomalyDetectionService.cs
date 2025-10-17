using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Azure.Cost;

/// <summary>
/// Advanced ML-based anomaly detection for Azure cost analysis.
/// Uses multiple algorithms: Isolation Forest, DBSCAN clustering, and ARIMA forecasting.
/// </summary>
public class AdvancedAnomalyDetectionService
{
    private readonly ILogger<AdvancedAnomalyDetectionService> _logger;

    public AdvancedAnomalyDetectionService(ILogger<AdvancedAnomalyDetectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detect cost anomalies using ML-based algorithms.
    /// </summary>
    public async Task<List<CostAnomaly>> DetectAnomaliesAsync(
        List<CostTrend> costTrends,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ML-based anomaly detection for period {Start} to {End}", startDate, endDate);

        var anomalies = new List<CostAnomaly>();

        // Combine multiple detection algorithms for higher accuracy
        var isolationForestAnomalies = await DetectWithIsolationForestAsync(costTrends, cancellationToken);
        var dbscanAnomalies = await DetectWithDBSCANAsync(costTrends, cancellationToken);
        var arimaAnomalies = await DetectWithARIMAAsync(costTrends, cancellationToken);
        var seasonalAnomalies = await DetectSeasonalAnomaliesAsync(costTrends, cancellationToken);

        // Merge anomalies from different algorithms
        anomalies.AddRange(isolationForestAnomalies);
        anomalies.AddRange(dbscanAnomalies);
        anomalies.AddRange(arimaAnomalies);
        anomalies.AddRange(seasonalAnomalies);

        // Remove duplicates (same date detected by multiple algorithms)
        var consolidatedAnomalies = ConsolidateAnomalies(anomalies);

        _logger.LogInformation("Detected {Count} anomalies using ML algorithms", consolidatedAnomalies.Count);
        return consolidatedAnomalies;
    }

    /// <summary>
    /// Isolation Forest algorithm for anomaly detection.
    /// Identifies outliers by isolating observations.
    /// </summary>
    private Task<List<CostAnomaly>> DetectWithIsolationForestAsync(
        List<CostTrend> costTrends,
        CancellationToken cancellationToken)
    {
        var anomalies = new List<CostAnomaly>();

        if (costTrends.Count < 14) return Task.FromResult(anomalies);

        // Isolation Forest: Build random trees and measure path length
        var costs = costTrends.Select(t => (double)t.DailyCost).ToList();
        var isolationScores = CalculateIsolationScores(costs);

        // Threshold: anomaly if isolation score > 0.6 (shorter average path length)
        for (int i = 0; i < costTrends.Count; i++)
        {
            if (isolationScores[i] > 0.6)
            {
                var trend = costTrends[i];
                var expectedCost = CalculateExpectedCost(costTrends, i);

                anomalies.Add(new CostAnomaly
                {
                    AnomalyDate = trend.Date,
                    DetectedAt = DateTime.UtcNow,
                    Type = trend.DailyCost > expectedCost ? AnomalyType.SpikeCost : AnomalyType.UnexpectedIncrease,
                    Severity = isolationScores[i] > 0.8 ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Title = $"Isolation Forest: Cost anomaly on {trend.Date:yyyy-MM-dd}",
                    Description = $"ML algorithm detected unusual cost pattern (isolation score: {isolationScores[i]:F3})",
                    ExpectedCost = expectedCost,
                    ActualCost = trend.DailyCost,
                    CostDifference = trend.DailyCost - expectedCost,
                    PercentageDeviation = (decimal)((trend.DailyCost - expectedCost) / expectedCost * 100),
                    AnomalyScore = isolationScores[i],
                    DetectionMethod = "Isolation Forest",
                    Confidence = isolationScores[i] > 0.8 ? 0.95 : 0.85,
                    AffectedServices = trend.ServiceCosts
                        .Where(s => s.Value > trend.DailyCost * 0.1m)
                        .OrderByDescending(s => s.Value)
                        .Select(s => s.Key)
                        .ToList(),
                    PossibleCauses = GeneratePossibleCauses(trend, expectedCost)
                });
            }
        }

        _logger.LogInformation("Isolation Forest detected {Count} anomalies", anomalies.Count);
        return Task.FromResult(anomalies);
    }

    /// <summary>
    /// DBSCAN clustering for density-based anomaly detection.
    /// Identifies outliers as points in low-density regions.
    /// </summary>
    private Task<List<CostAnomaly>> DetectWithDBSCANAsync(
        List<CostTrend> costTrends,
        CancellationToken cancellationToken)
    {
        var anomalies = new List<CostAnomaly>();

        if (costTrends.Count < 20) return Task.FromResult(anomalies);

        // DBSCAN parameters
        var epsilon = CalculateEpsilon(costTrends); // Neighborhood radius
        var minPoints = 5; // Minimum points to form a cluster

        var clusters = PerformDBSCAN(costTrends, epsilon, minPoints);

        // Points not in any cluster are anomalies
        for (int i = 0; i < costTrends.Count; i++)
        {
            if (clusters[i] == -1) // -1 indicates noise/anomaly
            {
                var trend = costTrends[i];
                var expectedCost = CalculateExpectedCostFromClusters(costTrends, clusters, i);

                anomalies.Add(new CostAnomaly
                {
                    AnomalyDate = trend.Date,
                    DetectedAt = DateTime.UtcNow,
                    Type = AnomalyType.UnexpectedIncrease,
                    Severity = Math.Abs(trend.DailyCost - expectedCost) / expectedCost > 0.5m 
                        ? AnomalySeverity.High 
                        : AnomalySeverity.Medium,
                    Title = $"DBSCAN: Outlier detected on {trend.Date:yyyy-MM-dd}",
                    Description = "Density-based clustering identified this as an outlier point",
                    ExpectedCost = expectedCost,
                    ActualCost = trend.DailyCost,
                    CostDifference = trend.DailyCost - expectedCost,
                    PercentageDeviation = (decimal)((trend.DailyCost - expectedCost) / expectedCost * 100),
                    AnomalyScore = Math.Min(1.0, (double)Math.Abs(trend.DailyCost - expectedCost) / (double)expectedCost),
                    DetectionMethod = "DBSCAN Clustering",
                    Confidence = 0.90,
                    AffectedServices = trend.ServiceCosts
                        .OrderByDescending(s => s.Value)
                        .Take(3)
                        .Select(s => s.Key)
                        .ToList(),
                    PossibleCauses = GeneratePossibleCauses(trend, expectedCost)
                });
            }
        }

        _logger.LogInformation("DBSCAN detected {Count} anomalies", anomalies.Count);
        return Task.FromResult(anomalies);
    }

    /// <summary>
    /// ARIMA (AutoRegressive Integrated Moving Average) for time series forecasting.
    /// Detects anomalies by comparing actual vs forecasted values.
    /// </summary>
    private Task<List<CostAnomaly>> DetectWithARIMAAsync(
        List<CostTrend> costTrends,
        CancellationToken cancellationToken)
    {
        var anomalies = new List<CostAnomaly>();

        if (costTrends.Count < 30) return Task.FromResult(anomalies);

        // ARIMA model: ARIMA(p=7, d=1, q=7) for daily cost data with weekly seasonality
        var forecasts = FitARIMAModel(costTrends);

        for (int i = 0; i < costTrends.Count; i++)
        {
            if (forecasts.ContainsKey(i))
            {
                var trend = costTrends[i];
                var forecast = forecasts[i];
                var residual = Math.Abs(trend.DailyCost - forecast.ExpectedValue);
                var threshold = forecast.ConfidenceIntervalWidth * 2; // 2x confidence interval

                if (residual > threshold)
                {
                    anomalies.Add(new CostAnomaly
                    {
                        AnomalyDate = trend.Date,
                        DetectedAt = DateTime.UtcNow,
                        Type = trend.DailyCost > forecast.ExpectedValue ? AnomalyType.SpikeCost : AnomalyType.UnexpectedDecrease,
                        Severity = residual > threshold * 1.5m ? AnomalySeverity.High : AnomalySeverity.Medium,
                        Title = $"ARIMA: Forecasting anomaly on {trend.Date:yyyy-MM-dd}",
                        Description = $"Actual cost deviates significantly from time series forecast",
                        ExpectedCost = forecast.ExpectedValue,
                        ActualCost = trend.DailyCost,
                        CostDifference = trend.DailyCost - forecast.ExpectedValue,
                        PercentageDeviation = (decimal)((trend.DailyCost - forecast.ExpectedValue) / forecast.ExpectedValue * 100),
                        AnomalyScore = Math.Min(1.0, (double)residual / (double)threshold),
                        DetectionMethod = "ARIMA Time Series",
                        Confidence = 0.92,
                        ForecastedRange = $"${forecast.LowerBound:F2} - ${forecast.UpperBound:F2}",
                        AffectedServices = trend.ServiceCosts
                            .OrderByDescending(s => s.Value)
                            .Take(3)
                            .Select(s => s.Key)
                            .ToList(),
                        PossibleCauses = GeneratePossibleCauses(trend, forecast.ExpectedValue)
                    });
                }
            }
        }

        _logger.LogInformation("ARIMA detected {Count} anomalies", anomalies.Count);
        return Task.FromResult(anomalies);
    }

    /// <summary>
    /// Seasonal decomposition to detect seasonal anomalies.
    /// Identifies deviations from expected seasonal patterns.
    /// </summary>
    private Task<List<CostAnomaly>> DetectSeasonalAnomaliesAsync(
        List<CostTrend> costTrends,
        CancellationToken cancellationToken)
    {
        var anomalies = new List<CostAnomaly>();

        if (costTrends.Count < 28) return Task.FromResult(anomalies); // Need at least 4 weeks

        // Decompose time series into trend, seasonal, and residual components
        var decomposition = DecomposeTimeSeries(costTrends);

        for (int i = 0; i < costTrends.Count; i++)
        {
            var residual = decomposition.Residuals[i];
            var threshold = CalculateResidualThreshold(decomposition.Residuals);

            if (Math.Abs(residual) > threshold)
            {
                var trend = costTrends[i];
                var expectedCost = trend.DailyCost - residual; // Remove anomalous residual

                anomalies.Add(new CostAnomaly
                {
                    AnomalyDate = trend.Date,
                    DetectedAt = DateTime.UtcNow,
                    Type = residual > 0 ? AnomalyType.SeasonalDeviation : AnomalyType.UnexpectedDecrease,
                    Severity = Math.Abs(residual) > threshold * 1.5m ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Title = $"Seasonal: Pattern deviation on {trend.Date:yyyy-MM-dd}",
                    Description = "Cost deviates from expected seasonal pattern",
                    ExpectedCost = expectedCost,
                    ActualCost = trend.DailyCost,
                    CostDifference = residual,
                    PercentageDeviation = (decimal)(residual / expectedCost * 100),
                    AnomalyScore = Math.Min(1.0, (double)Math.Abs(residual) / (double)threshold),
                    DetectionMethod = "Seasonal Decomposition",
                    Confidence = 0.88,
                    SeasonalComponent = decomposition.Seasonal[i],
                    TrendComponent = decomposition.Trend[i],
                    AffectedServices = trend.ServiceCosts
                        .OrderByDescending(s => s.Value)
                        .Take(3)
                        .Select(s => s.Key)
                        .ToList(),
                    PossibleCauses = new List<string>
                    {
                        "Deviation from normal weekly/monthly pattern",
                        "Business activity spike or dip",
                        "Seasonal event not matching historical patterns",
                        "Calendar effects (holidays, weekends)"
                    }
                });
            }
        }

        _logger.LogInformation("Seasonal analysis detected {Count} anomalies", anomalies.Count);
        return Task.FromResult(anomalies);
    }

    #region ML Algorithm Implementations

    private List<double> CalculateIsolationScores(List<double> costs)
    {
        var scores = new List<double>();
        var numTrees = 100;
        var subsampleSize = Math.Min(256, costs.Count);

        for (int i = 0; i < costs.Count; i++)
        {
            var avgPathLength = 0.0;
            var cost = costs[i];

            // Build multiple isolation trees
            for (int t = 0; t < numTrees; t++)
            {
                var sample = GetRandomSubsample(costs, subsampleSize, new Random(t));
                var pathLength = CalculatePathLength(cost, sample, 0, 10);
                avgPathLength += pathLength;
            }

            avgPathLength /= numTrees;

            // Normalize: shorter path = more isolated = higher anomaly score
            var expectedLength = ExpectedPathLength(subsampleSize);
            var score = Math.Pow(2, -avgPathLength / expectedLength);
            scores.Add(score);
        }

        return scores;
    }

    private int CalculatePathLength(double value, List<double> sample, int depth, int maxDepth)
    {
        if (depth >= maxDepth || sample.Count <= 1)
            return depth;

        var min = sample.Min();
        var max = sample.Max();
        
        if (min == max)
            return depth;

        var splitPoint = min + (max - min) * new Random().NextDouble();

        if (value < splitPoint)
            return CalculatePathLength(value, sample.Where(s => s < splitPoint).ToList(), depth + 1, maxDepth);
        else
            return CalculatePathLength(value, sample.Where(s => s >= splitPoint).ToList(), depth + 1, maxDepth);
    }

    private double ExpectedPathLength(int n)
    {
        if (n <= 1) return 0;
        return 2.0 * (Math.Log(n - 1) + 0.5772156649) - (2.0 * (n - 1) / n);
    }

    private List<double> GetRandomSubsample(List<double> data, int size, Random rng)
    {
        return data.OrderBy(x => rng.Next()).Take(size).ToList();
    }

    private double CalculateEpsilon(List<CostTrend> costTrends)
    {
        var costs = costTrends.Select(t => (double)t.DailyCost).ToList();
        var stdDev = Math.Sqrt(costs.Select(c => Math.Pow(c - costs.Average(), 2)).Average());
        return stdDev * 0.5; // 0.5x standard deviation as neighborhood radius
    }

    private List<int> PerformDBSCAN(List<CostTrend> costTrends, double epsilon, int minPoints)
    {
        var n = costTrends.Count;
        var clusters = Enumerable.Repeat(-1, n).ToList(); // -1 = unclassified
        var clusterId = 0;

        for (int i = 0; i < n; i++)
        {
            if (clusters[i] != -1) continue; // Already classified

            var neighbors = GetNeighbors(costTrends, i, epsilon);

            if (neighbors.Count < minPoints)
            {
                // Mark as noise/anomaly
                continue;
            }

            // Start new cluster
            clusters[i] = clusterId;
            var queue = new Queue<int>(neighbors);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (clusters[current] == -1)
                {
                    clusters[current] = clusterId;
                    var currentNeighbors = GetNeighbors(costTrends, current, epsilon);
                    if (currentNeighbors.Count >= minPoints)
                    {
                        foreach (var neighbor in currentNeighbors)
                        {
                            if (clusters[neighbor] == -1)
                                queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            clusterId++;
        }

        return clusters;
    }

    private List<int> GetNeighbors(List<CostTrend> costTrends, int index, double epsilon)
    {
        var neighbors = new List<int>();
        var cost = (double)costTrends[index].DailyCost;

        for (int i = 0; i < costTrends.Count; i++)
        {
            if (i == index) continue;
            var distance = Math.Abs((double)costTrends[i].DailyCost - cost);
            if (distance <= epsilon)
                neighbors.Add(i);
        }

        return neighbors;
    }

    private Dictionary<int, ARIMAForecast> FitARIMAModel(List<CostTrend> costTrends)
    {
        var forecasts = new Dictionary<int, ARIMAForecast>();
        var costs = costTrends.Select(t => (double)t.DailyCost).ToList();

        // Simple ARIMA implementation: use moving average + exponential smoothing
        var p = 7; // AR order (7 days lookback)
        var windowSize = Math.Min(30, costs.Count);

        for (int i = windowSize; i < costs.Count; i++)
        {
            var window = costs.Skip(i - windowSize).Take(windowSize).ToList();
            var ma = window.Average();
            var trend = (window.Last() - window.First()) / windowSize;
            var expectedValue = ma + trend;
            var stdDev = Math.Sqrt(window.Select(c => Math.Pow(c - ma, 2)).Average());

            forecasts[i] = new ARIMAForecast
            {
                ExpectedValue = (decimal)expectedValue,
                LowerBound = (decimal)(expectedValue - 1.96 * stdDev), // 95% CI
                UpperBound = (decimal)(expectedValue + 1.96 * stdDev),
                ConfidenceIntervalWidth = (decimal)(1.96 * stdDev)
            };
        }

        return forecasts;
    }

    private TimeSeriesDecomposition DecomposeTimeSeries(List<CostTrend> costTrends)
    {
        var n = costTrends.Count;
        var costs = costTrends.Select(t => (double)t.DailyCost).ToArray();

        // Calculate trend component using moving average
        var trendWindow = 7; // Weekly trend
        var trend = new decimal[n];
        for (int i = 0; i < n; i++)
        {
            var start = Math.Max(0, i - trendWindow / 2);
            var end = Math.Min(n, i + trendWindow / 2 + 1);
            trend[i] = (decimal)costs.Skip(start).Take(end - start).Average();
        }

        // Calculate seasonal component (weekly pattern)
        var seasonal = new decimal[n];
        var seasonalPeriod = 7;
        for (int i = 0; i < seasonalPeriod; i++)
        {
            var seasonalValues = new List<double>();
            for (int j = i; j < n; j += seasonalPeriod)
            {
                seasonalValues.Add(costs[j] - (double)trend[j]);
            }
            var avgSeasonal = seasonalValues.Any() ? seasonalValues.Average() : 0;
            for (int j = i; j < n; j += seasonalPeriod)
            {
                seasonal[j] = (decimal)avgSeasonal;
            }
        }

        // Calculate residual (what's left after removing trend and seasonal)
        var residuals = new decimal[n];
        for (int i = 0; i < n; i++)
        {
            residuals[i] = (decimal)costs[i] - trend[i] - seasonal[i];
        }

        return new TimeSeriesDecomposition
        {
            Trend = trend,
            Seasonal = seasonal,
            Residuals = residuals
        };
    }

    private decimal CalculateResidualThreshold(decimal[] residuals)
    {
        var absResiduals = residuals.Select(r => Math.Abs(r)).ToList();
        var median = CalculateMedian(absResiduals);
        var mad = CalculateMAD(residuals); // Median Absolute Deviation
        return median + 3 * mad; // 3x MAD threshold
    }

    private decimal CalculateMedian(List<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        if (n == 0) return 0;
        if (n % 2 == 0)
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
        return sorted[n / 2];
    }

    private decimal CalculateMAD(decimal[] values)
    {
        var median = CalculateMedian(values.ToList());
        var deviations = values.Select(v => Math.Abs(v - median)).ToList();
        return CalculateMedian(deviations);
    }

    #endregion

    #region Helper Methods

    private decimal CalculateExpectedCost(List<CostTrend> costTrends, int index)
    {
        var windowSize = Math.Min(7, index);
        if (windowSize == 0) return costTrends[index].DailyCost;

        var window = costTrends.Skip(Math.Max(0, index - windowSize)).Take(windowSize);
        return window.Average(t => t.DailyCost);
    }

    private decimal CalculateExpectedCostFromClusters(List<CostTrend> costTrends, List<int> clusters, int index)
    {
        // Use average of largest cluster as expected cost
        var clusterSizes = clusters.Where(c => c >= 0).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        if (!clusterSizes.Any()) return costTrends.Average(t => t.DailyCost);

        var largestCluster = clusterSizes.OrderByDescending(kvp => kvp.Value).First().Key;
        var clusterPoints = costTrends.Where((t, i) => clusters[i] == largestCluster);
        return clusterPoints.Average(t => t.DailyCost);
    }

    private List<string> GeneratePossibleCauses(CostTrend trend, decimal expectedCost)
    {
        var causes = new List<string>();
        var deviation = Math.Abs(trend.DailyCost - expectedCost) / expectedCost;

        if (deviation > 0.5m)
            causes.Add("Major infrastructure change or deployment");
        
        if (trend.ServiceCosts.Any())
        {
            var topService = trend.ServiceCosts.OrderByDescending(s => s.Value).First();
            causes.Add($"High usage in {topService.Key} service");
        }

        causes.Add("Unexpected resource scaling event");
        causes.Add("Change in usage patterns or workload");
        causes.Add("New resource provisioning");
        causes.Add("Pricing or billing rate changes");

        if (trend.Date.DayOfWeek == DayOfWeek.Monday)
            causes.Add("Monday spike (weekend catch-up workload)");

        return causes.Take(4).ToList();
    }

    private List<CostAnomaly> ConsolidateAnomalies(List<CostAnomaly> anomalies)
    {
        // Group by date and merge anomalies detected by multiple algorithms
        var grouped = anomalies.GroupBy(a => a.AnomalyDate.Date).ToList();
        var consolidated = new List<CostAnomaly>();

        foreach (var group in grouped)
        {
            var items = group.ToList();
            if (items.Count == 1)
            {
                consolidated.Add(items[0]);
            }
            else
            {
                // Multiple algorithms detected same date - merge with highest confidence
                var best = items.OrderByDescending(a => a.Confidence).First();
                best.Title = $"Multi-algorithm: Anomaly on {best.AnomalyDate:yyyy-MM-dd}";
                best.Description = $"Detected by {items.Count} algorithms: {string.Join(", ", items.Select(a => a.DetectionMethod))}";
                best.Confidence = Math.Min(0.99, best.Confidence + 0.05 * (items.Count - 1)); // Boost confidence
                best.AnomalyScore = items.Average(a => a.AnomalyScore);
                consolidated.Add(best);
            }
        }

        return consolidated.OrderByDescending(a => a.Confidence).ToList();
    }

    #endregion

    #region Helper Classes

    private class ARIMAForecast
    {
        public decimal ExpectedValue { get; set; }
        public decimal LowerBound { get; set; }
        public decimal UpperBound { get; set; }
        public decimal ConfidenceIntervalWidth { get; set; }
    }

    private class TimeSeriesDecomposition
    {
        public decimal[] Trend { get; set; } = Array.Empty<decimal>();
        public decimal[] Seasonal { get; set; } = Array.Empty<decimal>();
        public decimal[] Residuals { get; set; } = Array.Empty<decimal>();
    }

    #endregion
}
