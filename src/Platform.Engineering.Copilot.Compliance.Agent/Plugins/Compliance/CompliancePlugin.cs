using System.Text;
using System.Text.Json;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Audits;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Authorization;
using Platform.Engineering.Copilot.Core.Models.Audits;
using Platform.Engineering.Copilot.Core.Services.Audits;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Plugin for ATO Compliance operations including assessments, remediation, and evidence collection.
/// Supports NIST 800-53 compliance framework with automated remediation capabilities.
/// Split into partial classes for maintainability:
/// - CompliancePlugin.cs (base class, constructor, helpers)
/// - CompliancePlugin.Assessment.cs (run_compliance_assessment, get_control_family_details, get_compliance_status)
/// - CompliancePlugin.Remediation.cs (generate_remediation_plan, execute_remediation, validate_remediation, get_remediation_progress)
/// - CompliancePlugin.Reporting.cs (perform_risk_assessment, get_compliance_timeline, generate_compliance_certificate)
/// - CompliancePlugin.Evidence.cs (collect_evidence, generate_emass_package, generate_poam)
/// - CompliancePlugin.Security.cs (apply_security_hardening, validate_compliance_with_azure_policy)
/// - CompliancePlugin.Analytics.cs (get_compliance_history, get_assessment_audit_log, get_compliance_trends)
/// - CompliancePlugin.Helpers.cs (private helper methods)
/// </summary>
public partial class CompliancePlugin : BaseSupervisorPlugin
{
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly IRemediationEngine _remediationEngine;
    private readonly IAzureResourceService _azureResourceService;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly IMemoryCache _cache;
    private readonly ConfigService _configService;
    private readonly ComplianceAgentOptions _options;
    private readonly IUserContextService? _userContextService;
    private readonly IAuditLoggingService? _auditLoggingService;
    
    private const string LAST_SUBSCRIPTION_CACHE_KEY = "compliance_last_subscription";
    private const int ASSESSMENT_CACHE_HOURS = 4; // Cache assessments for 4 hours

    public CompliancePlugin(
        ILogger<CompliancePlugin> logger,
        Kernel kernel,
        IAtoComplianceEngine complianceEngine,
        IRemediationEngine remediationEngine,
        IAzureResourceService azureResourceService,
        AzureMcpClient azureMcpClient,
        IMemoryCache cache,
        ConfigService configService,
        IOptions<ComplianceAgentOptions> options,
        IUserContextService? userContextService = null,
        IAuditLoggingService? auditLoggingService = null) : base(logger, kernel)
    {
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _remediationEngine = remediationEngine ?? throw new ArgumentNullException(nameof(remediationEngine));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _userContextService = userContextService; // Optional for HTTP mode
        _auditLoggingService = auditLoggingService; // Optional for HTTP mode
    }
    
    // ========== AUTHORIZATION HELPERS ==========
    
    /// <summary>
    /// Checks if the current user has the required role for an operation.
    /// Returns true if authorization check passes or if running in stdio mode (no auth).
    /// </summary>
    private bool CheckAuthorization(params string[] requiredRoles)
    {
        // If no user context service (stdio mode), allow operation
        if (_userContextService == null)
        {
            _logger.LogDebug("Running in stdio mode - authorization bypass");
            return true;
        }

        // Check if user is authenticated
        if (!_userContextService.IsAuthenticated())
        {
            _logger.LogWarning("Unauthorized access attempt - user not authenticated");
            return false;
        }

        // Check if user has any of the required roles
        foreach (var role in requiredRoles)
        {
            if (_userContextService.IsInRole(role))
            {
                _logger.LogInformation("User authorized with role: {Role}", role);
                return true;
            }
        }

        var userRoles = string.Join(", ", _userContextService.GetUserRoles());
        _logger.LogWarning(
            "User lacks required roles. Required: {RequiredRoles}, User has: {UserRoles}",
            string.Join(", ", requiredRoles),
            userRoles);
        
        return false;
    }

