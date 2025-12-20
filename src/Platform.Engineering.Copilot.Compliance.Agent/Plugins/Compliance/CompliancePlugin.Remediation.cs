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

    #region AI-Enhanced Remediation Functions (TIER 3)

    /// <summary>
    /// Generate AI-powered remediation script (Azure CLI, PowerShell, or Terraform)
    /// </summary>
    [KernelFunction("generate_remediation_script")]
    [Description("Generate an AI-powered remediation script for a compliance finding. Supports Azure CLI, PowerShell, and Terraform. Returns executable code with explanations.")]
    // [RequireComplianceRole(ComplianceRoles.Administrator, ComplianceRoles.Analyst)]
    public async Task<string> GenerateRemediationScriptAsync(
        [Description("The finding ID to generate remediation for")] string findingId,
        [Description("Script type: AzureCLI, PowerShell, or Terraform")] string scriptType = "AzureCLI")
    {
        if (!CheckAuthorization(ComplianceRoles.Administrator, ComplianceRoles.Analyst))
        {
            _logger.LogWarning("Unauthorized access attempt to generate_remediation_script by user: {UserEmail}",
                _userContextService?.GetCurrentUserEmail() ?? "unknown");
            return "⛔ Access Denied: You must have Administrator or Analyst role to generate remediation scripts.";
        }

        try
        {
            await LogAuditEventAsync("generate_remediation_script", new { findingId, scriptType });

            _logger.LogInformation("Generating {ScriptType} remediation script for finding {FindingId}", scriptType, findingId);

            // Get finding from engine
            var findingModel = await _complianceEngine.GetFindingByIdAsync(findingId);

            if (findingModel == null)
            {
                return $"❌ Finding {findingId} not found.";
            }

            // Generate remediation script using AI
            var script = await _remediationEngine.GenerateRemediationScriptAsync(findingModel, scriptType);

            var output = new StringBuilder();
            output.AppendLine($"# 🤖 AI-Generated Remediation Script");
            output.AppendLine($"**Finding:** {findingId}");
            output.AppendLine($"**Script Type:** {scriptType}");
            output.AppendLine($"**Generated:** {script.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            if (script.RequiresApproval)
            {
                output.AppendLine("⚠️ **REQUIRES APPROVAL** - Critical/High severity remediation");
            }
            output.AppendLine();

            if (script.AvailableRemediations.Count > 0)
            {
                output.AppendLine("## Available Remediation Actions");
                foreach (var action in script.AvailableRemediations)
                {
                    output.AppendLine($"- **{action.Action}**: {action.Description} (Risk: {action.Risk}, Est: {action.EstimatedMinutes} min)");
                }
                output.AppendLine();
                output.AppendLine($"**Recommended:** {script.RecommendedAction}");
                output.AppendLine();
            }

            output.AppendLine("## Generated Script");
            output.AppendLine($"```{scriptType.ToLower()}");
            output.AppendLine(script.Script);
            output.AppendLine("```");

            return output.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("AI features not available"))
        {
            _logger.LogWarning("AI features not available for generate_remediation_script");
            return "⚠️ AI features not available. Azure OpenAI service is not configured.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate remediation script for finding {FindingId}", findingId);
            return $"❌ Error generating script: {ex.Message}";
        }
    }

    /// <summary>
    /// Get natural language remediation guidance
    /// </summary>
    [KernelFunction("get_remediation_guidance")]
    [Description("Get AI-powered natural language guidance for remediating a compliance finding. Returns step-by-step instructions in plain English.")]
    // [RequireComplianceRole(ComplianceRoles.Administrator, ComplianceRoles.Analyst, ComplianceRoles.Auditor)]
    public async Task<string> GetRemediationGuidanceAsync(
        [Description("The finding ID to get guidance for")] string findingId)
    {
        if (!CheckAuthorization(ComplianceRoles.Administrator, ComplianceRoles.Analyst, ComplianceRoles.Auditor))
        {
            _logger.LogWarning("Unauthorized access attempt to get_remediation_guidance by user: {UserEmail}",
                _userContextService?.GetCurrentUserEmail() ?? "unknown");
            return "⛔ Access Denied: You must have Administrator, Analyst, or Auditor role to view remediation guidance.";
        }

        try
        {
            await LogAuditEventAsync("get_remediation_guidance", new { findingId });

            _logger.LogInformation("Generating remediation guidance for finding {FindingId}", findingId);

            // Get finding from engine
            var findingModel = await _complianceEngine.GetFindingByIdAsync(findingId);

            if (findingModel == null)
            {
                return $"❌ Finding {findingId} not found.";
            }

            // Get AI guidance from remediation engine
            var guidance = await _remediationEngine.GetRemediationGuidanceAsync(findingModel);

            var controlId = findingModel.AffectedNistControls.FirstOrDefault() ?? "N/A";
            var output = new StringBuilder();
            output.AppendLine($"# 💡 Remediation Guidance");
            output.AppendLine($"**Finding:** {findingId} - {controlId}");
            output.AppendLine($"**Generated:** {guidance.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            output.AppendLine($"**Confidence:** {guidance.Confidence:P0}");
            output.AppendLine();
            output.AppendLine(guidance.Explanation);

            if (guidance.TechnicalPlan != null)
            {
                output.AppendLine();
                output.AppendLine("## Technical Details");
                output.AppendLine($"**Plan ID:** {guidance.TechnicalPlan.PlanId}");
                output.AppendLine($"**Total Findings:** {guidance.TechnicalPlan.TotalFindings}");
                output.AppendLine($"**Priority:** {guidance.TechnicalPlan.Priority}");
                output.AppendLine($"**Estimated Effort:** {guidance.TechnicalPlan.EstimatedEffort.TotalMinutes:F0} minutes");
                if (guidance.TechnicalPlan.RemediationItems.Any())
                {
                    output.AppendLine($"**Remediation Actions:** {guidance.TechnicalPlan.RemediationItems.Count}");
                }
            }

            return output.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("AI features not available"))
        {
            _logger.LogWarning("AI features not available for get_remediation_guidance");
            return "⚠️ AI features not available. Azure OpenAI service is not configured.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate guidance for finding {FindingId}", findingId);
            return $"❌ Error generating guidance: {ex.Message}";
        }
    }

    /// <summary>
    /// Prioritize findings using AI with business context
    /// </summary>
    [KernelFunction("prioritize_findings")]
    [Description("Use AI to prioritize compliance findings based on risk, business impact, and ease of remediation. Provide business context for better prioritization.")]
    // [RequireComplianceRole(ComplianceRoles.Administrator, ComplianceRoles.Analyst)]
    public async Task<string> PrioritizeFindingsAsync(
        [Description("Subscription ID to prioritize findings for")] string subscriptionId,
        [Description("Business context for prioritization (e.g., 'Production environment for healthcare app')")] string businessContext = "")
    {
        if (!CheckAuthorization(ComplianceRoles.Administrator, ComplianceRoles.Analyst))
        {
            _logger.LogWarning("Unauthorized access attempt to prioritize_findings by user: {UserEmail}",
                _userContextService?.GetCurrentUserEmail() ?? "unknown");
            return "⛔ Access Denied: You must have Administrator or Analyst role to prioritize findings.";
        }

        try
        {
            await LogAuditEventAsync("prioritize_findings", new { subscriptionId, businessContext });

            _logger.LogInformation("AI-prioritizing findings for subscription {SubscriptionId}", subscriptionId);

            // Get unresolved findings from engine
            var findingModels = await _complianceEngine.GetUnresolvedFindingsAsync(subscriptionId);

            if (findingModels.Count == 0)
            {
                return $"No unresolved findings found for subscription {subscriptionId}.";
            }

            // Get AI prioritization from remediation engine
            var prioritized = await _remediationEngine.PrioritizeFindingsWithAiAsync(
                findingModels.ToList(), businessContext);

            var output = new StringBuilder();
            output.AppendLine($"# 🎯 AI-Prioritized Findings");
            output.AppendLine($"**Subscription:** {subscriptionId}");
            output.AppendLine($"**Total Findings:** {findingModels.Count}");
            if (!string.IsNullOrEmpty(businessContext))
            {
                output.AppendLine($"**Business Context:** {businessContext}");
            }
            output.AppendLine();

            output.AppendLine("## Priority Rankings");
            foreach (var pf in prioritized.OrderBy(p => p.Priority))
            {
                var findingModel = findingModels.FirstOrDefault(f => f.Id == pf.FindingId);
                if (findingModel != null)
                {
                    output.AppendLine($"### Priority {pf.Priority}: {findingModel.AffectedNistControls.FirstOrDefault() ?? "N/A"}");
                    output.AppendLine($"**Finding ID:** {pf.FindingId}");
                    output.AppendLine($"**Severity:** {findingModel.Severity}");
                    output.AppendLine($"**Reasoning:** {pf.Reasoning}");
                    output.AppendLine();
                }
            }

            return output.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("AI features not available"))
        {
            _logger.LogWarning("AI features not available for prioritize_findings");
            return "⚠️ AI features not available. Azure OpenAI service is not configured.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prioritize findings for subscription {SubscriptionId}", subscriptionId);
            return $"❌ Error prioritizing findings: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute an AI-generated remediation script
    /// </summary>
    [KernelFunction("execute_ai_remediation")]
    [Description("Execute an AI-generated remediation script for a compliance finding. Supports dry-run mode and requires approval for critical findings.")]
    // [RequireComplianceRole(ComplianceRoles.Administrator)]
    public async Task<string> ExecuteAiRemediationAsync(
        [Description("The subscription ID containing the resource")] string subscriptionId,
        [Description("The finding ID to remediate")] string findingId,
        [Description("Dry run mode - simulate without making changes")] bool dryRun = true,
        [Description("Script type: AzureCLI, PowerShell, or Terraform")] string scriptType = "AzureCLI")
    {
        if (!CheckAuthorization(ComplianceRoles.Administrator))
        {
            _logger.LogWarning("Unauthorized access attempt to execute_ai_remediation by user: {UserEmail}",
                _userContextService?.GetCurrentUserEmail() ?? "unknown");
            return "⛔ Access Denied: You must have Administrator role to execute remediation scripts.";
        }

        try
        {
            await LogAuditEventAsync("execute_ai_remediation", new { subscriptionId, findingId, dryRun, scriptType });

            _logger.LogInformation("Executing AI remediation for finding {FindingId} (DryRun: {DryRun})", findingId, dryRun);

            // Get finding from engine
            var findingModel = await _complianceEngine.GetFindingByIdWithAssessmentAsync(findingId, subscriptionId);

            if (findingModel == null)
            {
                return $"❌ Finding {findingId} not found in subscription {subscriptionId}.";
            }

            // Execute remediation with AI script enabled
            var options = new RemediationExecutionOptions
            {
                DryRun = dryRun,
                UseAiScript = true, // Enable AI script execution path
                RequireApproval = findingModel.Severity is AtoFindingSeverity.Critical or AtoFindingSeverity.High,
                AutoValidate = true,
                AutoRollbackOnFailure = true,
                CaptureSnapshots = true,
                ExecutedBy = _userContextService?.GetCurrentUserEmail() ?? "system",
                Justification = $"AI-generated {scriptType} script execution for {findingId}"
            };

            var execution = await _remediationEngine.ExecuteRemediationAsync(
                subscriptionId, findingModel, options);

            var controlId = findingModel.AffectedNistControls.FirstOrDefault() ?? "N/A";
            var output = new StringBuilder();
            output.AppendLine($"# 🚀 AI Remediation Execution");
            output.AppendLine($"**Finding:** {findingId} - {controlId}");
            output.AppendLine($"**Subscription:** {subscriptionId}");
            output.AppendLine($"**Mode:** {(dryRun ? "DRY RUN (Simulation)" : "LIVE EXECUTION")}");
            output.AppendLine($"**Script Type:** {scriptType}");
            output.AppendLine($"**Status:** {(execution.Success ? "✅ SUCCESS" : "❌ FAILED")}");
            output.AppendLine($"**Duration:** {execution.Duration.TotalSeconds:F2} seconds");
            output.AppendLine();

            if (!string.IsNullOrEmpty(execution.Message))
            {
                output.AppendLine($"**Message:** {execution.Message}");
                output.AppendLine();
            }

            if (execution.StepsExecuted.Count > 0)
            {
                output.AppendLine("## Execution Steps");
                foreach (var step in execution.StepsExecuted)
                {
                    output.AppendLine($"{step.Order}. {step.Description}");
                    if (!string.IsNullOrEmpty(step.Command))
                    {
                        output.AppendLine($"   - Command: `{step.Command}`");
                    }
                }
                output.AppendLine();
            }

            if (execution.ChangesApplied.Count > 0)
            {
                output.AppendLine("## Changes Applied");
                foreach (var change in execution.ChangesApplied)
                {
                    output.AppendLine($"- {change}");
                }
                output.AppendLine();
            }

            if (!string.IsNullOrEmpty(execution.ErrorMessage))
            {
                output.AppendLine("## Error Details");
                output.AppendLine($"```");
                output.AppendLine(execution.ErrorMessage);
                output.AppendLine($"```");
            }

            if (!dryRun && execution.Success)
            {
                // Update finding status via engine
                await _complianceEngine.UpdateFindingStatusAsync(findingId, "Remediating");

                output.AppendLine("✅ Finding status updated to Remediating. Run compliance assessment to verify remediation.");
            }

            return output.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("AI features not available"))
        {
            _logger.LogWarning("AI features not available for execute_ai_remediation");
            return "⚠️ AI features not available. Azure OpenAI service is not configured.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute AI remediation for finding {FindingId}", findingId);
            return $"❌ Error executing AI remediation: {ex.Message}";
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Check if a finding can be automatically remediated
    /// </summary>
    private bool IsAutoRemediable(AtoFinding finding)
    {
        // Check if finding is marked as auto-remediable
        if (finding.IsAutoRemediable)
            return true;

        // Common auto-remediable patterns
        var autoRemediablePatterns = new[]
        {
            "enable encryption",
            "enable diagnostic",
            "enable https",
            "disable public access",
            "enable tls",
            "configure firewall",
            "enable logging",
            "enable monitoring"
        };

        var title = finding.Title?.ToLowerInvariant() ?? "";
        return autoRemediablePatterns.Any(pattern => title.Contains(pattern));
    }

    #endregion
}
