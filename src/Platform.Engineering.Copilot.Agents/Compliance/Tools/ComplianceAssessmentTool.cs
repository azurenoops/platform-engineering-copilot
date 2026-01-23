using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Compliance.Configuration;
using Platform.Engineering.Copilot.Agents.Compliance.State;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// Tool for running compliance assessments against Azure subscriptions.
/// Uses the real AtoComplianceEngine to scan actual Azure resources.
/// Supports NIST 800-53, FedRAMP, and other compliance frameworks.
/// </summary>
public class ComplianceAssessmentTool : BaseTool
{
    private readonly ComplianceStateAccessors _stateAccessors;
    private readonly ComplianceAgentOptions _options;
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly IProvisionedEnvironmentService _environmentService;

    public override string Name => "run_compliance_assessment";

    public override string Description =>
        "Runs a REAL compliance assessment against an Azure subscription using NIST 800-53, FedRAMP, " +
        "or other frameworks. Scans actual Azure resources using Azure Resource Graph and Defender for Cloud. " +
        "Returns findings organized by control family and severity with remediation guidance.";

    public ComplianceAssessmentTool(
        ILogger<ComplianceAssessmentTool> logger,
        ComplianceStateAccessors stateAccessors,
        IOptions<ComplianceAgentOptions> options,
        IAtoComplianceEngine complianceEngine,
        IProvisionedEnvironmentService environmentService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _options = options?.Value ?? new ComplianceAgentOptions();
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));

        Parameters.Add(new ToolParameter("environment_id", "Environment ID to assess (scans all subscriptions in environment). If provided, subscription_id is ignored.", false));
        Parameters.Add(new ToolParameter("subscription_id", "Azure subscription ID to assess", false));
        Parameters.Add(new ToolParameter("resource_group", "Optional resource group to scope assessment", false));
        Parameters.Add(new ToolParameter("control_families", "Comma-separated control families to assess (e.g., AC,AU,SC). Default: all", false));
        Parameters.Add(new ToolParameter("include_passed", "Include passed controls in results. Default: false", false));
        Parameters.Add(new ToolParameter("skip_cache", "Skip cached results and force a fresh scan. Default: false", false));
        Parameters.Add(new ToolParameter("conversation_id", "Conversation ID for state tracking", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var conversationId = GetOptionalString(arguments, "conversation_id") ?? Guid.NewGuid().ToString();
            var environmentId = GetOptionalString(arguments, "environment_id");
            var resourceGroup = GetOptionalString(arguments, "resource_group");
            var controlFamiliesStr = GetOptionalString(arguments, "control_families");
            var includePassed = GetOptionalBool(arguments, "include_passed", false);
            var skipCache = GetOptionalBool(arguments, "skip_cache", false);

            // ENVIRONMENT-SCOPED SCANNING
            if (!string.IsNullOrEmpty(environmentId))
            {
                Logger.LogInformation("Running environment-scoped compliance assessment for environment: {EnvironmentId}", environmentId);

                // Fetch provisioned environment
                var environment = await _environmentService.GetEnvironmentAsync(environmentId, cancellationToken);
                if (environment == null)
                {
                    return ToJson(new {
                        success = false,
                        error = $"Environment '{environmentId}' not found. Use 'list_provisioned_environments' tool to see available environments."
                    });
                }

                // ProvisionedEnvironment has a single SubscriptionId, not a list
                var subscriptionIds = new List<string> { environment.SubscriptionId };
                if (string.IsNullOrEmpty(environment.SubscriptionId))
                {
                    return ToJson(new {
                        success = false,
                        error = $"Environment '{environment.Name}' ({environmentId}) has no subscription configured."
                    });
                }

                Logger.LogInformation("Scanning environment '{EnvironmentName}' ({EnvironmentId}) with subscription {SubId}",
                    environment.Name, environmentId, environment.SubscriptionId);

                // Run environment assessment using the engine (with single subscription)
                var envAssessment = await _complianceEngine.RunEnvironmentAssessmentAsync(
                    environmentId,
                    environment.Name,
                    subscriptionIds,
                    !string.IsNullOrEmpty(environment.ResourceGroup) ? new[] { environment.ResourceGroup } : null,
                    null, // No progress reporter in tool
                    cancellationToken);

                // Track the operation
                await _stateAccessors.TrackComplianceOperationAsync(
                    conversationId,
                    "environment_assessment",
                    _options.DefaultFramework,
                    environmentId,
                    true,
                    envAssessment.TotalFindings,
                    envAssessment.Duration,
                    cancellationToken);

                // Format results for LLM
                var duration = DateTime.UtcNow - startTime;
                var envResult = FormatAssessmentResult(envAssessment, null, includePassed, isEnvironmentScan: true);
                
                return ToJson(new
                {
                    success = true,
                    assessment = envResult,
                    message = $"Scanned environment '{environment.Name}' ({environmentId}) in subscription {environment.SubscriptionId}. " +
                              $"Compliance: {envAssessment.OverallComplianceScore:F1}% ({envAssessment.TotalFindings} total findings: " +
                              $"{envAssessment.CriticalFindings} critical, {envAssessment.HighFindings} high, {envAssessment.MediumFindings} medium, {envAssessment.LowFindings} low)"
                });
            }

            // SUBSCRIPTION-SCOPED SCANNING (existing logic)
            // Get subscription from argument or state
            var subscriptionId = GetOptionalString(arguments, "subscription_id")
                ?? await _stateAccessors.GetCurrentSubscriptionAsync(conversationId, cancellationToken)
                ?? _options.DefaultSubscriptionId;

            if (string.IsNullOrEmpty(subscriptionId))
            {
                return ToJson(new { 
                    success = false, 
                    error = "Subscription ID is required. Use 'Set my subscription to <id>' first or provide subscription_id parameter." 
                });
            }

            Logger.LogInformation("Running REAL compliance assessment: Subscription={Subscription}, ResourceGroup={ResourceGroup}",
                subscriptionId, resourceGroup ?? "all");

            // Check for cached assessment (optional - can be disabled for always-fresh scans)
            var cacheHours = _options.Assessment.CacheDurationHours;
            if (cacheHours > 0 && !skipCache)
            {
                var cached = await _complianceEngine.GetCachedAssessmentAsync(
                    subscriptionId, resourceGroup, cacheHours, cancellationToken);
                if (cached != null)
                {
                    Logger.LogInformation("Returning cached assessment from {CachedAt}", cached.CompletedAt);
                    return ToJson(new
                    {
                        success = true,
                        fromCache = true,
                        assessment = FormatCachedAssessment(cached, controlFamiliesStr, includePassed)
                    });
                }
            }

            // Run REAL compliance assessment using AtoComplianceEngine
            Logger.LogInformation("Starting real Azure compliance scan...");
            
            var progress = new Progress<AssessmentProgress>(p =>
            {
                Logger.LogDebug("Assessment progress: {CompletedFamilies}/{TotalFamilies} - {CurrentFamily}: {Message}",
                    p.CompletedFamilies, p.TotalFamilies, p.CurrentFamily, p.Message);
            });

            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId, 
                resourceGroup,
                progress, 
                cancellationToken);

            // Check for errors
            if (!string.IsNullOrEmpty(assessment.Error))
            {
                Logger.LogWarning("Assessment completed with errors: {Error}", assessment.Error);
            }

            // Parse control families filter
            var controlFamilies = string.IsNullOrEmpty(controlFamiliesStr)
                ? null
                : controlFamiliesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim().ToUpper())
                    .ToHashSet();

            // Format and return results
            var result = FormatAssessmentResult(assessment, controlFamilies, includePassed, isEnvironmentScan: false);

            // Share summary with other agents
            var summary = new AssessmentSummary
            {
                AssessmentId = assessment.AssessmentId,
                Framework = _options.DefaultFramework,
                SubscriptionId = subscriptionId,
                AssessedAt = assessment.EndTime.DateTime,
                CompliancePercentage = assessment.OverallComplianceScore,
                CriticalFindings = assessment.CriticalFindings,
                HighFindings = assessment.HighFindings,
                MediumFindings = assessment.MediumFindings,
                LowFindings = assessment.LowFindings,
                TopControlFamiliesWithIssues = assessment.ControlFamilyResults
                    .Where(kv => kv.Value.Findings.Count > 0)
                    .OrderByDescending(kv => kv.Value.Findings.Count)
                    .Take(5)
                    .Select(kv => kv.Key)
                    .ToList()
            };
            await _stateAccessors.ShareAssessmentSummaryAsync(conversationId, summary, cancellationToken);

            // Track operation
            await _stateAccessors.TrackComplianceOperationAsync(
                conversationId, "assessment", _options.DefaultFramework, subscriptionId,
                true, assessment.TotalFindings, DateTime.UtcNow - startTime, cancellationToken);

            Logger.LogInformation("Compliance assessment complete: {Score}% compliant, {Findings} findings in {Duration}ms",
                assessment.OverallComplianceScore.ToString("F1"), 
                assessment.TotalFindings,
                (DateTime.UtcNow - startTime).TotalMilliseconds);

            return ToJson(new
            {
                success = true,
                assessment = result,
                message = $"Scanned {assessment.ControlFamilyResults.Count} control families across Azure subscription. " +
                          $"Compliance: {assessment.OverallComplianceScore:F1}% ({assessment.TotalFindings} total findings: " +
                          $"{assessment.CriticalFindings} critical, {assessment.HighFindings} high, {assessment.MediumFindings} medium, {assessment.LowFindings} low)"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error running compliance assessment");
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private object FormatAssessmentResult(
        AtoComplianceAssessment assessment, 
        HashSet<string>? controlFamilyFilter,
        bool includePassed,
        bool isEnvironmentScan = false)
    {
        var filteredFamilies = assessment.ControlFamilyResults
            .Where(kv => controlFamilyFilter == null || controlFamilyFilter.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var allFindings = filteredFamilies.Values
            .SelectMany(f => f.Findings)
            .Where(f => includePassed || f.ComplianceStatus != AtoComplianceStatus.Compliant)
            .ToList();

        return new
        {
            assessmentId = assessment.AssessmentId,
            subscriptionId = assessment.SubscriptionId,
            environmentId = assessment.EnvironmentId,
            environmentName = assessment.EnvironmentName,
            additionalSubscriptions = assessment.AdditionalSubscriptionIds,
            isEnvironmentScan = isEnvironmentScan,
            startTime = assessment.StartTime,
            endTime = assessment.EndTime,
            duration = assessment.Duration.TotalSeconds,
            summary = new
            {
                overallComplianceScore = assessment.OverallComplianceScore,
                totalFindings = assessment.TotalFindings,
                criticalFindings = assessment.CriticalFindings,
                highFindings = assessment.HighFindings,
                mediumFindings = assessment.MediumFindings,
                lowFindings = assessment.LowFindings,
                informationalFindings = assessment.InformationalFindings,
                controlFamiliesAssessed = filteredFamilies.Count
            },
            executiveSummary = assessment.ExecutiveSummary,
            recommendations = assessment.Recommendations,
            findingsByFamily = filteredFamilies.ToDictionary(
                kv => kv.Key,
                kv => new
                {
                    familyName = kv.Value.FamilyName,
                    complianceScore = kv.Value.ComplianceScore,
                    totalControls = kv.Value.TotalControls,
                    passedControls = kv.Value.PassedControls,
                    findingsCount = kv.Value.Findings.Count
                }),
            findings = allFindings.Select(f => new
            {
                findingId = f.Id,
                controlId = f.RuleId,
                // Use AffectedNistControls (populated by scanners) or fall back to AffectedControls
                controls = f.AffectedNistControls.Any() 
                    ? string.Join(", ", f.AffectedNistControls) 
                    : (f.AffectedControls.Any() ? string.Join(", ", f.AffectedControls) : "Not specified"),
                controlFamily = f.AffectedNistControls.FirstOrDefault() ?? f.AffectedControls.FirstOrDefault() ?? "Unknown",
                severity = f.Severity.ToString(),
                status = f.ComplianceStatus.ToString(),
                title = f.Title,
                description = f.Description,
                resourceId = f.ResourceId,
                resourceType = f.ResourceType,
                resourceName = f.ResourceName,
                canAutoRemediate = f.IsAutoRemediable,
                remediationGuidance = f.RemediationGuidance
            }).OrderBy(f => f.severity == "Critical" ? 0 : f.severity == "High" ? 1 : f.severity == "Medium" ? 2 : 3)
              .ThenBy(f => f.controlFamily)
              .ThenBy(f => f.controlId)
        };
    }

    private object FormatCachedAssessment(
        ComplianceAssessmentWithFindings cached,
        string? controlFamiliesStr,
        bool includePassed)
    {
        var controlFamilyFilter = string.IsNullOrEmpty(controlFamiliesStr)
            ? null
            : controlFamiliesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim().ToUpper())
                .ToHashSet();

        // Note: ComplianceFindingSummary doesn't have ControlFamily directly,
        // so we'll use ControlId prefix to infer family (e.g., "AC-1" -> "AC")
        var filteredFindings = cached.Findings
            .Where(f => controlFamilyFilter == null || 
                       (f.ControlId != null && controlFamilyFilter.Any(cf => f.ControlId.StartsWith(cf, StringComparison.OrdinalIgnoreCase))))
            .Where(f => includePassed || f.ComplianceStatus != "Passed")
            .ToList();

        var findingsByFamily = filteredFindings
            .GroupBy(f => ExtractControlFamily(f.ControlId))
            .ToDictionary(
                g => g.Key,
                g => new { count = g.Count(), findings = g.ToList() });

        var findingsBySeverity = filteredFindings
            .GroupBy(f => f.Severity ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        return new
        {
            assessmentId = cached.Id,
            subscriptionId = cached.SubscriptionId,
            assessedAt = cached.CompletedAt,
            summary = new
            {
                overallComplianceScore = cached.ComplianceScore,
                totalFindings = filteredFindings.Count,
                criticalFindings = findingsBySeverity.GetValueOrDefault("Critical", 0),
                highFindings = findingsBySeverity.GetValueOrDefault("High", 0),
                mediumFindings = findingsBySeverity.GetValueOrDefault("Medium", 0),
                lowFindings = findingsBySeverity.GetValueOrDefault("Low", 0)
            },
            findingsByFamily = findingsByFamily.ToDictionary(kv => kv.Key, kv => kv.Value.count),
            findings = filteredFindings.Select(f => new
            {
                findingId = f.FindingId,
                controlId = f.ControlId,
                controlFamily = ExtractControlFamily(f.ControlId),
                severity = f.Severity,
                status = f.ComplianceStatus,
                title = f.Title,
                resourceId = f.ResourceId,
                ruleId = f.RuleId
            })
        };
    }

    private static string ExtractControlFamily(string? controlId)
    {
        if (string.IsNullOrEmpty(controlId)) return "Unknown";
        
        // Extract family prefix (e.g., "AC-1" -> "AC", "AU-2" -> "AU")
        var dashIndex = controlId.IndexOf('-');
        return dashIndex > 0 ? controlId[..dashIndex].ToUpper() : controlId.ToUpper();
    }
}
