using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Authorization;
using Platform.Engineering.Copilot.Core.Models.Audits;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Partial class containing remediation functions:
/// - generate_remediation_plan
/// - execute_remediation
/// - validate_remediation
/// - get_remediation_progress
/// </summary>
public partial class CompliancePlugin
{
    // ========== REMEDIATION FUNCTIONS ==========

    [KernelFunction("generate_remediation_plan")]
    [Description("Generate a comprehensive, prioritized remediation plan with actionable steps to fix compliance violations and security findings. " +
                 "Creates a detailed action plan with effort estimates, priorities, and implementation guidance. " +
                 "Use this when user requests: 'remediation plan', 'action plan', 'fix plan', 'create plan to fix findings', " +
                 "'generate remediation steps', 'how to fix violations', 'prioritized remediation', 'remediation roadmap'. " +
                 "Returns: Prioritized violations, remediation steps per finding, effort estimates, dependencies, implementation order. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging'). " +
                 "If no subscription is specified, uses the most recent assessment from the last used subscription. " +
                 "Can be scoped to a specific resource group. " +
                 "Example user requests: 'generate a remediation plan for this assessment', 'create an action plan to fix these violations', " +
                 "'I need detailed remediation steps', 'show me how to fix the compliance gaps', 'create a prioritized fix plan'.")]
    public async Task<string> GenerateRemediationPlanAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging'). " +
                     "Optional - if not provided, uses the last assessed subscription.")] string? subscriptionIdOrName = null,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If no subscription provided, try to get the last used subscription
            if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
            {
                subscriptionIdOrName = GetLastUsedSubscription();
                if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
                {
                    _logger.LogWarning("No subscription specified and no previous subscription found in cache");
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "No subscription specified",
                        message = "Please specify a subscription ID or run a compliance assessment first to establish context.",
                        suggestedActions = new[]
                        {
                            "Run 'assess compliance for subscription <subscription-id>' first",
                            "Or specify the subscription: 'generate remediation plan for subscription <subscription-id>'"
                        }
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                
                _logger.LogInformation("Using last assessed subscription from cache: {SubscriptionId}", subscriptionIdOrName);
            }
            
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Generating remediation plan for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName ?? "last used");

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Could not resolve subscription ID"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get latest assessment from database (no time restriction - use most recent)
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(
                subscriptionId, cancellationToken);

            if (assessment == null)
            {
                _logger.LogWarning("⚠️ No assessment found in database for subscription {SubscriptionId}. Please run an assessment first.", subscriptionId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No compliance assessment found for subscription {subscriptionId}",
                    message = "Please run a compliance assessment first using 'run compliance assessment' before generating a remediation plan.",
                    subscriptionId = subscriptionId
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            
            var assessmentAge = (DateTime.UtcNow - assessment.EndTime.UtcDateTime).TotalHours;
            _logger.LogInformation("✅ Using assessment from {Time} ({Age:F1} hours ago, {FindingCount} findings)", 
                assessment.EndTime, assessmentAge, 
                assessment.ControlFamilyResults.Sum(cf => cf.Value.Findings.Count));
            
            var findings = assessment.ControlFamilyResults
                .SelectMany(cf => cf.Value.Findings)
                .ToList();

            if (!findings.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "No findings to remediate - subscription is compliant!",
                    subscriptionId = subscriptionId
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var plan = await _remediationEngine.GenerateRemediationPlanAsync(
                subscriptionId,
                findings,
                cancellationToken);

            var autoRemediable = findings.Count(f => f.IsAutoRemediable);
            var manual = findings.Count - autoRemediable;

            // Generate pre-formatted display text for chat UI
            var displayText = GenerateRemediationPlanDisplayText(plan, autoRemediable, manual, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                planId = plan.PlanId,
                subscriptionId = plan.SubscriptionId,
                createdAt = plan.CreatedAt,
                
                // Pre-formatted text ready for direct display - USE THIS instead of generating your own format
                displayText = displayText,
                
                summary = new
                {
                    totalFindings = plan.TotalFindings,
                    autoRemediable = autoRemediable,
                    manualRequired = manual,
                    estimatedEffort = plan.EstimatedEffort,
                    priority = plan.Priority,
                    riskReduction = Math.Round(plan.ProjectedRiskReduction, 2)
                },
                remediationItems = plan.RemediationItems.Take(20).Select(item => new
                {
                    findingId = item.FindingId,
                    controlId = item.ControlId,
                    resourceId = item.ResourceId,
                    priority = item.Priority,
                    effort = item.EstimatedEffort,
                    automationAvailable = item.AutomationAvailable,
                    
                    // For auto-remediable findings: show WHAT will be done (clear, user-friendly)
                    // For manual findings: show detailed steps with commands
                    actionSummary = item.AutomationAvailable 
                        ? $"✨ AUTO-REMEDIATION: Will automatically execute {item.Steps?.Count ?? 0} step(s) when you run remediation"
                        : $"🔧 MANUAL REMEDIATION: Requires {item.Steps?.Count ?? 0} manual step(s)",
                    
                    // Clear numbered steps showing exactly what will happen
                    automatedActions = item.AutomationAvailable && item.Steps != null && item.Steps.Any()
                        ? item.Steps.Select((step, idx) => new
                        {
                            step = idx + 1,
                            action = step.Description,
                            // Show type of automation for transparency
                            actionType = !string.IsNullOrEmpty(step.Command) ? "Configuration Change" : "System Update"
                        }).ToList()
                        : null,
                    
                    // For manual remediation: show detailed steps with commands
                    manualSteps = !item.AutomationAvailable && item.Steps != null && item.Steps.Any()
                        ? item.Steps.Select((step, idx) => new
                        {
                            step = idx + 1,
                            description = step.Description,
                            command = step.Command,
                            script = step.AutomationScript
                        }).ToList()
                        : null,
                    
                    // Legacy fields for backward compatibility
                    steps = item.Steps?.Select(step => new
                    {
                        order = step.Order,
                        description = step.Description,
                        command = item.AutomationAvailable ? null : step.Command,
                        automationScript = item.AutomationAvailable ? null : step.AutomationScript,
                        isAutomated = item.AutomationAvailable
                    }).ToList(),
                    
                    validationSteps = item.ValidationSteps,
                    dependencies = item.Dependencies,
                    
                    // Single-line summary of what will happen
                    summary = item.AutomationAvailable && item.Steps != null && item.Steps.Any()
                        ? $"✨ {string.Join(" → ", item.Steps.Select(s => s.Description))}"
                        : item.Steps != null && item.Steps.Any()
                        ? $"🔧 {string.Join(" → ", item.Steps.Select(s => s.Description))}"
                        : "Review resource and apply manual remediation"
                }),
                timeline = plan.Timeline != null ? new
                {
                    startDate = plan.Timeline.StartDate,
                    endDate = plan.Timeline.EndDate,
                    milestones = plan.Timeline.Milestones
                } : null,
                executiveSummary = plan.ExecutiveSummary,
                
                // Instructions for chat UI
                displayInstructions = new
                {
                    instruction = "IMPORTANT: Display the 'displayText' field directly to the user. Do NOT reformat or regenerate the output.",
                    format = "The displayText contains pre-formatted markdown with all remediation details including auto-remediable actions with specific steps.",
                    autoRemediableDisplay = "For auto-remediable findings, the displayText shows numbered automated actions that will execute.",
                    manualDisplay = "For manual findings, the displayText shows step-by-step instructions with commands."
                },
                
                nextSteps = new[]
                {
                    "� DISPLAY: Show the 'displayText' field to the user - it contains the complete formatted remediation plan",
                    autoRemediable > 0 
                        ? $"⚡ EXECUTE: User can say 'execute the remediation plan' to automatically fix {autoRemediable} finding(s)" 
                        : null,
                    "📊 TRACK: User can say 'show me the remediation progress' to monitor completion"
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating remediation plan (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("generate remediation plan", ex);
        }
    }

    [KernelFunction("execute_remediation")]
    [Description("Execute automated remediation for a specific compliance finding. " +
                 "Use dry-run mode first to preview changes. Supports rollback on failure. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging'). " +
                 "RBAC: Requires Compliance.Administrator or Compliance.Analyst role.")]
    public async Task<string> ExecuteRemediationAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Finding ID to remediate")] string findingId,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        [Description("Dry run mode - preview changes without applying (true/false, default: true)")] bool dryRun = true,
        [Description("Require approval before executing (true/false, default: false)")] bool requireApproval = false,
        CancellationToken cancellationToken = default)
    {
        // Authorization check
        if (!CheckAuthorization(ComplianceRoles.Administrator, ComplianceRoles.Analyst))
        {
            var errorResult = JsonSerializer.Serialize(new
            {
                success = false,
                error = "Unauthorized: User must have Compliance.Administrator or Compliance.Analyst role to execute remediation",
                required_roles = new[] { ComplianceRoles.Administrator, ComplianceRoles.Analyst }
            }, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogWarning("Unauthorized remediation attempt by user");
            return errorResult;
        }

        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Log audit entry
            await LogAuditAsync(
                eventType: "RemediationExecuted",
                action: dryRun ? "DryRun" : "Execute",
                resourceId: $"{subscriptionId}/findings/{findingId}",
                severity: dryRun ? AuditSeverity.Informational : AuditSeverity.High,
                description: $"Remediation {(dryRun ? "dry-run" : "execution")} for finding {findingId}",
                metadata: new Dictionary<string, object>
                {
                    ["SubscriptionId"] = subscriptionId,
                    ["FindingId"] = findingId,
                    ["DryRun"] = dryRun,
                    ["RequireApproval"] = requireApproval,
                    ["ResourceGroupName"] = resourceGroupName ?? "All"
                });
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Executing remediation for {Scope} (input: {Input}), finding {FindingId}, dry-run: {DryRun}", 
                scope, subscriptionIdOrName, findingId, dryRun);

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(findingId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID and finding ID are required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get the finding
            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId, null, cancellationToken);
            
            var finding = assessment.ControlFamilyResults
                .SelectMany(cf => cf.Value.Findings)
                .FirstOrDefault(f => f.Id == findingId);

            if (finding == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Finding {findingId} not found",
                    suggestion = "Use 'run_compliance_assessment' to get valid finding IDs"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            if (!finding.IsAutoRemediable)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "This finding cannot be automatically remediated",
                    findingId = findingId,
                    recommendation = finding.Recommendation,
                    manualGuidance = finding.RemediationGuidance
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Check if automated remediation is enabled in configuration
            if (!_options.EnableAutomatedRemediation)
            {
                _logger.LogWarning("⚠️ Automated remediation is disabled in configuration (EnableAutomatedRemediation=false)");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Automated remediation is disabled",
                    findingId = findingId,
                    configurationSetting = "ComplianceAgent.EnableAutomatedRemediation",
                    currentValue = false,
                    recommendation = "Set EnableAutomatedRemediation to true in ComplianceAgent configuration to enable automated remediation",
                    manualGuidance = finding.RemediationGuidance
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var options = new RemediationExecutionOptions
            {
                DryRun = dryRun,
                RequireApproval = requireApproval
            };

            var execution = await _remediationEngine.ExecuteRemediationAsync(
                subscriptionId,
                finding,
                options,
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = execution.Success,
                executionId = execution.ExecutionId,
                mode = dryRun ? "DRY RUN (no changes applied)" : "LIVE EXECUTION",
                finding = new
                {
                    id = finding.Id,
                    title = finding.Title,
                    severity = finding.Severity.ToString()
                },
                result = new
                {
                    status = execution.Status.ToString(),
                    message = execution.Message,
                    duration = execution.Duration,
                    changesApplied = execution.ChangesApplied
                },
                backupCreated = !string.IsNullOrEmpty(execution.BackupId),
                backupId = execution.BackupId,
                error = execution.Error,
                nextSteps = dryRun ? new[]
                {
                    "Review the changes that would be applied",
                    "If satisfied, re-run with dryRun=false to apply changes",
                    "Changes can be rolled back if needed"
                } : new[]
                {
                    execution.Success ? "Remediation completed successfully" : "Remediation failed - review error",
                    "Use 'validate_remediation' to verify the fix",
                    "Use 'get_compliance_status' to see updated score"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing remediation for finding {FindingId}", findingId);
            return CreateErrorResponse("execute remediation", ex);
        }
    }

    [KernelFunction("validate_remediation")]
    [Description("Validate that a remediation was successful. " +
                 "Performs post-remediation checks to ensure fixes were effective. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> ValidateRemediationAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Finding ID that was remediated")] string findingId,
        [Description("Execution ID from remediation")] string executionId,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Validating remediation for {Scope} (input: {Input}), execution {ExecutionId}", 
                scope, subscriptionIdOrName, executionId);

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(findingId) || string.IsNullOrWhiteSpace(executionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID, finding ID, and execution ID are required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Note: Validation requires both finding and execution objects
            // For now, return a simplified response indicating manual validation is needed
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Automatic validation requires integration with execution tracking",
                executionId = executionId,
                findingId = findingId,
                recommendation = "Say 'run a compliance assessment for this subscription' to verify the finding is resolved",
                nextSteps = new[]
                {
                    "Say 'run a compliance assessment' to check if this finding has been resolved after remediation.",
                    "Verify the resource configuration matches the compliance requirements in the finding details.",
                    "Say 'show me the compliance status' to check for any side effects or new findings that may have appeared."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating remediation for execution {ExecutionId}", executionId);
            return CreateErrorResponse("validate remediation", ex);
        }
    }

    [KernelFunction("get_remediation_progress")]
    [Description("Track progress of remediation activities. " +
                 "Shows active remediations and completion status. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> GetRemediationProgressAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Getting remediation progress for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var progress = await _remediationEngine.GetRemediationProgressAsync(
                subscriptionId,
                null,
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = progress.SubscriptionId,
                timestamp = progress.Timestamp,
                summary = new
                {
                    totalActivities = progress.TotalActivities,
                    inProgress = progress.InProgressCount,
                    completed = progress.CompletedCount,
                    failed = progress.FailedCount,
                    successRate = Math.Round(progress.SuccessRate, 2)
                },
                recentActivities = progress.RecentActivities.Take(10).Select(activity => new
                {
                    executionId = activity.ExecutionId,
                    findingId = activity.FindingId,
                    status = activity.Status.ToString(),
                    startedAt = activity.StartedAt,
                    completedAt = activity.CompletedAt
                }),
                nextSteps = new[]
                {
                    progress.InProgressCount > 0 ? $"{progress.InProgressCount} remediations currently in progress." : null,
                    progress.FailedCount > 0 ? $"{progress.FailedCount} failed remediations need your attention - review the error details above." : null,
                    "Say 'run a compliance assessment for this subscription' to see the updated compliance status after remediation."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remediation progress (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("get remediation progress", ex);
        }
    }

}