    /// <summary>
    /// Logs an audit entry for a compliance operation.
    /// </summary>
    private async Task LogAuditAsync(
        string eventType,
        string action,
        string resourceId,
        AuditSeverity severity = AuditSeverity.Informational,
        string? description = null,
        Dictionary<string, object>? metadata = null)
    {
        if (_auditLoggingService == null || _userContextService == null)
            return;

        try
        {
            await _auditLoggingService.LogAsync(new AuditLogEntry
            {
                EventType = eventType,
                EventCategory = "Compliance",
                ActorId = _userContextService.GetCurrentUserId(),
                ActorName = _userContextService.GetCurrentUserName(),
                ActorType = "User",
                Action = action,
                ResourceId = resourceId,
                ResourceType = "ComplianceOperation",
                Description = description ?? $"{action} on {resourceId}",
                Result = "Success",
                Severity = severity,
                Metadata = metadata?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>(),
                ComplianceContext = new ComplianceContext
                {
                    RequiresReview = severity >= AuditSeverity.High,
                    ControlIds = new List<string> { "AC-2", "AC-6", "AU-2", "AU-3" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry for {EventType}", eventType);
        }
    }
    
    /// <summary>
    /// Logs an audit event with simplified parameters (wrapper for LogAuditAsync).
    /// </summary>
    private async Task LogAuditEventAsync(string action, object data)
    {
        if (_auditLoggingService == null)
            return;
            
        var metadata = new Dictionary<string, object>
        {
            ["data"] = data
        };
        
        await LogAuditAsync(
            eventType: "ComplianceOperation",
            action: action,
            resourceId: action,
            severity: AuditSeverity.Informational,
            description: $"Compliance operation: {action}",
            metadata: metadata);
    }
    
    // ========== CONFIGURATION HELPERS ==========
    
    /// <summary>
    /// Gets the effective compliance framework to use (parameter or configured default)
    /// </summary>
    private string GetEffectiveFramework(string? requestedFramework = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedFramework))
        {
            _logger.LogDebug("Using requested framework: {Framework}", requestedFramework);
            return requestedFramework;
        }
        
        _logger.LogDebug("Using default framework from configuration: {Framework}", _options.DefaultFramework);
        return _options.DefaultFramework;
    }
    
    /// <summary>
    /// Gets the effective compliance baseline to use (parameter or configured default)
    /// </summary>
    private string GetEffectiveBaseline(string? requestedBaseline = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedBaseline))
        {
            _logger.LogDebug("Using requested baseline: {Baseline}", requestedBaseline);
            return requestedBaseline;
        }
        
        _logger.LogDebug("Using default baseline from configuration: {Baseline}", _options.DefaultBaseline);
        return _options.DefaultBaseline;
    }
    
    // ========== SUBSCRIPTION LOOKUP HELPER ==========
    
    /// <summary>
    /// Stores the last used subscription ID in cache AND persistent config file for session continuity
    /// </summary>
    private void SetLastUsedSubscription(string subscriptionId)
    {
        // Store in memory cache for current session
        _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
        
        // ALSO store in persistent config file for cross-session persistence
        try
        {
            _configService.SetDefaultSubscription(subscriptionId);
            _logger.LogInformation("Stored subscription in persistent config: {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist subscription to config file, will only use cache");
        }
    }
    
    /// <summary>
    /// Gets the last used subscription ID from cache, or persistent config file if cache is empty
    /// </summary>
    private string? GetLastUsedSubscription()
    {
        // Try cache first (fastest)
        if (_cache.TryGetValue<string>(LAST_SUBSCRIPTION_CACHE_KEY, out var subscriptionId))
        {
            _logger.LogDebug("Retrieved last used subscription from cache: {SubscriptionId}", subscriptionId);
            return subscriptionId;
        }
        
        // Fall back to persistent config file (survives restarts)
        try
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Retrieved subscription from persistent config: {SubscriptionId}", subscriptionId);
                // Populate cache for future requests in this session
                _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
                return subscriptionId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read subscription from config file");
        }
        
