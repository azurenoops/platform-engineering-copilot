using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Partial class containing analytics and history functions:
/// - get_compliance_history
/// - get_assessment_audit_log
/// - get_compliance_trends
/// </summary>
public partial class CompliancePlugin
{
    #region Compliance Analytics & Reporting

    [KernelFunction("get_compliance_history")]
    [Description("View historical compliance scores and how they've changed over time. " +
                 "Shows trend analysis to identify if compliance is improving or declining. " +
                 "Useful for tracking compliance posture over weeks/months and preparing for audits. " +
                 "Example: 'Show me compliance history for the last 30 days' or 'How has compliance changed over time?'")]
    public async Task<string> GetComplianceHistoryAsync(
        [Description("Azure subscription ID or friendly name. If not provided, uses default subscription.")] 
        string? subscriptionIdOrName = null,
        [Description("Number of days of history to retrieve (default: 30, max: 365)")] 
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Limit days to reasonable range
            days = Math.Min(Math.Max(days, 1), 365);
            
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            
            // Query historical assessments via engine
            var assessments = await _complianceEngine.GetComplianceHistoryAsync(
                subscriptionId,
                cutoffDate,
                DateTime.UtcNow,
                cancellationToken);

            if (!assessments.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"No compliance assessments found in the last {days} days",
                    subscriptionId,
                    hint = "Run a compliance assessment to start building historical data"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Calculate trend
            var trend = CalculateTrend(assessments.Select(a => (double)a.ComplianceScore).ToList());
            var latestScore = assessments.Last().ComplianceScore;
            var oldestScore = assessments.First().ComplianceScore;
            var scoreChange = latestScore - oldestScore;

            // Find best and worst scores
            var bestAssessment = assessments.OrderByDescending(a => a.ComplianceScore).First();
            var worstAssessment = assessments.OrderBy(a => a.ComplianceScore).First();

            // Format output
            var trendEmoji = trend.Direction switch
            {
                "improving" => "📈",
                "declining" => "📉",
                _ => "➡️"
            };

            var output = $@"
# 📊 COMPLIANCE HISTORY

**Subscription:** `{subscriptionId}`
**Period:** Last {days} days
**Assessments:** {assessments.Count} compliance checks

---

## 🎯 CURRENT STATUS

**Latest Score:** {Math.Round(latestScore, 1)}% ({GetComplianceGrade(Convert.ToDouble(latestScore))})
**Date:** {assessments.Last().CompletedAt:yyyy-MM-dd HH:mm} UTC
**Total Findings:** {assessments.Last().TotalFindings} ({assessments.Last().CriticalFindings} critical, {assessments.Last().HighFindings} high)

---

## {trendEmoji} TREND ANALYSIS

**Trend:** {trend.Direction.ToUpper()} {(trend.Direction != "stable" ? $"({Math.Abs(Math.Round(trend.ChangeRate, 1))}% per assessment)" : "")}
**Score Change:** {(scoreChange >= 0 ? "+" : "")}{Math.Round(scoreChange, 1)}% (from {Math.Round(oldestScore, 1)}% to {Math.Round(latestScore, 1)}%)

{(trend.Direction == "improving" ? "✅ **Great progress!** Your compliance posture is getting better." : 
  trend.Direction == "declining" ? "⚠️ **Attention needed!** Compliance is declining - review recent changes." :
  "ℹ️ Compliance has remained relatively stable.")}

---

## 📈 HISTORICAL SCORES

{string.Join("\n", assessments.Select(a => 
    $"- **{a.CompletedAt:MMM dd, yyyy}**: {Math.Round(a.ComplianceScore, 1)}% {GenerateScoreBar(Convert.ToDouble(a.ComplianceScore))} ({a.TotalFindings} findings)"))}

---

## 🏆 BEST & WORST

**Best Score:** {Math.Round(bestAssessment.ComplianceScore, 1)}% on {bestAssessment.CompletedAt:yyyy-MM-dd}
**Worst Score:** {Math.Round(worstAssessment.ComplianceScore, 1)}% on {worstAssessment.CompletedAt:yyyy-MM-dd}
**Range:** {Math.Round(bestAssessment.ComplianceScore - worstAssessment.ComplianceScore, 1)}% variance

---

## 💡 INSIGHTS

{(assessments.Count < 3 ? "📌 Run more assessments to build a better trend analysis (3+ recommended)." : "")}
{(trend.Direction == "improving" && latestScore < 90 ? $"📌 You're on track! Keep improving to reach 90% (currently {Math.Round(90 - latestScore, 1)}% away)." : "")}
{(trend.Direction == "declining" ? "📌 Review recent infrastructure changes that may have introduced compliance issues." : "")}
{(latestScore >= 90 ? "📌 Excellent! Maintain your current score with regular monitoring." : "")}

**Next Steps:**
- Run 'get compliance trends' for detailed analysis
- Run 'get assessment audit log' to see who ran assessments
- Run 'check NIST 800-53 compliance' for latest findings
";

            return JsonSerializer.Serialize(new
            {
                success = true,
                formatted_output = output,
                subscriptionId,
                period = new
                {
                    days,
                    startDate = cutoffDate,
                    endDate = DateTime.UtcNow,
                    assessmentCount = assessments.Count
                },
                trend = new
                {
                    direction = trend.Direction,
                    changeRate = Math.Round(trend.ChangeRate, 2),
                    scoreChange = Math.Round(scoreChange, 1),
                    interpretation = trend.Direction == "improving" ? "Compliance is improving over time" :
                                   trend.Direction == "declining" ? "Compliance is declining - needs attention" :
                                   "Compliance remains stable"
                },
                currentStatus = new
                {
                    score = Math.Round(latestScore, 1),
                    grade = GetComplianceGrade(Convert.ToDouble(latestScore)),
                    date = assessments.Last().CompletedAt,
                    findings = assessments.Last().TotalFindings
                },
                bestScore = Math.Round(bestAssessment.ComplianceScore, 1),
                worstScore = Math.Round(worstAssessment.ComplianceScore, 1),
                history = assessments.Select(a => new
                {
                    date = a.CompletedAt,
                    score = Math.Round(a.ComplianceScore, 1),
                    findings = a.TotalFindings,
                    critical = a.CriticalFindings,
                    high = a.HighFindings
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance history");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to retrieve compliance history: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_assessment_audit_log")]
    [Description("View audit trail of compliance assessments showing who ran them and when. " +
                 "Useful for audit compliance, accountability, and tracking assessment frequency. " +
                 "Example: 'Who ran compliance assessments this week?' or 'Show me audit log'")]
    public async Task<string> GetAssessmentAuditLogAsync(
        [Description("Azure subscription ID or friendly name. If not provided, uses default subscription.")] 
        string? subscriptionIdOrName = null,
        [Description("Number of days of audit history to retrieve (default: 7, max: 90)")] 
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Limit days to reasonable range
            days = Math.Min(Math.Max(days, 1), 90);
            
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            
            // Query audit log via engine
            var auditLog = await _complianceEngine.GetAssessmentAuditLogAsync(
                subscriptionId,
                cutoffDate,
                DateTime.UtcNow,
                cancellationToken);

            if (!auditLog.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"No assessments found in the last {days} days",
                    subscriptionId,
                    hint = "Run a compliance assessment to start building audit history"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Group by user
            var byUser = auditLog.GroupBy(a => a.InitiatedBy ?? "Unknown")
                .Select(g => new
                {
                    user = g.Key,
                    count = g.Count(),
                    lastRun = g.Max(a => a.CompletedAt)
                })
                .OrderByDescending(u => u.count)
                .ToList();

            // Calculate statistics
            var totalAssessments = auditLog.Count;
            var avgDuration = auditLog.Where(a => a.Duration.HasValue)
                .Select(a => TimeSpan.FromTicks(a.Duration!.Value).TotalSeconds)
                .DefaultIfEmpty(0)
                .Average();
            var completedCount = auditLog.Count(a => a.Status == "Completed");
            var failedCount = auditLog.Count(a => a.Status != "Completed");

            var output = $@"
# 📋 COMPLIANCE ASSESSMENT AUDIT LOG

**Subscription:** `{subscriptionId}`
**Period:** Last {days} days
**Total Assessments:** {totalAssessments}

---

## 📊 SUMMARY

| Metric | Value |
|--------|-------|
| ✅ **Completed** | {completedCount} |
| ❌ **Failed** | {failedCount} |
| ⏱️ **Avg Duration** | {Math.Round(avgDuration, 1)}s |
| 👥 **Unique Users** | {byUser.Count} |

---

## 👥 ASSESSMENTS BY USER

{string.Join("\n", byUser.Select(u => 
    $"- **{u.user}**: {u.count} assessment{(u.count > 1 ? "s" : "")} (last: {u.lastRun:MMM dd, HH:mm})"))}

---

## 📝 RECENT ASSESSMENTS

{string.Join("\n", auditLog.Take(10).Select(a => 
    $@"### {a.CompletedAt:yyyy-MM-dd HH:mm} UTC
- **ID:** `{a.Id}`
- **User:** {a.InitiatedBy ?? "Unknown"}
- **Status:** {(a.Status == "Completed" ? "✅" : "❌")} {a.Status}
- **Score:** {Math.Round(a.ComplianceScore, 1)}% ({a.TotalFindings} findings: {a.CriticalFindings} critical, {a.HighFindings} high)
- **Scope:** {(string.IsNullOrEmpty(a.ResourceGroupName) ? "Full subscription" : $"Resource group: {a.ResourceGroupName}")}
- **Duration:** {(a.Duration.HasValue ? $"{Math.Round(TimeSpan.FromTicks(a.Duration.Value).TotalSeconds, 1)}s" : "N/A")}
"))}

{(auditLog.Count > 10 ? $"\n*Showing 10 of {auditLog.Count} assessments*" : "")}

---

## 💡 INSIGHTS

{(totalAssessments < 5 ? "📌 Low assessment frequency - consider running weekly checks to track compliance drift." : "")}
{(failedCount > 0 ? $"⚠️ {failedCount} assessment{(failedCount > 1 ? "s" : "")} failed - review logs for errors." : "")}
{(avgDuration > 30 ? "⏱️ Assessments taking longer than expected - consider scoping to resource groups." : "")}
{(byUser.Count == 1 ? "👤 Single user running assessments - consider sharing responsibility across team." : "")}

**Next Steps:**
- Run 'get compliance history' to see score trends
- Run 'get compliance trends' for detailed analytics
";

            return JsonSerializer.Serialize(new
            {
                success = true,
                formatted_output = output,
                subscriptionId,
                period = new { days, startDate = cutoffDate, endDate = DateTime.UtcNow },
                statistics = new
                {
                    totalAssessments,
                    completed = completedCount,
                    failed = failedCount,
                    averageDuration = Math.Round(avgDuration, 1),
                    uniqueUsers = byUser.Count
                },
                byUser = byUser.Select(u => new
                {
                    user = u.user,
                    assessmentCount = u.count,
                    lastRun = u.lastRun
                }),
                recentAssessments = auditLog.Take(20).Select(a => new
                {
                    id = a.Id,
                    date = a.CompletedAt,
                    user = a.InitiatedBy,
                    status = a.Status,
                    score = Math.Round(a.ComplianceScore, 1),
                    findings = a.TotalFindings,
                    duration = a.Duration.HasValue ? TimeSpan.FromTicks(a.Duration.Value).TotalSeconds : (double?)null,
                    scope = string.IsNullOrEmpty(a.ResourceGroupName) ? "subscription" : a.ResourceGroupName
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to retrieve audit log: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_compliance_trends")]
    [Description("Analyze compliance trends with detailed metrics and comparison reports. " +
                 "Shows which findings are increasing/decreasing, control family performance, and predictive insights. " +
                 "Example: 'Analyze compliance trends for the last quarter' or 'What are my persistent compliance issues?'")]
    public async Task<string> GetComplianceTrendsAsync(
        [Description("Azure subscription ID or friendly name. If not provided, uses default subscription.")] 
        string? subscriptionIdOrName = null,
        [Description("Number of days to analyze (default: 90, max: 365)")] 
        int days = 90,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Limit days to reasonable range
            days = Math.Min(Math.Max(days, 7), 365);
            
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            
            // Query assessments with findings via engine
            var assessments = await _complianceEngine.GetComplianceTrendsDataAsync(
                subscriptionId,
                cutoffDate,
                DateTime.UtcNow,
                cancellationToken);

            if (assessments.Count < 2)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Need at least 2 assessments for trend analysis (found: {assessments.Count})",
                    subscriptionId,
                    hint = "Run more compliance assessments over time to build trend data"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Calculate overall trend
            var scoreTrend = CalculateTrend(assessments.Select(a => (double)a.ComplianceScore).ToList());
            
            // Analyze findings trends
            var firstAssessment = assessments.First();
            var latestAssessment = assessments.Last();
            
            var findingChange = new
            {
                total = latestAssessment.TotalFindings - firstAssessment.TotalFindings,
                critical = latestAssessment.CriticalFindings - firstAssessment.CriticalFindings,
                high = latestAssessment.HighFindings - firstAssessment.HighFindings,
                medium = latestAssessment.MediumFindings - firstAssessment.MediumFindings,
                low = latestAssessment.LowFindings - firstAssessment.LowFindings
            };

            // Find persistent issues (findings in multiple assessments)
            var allFindingIds = assessments
                .SelectMany(a => a.Findings.Select(f => f.RuleId))
                .GroupBy(id => id)
                .Where(g => g.Count() >= Math.Max(2, assessments.Count / 2))
                .Select(g => new { ruleId = g.Key, occurrences = g.Count() })
                .OrderByDescending(f => f.occurrences)
                .Take(10)
                .ToList();

            // Severity distribution over time
            var severityTrends = new
            {
                critical = CalculateTrend(assessments.Select(a => (double)a.CriticalFindings).ToList()),
                high = CalculateTrend(assessments.Select(a => (double)a.HighFindings).ToList()),
                medium = CalculateTrend(assessments.Select(a => (double)a.MediumFindings).ToList()),
                low = CalculateTrend(assessments.Select(a => (double)a.LowFindings).ToList())
            };

            var output = $@"
# 📊 COMPLIANCE TRENDS ANALYSIS

**Subscription:** `{subscriptionId}`
**Analysis Period:** {days} days ({assessments.First().CompletedAt:MMM dd, yyyy} - {assessments.Last().CompletedAt:MMM dd, yyyy})
**Assessments Analyzed:** {assessments.Count}

---

## 🎯 OVERALL TREND

**Direction:** {scoreTrend.Direction.ToUpper()} {(scoreTrend.Direction != "stable" ? $"({Math.Abs(Math.Round(scoreTrend.ChangeRate, 1))}% change per assessment)" : "")}
**Score Change:** {(latestAssessment.ComplianceScore >= firstAssessment.ComplianceScore ? "+" : "")}{Math.Round(latestAssessment.ComplianceScore - firstAssessment.ComplianceScore, 1)}%
**Current:** {Math.Round(latestAssessment.ComplianceScore, 1)}% (was {Math.Round(firstAssessment.ComplianceScore, 1)}%)

{(scoreTrend.Direction == "improving" ? "✅ **Excellent!** Compliance is trending upward." :
  scoreTrend.Direction == "declining" ? "⚠️ **Alert!** Compliance is trending downward - immediate attention needed." :
  "ℹ️ Compliance remains relatively stable.")}

---

## 📈 FINDINGS TRENDS

| Severity | Change | Trend | Current |
|----------|--------|-------|---------|
| 🔴 **Critical** | {(findingChange.critical >= 0 ? "+" : "")}{findingChange.critical} | {severityTrends.critical.Direction} | {latestAssessment.CriticalFindings} |
| 🟠 **High** | {(findingChange.high >= 0 ? "+" : "")}{findingChange.high} | {severityTrends.high.Direction} | {latestAssessment.HighFindings} |
| 🟡 **Medium** | {(findingChange.medium >= 0 ? "+" : "")}{findingChange.medium} | {severityTrends.medium.Direction} | {latestAssessment.MediumFindings} |
| 🟢 **Low** | {(findingChange.low >= 0 ? "+" : "")}{findingChange.low} | {severityTrends.low.Direction} | {latestAssessment.LowFindings} |
| **Total** | {(findingChange.total >= 0 ? "+" : "")}{findingChange.total} | | {latestAssessment.TotalFindings} |

---

## 🔁 PERSISTENT ISSUES

{(allFindingIds.Any() ? 
    $"*Issues appearing in {Math.Max(2, assessments.Count / 2)}+ assessments:*\n\n" +
    string.Join("\n", allFindingIds.Select(f => 
        $"- **{f.ruleId}**: Appeared in {f.occurrences}/{assessments.Count} assessments")) :
    "*No persistent issues found - findings are being resolved!*")}

---

## 📊 SCORE TIMELINE

{string.Join("\n", assessments.Select(a => 
    $"- **{a.CompletedAt:MMM dd}**: {Math.Round(a.ComplianceScore, 1)}% {GenerateScoreBar(Convert.ToDouble(a.ComplianceScore))}"))}

---

## 💡 INSIGHTS & RECOMMENDATIONS

{(scoreTrend.Direction == "improving" && latestAssessment.ComplianceScore < 90 ? 
    $"📈 You're making progress! At current rate, you could reach 90% in approximately {Math.Ceiling((90 - (double)latestAssessment.ComplianceScore) / Math.Abs(scoreTrend.ChangeRate))} assessments." : "")}

{(scoreTrend.Direction == "declining" ? 
    "⚠️ **Action Required:** Investigate recent infrastructure changes. Review the persistent issues above." : "")}

{(findingChange.critical > 0 ? 
    $"🔴 **Critical Alert:** {findingChange.critical} new critical finding{(Math.Abs(findingChange.critical) > 1 ? "s" : "")} since {firstAssessment.CompletedAt:MMM dd}. Immediate remediation needed!" : "")}

{(allFindingIds.Any() ? 
    $"🔁 {allFindingIds.Count} persistent issue{(allFindingIds.Count > 1 ? "s" : "")} detected. These require strategic remediation or policy changes." : "")}

{(scoreTrend.Direction == "stable" && latestAssessment.ComplianceScore >= 90 ? 
    "✅ **Excellent!** Maintaining high compliance. Continue regular monitoring." : "")}

**Recommended Actions:**
{(findingChange.critical > 0 ? "1. Generate remediation plan for critical findings\n" : "")}
{(allFindingIds.Any() ? $"{(findingChange.critical > 0 ? "2" : "1")}. Address persistent issues with policy/process changes\n" : "")}
{(scoreTrend.Direction == "declining" ? $"{(findingChange.critical > 0 || allFindingIds.Any() ? "3" : "1")}. Review recent deployments for compliance drift\n" : "")}
- Run 'get control family details' for specific issue breakdown
- Set up automated weekly assessments to catch drift early
";

            return JsonSerializer.Serialize(new
            {
                success = true,
                formatted_output = output,
                subscriptionId,
                analysisperiod = new
                {
                    days,
                    startDate = firstAssessment.CompletedAt,
                    endDate = latestAssessment.CompletedAt,
                    assessmentCount = assessments.Count
                },
                overallTrend = new
                {
                    direction = scoreTrend.Direction,
                    changeRate = Math.Round(scoreTrend.ChangeRate, 2),
                    scoreChange = Math.Round((double)(latestAssessment.ComplianceScore - firstAssessment.ComplianceScore), 1),
                    currentScore = Math.Round(latestAssessment.ComplianceScore, 1),
                    previousScore = Math.Round(firstAssessment.ComplianceScore, 1)
                },
                findingsTrends = new
                {
                    total = findingChange.total,
                    critical = new { change = findingChange.critical, trend = severityTrends.critical.Direction },
                    high = new { change = findingChange.high, trend = severityTrends.high.Direction },
                    medium = new { change = findingChange.medium, trend = severityTrends.medium.Direction },
                    low = new { change = findingChange.low, trend = severityTrends.low.Direction }
                },
                persistentIssues = allFindingIds.Select(f => new
                {
                    ruleId = f.ruleId,
                    occurrences = f.occurrences,
                    percentage = Math.Round((double)f.occurrences / assessments.Count * 100, 0)
                }),
                recommendations = new[]
                {
                    findingChange.critical > 0 ? "Immediately address new critical findings" : null,
                    allFindingIds.Any() ? "Create strategic plan for persistent issues" : null,
                    scoreTrend.Direction == "declining" ? "Review recent infrastructure changes" : null,
                    scoreTrend.Direction == "improving" ? "Continue current compliance practices" : null
                }.Where(r => r != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing compliance trends");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to analyze trends: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }


    #endregion
}