        return null;
    }
    
    /// <summary>
    /// Resolves a subscription identifier to a GUID. Accepts either a GUID or a friendly name.
    /// If null/empty, tries to use the last used subscription from session context.
    /// First queries Azure for subscription by name, then falls back to static dictionary.
    /// </summary>
    private async Task<string> ResolveSubscriptionIdAsync(string? subscriptionIdOrName)
    {
        // If no subscription provided, try to use last used subscription
        if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
        {
            var lastUsed = GetLastUsedSubscription();
            if (!string.IsNullOrWhiteSpace(lastUsed))
            {
                _logger.LogInformation("Using last used subscription from session: {SubscriptionId}", lastUsed);
                return lastUsed;
            }
            throw new ArgumentException("Subscription ID or name is required. No previous subscription found in session.", nameof(subscriptionIdOrName));
        }
        
        // Check if it's already a valid GUID
        if (Guid.TryParse(subscriptionIdOrName, out _))
        {
            SetLastUsedSubscription(subscriptionIdOrName);
            return subscriptionIdOrName;
        }
        
        // Try to query Azure for subscription by name
        try
        {
            var subscription = await _azureResourceService.GetSubscriptionByNameAsync(subscriptionIdOrName);
            _logger.LogInformation("Resolved subscription name '{Name}' to ID '{SubscriptionId}' via Azure API", 
                subscriptionIdOrName, subscription.SubscriptionId);
            SetLastUsedSubscription(subscription.SubscriptionId);
            return subscription.SubscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve subscription '{Name}' via Azure API, trying static lookup", 
                subscriptionIdOrName);
        }
               
        // If not found, throw with helpful message
        throw new ArgumentException(
            $"Subscription '{subscriptionIdOrName}' not found. " +
            $"Or provide a valid GUID.", 
            nameof(subscriptionIdOrName));
    }

    #region Database Caching Helpers

    /// <summary>
    /// Retrieves a cached compliance assessment from the database if available and not expired.
    /// </summary>
    private async Task<ComplianceAssessmentWithFindings?> GetCachedAssessmentAsync(
        string subscriptionId, 
        string? resourceGroupName, 
        CancellationToken cancellationToken)
    {
        return await _complianceEngine.GetCachedAssessmentAsync(
            subscriptionId,
            resourceGroupName,
            ASSESSMENT_CACHE_HOURS,
            cancellationToken);
    }

    /// <summary>
    /// Formats a cached assessment into the same JSON structure as a fresh assessment.
    /// </summary>
    private string FormatCachedAssessment(ComplianceAssessmentWithFindings cached, string scope, TimeSpan cacheAge)
    {
        try
        {
            // Deserialize the stored assessment data
            var assessmentData = JsonSerializer.Deserialize<JsonElement>(cached.Results ?? "{}");
            
            // Add cache metadata to the response
            var response = new Dictionary<string, object?>
            {
                ["success"] = true,
                ["cached"] = true,
                ["cacheAge"] = $"{Math.Round(cacheAge.TotalMinutes, 1)} minutes",
                ["cachedAt"] = cached.CompletedAt,
                ["assessmentId"] = cached.Id,
                ["subscriptionId"] = cached.SubscriptionId,
                ["resourceGroupName"] = cached.ResourceGroupName,
                ["scope"] = scope,
                ["timestamp"] = cached.CompletedAt,
                ["duration"] = cached.Duration
            };

            // Merge the original assessment data
            if (assessmentData.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in assessmentData.EnumerateObject())
                {
                    if (!response.ContainsKey(property.Name))
                    {
                        response[property.Name] = property.Value;
                    }
                }
            }

            // Add cache notice to formatted_output if it exists
            if (response.TryGetValue("formatted_output", out var formattedOutput) && formattedOutput is JsonElement elem)
            {
                var output = elem.GetString() ?? "";
                var cacheNotice = $"\n\n---\n\n🔄 **CACHED RESULTS** (Age: {Math.Round(cacheAge.TotalMinutes, 1)} minutes, expires in {Math.Round((ASSESSMENT_CACHE_HOURS * 60) - cacheAge.TotalMinutes, 1)} minutes)\n";
                response["formatted_output"] = output.Replace("# 📊 NIST 800-53 COMPLIANCE ASSESSMENT", 
                    $"# 📊 NIST 800-53 COMPLIANCE ASSESSMENT{cacheNotice}");
            }

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to format cached assessment - will run fresh assessment");
            throw; // This will cause the caller to run a fresh assessment
        }
    }

    /// <summary>
    /// Calculates trend direction and rate of change from a list of values over time.
    /// </summary>
    /// <param name="values">List of values in chronological order</param>
    /// <returns>Trend analysis with direction and change rate</returns>
    private (string Direction, double ChangeRate) CalculateTrend(List<double> values)
    {
        if (values.Count < 2)
        {
            return ("stable", 0);
        }

        // Calculate simple linear regression slope
        var n = values.Count;
        var xValues = Enumerable.Range(0, n).Select(i => (double)i).ToList();
        
        var xMean = xValues.Average();
        var yMean = values.Average();
        
        var numerator = xValues.Zip(values, (x, y) => (x - xMean) * (y - yMean)).Sum();
        var denominator = xValues.Sum(x => Math.Pow(x - xMean, 2));
        
        var slope = denominator != 0 ? numerator / denominator : 0;
        
        // Determine direction based on slope
        // Threshold: 0.5% change per assessment is considered "stable"
        var direction = Math.Abs(slope) < 0.5 ? "stable" :
                       slope > 0 ? "improving" : "declining";
        
        return (direction, slope);
    }

    #endregion

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
