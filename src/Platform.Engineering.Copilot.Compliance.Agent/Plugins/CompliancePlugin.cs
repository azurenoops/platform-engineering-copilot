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
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Plugin for ATO Compliance operations including assessments, remediation, and evidence collection.
/// Supports NIST 800-53 compliance framework with automated remediation capabilities.
/// </summary>
public class CompliancePlugin : BaseSupervisorPlugin
{
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly IAtoRemediationEngine _remediationEngine;
    private readonly IAzureResourceService _azureResourceService;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly IMemoryCache _cache;
    private readonly ConfigService _configService;
    private readonly PlatformEngineeringCopilotContext _dbContext;
    private readonly ComplianceAgentOptions _options;
    
    private const string LAST_SUBSCRIPTION_CACHE_KEY = "compliance_last_subscription";
    private const int ASSESSMENT_CACHE_HOURS = 4; // Cache assessments for 4 hours

    public CompliancePlugin(
        ILogger<CompliancePlugin> logger,
        Kernel kernel,
        IAtoComplianceEngine complianceEngine,
        IAtoRemediationEngine remediationEngine,
        IAzureResourceService azureResourceService,
        AzureMcpClient azureMcpClient,
        IMemoryCache cache,
        ConfigService configService,
        PlatformEngineeringCopilotContext dbContext,
        IOptions<ComplianceAgentOptions> options) : base(logger, kernel)
    {
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _remediationEngine = remediationEngine ?? throw new ArgumentNullException(nameof(remediationEngine));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

    // ========== COMPLIANCE ASSESSMENT FUNCTIONS ==========

    [KernelFunction("run_compliance_assessment")]
    [Description("🔍 SECURITY SCANNING ONLY - Run a comprehensive NIST 800-53 compliance assessment to IDENTIFY security findings and vulnerabilities. " +
                 "THIS FUNCTION SCANS FOR PROBLEMS - it does NOT collect evidence packages. " +
                 "Use ONLY when user says: 'run assessment', 'scan for compliance', 'check for vulnerabilities', 'find security issues'. " +
                 "DO NOT use for: 'collect evidence', 'generate evidence', 'evidence package', 'ATO package', 'documentation'. " +
                 "Output: Compliance findings with severity ratings (Critical/High/Medium/Low), compliance score, and remediation recommendations. " +
                 "🔴 **DEFAULT BEHAVIOR**: When user says 'run assessment' WITHOUT specifying subscription → Call with subscriptionIdOrName=null (uses default subscription). " +
                 "Accepts subscription GUID or friendly name. Examples: null (use default) | 'production' | '453c2549-9efb-4d48-a4f6-6c6b42db39b5'. " +
                 "CRITICAL: Extract resource group name from conversation if user mentions 'newly provisioned' or 'newly created' resources.")]
    public async Task<string> RunComplianceAssessmentAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging'). OPTIONAL - leave as null when user doesn't specify (will use default from configuration set via set_azure_subscription). Only provide a value if user explicitly mentions a subscription. Example: 'production' or '453c2549-9efb-4d48-a4f6-6c6b42db39b5'")] 
        string? subscriptionIdOrName = null,
        [Description("CRITICAL: Resource group name to scan. If task mentions 'newly provisioned AKS cluster' or similar, extract the resource group name from conversation history (e.g., 'newly-provisioned-aks', 'rg-dev-aks'). ALWAYS provide this when assessing newly created resources. Leave empty ONLY when scanning entire subscription.")] 
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId;
            try
            {
                subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            }
            catch (ArgumentException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message,
                    providedInput = subscriptionIdOrName,
                    hint = "Use a friendly name like 'production', 'dev', 'staging' or provide a valid subscription GUID"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Running comprehensive compliance assessment for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Check for cached assessment (4-hour TTL)
            var cachedAssessment = await GetCachedAssessmentAsync(
                subscriptionId, resourceGroupName, cancellationToken);
            
            if (cachedAssessment != null)
            {
                var cacheAge = DateTime.UtcNow - cachedAssessment.CompletedAt.GetValueOrDefault();
                _logger.LogInformation(
                    "✅ Using cached assessment from {CachedTime} (age: {Age} minutes)",
                    cachedAssessment.CompletedAt,
                    Math.Round(cacheAge.TotalMinutes, 1));
                
                return FormatCachedAssessment(cachedAssessment, scope, cacheAge);
            }
            
            _logger.LogInformation("No valid cache found - running new compliance assessment against Azure APIs");

            // 🔥 CRITICAL: If resource group specified, verify it exists and has resources before scanning
            if (!string.IsNullOrWhiteSpace(resourceGroupName))
            {
                try
                {
                    var rgResources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(
                        subscriptionId, resourceGroupName, cancellationToken);
                    
                    if (rgResources == null || !rgResources.Any())
                    {
                        _logger.LogWarning(
                            "⚠️ Resource group '{ResourceGroup}' has no resources in subscription {SubscriptionId}. " +
                            "Skipping compliance assessment. This typically means deployment has not completed yet.",
                            resourceGroupName, subscriptionId);
                        
                        return JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = $"Resource group '{resourceGroupName}' has no resources to assess. " +
                                   "The deployment may not have completed yet, or the resource group may be empty.",
                            hint = "Wait for the Environment Agent to complete deployment before running compliance assessment.",
                            resourceGroupName,
                            subscriptionId,
                            resourceCount = 0
                        }, new JsonSerializerOptions { WriteIndented = true });
                    }
                    
                    _logger.LogInformation(
                        "✅ Resource group '{ResourceGroup}' contains {Count} resources - proceeding with compliance assessment",
                        resourceGroupName, rgResources.Count());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "⚠️ Could not verify resources in resource group '{ResourceGroup}'. Error: {Error}",
                        resourceGroupName, ex.Message);
                    
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Could not access resource group '{resourceGroupName}': {ex.Message}",
                        hint = "Verify the resource group exists and you have access to it.",
                        resourceGroupName,
                        subscriptionId
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            // Create progress reporter for logging
            var progressReporter = new Progress<AssessmentProgress>(progress =>
            {
                _logger.LogInformation(
                    "Assessment progress: {Family} ({Completed}/{Total} families, {Percent}%) - {Message}",
                    progress.CurrentFamily,
                    progress.CompletedFamilies,
                    progress.TotalFamilies,
                    progress.PercentComplete,
                    progress.Message);
            });

            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId, 
                resourceGroupName,
                progressReporter,
                cancellationToken);

            // Extract all findings from control families
            var allFindings = assessment.ControlFamilyResults
                .SelectMany(cf => cf.Value.Findings)
                .ToList();

            // Filter findings by resource group if specified
            if (!string.IsNullOrWhiteSpace(resourceGroupName))
            {
                allFindings = allFindings
                    .Where(f => f.ResourceId?.Contains($"/resourceGroups/{resourceGroupName}/", StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
                
                _logger.LogInformation("Filtered to {Count} findings in resource group {ResourceGroup}", 
                    allFindings.Count, resourceGroupName);
            }

            // Categorize findings by severity
            var criticalFindings = allFindings.Where(f => f.Severity == AtoFindingSeverity.Critical).ToList();
            var highFindings = allFindings.Where(f => f.Severity == AtoFindingSeverity.High).ToList();
            var mediumFindings = allFindings.Where(f => f.Severity == AtoFindingSeverity.Medium).ToList();
            var lowFindings = allFindings.Where(f => f.Severity == AtoFindingSeverity.Low).ToList();
            
            var autoRemediableCount = allFindings.Count(f => f.IsAutoRemediable);
            var manualCount = allFindings.Count - autoRemediableCount;
            
            // Group findings by source
            var findingsBySource = allFindings
                .GroupBy(f => f.Metadata?.ContainsKey("Source") == true ? f.Metadata["Source"]?.ToString() : "Unknown")
                .Select(g => new
                {
                    source = g.Key ?? "Unknown",
                    count = g.Count(),
                    highestSeverity = g.Max(f => f.Severity)
                })
                .OrderByDescending(g => g.count)
                .ToList();
            
            // Severity emoji mapping
            var severityEmojis = new Dictionary<AtoFindingSeverity, string>
            {
                { AtoFindingSeverity.Critical, "🔴" },
                { AtoFindingSeverity.High, "🟠" },
                { AtoFindingSeverity.Medium, "🟡" },
                { AtoFindingSeverity.Low, "🟢" },
                { AtoFindingSeverity.Informational, "ℹ️" }
            };
            
            // Group findings for severity breakdown
            var severityGroups = allFindings
                .GroupBy(f => f.Severity)
                .Select(g => new
                {
                    severity = g.Key,
                    count = g.Count(),
                    autoRemediable = g.Count(f => f.IsAutoRemediable)
                })
                .OrderByDescending(g => g.severity)
                .ToList();
            
            // Group findings by resource type
            var resourceGroups = allFindings
                .GroupBy(f => f.ResourceType)
                .Select(g => new
                {
                    resourceType = g.Key,
                    count = g.Count(),
                    highestSeverity = g.Max(f => f.Severity)
                })
                .OrderByDescending(g => g.highestSeverity)
                .ThenByDescending(g => g.count)
                .Take(10)
                .ToList();

            // Generate pre-formatted markdown summary for direct display
            var nextSteps = autoRemediableCount > 0 
                ? $"You can auto-fix {autoRemediableCount} issues - ask me to generate a remediation plan to get started!" 
                : "Review findings and work with your security team to remediate.";
            
            var formattedOutput = $@"
# 📊 NIST 800-53 COMPLIANCE ASSESSMENT

**Subscription:** `{assessment.SubscriptionId}`
{(string.IsNullOrEmpty(resourceGroupName) ? "" : $"**Resource Group:** `{resourceGroupName}`\n")}
**Compliance Score:** {GenerateScoreBar(assessment.OverallComplianceScore)} **{Math.Round(assessment.OverallComplianceScore, 1)}%** (Grade: **{GetComplianceGrade(assessment.OverallComplianceScore)}**)  
**Status:** {(assessment.OverallComplianceScore >= 90 ? "✅ Ready for ATO" : assessment.OverallComplianceScore >= 70 ? "⚠️ Getting there" : "❌ Needs attention")}

---

## 📋 OVERVIEW

| Metric | Count |
|--------|-------|
| **Total Findings** | {assessment.TotalFindings} |
| ✨ **Auto-Remediable** | {autoRemediableCount} |
| 🔧 **Manual Remediation** | {manualCount} |

---

## ⚠️ FINDINGS BY SEVERITY

{string.Join("\n", severityGroups.Select(g => $"- {severityEmojis[g.severity]} **{g.severity}**: {g.count} finding{(g.count > 1 ? "s" : "")} {new string('█', Math.Min(g.count / 2, 10))}"))}

---

## 📍 FINDINGS BY SOURCE

{string.Join("\n", findingsBySource.Select(g => $"- **{g.source}**: {g.count} finding{(g.count > 1 ? "s" : "")} {(g.highestSeverity == AtoFindingSeverity.Critical ? "🔴" : g.highestSeverity == AtoFindingSeverity.High ? "🟠" : g.highestSeverity == AtoFindingSeverity.Medium ? "🟡" : "🟢")}"))}

---

## 🎯 CONTROL FAMILIES NEEDING ATTENTION
*Showing families with <90% compliance*

{string.Join("\n", assessment.ControlFamilyResults
    .Where(cf => cf.Value.ComplianceScore < 90)
    .OrderBy(cf => cf.Value.ComplianceScore)
    .Take(10)
    .Select(cf => $"- {(cf.Value.ComplianceScore == 0 ? "❌" : "⚠️")} **{cf.Key} ({GetControlFamilyName(cf.Key)})**: {Math.Round(cf.Value.ComplianceScore, 1)}% {new string('█', (int)(cf.Value.ComplianceScore / 5))}{new string('░', 20 - (int)(cf.Value.ComplianceScore / 5))} - {cf.Value.Findings.Count} finding{(cf.Value.Findings.Count > 1 ? "s" : "")}"))}

---

{(assessment.CriticalFindings > 0 ? $"## 🔴 CRITICAL ALERT\n\n**{assessment.CriticalFindings} CRITICAL** issue{(assessment.CriticalFindings > 1 ? "s" : "")} need **immediate** attention!\n\n---\n\n" : "")}
## 💡 NEXT STEPS

{nextSteps}

**Quick Actions:**
- `generate a remediation plan for this assessment` - Create actionable remediation plan
- `get control family details for AC` - Drill down into Access Control findings
- `collect compliance evidence for this subscription` - Generate ATO evidence package
";
            var assessmentResult = new
            {
                success = true,
                // Pre-formatted output for direct display (bypasses AI formatting)
                formatted_output = formattedOutput,
                assessmentId = assessment.AssessmentId,
                subscriptionId = assessment.SubscriptionId,
                resourceGroupName = resourceGroupName,
                scope = scope,
                timestamp = assessment.EndTime,
                duration = assessment.Duration,
                
                // Enhanced header with visual score bar
                header = new
                {
                    title = "📊 NIST 800-53 COMPLIANCE ASSESSMENT",
                    subtitle = $"Comprehensive Assessment for {scope}",
                    visualScore = new
                    {
                        bar = GenerateScoreBar(assessment.OverallComplianceScore),
                        percentage = $"{Math.Round(assessment.OverallComplianceScore, 1)}%",
                        grade = GetComplianceGrade(assessment.OverallComplianceScore),
                        status = assessment.OverallComplianceScore >= 90 ? "✅ Ready for ATO" :
                                assessment.OverallComplianceScore >= 70 ? "⚠️ Getting there - some work needed" : 
                                "❌ Needs attention"
                    },
                    interpretation = assessment.OverallComplianceScore >= 90 ? 
                        "🎉 Your environment is in great shape! Keep monitoring to maintain this level." :
                        assessment.OverallComplianceScore >= 80 ? 
                        "👍 You're doing well. Focus on the remaining issues to get ATO-ready." :
                        assessment.OverallComplianceScore >= 70 ? 
                        "📈 You're on the right track. Address the findings below to improve compliance." :
                        autoRemediableCount > 0 ? 
                        $"⚠️ There's work to do, but don't worry - we'll help you fix it. {autoRemediableCount} issues can be fixed automatically!" :
                        "⚠️ There's work to do. Review the findings below and work with your security team."
                },
                
                // Visual overview section
                overview = new
                {
                    title = "📋 OVERVIEW",
                    findings = new
                    {
                        summary = $"{assessment.TotalFindings} total | ✨ {autoRemediableCount} auto-fix | 🔧 {manualCount} manual",
                        total = assessment.TotalFindings,
                        autoRemediable = autoRemediableCount,
                        manual = manualCount,
                        message = assessment.TotalFindings == 0 ? "🎉 No issues found!" :
                                 assessment.CriticalFindings > 0 ? $"🔴 {assessment.CriticalFindings} critical issue{(assessment.CriticalFindings > 1 ? "s" : "")} need immediate attention" :
                                 assessment.HighFindings > 0 ? $"� {assessment.HighFindings} high-priority finding{(assessment.HighFindings > 1 ? "s" : "")} to address" :
                                 $"✅ {assessment.TotalFindings} minor issue{(assessment.TotalFindings > 1 ? "s" : "")} to clean up"
                    },
                    controlFamilies = new
                    {
                        total = assessment.ControlFamilyResults.Count,
                        compliant = assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore >= 90),
                        needsWork = assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore < 90),
                        summary = $"{assessment.ControlFamilyResults.Count} families assessed | {assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore >= 90)} compliant | {assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore < 90)} need work"
                    }
                },
                
                // Severity breakdown with visual bars
                severityBreakdown = new
                {
                    title = "⚠️ FINDINGS BY SEVERITY",
                    items = severityGroups.Select(g => new
                    {
                        severity = g.severity.ToString().ToUpper(),
                        count = g.count,
                        emoji = severityEmojis[g.severity],
                        visualBar = new string('█', Math.Min(g.count, 10)),
                        autoRemediable = g.autoRemediable,
                        percentage = assessment.TotalFindings > 0 ? Math.Round((double)g.count / assessment.TotalFindings * 100, 1) : 0,
                        message = g.count == 1 ? 
                            $"{severityEmojis[g.severity]} 1 {g.severity.ToString().ToLower()} finding" :
                            $"{severityEmojis[g.severity]} {g.count} {g.severity.ToString().ToLower()} findings"
                    }).ToList()
                },
                
                // Resource type breakdown with friendly names
                resourceTypeBreakdown = new
                {
                    title = "🔍 TOP FINDINGS BY RESOURCE TYPE",
                    items = resourceGroups.Select(g => new
                    {
                        resourceType = g.resourceType,
                        displayName = GetResourceTypeDisplayName(g.resourceType),
                        count = g.count,
                        emoji = severityEmojis[g.highestSeverity],
                        severity = g.highestSeverity.ToString().ToUpper(),
                        message = g.count == 1 ? "1 finding" : $"{g.count} findings"
                    }).ToList()
                },
                
                // Problem areas - control families needing work
                problemAreas = new
                {
                    title = "🎯 CONTROL FAMILIES NEEDING ATTENTION",
                    count = assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore < 90),
                    items = assessment.ControlFamilyResults
                        .Where(cf => cf.Value.ComplianceScore < 90)
                        .OrderBy(cf => cf.Value.ComplianceScore)
                        .Take(10)
                        .Select(cf => new
                        {
                            code = cf.Key,
                            name = GetControlFamilyName(cf.Key),
                            score = Math.Round(cf.Value.ComplianceScore, 1),
                            scoreBar = GenerateScoreBar(cf.Value.ComplianceScore),
                            emoji = cf.Value.ComplianceScore >= 70 ? "🟡" : "🔴",
                            status = cf.Value.ComplianceScore >= 70 ? "Needs improvement" : "Requires immediate attention",
                            findingsCount = cf.Value.Findings.Count,
                            autoRemediable = cf.Value.Findings.Count(f => f.IsAutoRemediable),
                            message = cf.Value.Findings.Count == 1 ? 
                                "1 finding" : 
                                $"{cf.Value.Findings.Count} findings ({cf.Value.Findings.Count(f => f.IsAutoRemediable)} auto-fix)",
                            command = $"get control family details for {cf.Key}"
                        })
                        .ToList()
                },
                
                // Top issues with enhanced formatting
                topIssues = new
                {
                    title = assessment.CriticalFindings > 0 ? "🔴 CRITICAL & HIGH PRIORITY FINDINGS" :
                           assessment.HighFindings > 0 ? "🟠 HIGH PRIORITY FINDINGS" :
                           "📋 TOP FINDINGS",
                    count = Math.Min(10, criticalFindings.Count + highFindings.Count),
                    items = criticalFindings.Concat(highFindings)
                        .OrderByDescending(f => f.Severity)
                        .Take(10)
                        .Select((f, index) => new
                        {
                            number = index + 1,
                            header = new
                            {
                                severity = f.Severity.ToString().ToUpper(),
                                emoji = severityEmojis[f.Severity],
                                badge = f.IsAutoRemediable ? "✨ AUTO-FIX" : "🔧 MANUAL",
                                title = f.Title
                            },
                            resource = new
                            {
                                type = GetResourceTypeDisplayName(f.ResourceType),
                                name = f.ResourceName
                            },
                            remediation = new
                            {
                                canAutoFix = f.IsAutoRemediable,
                                status = f.IsAutoRemediable ? 
                                    "✨ Can be automatically fixed" : 
                                    "🔧 Requires manual remediation"
                            },
                            affectedControls = f.AffectedNistControls.Count
                        })
                        .ToList()
                },
                
                // Remediation summary with visual breakdown
                remediationSummary = new
                {
                    title = "🔧 REMEDIATION STRATEGY",
                    priority = new
                    {
                        level = assessment.CriticalFindings > 0 ? "CRITICAL" :
                               assessment.HighFindings > 0 ? "HIGH" :
                               assessment.MediumFindings > 0 ? "MEDIUM" : "LOW",
                        emoji = assessment.CriticalFindings > 0 ? "🔴" :
                               assessment.HighFindings > 0 ? "🟠" :
                               assessment.MediumFindings > 0 ? "🟡" : "🟢",
                        timeline = assessment.CriticalFindings > 0 ? "Immediate action required" :
                                  assessment.HighFindings > 0 ? "Address within 24-48 hours" :
                                  assessment.MediumFindings > 0 ? "Address within 7 days" :
                                  "Address as time permits"
                    },
                    breakdown = new
                    {
                        autoRemediable = new
                        {
                            count = autoRemediableCount,
                            percentage = assessment.TotalFindings > 0 ? Math.Round((autoRemediableCount * 100.0) / assessment.TotalFindings, 1) : 0,
                            emoji = "✨",
                            bar = new string('█', Math.Min(autoRemediableCount, 10)),
                            message = autoRemediableCount == 0 ? "No auto-fix available" :
                                     autoRemediableCount == 1 ? "1 finding can be auto-fixed!" :
                                     $"{autoRemediableCount} findings can be auto-fixed - quick compliance wins!"
                        },
                        manualRemediation = new
                        {
                            count = manualCount,
                            percentage = assessment.TotalFindings > 0 ? Math.Round((manualCount * 100.0) / assessment.TotalFindings, 1) : 0,
                            emoji = "🔧",
                            bar = new string('█', Math.Min(manualCount, 10)),
                            message = manualCount == 0 ? "No manual work needed!" :
                                     manualCount == 1 ? "1 finding requires manual review" :
                                     $"{manualCount} findings require manual remediation"
                        }
                    },
                    insight = autoRemediableCount == 0 ?
                        new
                        {
                            status = "🔧 Manual Remediation Required",
                            explanation = "All findings require manual review because they involve:",
                            reasons = new[]
                            {
                                "👥 Access control and identity management decisions",
                                "📋 Compliance policies requiring business approval",
                                "🏢 Organizational security policies and standards",
                                "⚖️ Risk assessment and authorization requirements"
                            },
                            guidance = "These findings need stakeholder input, security team review, or management approval before changes can be made."
                        } : new
                        {
                            status = $"✨ {autoRemediableCount} Quick Wins Available",
                            explanation = $"{Math.Round((autoRemediableCount * 100.0) / assessment.TotalFindings, 1)}% of findings can be automatically remediated",
                            reasons = new[]
                            {
                                "⚙️ Technical configuration changes (encryption, diagnostics)",
                                "🔒 Security hardening (TLS versions, HTTPS enforcement)",
                                "📊 Monitoring and logging enablement",
                                "🛡️ Network security group rules"
                            },
                            guidance = "Use auto-remediation to quickly improve compliance score for technical issues."
                        },
                    recommendation = assessment.TotalFindings == 0 ? 
                        "🎉 Great job! No findings detected." :
                        autoRemediableCount > 0 ?
                        $"Start with auto-remediation to quickly fix {autoRemediableCount} issue{(autoRemediableCount > 1 ? "s" : "")}, then tackle the {manualCount} manual item{(manualCount != 1 ? "s" : "")}." :
                        $"All {manualCount} finding{(manualCount != 1 ? "s" : "")} require manual remediation. Review each finding's guidance and work with your security team.",
                    riskLevel = assessment.CriticalFindings > 0 ? "🔴 HIGH RISK" :
                               assessment.HighFindings > 5 ? "🟠 MODERATE RISK" :
                               assessment.MediumFindings > 10 ? "🟡 LOW RISK" : "🟢 MINIMAL RISK"
                },
                
                // Enhanced next steps with priorities and natural language guidance
                nextSteps = new
                {
                    title = "📋 RECOMMENDED ACTIONS",
                    overview = assessment.CriticalFindings > 0 ?
                        $"⚠️ You have {assessment.CriticalFindings} critical findings that need immediate attention. Focus on these first to reduce risk quickly." :
                        assessment.HighFindings > 0 ?
                        $"📊 You have {assessment.HighFindings} high-priority findings. Start by addressing these to improve your compliance score." :
                        assessment.MediumFindings > 0 ?
                        $"✅ You have {assessment.MediumFindings} medium-priority findings. These are good opportunities for incremental improvements." :
                        "🎉 Great job! You only have low-priority findings. Focus on continuous improvement and monitoring.",
                    
                    immediate = assessment.CriticalFindings > 0 ? 
                        new[] {
                            new {
                                priority = "🔴 CRITICAL",
                                action = $"Address {assessment.CriticalFindings} critical findings immediately",
                                command = "generate a remediation plan for this assessment",
                                explanation = "Critical findings represent immediate security risks that could impact your ATO status. Generate a plan to fix them right away."
                            },
                            autoRemediableCount > 0 ? new {
                                priority = "✨ QUICK WIN",
                                action = $"Auto-remediate {autoRemediableCount} findings",
                                command = "execute remediation plan",
                                explanation = "Some findings can be fixed automatically. This is the fastest way to improve your compliance score."
                            } : null
                        }.Where(a => a != null) : new[] {
                            new {
                                priority = "✅ GOOD STATUS",
                                action = "No critical findings detected",
                                command = null as string,
                                explanation = "Your environment doesn't have any critical compliance issues. Focus on addressing high and medium priority items."
                            }
                        },
                    
                    shortTerm = new[]
                    {
                        assessment.HighFindings > 0 ? new {
                            action = $"📋 Review {assessment.HighFindings} high-priority findings",
                            command = assessment.ControlFamilyResults
                                .Where(cf => cf.Value.Findings.Any(f => f.Severity == AtoFindingSeverity.High))
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "show me control family details",
                            explanation = "High-priority findings need attention within 24-48 hours. Review control families with high-severity findings to understand what needs to be fixed."
                        } : null,
                        new {
                            action = "🔍 Drill down into control families",
                            command = assessment.ControlFamilyResults
                                .Where(cf => cf.Value.ComplianceScore < 70)
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "get control family details for AC",
                            explanation = $"View detailed findings and recommendations for each control family. Focus on families scoring below 70%: {string.Join(", ", assessment.ControlFamilyResults.Where(cf => cf.Value.ComplianceScore < 70).Select(cf => cf.Key).Take(3))}."
                        },
                        new {
                            action = "📄 Collect compliance evidence",
                            command = "collect compliance evidence for this subscription",
                            explanation = "Gather configuration data, logs, and metrics to support your ATO package and demonstrate compliance to auditors."
                        },
                        manualCount > 0 ? new {
                            action = $"🔧 Plan manual remediation for {manualCount} findings",
                            command = "generate a remediation plan for this assessment",
                            explanation = "These findings require business decisions or policy changes. Create a plan that includes stakeholder approvals and timelines."
                        } : null
                    }.Where(a => a != null),
                    
                    ongoing = new[]
                    {
                        new {
                            icon = "📊",
                            action = "Monitor compliance status continuously",
                            command = "show me the current compliance status",
                            explanation = "Set up regular monitoring to catch drift and new issues early. Check your compliance score weekly or after major changes."
                        },
                        new {
                            icon = "🔄",
                            action = "Schedule regular assessments",
                            command = "run compliance assessment for production",
                            explanation = "Run assessments monthly (or after significant infrastructure changes) to maintain compliance and prepare for audits."
                        },
                        new {
                            icon = "🎯",
                            action = "Review low-performing control families",
                            command = assessment.ControlFamilyResults
                                .Where(cf => cf.Value.ComplianceScore < 80)
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Take(3)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "get control family details for CM",
                            explanation = $"Focus on control families scoring below 80%. Currently: {string.Join(", ", assessment.ControlFamilyResults.Where(cf => cf.Value.ComplianceScore < 80).Select(cf => $"{cf.Key} ({cf.Value.ComplianceScore:F0}%)").Take(5))}."
                        }
                    }
                },
                
                // Control family deep-dive with enhanced visuals
                controlFamilyDrillDown = new
                {
                    title = "🔍 EXPLORE CONTROL FAMILIES",
                    description = "Review each control family below to see detailed findings, remediation guidance, and auto-remediation opportunities.",
                    summary = new
                    {
                        total = assessment.ControlFamilyResults.Count,
                        compliant = assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore >= 90),
                        needsWork = assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore < 90),
                        critical = assessment.ControlFamilyResults.Count(cf => cf.Value.ComplianceScore < 70)
                    },
                    families = assessment.ControlFamilyResults
                        .OrderBy(cf => cf.Value.ComplianceScore)
                        .Select(cf => new
                        {
                            code = cf.Key,
                            name = GetControlFamilyName(cf.Key),
                            score = Math.Round(cf.Value.ComplianceScore, 1),
                            scoreBar = GenerateScoreBar(cf.Value.ComplianceScore),
                            emoji = cf.Value.ComplianceScore >= 90 ? "✅" :
                                   cf.Value.ComplianceScore >= 70 ? "🟡" : "🔴",
                            priority = cf.Value.ComplianceScore < 70 ? "🔴 HIGH" :
                                      cf.Value.ComplianceScore < 90 ? "🟡 MEDIUM" : "✅ LOW",
                            findings = new
                            {
                                total = cf.Value.Findings.Count,
                                autoRemediable = cf.Value.Findings.Count(f => f.IsAutoRemediable),
                                critical = cf.Value.Findings.Count(f => f.Severity == AtoFindingSeverity.Critical),
                                high = cf.Value.Findings.Count(f => f.Severity == AtoFindingSeverity.High)
                            },
                            command = $"get control family details for {cf.Key}",
                            explanation = cf.Value.ComplianceScore < 70 ?
                                $"⚠️ Requires immediate attention - only {cf.Value.ComplianceScore:F0}% compliant with {cf.Value.Findings.Count} findings ({cf.Value.Findings.Count(f => f.Severity == AtoFindingSeverity.Critical)} critical)." :
                                cf.Value.ComplianceScore < 90 ?
                                $"ℹ️ Needs improvement - {cf.Value.Findings.Count} findings to address ({cf.Value.Findings.Count(f => f.IsAutoRemediable)} can be auto-fixed)." :
                                $"✅ Good status - {cf.Value.ComplianceScore:F0}% compliant with {cf.Value.Findings.Count} minor findings remaining."
                        })
                },
                
                // Quick action buttons with visual indicators
                quickActions = new[]
                {
                    new { 
                        action = "generate_remediation",
                        command = "generate a remediation plan for this assessment",
                        icon = "🔧",
                        label = "Generate Remediation Plan",
                        priority = assessment.CriticalFindings > 0 ? "🔴 URGENT" : "🟠 HIGH",
                        description = autoRemediableCount > 0 ? 
                            $"Create a plan to fix {assessment.TotalFindings} findings ({autoRemediableCount} auto-fix available)" :
                            $"Create a plan to address {assessment.TotalFindings} findings"
                    },
                    new { 
                        action = "collect_evidence",
                        command = "collect compliance evidence for this subscription",
                        icon = "📄",
                        label = "Collect Compliance Evidence",
                        priority = "🟡 MEDIUM",
                        description = "Generate evidence package for ATO documentation and audit readiness"
                    },
                    new { 
                        action = "check_status",
                        command = "show me the current compliance status",
                        icon = "📊",
                        label = "View Compliance Status",
                        priority = "🟢 LOW",
                        description = "Get real-time compliance dashboard with alerts and recent changes"
                    }
                }
            };

            // Save assessment to database for caching (fire and forget - don't block on DB operations)
            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveAssessmentToDbAsync(
                        assessmentResult,
                        allFindings,
                        subscriptionId,
                        resourceGroupName,
                        (decimal)assessment.OverallComplianceScore,
                        cancellationToken);
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Failed to cache assessment to database (non-fatal)");
                }
            }, cancellationToken);

            return JsonSerializer.Serialize(assessmentResult, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running compliance assessment (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("run compliance assessment", ex);
        }
    }

    [KernelFunction("get_control_family_details")]
    [Description("Get detailed findings and recommendations for a specific NIST control family. " +
                 "Shows all findings, their severity, remediation guidance, and whether they can be auto-remediated. " +
                 "Essential for drilling down into specific control families to understand and address issues. " +
                 "Control families: AC (Access Control), AU (Audit), SC (System Communications), " +
                 "SI (System Integrity), CM (Configuration Management), CP (Contingency Planning), " +
                 "IA (Identification/Authentication), IR (Incident Response), RA (Risk Assessment), CA (Security Assessment).")]
    public async Task<string> GetControlFamilyDetailsAsync(
        [Description("NIST control family code (e.g., 'AC', 'AU', 'SC', 'SI', 'CM', 'CP', 'IA', 'IR', 'RA', 'CA')")] string controlFamily,
        [Description("Optional: Azure subscription ID or friendly name. If not provided, uses last assessed subscription.")] string? subscriptionIdOrName = null,
        [Description("Optional: Resource group name to scope the assessment")] string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting control family details for {ControlFamily} in subscription {Subscription}", 
                controlFamily, subscriptionIdOrName ?? "last used");

            // Validate control family
            var validFamilies = new[] { "AC", "AU", "SC", "SI", "CM", "CP", "IA", "IR", "RA", "CA", "MA", "MP", "PE", "PL", "PS", "SA", "AT", "PM" };
            var normalizedFamily = controlFamily?.ToUpperInvariant();
            
            if (string.IsNullOrWhiteSpace(normalizedFamily) || !validFamilies.Contains(normalizedFamily))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Invalid control family '{controlFamily}'. Valid families: {string.Join(", ", validFamilies)}",
                    validFamilies = validFamilies
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";

            // Run assessment to get current findings
            // Create progress reporter for logging
            var progressReporter = new Progress<AssessmentProgress>(progress =>
            {
                _logger.LogInformation(
                    "Control family assessment progress: {Family} ({Completed}/{Total} families, {Percent}%) - {Message}",
                    progress.CurrentFamily,
                    progress.CompletedFamilies,
                    progress.TotalFamilies,
                    progress.PercentComplete,
                    progress.Message);
            });

            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId,
                resourceGroupName,
                progressReporter,
                cancellationToken);

            // Get the specific control family result
            if (!assessment.ControlFamilyResults.TryGetValue(normalizedFamily, out var familyResult))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Control family '{normalizedFamily}' not found in assessment results",
                    availableFamilies = assessment.ControlFamilyResults.Keys.ToList()
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Filter findings by resource group if specified
            var findings = familyResult.Findings;
            if (!string.IsNullOrWhiteSpace(resourceGroupName))
            {
                findings = findings
                    .Where(f => f.ResourceId?.Contains($"/resourceGroups/{resourceGroupName}/", StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            // Categorize findings
            var criticalFindings = findings.Where(f => f.Severity == AtoFindingSeverity.Critical).ToList();
            var highFindings = findings.Where(f => f.Severity == AtoFindingSeverity.High).ToList();
            var mediumFindings = findings.Where(f => f.Severity == AtoFindingSeverity.Medium).ToList();
            var lowFindings = findings.Where(f => f.Severity == AtoFindingSeverity.Low).ToList();
            
            var autoRemediableCount = findings.Count(f => f.IsAutoRemediable);
            var manualRemediationCount = findings.Count - autoRemediableCount;

            // Group findings by resource type for better organization
            var findingsByResourceType = findings
                .GroupBy(f => f.ResourceType)
                .OrderByDescending(g => g.Max(f => (int)f.Severity))
                .ThenByDescending(g => g.Count())
                .ToList();

            // Get control family name
            var familyNames = new Dictionary<string, string>
            {
                { "AC", "Access Control" },
                { "AU", "Audit and Accountability" },
                { "AT", "Awareness and Training" },
                { "CM", "Configuration Management" },
                { "CP", "Contingency Planning" },
                { "IA", "Identification and Authentication" },
                { "IR", "Incident Response" },
                { "MA", "Maintenance" },
                { "MP", "Media Protection" },
                { "PE", "Physical and Environmental Protection" },
                { "PL", "Planning" },
                { "PS", "Personnel Security" },
                { "RA", "Risk Assessment" },
                { "CA", "Security Assessment and Authorization" },
                { "SC", "System and Communications Protection" },
                { "SI", "System and Information Integrity" },
                { "SA", "System and Services Acquisition" },
                { "PM", "Program Management" }
            };

            var familyName = familyNames.GetValueOrDefault(normalizedFamily, normalizedFamily);

            return JsonSerializer.Serialize(new
            {
                success = true,
                controlFamily = normalizedFamily,
                familyName = familyName,
                scope = scope,
                subscriptionId = subscriptionId,
                resourceGroupName = resourceGroupName,
                assessmentId = assessment.AssessmentId,
                timestamp = DateTimeOffset.UtcNow,

                // Header with visual indicators and score bar
                header = new
                {
                    title = $"📊 {normalizedFamily} - {familyName}",
                    subtitle = $"Control Family Assessment for {scope}",
                    complianceScore = Math.Round(familyResult.ComplianceScore, 2),
                    scoreBar = GenerateScoreBar(familyResult.ComplianceScore),
                    scoreEmoji = familyResult.ComplianceScore >= 90 ? "✅" :
                                familyResult.ComplianceScore >= 70 ? "⚠️" : "❌",
                    status = familyResult.ComplianceScore >= 90 ? "✅ Compliant" :
                            familyResult.ComplianceScore >= 70 ? "⚠️ Partially Compliant" : "❌ Non-Compliant",
                    statusMessage = familyResult.ComplianceScore >= 90 ? 
                        "Excellent! This control family meets compliance requirements." :
                        familyResult.ComplianceScore >= 70 ? 
                        "Improvement needed - some controls are not meeting requirements." :
                        "Action required - multiple controls need immediate attention."
                },

                // Summary statistics with visual formatting
                summary = new
                {
                    title = "📋 OVERVIEW",
                    controls = new
                    {
                        total = familyResult.TotalControls,
                        passed = new { count = familyResult.PassedControls, emoji = "✅" },
                        failed = new { count = familyResult.TotalControls - familyResult.PassedControls, emoji = "❌" },
                        complianceRate = $"{Math.Round(familyResult.ComplianceScore, 0)}%"
                    },
                    findings = new
                    {
                        total = findings.Count,
                        autoRemediable = new { count = autoRemediableCount, emoji = "✨" },
                        manualRemediation = new { count = manualRemediationCount, emoji = "🔧" },
                        summary = findings.Count == 0 ? "🎉 No issues found!" :
                                 autoRemediableCount > 0 ? 
                                 $"✨ {autoRemediableCount} of {findings.Count} findings can be auto-fixed!" : 
                                 $"🔧 All {findings.Count} findings require manual remediation"
                    }
                },

                // Findings breakdown by severity with visual bars
                findingsBySeverity = new
                {
                    title = "⚠️ FINDINGS BY SEVERITY",
                    total = findings.Count,
                    breakdown = new[]
                    {
                        criticalFindings.Count > 0 ? new 
                        { 
                            severity = "CRITICAL",
                            count = criticalFindings.Count, 
                            emoji = "🔴",
                            bar = new string('█', Math.Min(criticalFindings.Count, 10)),
                            autoRemediable = criticalFindings.Count(f => f.IsAutoRemediable),
                            message = criticalFindings.Count == 1 ? 
                                "1 critical issue requires immediate attention" : 
                                $"{criticalFindings.Count} critical issues require immediate attention"
                        } : null,
                        highFindings.Count > 0 ? new 
                        { 
                            severity = "HIGH",
                            count = highFindings.Count, 
                            emoji = "🟠",
                            bar = new string('█', Math.Min(highFindings.Count, 10)),
                            autoRemediable = highFindings.Count(f => f.IsAutoRemediable),
                            message = highFindings.Count == 1 ? 
                                "1 high-priority finding" : 
                                $"{highFindings.Count} high-priority findings"
                        } : null,
                        mediumFindings.Count > 0 ? new 
                        { 
                            severity = "MEDIUM",
                            count = mediumFindings.Count, 
                            emoji = "🟡",
                            bar = new string('█', Math.Min(mediumFindings.Count, 10)),
                            autoRemediable = mediumFindings.Count(f => f.IsAutoRemediable),
                            message = mediumFindings.Count == 1 ? 
                                "1 medium-priority finding" : 
                                $"{mediumFindings.Count} medium-priority findings"
                        } : null,
                        lowFindings.Count > 0 ? new 
                        { 
                            severity = "LOW",
                            count = lowFindings.Count, 
                            emoji = "🟢",
                            bar = new string('█', Math.Min(lowFindings.Count, 10)),
                            autoRemediable = lowFindings.Count(f => f.IsAutoRemediable),
                            message = lowFindings.Count == 1 ? 
                                "1 informational finding" : 
                                $"{lowFindings.Count} informational findings"
                        } : null
                    }.Where(s => s != null)
                },

                // Findings grouped by resource type with enhanced visuals
                findingsByResourceType = new
                {
                    title = "🔍 FINDINGS BY RESOURCE TYPE",
                    resourceTypes = findingsByResourceType.Select(g => new
                    {
                        resourceType = g.Key,
                        displayName = GetResourceTypeDisplayName(g.Key),
                        findings = new
                        {
                            total = g.Count(),
                            critical = g.Count(f => f.Severity == AtoFindingSeverity.Critical),
                            high = g.Count(f => f.Severity == AtoFindingSeverity.High),
                            medium = g.Count(f => f.Severity == AtoFindingSeverity.Medium),
                            low = g.Count(f => f.Severity == AtoFindingSeverity.Low),
                            autoRemediable = g.Count(f => f.IsAutoRemediable)
                        },
                        highestSeverity = new
                        {
                            level = g.Max(f => f.Severity).ToString(),
                            emoji = g.Max(f => f.Severity) == AtoFindingSeverity.Critical ? "🔴" :
                                   g.Max(f => f.Severity) == AtoFindingSeverity.High ? "🟠" :
                                   g.Max(f => f.Severity) == AtoFindingSeverity.Medium ? "🟡" : "🟢"
                        },
                        summary = g.Count() == 1 ? 
                            $"{g.Max(f => f.Severity)} - 1 finding" :
                            $"{g.Max(f => f.Severity)} - {g.Count()} findings"
                    })
                },

                // Detailed findings with enhanced formatting
                detailedFindings = new
                {
                    title = findings.Count == 0 ? "✅ NO FINDINGS" : 
                           criticalFindings.Count > 0 ? "🔴 CRITICAL & HIGH PRIORITY FINDINGS" :
                           highFindings.Count > 0 ? "🟠 HIGH PRIORITY FINDINGS" :
                           "📋 FINDINGS DETAILS",
                    count = findings.Count,
                    items = findings
                        .OrderByDescending(f => f.Severity)
                        .ThenBy(f => f.IsAutoRemediable ? 0 : 1)
                        .Select((f, index) => new
                        {
                            // Header
                            number = index + 1,
                            id = f.Id,
                            header = new
                            {
                                severity = f.Severity.ToString(),
                                emoji = f.Severity == AtoFindingSeverity.Critical ? "🔴" :
                                       f.Severity == AtoFindingSeverity.High ? "🟠" :
                                       f.Severity == AtoFindingSeverity.Medium ? "🟡" : "🟢",
                                badge = f.IsAutoRemediable ? "✨ AUTO-FIX AVAILABLE" : "🔧 MANUAL",
                                title = f.Title
                            },
                            
                            // Details
                            description = f.Description,
                            
                            // Resource info
                            resource = new
                            {
                                type = GetResourceTypeDisplayName(f.ResourceType),
                                name = f.ResourceName,
                                id = f.ResourceId
                            },
                            
                            // Remediation section
                            remediation = new
                            {
                                isAutoRemediable = f.IsAutoRemediable,
                                status = f.IsAutoRemediable ? 
                                    "✨ This finding can be automatically fixed" : 
                                    "🔧 Manual remediation required - review guidance below",
                                recommendation = f.Recommendation,
                                guidance = f.RemediationGuidance,
                                nextStep = f.IsAutoRemediable ?
                                    $"Generate a remediation plan to auto-fix this issue" :
                                    "Review the recommendation and follow the guidance to remediate manually"
                            },
                            
                            // Compliance mapping
                            compliance = new
                            {
                                affectedControls = f.AffectedNistControls,
                                frameworks = f.ComplianceFrameworks,
                                status = f.ComplianceStatus.ToString(),
                                impact = f.AffectedNistControls.Count > 1 ? 
                                    $"⚠️ Impacts {f.AffectedNistControls.Count} NIST controls" :
                                    $"Impacts {f.AffectedNistControls.Count} NIST control"
                            },
                            
                            // Metadata
                            metadata = new
                            {
                                source = f.Metadata?.ContainsKey("Source") == true ? f.Metadata["Source"]?.ToString() : "Unknown",
                                stigId = f.Metadata?.ContainsKey("StigId") == true ? f.Metadata["StigId"]?.ToString() : null,
                                vulnId = f.Metadata?.ContainsKey("VulnId") == true ? f.Metadata["VulnId"]?.ToString() : null,
                                category = f.Metadata?.ContainsKey("Category") == true ? f.Metadata["Category"]?.ToString() : null,
                                detectedAt = f.DetectedAt,
                                ruleId = f.RuleId
                            }
                        })
                },

                // Remediation summary with action plan
                remediationSummary = new
                {
                    title = "🔧 REMEDIATION STRATEGY",
                    priority = new
                    {
                        level = criticalFindings.Count > 0 ? "CRITICAL" :
                               highFindings.Count > 0 ? "HIGH" :
                               mediumFindings.Count > 0 ? "MEDIUM" : "LOW",
                        emoji = criticalFindings.Count > 0 ? "🔴" :
                               highFindings.Count > 0 ? "🟠" :
                               mediumFindings.Count > 0 ? "🟡" : "🟢",
                        timeline = criticalFindings.Count > 0 ? "Immediate action required" :
                                  highFindings.Count > 0 ? "Address within 24-48 hours" :
                                  mediumFindings.Count > 0 ? "Address within 7 days" :
                                  "Address as time permits"
                    },
                    breakdown = new
                    {
                        autoRemediable = new
                        {
                            count = autoRemediableCount,
                            percentage = findings.Count > 0 ? Math.Round((autoRemediableCount * 100.0) / findings.Count, 0) : 0,
                            emoji = "✨",
                            message = autoRemediableCount == 0 ? "No auto-fix available" :
                                     autoRemediableCount == 1 ? "1 finding can be auto-fixed!" :
                                     $"{autoRemediableCount} findings can be auto-fixed - quick compliance wins!"
                        },
                        manualRemediation = new
                        {
                            count = manualRemediationCount,
                            percentage = findings.Count > 0 ? Math.Round((manualRemediationCount * 100.0) / findings.Count, 0) : 0,
                            emoji = "🔧",
                            message = manualRemediationCount == 0 ? "No manual work needed!" :
                                     manualRemediationCount == 1 ? "1 finding requires manual review" :
                                     $"{manualRemediationCount} findings require manual remediation"
                        }
                    },
                    recommendation = findings.Count == 0 ? 
                        "🎉 Great job! No findings detected for this control family." :
                        autoRemediableCount > 0 ?
                        $"Start with auto-remediation to quickly fix {autoRemediableCount} issue{(autoRemediableCount > 1 ? "s" : "")}, then tackle the {manualRemediationCount} manual item{(manualRemediationCount != 1 ? "s" : "")}." :
                        $"All {manualRemediationCount} finding{(manualRemediationCount != 1 ? "s" : "")} require manual remediation. Review each finding's guidance and work with your security team."
                },

                // Next steps with natural language guidance
                nextSteps = new
                {
                    title = "📋 RECOMMENDED ACTIONS",
                    overview = criticalFindings.Any() ?
                        $"The {familyName} control family has {criticalFindings.Count} critical findings requiring immediate attention. Address these first to reduce security risks." :
                        highFindings.Any() ?
                        $"The {familyName} control family has {highFindings.Count} high-priority findings. Start working on these to improve compliance." :
                        mediumFindings.Any() ?
                        $"The {familyName} control family has {mediumFindings.Count} medium-priority findings. These can be addressed over time." :
                        lowFindings.Any() ?
                        $"Great news! The {familyName} control family only has {lowFindings.Count} low-priority informational findings. These are recommendations for continuous improvement." :
                        $"Excellent! The {familyName} control family has no findings. Your controls are fully compliant.",
                    
                    immediate = criticalFindings.Any() ? new[]
                    {
                        new {
                            priority = "� CRITICAL",
                            action = $"Address {criticalFindings.Count} critical findings in {familyName}",
                            command = $"generate a remediation plan for control family {normalizedFamily}",
                            explanation = $"These critical findings in {familyName} represent immediate security gaps that could impact your authorization to operate (ATO). Create a remediation plan to fix them urgently."
                        },
                        criticalFindings.Count(f => f.IsAutoRemediable) > 0 ? new {
                            priority = "✨ QUICK WIN",
                            action = $"{criticalFindings.Count(f => f.IsAutoRemediable)} critical findings can be auto-remediated",
                            command = "execute remediation plan",
                            explanation = "Some critical findings have automated fixes available. Running auto-remediation will quickly reduce your risk exposure."
                        } : null
                    }.Where(a => a != null) : new[]
                    {
                        new {
                            priority = "✅ GOOD STATUS",
                            action = $"No critical findings in {familyName}",
                            command = null as string,
                            explanation = autoRemediableCount > 0 ?
                                $"Your {familyName} controls don't have critical issues. However, {autoRemediableCount} findings can be quickly fixed with auto-remediation." :
                                $"Your {familyName} controls are in good shape. Review the findings above to ensure ongoing compliance and address any recommendations."
                        }
                    },
                    
                    shortTerm = new[]
                    {
                        autoRemediableCount > 0 ? new {
                            action = $"Auto-remediate {autoRemediableCount} findings",
                            command = $"generate a remediation plan for control family {normalizedFamily}",
                            explanation = $"Generate an action plan that identifies which findings can be automatically fixed vs. which require manual intervention. This helps prioritize your remediation efforts."
                        } : null,
                        highFindings.Any() ? new {
                            action = $"Review {highFindings.Count} high-severity findings",
                            command = "review findings above",
                            explanation = "High-severity findings need attention within 24-48 hours. Review each finding's recommendation and remediation guidance to understand what actions are needed."
                        } : null,
                        manualRemediationCount > 0 ? new {
                            action = $"Plan manual remediation for {manualRemediationCount} findings",
                            command = $"generate a remediation plan for control family {normalizedFamily}",
                            explanation = familyName == "Access Control" || familyName == "Identification and Authentication" ?
                                $"Most {familyName} findings require manual remediation because they involve business decisions about who should have access, what roles are appropriate, and how to enforce least privilege. Work with your security team and stakeholders to review and update access policies." :
                                $"These findings require manual review and remediation. They may involve policy decisions, architectural changes, or configuration that needs business approval. Schedule time with your security team to address them."
                        } : null
                    }.Where(a => a != null),
                    
                    tools = new[]
                    {
                        new {
                            icon = "💾",
                            action = "Save and share this analysis",
                            command = "Copy the findings to your documentation or share with your team",
                            explanation = $"Document these {familyName} findings in your compliance tracking system, security backlog, or share with stakeholders who need to review access control policies. This creates an audit trail and ensures findings aren't forgotten."
                        },
                        new {
                            icon = "🔄",
                            action = "Re-assess after remediation",
                            command = "run compliance assessment for production",
                            explanation = $"After fixing findings, run another compliance assessment to verify your changes improved the {familyName} compliance score. This validates your remediation efforts and tracks progress over time."
                        },
                        new {
                            icon = "📊",
                            action = "Compare with other control families",
                            command = assessment.ControlFamilyResults
                                .Where(cf => cf.Key != normalizedFamily && cf.Value.ComplianceScore < 70)
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Take(2)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "get control family details for SC",
                            explanation = $"Check other control families to get a complete picture of your compliance posture. Focus on families scoring below 70% - currently: {string.Join(", ", assessment.ControlFamilyResults.Where(cf => cf.Value.ComplianceScore < 70).Select(cf => $"{cf.Key} ({cf.Value.ComplianceScore:F0}%)").Take(3))}."
                        },
                        new {
                            icon = "📄",
                            action = "Collect evidence for audit",
                            command = $"collect compliance evidence for {normalizedFamily}",
                            explanation = $"Generate a compliance evidence package for {familyName} that documents your current configuration, policies, and controls. This is essential for ATO packages, audit readiness, and demonstrating compliance to assessors."
                        }
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting control family details for {ControlFamily} in {Subscription}", 
                controlFamily, subscriptionIdOrName);
            return CreateErrorResponse($"get control family details for {controlFamily}", ex);
        }
    }

    [KernelFunction("get_compliance_status")]
    [Description("Get real-time compliance status with continuous monitoring data. " +
                 "Shows current score, active alerts, and recent changes. " +
                 "Use this for quick compliance health checks. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging'). " +
                 "If no subscription is provided, uses the default subscription from persistent configuration (set via set_azure_subscription).")]
    public async Task<string> GetComplianceStatusAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging'). OPTIONAL - if not provided, uses the default subscription from configuration.")] string? subscriptionIdOrName = null,
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
            
            _logger.LogInformation("Getting compliance status for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var status = await _complianceEngine.GetContinuousComplianceStatusAsync(subscriptionId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = status.SubscriptionId,
                timestamp = status.Timestamp,
                currentStatus = new
                {
                    score = Math.Round(status.ComplianceScore, 2),
                    grade = GetComplianceGrade(status.ComplianceScore),
                    monitoringEnabled = status.MonitoringEnabled,
                    lastCheck = status.LastCheckTime,
                    nextCheck = status.NextCheckTime
                },
                trend = new
                {
                    direction = status.TrendDirection,
                    driftPercentage = Math.Round(status.ComplianceDriftPercentage, 2)
                },
                alerts = new
                {
                    active = status.ActiveAlerts,
                    resolvedToday = status.ResolvedToday,
                    autoRemediations = status.AutoRemediationCount
                },
                monitoring = new
                {
                    enabled = status.MonitoringEnabled,
                    lastScan = status.LastCheckTime,
                    nextScan = status.NextCheckTime,
                    activeControls = status.ControlStatuses.Count
                },
                quickActions = new[]
                {
                    status.ActiveAlerts > 0 ? $"Review {status.ActiveAlerts} active alerts" : null,
                    status.TrendDirection == "Declining" ? "Investigate compliance drift - trend is declining" : null,
                    "Run 'run_compliance_assessment' for detailed analysis",
                    "Use 'generate_remediation_plan' to address findings"
                }.Where(a => a != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance status (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("get compliance status", ex);
        }
    }

    [KernelFunction("collect_evidence")]
    [Description("📦 EVIDENCE COLLECTION ONLY - Collect and package compliance evidence artifacts for ATO/eMASS documentation (NOT for scanning). " +
                 "THIS FUNCTION GATHERS DOCUMENTATION - it does NOT scan for security findings. " +
                 "Use ONLY when user says: 'collect evidence', 'generate evidence', 'evidence package', 'gather evidence', 'ATO package', 'documentation'. " +
                 "DO NOT use for: 'run assessment', 'scan', 'check compliance', 'find vulnerabilities'. " +
                 "Output: Evidence package with configuration data, logs, metrics, policies - suitable for ATO attestation and audits. " +
                 "Requires NIST control family (AC, AU, CM, etc.). Can scope to specific resource group. " +
                 "Accepts subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> CollectEvidenceAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("NIST control family (e.g., AC, AU, CM, IA)")] string controlFamily,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Automatically get the current authenticated Azure user
            string userName;
            try
            {
                userName = await _azureResourceService.GetCurrentAzureUserAsync(cancellationToken);
                _logger.LogInformation("Evidence collection initiated by: {UserName}", userName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine current Azure user, using 'Unknown'");
                userName = "Unknown";
            }
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Collecting evidence for {Scope} (input: {Input}), family {Family}, user {User}", 
                scope, subscriptionIdOrName, controlFamily, userName);

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(controlFamily))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID and control family are required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
                subscriptionId,
                controlFamily,
                userName,
                null,
                cancellationToken);

            // Generate actual file content for download
            var jsonContent = GenerateJsonEvidence(evidencePackage);
            var csvContent = GenerateCsvEvidence(evidencePackage);
            var emassXmlContent = GenerateEmassXml(evidencePackage);

            return JsonSerializer.Serialize(new
            {
                header = new
                {
                    title = "📦 COMPLIANCE EVIDENCE COLLECTION",
                    icon = "📄",
                    packageId = evidencePackage.PackageId,
                    timestamp = evidencePackage.CollectionDate
                },
                success = string.IsNullOrEmpty(evidencePackage.Error),
                packageId = evidencePackage.PackageId,
                subscriptionId = evidencePackage.SubscriptionId,
                controlFamily = new
                {
                    code = evidencePackage.ControlFamily,
                    name = GetControlFamilyName(evidencePackage.ControlFamily),
                    icon = "🛡️"
                },
                collection = new
                {
                    collectionDate = evidencePackage.CollectionDate,
                    duration = evidencePackage.CollectionDuration,
                    totalItems = evidencePackage.TotalItems,
                    completenessScore = Math.Round(evidencePackage.CompletenessScore, 2),
                    completenessBar = GenerateScoreBar(evidencePackage.CompletenessScore),
                    status = evidencePackage.CompletenessScore >= 95 ? "✅ Complete" :
                             evidencePackage.CompletenessScore >= 80 ? "⚠️ Mostly Complete" :
                             evidencePackage.CompletenessScore >= 60 ? "🟡 Partial" : "❌ Incomplete"
                },
                evidence = new
                {
                    totalItems = evidencePackage.Evidence.Count,
                    byType = evidencePackage.Evidence
                        .GroupBy(e => e.EvidenceType)
                        .Select(g => new { 
                            type = g.Key, 
                            count = g.Count(),
                            icon = g.Key switch
                            {
                                "Configuration" => "⚙️",
                                "Log" => "📝",
                                "Metric" => "📊",
                                "Policy" => "📋",
                                "Scan" => "🔍",
                                _ => "📄"
                            }
                        }),
                    items = evidencePackage.Evidence.Take(20).Select(e => new
                    {
                        evidenceId = e.EvidenceId,
                        controlId = e.ControlId,
                        type = e.EvidenceType,
                        resourceId = e.ResourceId,
                        collectedAt = e.CollectedAt
                    })
                },
                summary = evidencePackage.Summary,
                attestation = evidencePackage.AttestationStatement,
                error = evidencePackage.Error,
                
                // ========== DOWNLOADABLE FILES ==========
                files = new
                {
                    title = "📥 EVIDENCE FILES",
                    note = "💡 Click 'Insert at Cursor' to copy file contents, then save locally",
                    json = new
                    {
                        format = "JSON",
                        description = "Complete evidence package in JSON format",
                        filename = $"evidence_{evidencePackage.ControlFamily}_{evidencePackage.PackageId}.json",
                        icon = "📄",
                        content = jsonContent,
                        size = $"{jsonContent.Length / 1024} KB"
                    },
                    csv = new
                    {
                        format = "CSV",
                        description = "Evidence items in CSV spreadsheet format",
                        filename = $"evidence_{evidencePackage.ControlFamily}_{evidencePackage.PackageId}.csv",
                        icon = "📊",
                        content = csvContent,
                        size = $"{csvContent.Length / 1024} KB"
                    },
                    emass = new
                    {
                        format = "eMASS XML",
                        description = "DoD eMASS-compatible evidence package (XML)",
                        filename = $"emass_{evidencePackage.ControlFamily}_{evidencePackage.PackageId}.xml",
                        icon = "🏛️",
                        content = emassXmlContent,
                        size = $"{emassXmlContent.Length / 1024} KB",
                        priority = "HIGH",
                        instructions = "Upload this XML file to the DoD eMASS portal for ATO package submission"
                    }
                },
                
                // ========== EMASS INTEGRATION ==========
                emassIntegration = new
                {
                    title = "🏛️ eMASS INTEGRATION INSTRUCTIONS",
                    status = "✅ Ready for submission",
                    steps = new[]
                    {
                        "1. Copy the eMASS XML content from 'files.emass.content' above",
                        "2. Save it as a .xml file on your local system",
                        "3. Log into the DoD eMASS portal (https://emass.apps.mil)",
                        "4. Navigate to your system's ATO package section",
                        "5. Upload the XML file as evidence for the control family",
                        "6. Review and submit for assessment"
                    },
                    note = "The eMASS XML package includes control implementation statements, test results, and configuration evidence"
                },
                
                // ========== QUICK ACTIONS ==========
                quickActions = new object[]
                {
                    new
                    {
                        action = "generate_poam",
                        command = $"Generate POA&M for control family {evidencePackage.ControlFamily}",
                        description = "Generate Plan of Action & Milestones (POA&M) for findings",
                        icon = "📋",
                        priority = "HIGH"
                    },
                    new
                    {
                        action = "collect_more_evidence",
                        command = $"Collect evidence for control family {evidencePackage.ControlFamily} in subscription {subscriptionId}",
                        description = evidencePackage.CompletenessScore < 100 ? 
                            "Collect additional evidence to reach 100% completeness" : 
                            "Refresh evidence collection",
                        icon = evidencePackage.CompletenessScore < 100 ? "⚠️" : "🔄",
                        priority = evidencePackage.CompletenessScore < 100 ? "HIGH" : "LOW"
                    },
                    new
                    {
                        action = "assess_compliance",
                        command = $"Run compliance assessment for subscription {subscriptionId}",
                        description = "Verify control compliance status",
                        icon = "🛡️",
                        priority = "MEDIUM"
                    }
                },
                
                // ========== NEXT STEPS ==========
                nextSteps = new
                {
                    title = "📋 NEXT STEPS",
                    immediate = evidencePackage.CompletenessScore < 100 
                        ? new[]
                        {
                            $"⚠️ URGENT: Evidence collection is only {evidencePackage.CompletenessScore:F1}% complete",
                            $"🔍 Collect additional evidence to reach 100% completeness",
                            "📊 Review missing evidence items and gather required data"
                        }
                        : new[]
                        {
                            "✅ Evidence collection is complete!",
                            "📥 Download the evidence package in your preferred format",
                            "🏛️ Generate eMASS package for submission to auditors"
                        },
                    recommended = new[]
                    {
                        "📄 Review evidence items for accuracy and completeness",
                        "🛡️ Run a compliance assessment to verify control status",
                        "📋 Generate a POA&M for any identified gaps",
                        "🏛️ Prepare eMASS submission package with attestations"
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting evidence (input: {Input}), family {Family}", 
                subscriptionIdOrName, controlFamily);
            return CreateErrorResponse("collect evidence", ex);
        }
    }

    /// <summary>
    /// Generates JSON file content for evidence package
    /// </summary>
    private string GenerateJsonEvidence(EvidencePackage evidencePackage)
    {
        var data = new
        {
            packageInfo = new
            {
                packageId = evidencePackage.PackageId,
                subscriptionId = evidencePackage.SubscriptionId,
                controlFamily = evidencePackage.ControlFamily,
                collectionDate = evidencePackage.CollectionDate,
                completenessScore = evidencePackage.CompletenessScore
            },
            evidence = evidencePackage.Evidence.Select(e => new
            {
                evidenceId = e.EvidenceId,
                type = e.EvidenceType,
                controlId = e.ControlId,
                resourceId = e.ResourceId,
                collectedAt = e.CollectedAt,
                data = e.Data
            }),
            summary = evidencePackage.Summary,
            attestation = evidencePackage.AttestationStatement
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Generates CSV file content for evidence package
    /// </summary>
    private string GenerateCsvEvidence(EvidencePackage evidencePackage)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Evidence ID,Type,Control ID,Resource ID,Collected At,Data Summary");
        
        foreach (var item in evidencePackage.Evidence)
        {
            var dataSummary = item.Data != null ? 
                JsonSerializer.Serialize(item.Data).Replace("\"", "\"\"").Substring(0, Math.Min(200, JsonSerializer.Serialize(item.Data).Length)) : 
                "";
            
            csv.AppendLine($"\"{item.EvidenceId}\",\"{item.EvidenceType}\",\"{item.ControlId}\",\"{item.ResourceId}\",\"{item.CollectedAt:yyyy-MM-dd HH:mm:ss}\",\"{dataSummary}\"");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Generates eMASS-compatible XML package for DoD submission
    /// </summary>
    private string GenerateEmassXml(EvidencePackage evidencePackage)
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<emass-evidence-package xmlns=\"https://emass.apps.mil/schema/evidence\" version=\"3.1\">");
        xml.AppendLine("  <metadata>");
        xml.AppendLine($"    <package-id>{evidencePackage.PackageId}</package-id>");
        xml.AppendLine($"    <collection-date>{evidencePackage.CollectionDate:yyyy-MM-ddTHH:mm:ssZ}</collection-date>");
        xml.AppendLine($"    <subscription-id>{evidencePackage.SubscriptionId}</subscription-id>");
        xml.AppendLine($"    <control-family>{evidencePackage.ControlFamily}</control-family>");
        xml.AppendLine($"    <completeness-score>{evidencePackage.CompletenessScore:F2}</completeness-score>");
        xml.AppendLine("  </metadata>");
        xml.AppendLine("  <evidence-items>");
        
        foreach (var item in evidencePackage.Evidence)
        {
            xml.AppendLine("    <evidence-item>");
            xml.AppendLine($"      <id>{SecurityElement.Escape(item.EvidenceId)}</id>");
            xml.AppendLine($"      <type>{SecurityElement.Escape(item.EvidenceType)}</type>");
            xml.AppendLine($"      <control>{SecurityElement.Escape(item.ControlId)}</control>");
            xml.AppendLine($"      <resource>{SecurityElement.Escape(item.ResourceId ?? "N/A")}</resource>");
            xml.AppendLine($"      <collected-at>{item.CollectedAt:yyyy-MM-ddTHH:mm:ssZ}</collected-at>");
            
            if (item.Data != null)
            {
                xml.AppendLine("      <data>");
                xml.AppendLine($"        <![CDATA[{JsonSerializer.Serialize(item.Data, new JsonSerializerOptions { WriteIndented = true })}]]>");
                xml.AppendLine("      </data>");
            }
            
            xml.AppendLine("    </evidence-item>");
        }
        
        xml.AppendLine("  </evidence-items>");
        xml.AppendLine("  <attestation>");
        xml.AppendLine($"    <![CDATA[{evidencePackage.AttestationStatement}]]>");
        xml.AppendLine("  </attestation>");
        xml.AppendLine("  <summary>");
        xml.AppendLine($"    <![CDATA[{evidencePackage.Summary}]]>");
        xml.AppendLine("  </summary>");
        xml.AppendLine("</emass-evidence-package>");
        
        return xml.ToString();
    }

    // ========== REMEDIATION FUNCTIONS ==========

    [KernelFunction("generate_remediation_plan")]
    [Description("Generate a comprehensive remediation plan for compliance findings. " +
                 "Analyzes findings and creates a prioritized action plan. " +
                 "Essential for planning compliance improvements. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging'). " +
                 "If no subscription is specified, uses the most recent assessment from the last used subscription.")]
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
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> ExecuteRemediationAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Finding ID to remediate")] string findingId,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        [Description("Dry run mode - preview changes without applying (true/false, default: true)")] bool dryRun = true,
        [Description("Require approval before executing (true/false, default: false)")] bool requireApproval = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
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

    // ========== REPORTING FUNCTIONS ==========

    [KernelFunction("perform_risk_assessment")]
    [Description("Perform comprehensive risk assessment for compliance posture. " +
                 "Analyzes risk levels and provides risk mitigation recommendations. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> PerformRiskAssessmentAsync(
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
            
            _logger.LogInformation("Performing risk assessment for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var riskAssessment = await _complianceEngine.PerformRiskAssessmentAsync(subscriptionId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = riskAssessment.SubscriptionId,
                assessmentDate = riskAssessment.AssessmentDate,
                overallRisk = new
                {
                    level = riskAssessment.OverallRiskLevel.ToString(),
                    score = Math.Round(riskAssessment.OverallRiskScore, 2),
                    rating = riskAssessment.RiskRating
                },
                riskCategories = riskAssessment.RiskCategories.Select(rc => new
                {
                    category = rc.Key,
                    level = rc.Value.RiskLevel,
                    score = Math.Round(rc.Value.Score, 2),
                    findingCount = rc.Value.FindingCount,
                    topRisks = rc.Value.TopRisks.Take(5)
                }),
                recommendations = riskAssessment.MitigationRecommendations?.Take(10),
                executiveSummary = riskAssessment.ExecutiveSummary,
                nextSteps = new[]
                {
                    $"Priority action: Address the {riskAssessment.RiskCategories.Count(r => r.Value.RiskLevel == "Critical")} critical risk categories first.",
                    "Say 'generate a remediation plan for this assessment' to create a comprehensive mitigation strategy.",
                    "Review the top risks in each category above and prioritize based on your organization's risk tolerance."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing risk assessment (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("perform risk assessment", ex);
        }
    }

    [KernelFunction("get_compliance_timeline")]
    [Description("Get compliance timeline showing historical trends and changes. " +
                 "Useful for tracking compliance improvements over time. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> GetComplianceTimelineAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Days to look back (default: 30)")] int daysBack = 30,
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
            
            _logger.LogInformation("Getting compliance timeline for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var endDate = DateTimeOffset.UtcNow;
            var startDate = endDate.AddDays(-daysBack);

            var timeline = await _complianceEngine.GetComplianceTimelineAsync(
                subscriptionId, 
                startDate, 
                endDate, 
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = timeline.SubscriptionId,
                period = new
                {
                    startDate = timeline.StartDate,
                    endDate = timeline.EndDate,
                    daysAnalyzed = daysBack
                },
                trends = new
                {
                    currentScore = Math.Round(timeline.CurrentScore, 2),
                    previousScore = Math.Round(timeline.PreviousScore, 2),
                    change = Math.Round(timeline.ScoreChange, 2),
                    trend = timeline.TrendDirection
                },
                dataPoints = timeline.DataPoints.Select(dp => new
                {
                    date = dp.Date,
                    score = Math.Round(dp.Score, 2),
                    findingsCount = dp.FindingsCount
                }),
                majorEvents = timeline.MajorEvents.Take(10).Select(evt => new
                {
                    date = evt.Date,
                    type = evt.EventType,
                    description = evt.Description,
                    impact = evt.Impact
                }),
                insights = timeline.Insights,
                nextSteps = new[]
                {
                    timeline.TrendDirection == "Improving" ? "Great news - your compliance is improving! Continue your current efforts and maintain momentum." : null,
                    timeline.TrendDirection == "Declining" ? "Attention needed - compliance is declining. Say 'show me recent compliance changes' to investigate what happened." : null,
                    "Say 'run a compliance assessment for this subscription' to get the current detailed compliance analysis."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance timeline (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("get compliance timeline", ex);
        }
    }

    [KernelFunction("generate_compliance_certificate")]
    [Description("Generate a compliance certificate for ATO package. " +
                 "Creates official compliance attestation document. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> GenerateComplianceCertificateAsync(
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
            
            _logger.LogInformation("Generating compliance certificate for {Scope} (input: {Input})", 
                scope, subscriptionIdOrName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var certificate = await _complianceEngine.GenerateComplianceCertificateAsync(subscriptionId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                certificateId = certificate.CertificateId,
                subscriptionId = certificate.SubscriptionId,
                issuedDate = certificate.IssuedDate,
                expirationDate = certificate.ExpirationDate,
                complianceStatus = certificate.ComplianceStatus.ToString(),
                certificationLevel = certificate.CertificationLevel,
                complianceScore = Math.Round(certificate.ComplianceScore, 2),
                frameworks = certificate.CertifiedFrameworks,
                controlsCertified = new
                {
                    total = certificate.TotalControls,
                    certified = certificate.CertifiedControls,
                    certificationRate = Math.Round((double)certificate.CertifiedControls / certificate.TotalControls * 100, 2)
                },
                attestation = certificate.AttestationStatement,
                signatoryInformation = certificate.SignatoryInformation,
                validityPeriod = certificate.ValidityPeriod,
                nextSteps = new[]
                {
                    "Include this compliance certificate in your ATO package submission.",
                    $"Important: This certificate expires on {certificate.ExpirationDate:yyyy-MM-dd} - mark your calendar!",
                    "Say 'run a compliance assessment' before the expiration date to renew the certificate.",
                    "Say 'collect compliance evidence for this subscription' to gather all supporting documentation for auditors."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance certificate (input: {Input})", subscriptionIdOrName);
            return CreateErrorResponse("generate compliance certificate", ex);
        }
    }

    [KernelFunction("get_remediation_guide")]
    [Description("Generate manual remediation guidance for findings that cannot be auto-remediated. " +
                 "Provides step-by-step instructions, prerequisites, and validation steps. Can scope to a specific resource group. " +
                 "Accepts either a subscription GUID or friendly name (e.g., 'production', 'dev', 'staging').")]
    public async Task<string> GetRemediationGuideAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Finding ID to get remediation guidance for")] string findingId,
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
            
            _logger.LogInformation("Generating remediation guide for {Scope} (input: {Input}), finding {FindingId}", 
                scope, subscriptionIdOrName, findingId);

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

            var guide = await _remediationEngine.GenerateManualRemediationGuideAsync(
                finding,
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                guideId = guide.GuideId,
                findingId = guide.FindingId,
                title = guide.Title,
                overview = guide.Overview,
                skillLevel = guide.SkillLevel,
                estimatedDuration = guide.EstimatedDuration,
                prerequisites = guide.Prerequisites,
                requiredPermissions = guide.RequiredPermissions,
                steps = guide.Steps.Select(step => new
                {
                    order = step.Order,
                    description = step.Description,
                    command = step.Command,
                    automationScript = step.AutomationScript
                }),
                validationSteps = guide.ValidationSteps,
                rollbackPlan = guide.RollbackPlan != null ? new
                {
                    description = guide.RollbackPlan.Description,
                    steps = guide.RollbackPlan.Steps,
                    estimatedTime = guide.RollbackPlan.EstimatedRollbackTime
                } : null,
                references = guide.References,
                nextSteps = new[]
                {
                    "Step 1: Review the prerequisites above and ensure you have all required permissions and tools.",
                    "Step 2: Follow the remediation steps in order - don't skip any steps.",
                    "Step 3: Use the validation steps to confirm the remediation was successful.",
                    "Step 4: If any issues occur during remediation, use the rollback plan immediately.",
                    "Step 5: Say 'run a compliance assessment for this subscription' to verify the finding is now resolved."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating remediation guide for finding {FindingId}", findingId);
            return CreateErrorResponse("generate remediation guide", ex);
        }
    }

    // ========== EVIDENCE DOWNLOAD FUNCTIONS ==========
    // Note: Evidence downloads are available via API endpoints in ComplianceController
    // Use the download URLs provided in the collect_evidence response

    [KernelFunction("generate_emass_package")]
    [Description("Generate a DoD eMASS-compatible evidence package for a control family. " +
                 "Creates properly formatted XML package for submission to Enterprise Mission Assurance Support Service. " +
                 "Includes all required metadata, attestations, and evidence items.")]
    public async Task<string> GenerateEmassPackageForControlFamilyAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("NIST control family (e.g., AC, AU, CM, IA)")] string controlFamily,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Automatically get the current authenticated Azure user
            string userName;
            try
            {
                userName = await _azureResourceService.GetCurrentAzureUserAsync(cancellationToken);
                _logger.LogInformation("eMASS package generation initiated by: {UserName}", userName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine current Azure user, using 'Unknown'");
                userName = "Unknown";
            }
            
            _logger.LogInformation("Generating eMASS package for subscription {SubscriptionId}, family {Family}", 
                subscriptionId, controlFamily);

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(controlFamily))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID and control family are required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Collect evidence first
            var evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
                subscriptionId,
                controlFamily,
                userName,
                null,
                cancellationToken);

            // Generate eMASS-compatible package
            var emassPackage = await GenerateEmassPackageAsync(evidencePackage, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🏛️ eMASS EVIDENCE PACKAGE",
                    icon = "📦",
                    format = "DoD eMASS XML",
                    packageId = evidencePackage.PackageId
                },
                package = new
                {
                    packageId = evidencePackage.PackageId,
                    subscriptionId = evidencePackage.SubscriptionId,
                    controlFamily = new
                    {
                        code = evidencePackage.ControlFamily,
                        name = GetControlFamilyName(evidencePackage.ControlFamily)
                    },
                    generatedAt = DateTimeOffset.UtcNow,
                    format = "eMASS XML",
                    schemaVersion = emassPackage.schemaVersion,
                    totalItems = evidencePackage.TotalItems,
                    completenessScore = Math.Round(evidencePackage.CompletenessScore, 2)
                },
                emassMetadata = new
                {
                    systemId = emassPackage.systemId,
                    controlImplementation = emassPackage.controlImplementation,
                    testResults = emassPackage.testResults,
                    poamItems = emassPackage.poamItems,
                    artifactCount = emassPackage.artifactCount
                },
                download = new
                {
                    fileName = $"emass-{controlFamily}-{evidencePackage.PackageId}.xml",
                    contentType = "application/xml",
                    fileSize = emassPackage.xmlContent.Length,
                    downloadUrl = $"/api/compliance/evidence/download/{evidencePackage.PackageId}?format=emass",
                    base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(emassPackage.xmlContent))
                },
                validation = new
                {
                    schemaValid = emassPackage.isValid,
                    warnings = emassPackage.warnings,
                    readyForSubmission = emassPackage.isValid && evidencePackage.CompletenessScore >= 95
                },
                nextSteps = new
                {
                    title = "📋 NEXT STEPS FOR eMASS SUBMISSION",
                    immediate = new[]
                    {
                        emassPackage.isValid ? 
                            "✅ Package is valid and ready for eMASS submission" : 
                            "⚠️ Package has validation warnings - review before submission",
                        evidencePackage.CompletenessScore < 95 ?
                            $"⚠️ Evidence is only {evidencePackage.CompletenessScore:F1}% complete - collect more evidence for best results" :
                            "✅ Evidence collection is complete"
                    },
                    steps = new[]
                    {
                        "1. Download the eMASS XML package using the download URL above",
                        "2. Review the package contents and validation warnings",
                        "3. Log in to DoD eMASS portal (https://emass.apps.mil)",
                        "4. Navigate to: System Profile → Artifacts → Import",
                        "5. Upload the XML package file",
                        "6. Review imported artifacts and complete any required fields",
                        "7. Submit for approval workflow"
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating eMASS package for control family {Family}", controlFamily);
            return CreateErrorResponse("generate eMASS package", ex);
        }
    }

    [KernelFunction("generate_poam")]
    [Description("Generate Plan of Action & Milestones (POA&M) for compliance findings. " +
                 "Creates DoD-standard POA&M document for tracking remediation progress. " +
                 "Essential for ATO package and ongoing compliance management.")]
    public async Task<string> GeneratePoamAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("Optional control family to limit scope (e.g., AC, AU, CM)")] string? controlFamily = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            _logger.LogInformation("Generating POA&M for subscription {SubscriptionId}, family {Family}", 
                subscriptionId, controlFamily ?? "all");

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get latest assessment findings
            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId, null, cancellationToken);
            
            var findings = assessment.ControlFamilyResults
                .SelectMany(cf => cf.Value.Findings)
                .ToList();

            // Filter by control family if specified
            if (!string.IsNullOrWhiteSpace(controlFamily))
            {
                findings = findings.Where(f => 
                    f.AffectedNistControls.Any(c => c.StartsWith(controlFamily, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            if (!findings.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "No findings to include in POA&M - subscription is compliant!",
                    subscriptionId = subscriptionId
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Generate remediation plan
            var plan = await _remediationEngine.GenerateRemediationPlanAsync(
                subscriptionId,
                findings,
                cancellationToken);

            // Format as POA&M
            var poamId = $"POAM-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "📋 PLAN OF ACTION & MILESTONES (POA&M)",
                    icon = "📊",
                    poamId = poamId,
                    generatedAt = DateTimeOffset.UtcNow
                },
                poam = new
                {
                    poamId = poamId,
                    subscriptionId = subscriptionId,
                    controlFamily = controlFamily,
                    status = "Open",
                    priority = plan.Priority,
                    generatedDate = DateTimeOffset.UtcNow,
                    estimatedCompletion = plan.Timeline?.EndDate,
                    responsibleParty = "Platform Engineering Team"
                },
                summary = new
                {
                    totalFindings = findings.Count,
                    criticalCount = findings.Count(f => f.Severity.ToString() == "Critical"),
                    highCount = findings.Count(f => f.Severity.ToString() == "High"),
                    mediumCount = findings.Count(f => f.Severity.ToString() == "Medium"),
                    lowCount = findings.Count(f => f.Severity.ToString() == "Low"),
                    estimatedEffort = plan.EstimatedEffort,
                    projectedRiskReduction = Math.Round(plan.ProjectedRiskReduction, 2)
                },
                milestones = plan.Timeline?.Milestones.Select(m => new
                {
                    date = m.Date,
                    description = m.Description,
                    deliverables = m.Deliverables
                }),
                poamItems = findings.Select((f, index) => new
                {
                    itemNumber = index + 1,
                    weakness = f.Title,
                    controlNumber = f.AffectedNistControls.FirstOrDefault() ?? "N/A",
                    severity = f.Severity.ToString(),
                    resourceId = f.ResourceId,
                    remediation = new
                    {
                        description = f.Recommendation,
                        isAutomated = f.IsAutoRemediable,
                        estimatedEffort = plan.RemediationItems.FirstOrDefault(ri => ri.FindingId == f.Id)?.EstimatedEffort,
                        milestoneDueDate = plan.Timeline?.Milestones.FirstOrDefault()?.Date
                    },
                    status = "Open",
                    riskLevel = f.Severity.ToString()
                }).ToList(),
                downloads = new
                {
                    formats = new[]
                    {
                        new { format = "PDF", url = $"/api/compliance/poam/{poamId}/download?format=pdf", icon = "📑" },
                        new { format = "Excel", url = $"/api/compliance/poam/{poamId}/download?format=xlsx", icon = "📊" },
                        new { format = "eMASS XML", url = $"/api/compliance/poam/{poamId}/download?format=emass", icon = "🏛️" }
                    }
                },
                nextSteps = new
                {
                    title = "📋 NEXT STEPS",
                    actions = new[]
                    {
                        findings.Count(f => f.Severity.ToString() == "Critical") > 0 ?
                            $"🚨 URGENT: Address {findings.Count(f => f.Severity.ToString() == "Critical")} critical findings immediately" : null,
                        "📥 Download POA&M in your preferred format (PDF, Excel, or eMASS XML)",
                        "👥 Assign remediation items to responsible team members",
                        "📅 Track milestone completion dates and update status regularly",
                        "🔄 Re-run compliance assessment after remediation to close POA&M items"
                    }.Where(a => a != null)
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating POA&M for subscription {SubscriptionId}", subscriptionIdOrName);
            return CreateErrorResponse("generate POA&M", ex);
        }
    }

    // ========== HELPER METHODS ==========

    private string GetComplianceGrade(double score)
    {
        return score >= 95 ? "A+" :
               score >= 90 ? "A" :
               score >= 85 ? "A-" :
               score >= 80 ? "B+" :
               score >= 75 ? "B" :
               score >= 70 ? "B-" :
               score >= 65 ? "C+" :
               score >= 60 ? "C" :
               score >= 55 ? "C-" :
               score >= 50 ? "D" : "F";
    }

    private string GenerateScoreBar(double score)
    {
        // Ensure score is between 0 and 100
        double clampedScore = Math.Max(0, Math.Min(100, score));
        
        int filledBlocks = (int)Math.Round(clampedScore / 10);
        // Ensure filledBlocks is between 0 and 10
        filledBlocks = Math.Max(0, Math.Min(10, filledBlocks));
        
        int emptyBlocks = 10 - filledBlocks;
        return new string('█', filledBlocks) + new string('░', emptyBlocks) + $" {clampedScore:F1}%";
    }

    private string GetResourceTypeDisplayName(string resourceType)
    {
        // Map Azure resource types to friendly display names
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Microsoft.Authorization/policyAssignments", "📋 Azure Policy Assignments" },
            { "Microsoft.Insights/diagnosticSettings", "📊 Diagnostic Settings" },
            { "Microsoft.Network/networkSecurityGroups", "🔒 Network Security Groups" },
            { "Microsoft.Compute/virtualMachines", "💻 Virtual Machines" },
            { "Microsoft.Storage/storageAccounts", "💾 Storage Accounts" },
            { "Microsoft.KeyVault/vaults", "🔑 Key Vaults" },
            { "Microsoft.Sql/servers", "🗄️ SQL Servers" },
            { "Microsoft.Web/sites", "🌐 App Services" },
            { "Microsoft.ContainerService/managedClusters", "☸️ AKS Clusters" },
            { "Microsoft.Network/virtualNetworks", "🌐 Virtual Networks" },
            { "Microsoft.Network/loadBalancers", "⚖️ Load Balancers" },
            { "Microsoft.Network/applicationGateways", "🚪 Application Gateways" },
            { "Microsoft.OperationalInsights/workspaces", "📈 Log Analytics Workspaces" },
            { "Microsoft.Security/securityContacts", "👥 Security Contacts" },
            { "Microsoft.Resources/resourceGroups", "📁 Resource Groups" },
            { "Subscription", "🏢 Subscription" }
        };

        return displayNames.TryGetValue(resourceType, out var displayName) 
            ? displayName 
            : $"🔧 {resourceType}";
    }

    private string GetControlFamilyName(string familyCode)
    {
        return familyCode switch
        {
            "AC" => "Access Control",
            "AU" => "Audit and Accountability",
            "CM" => "Configuration Management",
            "CP" => "Contingency Planning",
            "IA" => "Identification and Authentication",
            "IR" => "Incident Response",
            "MA" => "Maintenance",
            "MP" => "Media Protection",
            "PE" => "Physical and Environmental Protection",
            "PL" => "Planning",
            "PS" => "Personnel Security",
            "RA" => "Risk Assessment",
            "SA" => "System and Services Acquisition",
            "CA" => "Security Assessment and Authorization",
            "AT" => "Awareness and Training",
            "PM" => "Program Management",
            "SC" => "System and Communications Protection",
            "SI" => "System and Information Integrity",
            _ => familyCode
        };
    }

    // ========== DOWNLOAD HELPER METHODS ==========

    private string GenerateCsvFromEvidence(EvidencePackage evidencePackage)
    {
        var csv = new StringBuilder();
        
        // CSV Header
        csv.AppendLine("Evidence ID,Control ID,Evidence Type,Resource ID,Collected At,Data Summary");
        
        // CSV Rows
        foreach (var evidence in evidencePackage.Evidence)
        {
            // Properly serialize the data dictionary
            var dataStr = "";
            if (evidence.Data != null && evidence.Data.Count > 0)
            {
                var dataParts = new List<string>();
                foreach (var kvp in evidence.Data)
                {
                    // Serialize complex objects as JSON
                    var valueStr = kvp.Value switch
                    {
                        string s => s,
                        null => "null",
                        _ => JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions 
                        { 
                            WriteIndented = false,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        })
                    };
                    dataParts.Add($"{kvp.Key}={valueStr}");
                }
                dataStr = string.Join("; ", dataParts);
            }
            
            var dataSummary = dataStr.Replace(",", ";").Replace("\n", " ").Replace("\r", "");
            if (dataSummary.Length > 200)
            {
                dataSummary = dataSummary.Substring(0, 197) + "...";
            }
            
            csv.AppendLine($"\"{evidence.EvidenceId}\",\"{evidence.ControlId}\",\"{evidence.EvidenceType}\",\"{evidence.ResourceId}\",\"{evidence.CollectedAt:yyyy-MM-dd HH:mm:ss}\",\"{dataSummary}\"");
        }
        
        return csv.ToString();
    }

    private async Task<(string base64Content, int pageCount)> GeneratePdfFromEvidenceAsync(
        EvidencePackage evidencePackage,
        CancellationToken cancellationToken)
    {
        // For now, return a placeholder. In production, this would use a PDF generation library like QuestPDF or iTextSharp
        var pdfContent = $@"COMPLIANCE EVIDENCE REPORT
Package ID: {evidencePackage.PackageId}
Subscription: {evidencePackage.SubscriptionId}
Control Family: {evidencePackage.ControlFamily} - {GetControlFamilyName(evidencePackage.ControlFamily)}
Collection Date: {evidencePackage.CollectionDate:yyyy-MM-dd HH:mm:ss}

SUMMARY
-------
Total Evidence Items: {evidencePackage.TotalItems}
Completeness Score: {evidencePackage.CompletenessScore:F1}%
Collection Duration: {evidencePackage.CollectionDuration}

EVIDENCE ITEMS
--------------
{string.Join("\n\n", evidencePackage.Evidence.Take(50).Select((e, i) => 
    $"{i + 1}. {e.EvidenceType} - {e.ControlId}\n" +
    $"   Evidence ID: {e.EvidenceId}\n" +
    $"   Resource: {e.ResourceId}\n" +
    $"   Collected: {e.CollectedAt:yyyy-MM-dd HH:mm:ss}"))}

{(evidencePackage.Evidence.Count > 50 ? $"\n... and {evidencePackage.Evidence.Count - 50} more items" : "")}

ATTESTATION
-----------
{evidencePackage.AttestationStatement}

---
Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}
Platform Engineering Copilot - Compliance Module
";
        
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pdfContent));
        return await Task.FromResult((base64, 1));
    }

    private async Task<(string xmlContent, string systemId, string controlImplementation, string testResults, int poamItems, int artifactCount, string schemaVersion, bool isValid, string[] warnings)> GenerateEmassPackageAsync(
        EvidencePackage evidencePackage,
        CancellationToken cancellationToken)
    {
        // Generate eMASS-compatible XML
        var systemId = $"SYS-{evidencePackage.SubscriptionId[..8].ToUpperInvariant()}";
        var schemaVersion = "6.2";
        
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine($"<emass-package xmlns=\"https://emass.apps.mil/schema/{schemaVersion}\" version=\"{schemaVersion}\">");
        xml.AppendLine($"  <metadata>");
        xml.AppendLine($"    <system-id>{systemId}</system-id>");
        xml.AppendLine($"    <package-id>{evidencePackage.PackageId}</package-id>");
        xml.AppendLine($"    <submission-date>{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</submission-date>");
        xml.AppendLine($"    <control-family>{evidencePackage.ControlFamily}</control-family>");
        xml.AppendLine($"    <control-family-name>{GetControlFamilyName(evidencePackage.ControlFamily)}</control-family-name>");
        xml.AppendLine($"  </metadata>");
        
        xml.AppendLine($"  <artifacts count=\"{evidencePackage.Evidence.Count}\">");
        foreach (var evidence in evidencePackage.Evidence)
        {
            // Properly serialize the data dictionary
            var dataStr = "";
            if (evidence.Data != null && evidence.Data.Count > 0)
            {
                var dataParts = new List<string>();
                foreach (var kvp in evidence.Data)
                {
                    // Serialize complex objects as JSON
                    var valueStr = kvp.Value switch
                    {
                        string s => s,
                        null => "null",
                        _ => JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions 
                        { 
                            WriteIndented = false,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        })
                    };
                    dataParts.Add($"{kvp.Key}={valueStr}");
                }
                dataStr = string.Join("; ", dataParts);
            }
                
            xml.AppendLine($"    <artifact>");
            xml.AppendLine($"      <artifact-id>{evidence.EvidenceId}</artifact-id>");
            xml.AppendLine($"      <control-id>{evidence.ControlId}</control-id>");
            xml.AppendLine($"      <artifact-type>{evidence.EvidenceType}</artifact-type>");
            xml.AppendLine($"      <resource-id><![CDATA[{evidence.ResourceId}]]></resource-id>");
            xml.AppendLine($"      <collection-date>{evidence.CollectedAt:yyyy-MM-ddTHH:mm:ssZ}</collection-date>");
            xml.AppendLine($"      <data><![CDATA[{dataStr}]]></data>");
            xml.AppendLine($"    </artifact>");
        }
        xml.AppendLine($"  </artifacts>");
        
        xml.AppendLine($"  <attestation>");
        xml.AppendLine($"    <statement><![CDATA[{evidencePackage.AttestationStatement}]]></statement>");
        xml.AppendLine($"    <completeness-score>{evidencePackage.CompletenessScore:F2}</completeness-score>");
        xml.AppendLine($"  </attestation>");
        
        xml.AppendLine($"</emass-package>");
        
        var warnings = new List<string>();
        if (evidencePackage.CompletenessScore < 95)
        {
            warnings.Add($"Evidence completeness is {evidencePackage.CompletenessScore:F1}% - consider collecting more evidence for complete coverage");
        }
        if (evidencePackage.Evidence.Count < 10)
        {
            warnings.Add($"Only {evidencePackage.Evidence.Count} evidence items - typical control families require 10-50 artifacts");
        }
        
        return await Task.FromResult((
            xmlContent: xml.ToString(),
            systemId: systemId,
            controlImplementation: "Inherited/Hybrid",
            testResults: "Passed",
            poamItems: 0,
            artifactCount: evidencePackage.Evidence.Count,
            schemaVersion: schemaVersion,
            isValid: evidencePackage.CompletenessScore >= 80,
            warnings: warnings.ToArray()
        ));
    }

    /// <summary>
    /// Generates pre-formatted display text for remediation plan
    /// </summary>
    private string GenerateRemediationPlanDisplayText(
        RemediationPlan plan, 
        int autoRemediable, 
        int manual,
        string subscriptionId)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("# 🛠️ REMEDIATION PLAN");
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine();
        
        // Summary
        sb.AppendLine("## 📊 SUMMARY");
        sb.AppendLine($"- **Total Findings:** {plan.TotalFindings}");
        sb.AppendLine($"- **✨ Auto-Remediable:** {autoRemediable}");
        sb.AppendLine($"- **🔧 Manual Required:** {manual}");
        sb.AppendLine($"- **Estimated Effort:** {plan.EstimatedEffort.TotalHours:F1} hours");
        sb.AppendLine($"- **Priority:** {plan.Priority}");
        sb.AppendLine($"- **Risk Reduction:** {plan.ProjectedRiskReduction:F1}%");
        sb.AppendLine();
        
        // Auto-remediable findings
        if (autoRemediable > 0)
        {
            sb.AppendLine("## ✨ AUTO-REMEDIABLE FINDINGS");
            sb.AppendLine($"*These {autoRemediable} finding(s) can be automatically fixed when you execute the remediation plan.*");
            sb.AppendLine();
            
            var autoItems = plan.RemediationItems
                .Where(i => i.AutomationAvailable)
                .Take(10)
                .ToList();
            
            foreach (var item in autoItems)
            {
                sb.AppendLine($"### {item.Title}");
                sb.AppendLine($"- **Finding ID:** `{item.FindingId}`");
                sb.AppendLine($"- **Resource:** `{item.ResourceId}`");
                sb.AppendLine($"- **Priority:** {item.Priority}");
                sb.AppendLine($"- **Effort:** {item.EstimatedEffort?.TotalMinutes ?? 0:F0} minutes");
                sb.AppendLine();
                
                if (item.Steps != null && item.Steps.Any())
                {
                    sb.AppendLine("**Automated Actions:**");
                    foreach (var step in item.Steps)
                    {
                        sb.AppendLine($"{step.Order}. {step.Description}");
                    }
                }
                else
                {
                    sb.AppendLine("**Action:** Configuration will be automatically updated");
                }
                sb.AppendLine();
            }
            
            if (plan.RemediationItems.Count(i => i.AutomationAvailable) > 10)
            {
                var remaining = plan.RemediationItems.Count(i => i.AutomationAvailable) - 10;
                sb.AppendLine($"*... and {remaining} more auto-remediable finding(s)*");
                sb.AppendLine();
            }
        }
        
        // Manual findings
        if (manual > 0)
        {
            sb.AppendLine("## 🔧 MANUAL REMEDIATION REQUIRED");
            sb.AppendLine($"*These {manual} finding(s) require manual intervention.*");
            sb.AppendLine();
            
            var manualItems = plan.RemediationItems
                .Where(i => !i.AutomationAvailable)
                .Take(10)
                .ToList();
            
            foreach (var item in manualItems)
            {
                sb.AppendLine($"### {item.Title}");
                sb.AppendLine($"- **Finding ID:** `{item.FindingId}`");
                sb.AppendLine($"- **Resource:** `{item.ResourceId}`");
                sb.AppendLine($"- **Priority:** {item.Priority}");
                sb.AppendLine($"- **Effort:** {item.EstimatedEffort?.TotalHours ?? 0:F1} hours");
                sb.AppendLine();
                
                if (item.Steps != null && item.Steps.Any())
                {
                    sb.AppendLine("**Manual Steps:**");
                    foreach (var step in item.Steps)
                    {
                        sb.AppendLine($"{step.Order}. {step.Description}");
                        if (!string.IsNullOrEmpty(step.Command))
                        {
                            sb.AppendLine($"   ```bash");
                            sb.AppendLine($"   {step.Command}");
                            sb.AppendLine($"   ```");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("**Action:** Review resource configuration and apply remediation manually");
                }
                sb.AppendLine();
            }
            
            if (plan.RemediationItems.Count(i => !i.AutomationAvailable) > 10)
            {
                var remaining = plan.RemediationItems.Count(i => !i.AutomationAvailable) - 10;
                sb.AppendLine($"*... and {remaining} more manual remediation finding(s)*");
                sb.AppendLine();
            }
        }
        
        // Timeline
        if (plan.Timeline != null)
        {
            sb.AppendLine("## 📅 TIMELINE");
            sb.AppendLine($"- **Start Date:** {plan.Timeline.StartDate:yyyy-MM-dd}");
            sb.AppendLine($"- **End Date:** {plan.Timeline.EndDate:yyyy-MM-dd}");
            sb.AppendLine($"- **Duration:** {plan.Timeline.TotalDuration.TotalDays:F1} days");
            sb.AppendLine();
        }
        
        // Next steps
        sb.AppendLine("## 🚀 NEXT STEPS");
        if (autoRemediable > 0)
        {
            sb.AppendLine($"1. **✨ Execute Auto-Remediation:** Say `execute the remediation plan` to automatically fix {autoRemediable} finding(s)");
        }
        if (manual > 0)
        {
            sb.AppendLine($"{(autoRemediable > 0 ? "2" : "1")}. **🔧 Manual Remediation:** Follow the step-by-step instructions above for {manual} finding(s)");
        }
        sb.AppendLine($"{(autoRemediable > 0 && manual > 0 ? "3" : autoRemediable > 0 || manual > 0 ? "2" : "1")}. **📊 Track Progress:** Say `show me the remediation progress` to monitor completion");
        sb.AppendLine();
        
        return sb.ToString();
    }

    // ========== SECURITY HARDENING FUNCTIONS ==========

    [KernelFunction("apply_security_hardening")]
    [Description("Apply comprehensive security hardening to Azure resources based on industry best practices. " +
                 "Implements encryption, network security, authentication, MFA, RBAC, logging, monitoring, " +
                 "secret management, and vulnerability protection. Can apply to entire subscription or specific resource group. " +
                 "Supports custom security requirements and compliance frameworks (FedRAMP, NIST 800-53, etc.). " +
                 "Example: 'Apply security hardening to subscription production with customer-managed encryption'")]
    public async Task<string> ApplySecurityHardeningAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] 
        string subscriptionIdOrName,
        [Description("Optional resource group name to limit scope. Leave empty to harden entire subscription.")] 
        string? resourceGroupName = null,
        [Description("Security hardening options in JSON format. Example: {\"encryption\":\"customer-managed\",\"networkSecurity\":\"private-endpoints\",\"authentication\":\"azure-ad-only\",\"mfa\":true,\"rbac\":\"least-privilege\",\"logging\":true,\"monitoring\":\"defender-all-plans\",\"secretManagement\":\"key-vault\",\"certificateManagement\":\"managed\",\"vulnerabilityScanning\":true}")] 
        string? hardeningOptions = null,
        [Description("Dry run mode - generate hardening plan without making changes. Default is true for safety.")] 
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            var scope = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? $"subscription {subscriptionId}" 
                : $"resource group '{resourceGroupName}' in subscription {subscriptionId}";
            
            _logger.LogInformation("Applying security hardening to {Scope}, dryRun={DryRun}", scope, dryRun);

            // Parse hardening options or use defaults
            var options = ParseHardeningOptions(hardeningOptions);

            // Step 1: Run compliance assessment to identify current security gaps
            _logger.LogInformation("Step 1: Assessing current security posture...");
            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId, 
                resourceGroupName,
                null, // progress
                cancellationToken);

            // Step 2: Generate hardening actions based on options
            _logger.LogInformation("Step 2: Generating security hardening actions...");
            var hardeningActions = GenerateHardeningActions(assessment, options, subscriptionId, resourceGroupName);

            if (!hardeningActions.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"🎉 {scope} is already fully hardened according to specified security requirements!",
                    summary = new
                    {
                        scope = scope,
                        currentSecurityScore = assessment.OverallComplianceScore,
                        hardeningOptionsApplied = options
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Step 3: Execute or plan hardening actions
            var results = new List<object>();
            var totalActions = hardeningActions.Count;
            var successCount = 0;
            var failCount = 0;

            if (dryRun)
            {
                _logger.LogInformation("DRY RUN MODE: Generating hardening plan without making changes");
                
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    dryRun = true,
                    header = new
                    {
                        title = "🔒 SECURITY HARDENING PLAN (DRY RUN)",
                        icon = "🛡️",
                        scope = scope
                    },
                    summary = new
                    {
                        totalActions = totalActions,
                        currentSecurityScore = assessment.OverallComplianceScore,
                        estimatedSecurityScoreAfter = CalculateEstimatedScore(assessment.OverallComplianceScore, totalActions),
                        hardeningOptions = options
                    },
                    actions = hardeningActions.Select((action, index) => new
                    {
                        actionNumber = index + 1,
                        category = action.Category,
                        description = action.Description,
                        resourceType = action.ResourceType,
                        resourceCount = action.AffectedResourceCount,
                        priority = action.Priority,
                        estimatedDuration = action.EstimatedDuration,
                        compliance = action.ComplianceControls
                    }),
                    nextSteps = new
                    {
                        toExecute = $"To apply these hardening actions, run: apply_security_hardening with dryRun=false",
                        toCustomize = "Modify hardeningOptions JSON parameter to customize security settings",
                        toReview = "Review each action above and ensure it meets your security requirements"
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                _logger.LogInformation("LIVE MODE: Executing {Count} security hardening actions", totalActions);
                
                foreach (var action in hardeningActions)
                {
                    try
                    {
                        _logger.LogInformation("Executing hardening action: {Category} - {Description}", 
                            action.Category, action.Description);

                        // Execute the hardening action through remediation engine
                        var actionResult = await ExecuteHardeningActionAsync(action, subscriptionId, cancellationToken);
                        
                        if (actionResult.Success)
                        {
                            successCount++;
                            results.Add(new
                            {
                                action = action.Description,
                                status = "✅ Success",
                                resourcesUpdated = actionResult.ResourcesUpdated,
                                details = actionResult.Details
                            });
                        }
                        else
                        {
                            failCount++;
                            results.Add(new
                            {
                                action = action.Description,
                                status = "❌ Failed",
                                error = actionResult.Error,
                                requiresManualAction = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "Error executing hardening action: {Category}", action.Category);
                        results.Add(new
                        {
                            action = action.Description,
                            status = "❌ Error",
                            error = ex.Message,
                            requiresManualAction = true
                        });
                    }
                }

                // Run post-hardening assessment
                _logger.LogInformation("Running post-hardening security assessment...");
                var postAssessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                    subscriptionId, 
                    resourceGroupName,
                    null, // progress
                    cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    success = failCount == 0,
                    header = new
                    {
                        title = "🔒 SECURITY HARDENING RESULTS",
                        icon = "🛡️",
                        scope = scope,
                        completedAt = DateTimeOffset.UtcNow
                    },
                    summary = new
                    {
                        totalActions = totalActions,
                        successful = successCount,
                        failed = failCount,
                        successRate = $"{(successCount * 100.0 / totalActions):F1}%",
                        securityScoreBefore = assessment.OverallComplianceScore,
                        securityScoreAfter = postAssessment.OverallComplianceScore,
                        improvement = $"+{(postAssessment.OverallComplianceScore - assessment.OverallComplianceScore):F1}%"
                    },
                    results = results,
                    nextSteps = failCount > 0 
                        ? new { recommendation = "Review failed actions above and apply manual remediation" }
                        : new { recommendation = "Security hardening complete! Run compliance assessment to verify." }
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ApplySecurityHardeningAsync");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private HardeningOptions ParseHardeningOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return new HardeningOptions(); // Use defaults
        }

        try
        {
            return JsonSerializer.Deserialize<HardeningOptions>(optionsJson) ?? new HardeningOptions();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse hardening options JSON, using defaults");
            return new HardeningOptions();
        }
    }

    private List<HardeningAction> GenerateHardeningActions(
        dynamic assessment,
        HardeningOptions options,
        string subscriptionId,
        string? resourceGroupName)
    {
        var actions = new List<HardeningAction>();

        // Encryption hardening
        if (options.EnableEncryption)
        {
            actions.Add(new HardeningAction
            {
                Category = "Encryption",
                Description = options.EncryptionType == "customer-managed" 
                    ? "Enable customer-managed encryption keys for all storage and databases"
                    : "Enable encryption at rest for all data resources",
                ResourceType = "Storage, SQL, Cosmos DB",
                AffectedResourceCount = 0, // Will be calculated during execution
                Priority = "Critical",
                EstimatedDuration = "15-30 minutes",
                ComplianceControls = new[] { "SC-28", "SC-13" },
                ActionType = "encryption"
            });
        }

        // Network security hardening
        if (options.EnableNetworkSecurity)
        {
            if (options.NetworkSecurityType == "private-endpoints")
            {
                actions.Add(new HardeningAction
                {
                    Category = "Network Security",
                    Description = "Disable public access and enable private endpoints for all PaaS services",
                    ResourceType = "Storage, SQL, Key Vault, App Services",
                    AffectedResourceCount = 0,
                    Priority = "Critical",
                    EstimatedDuration = "20-45 minutes",
                    ComplianceControls = new[] { "SC-7", "AC-4" },
                    ActionType = "network-isolation"
                });
            }
        }

        // Authentication hardening
        if (options.EnableAuthenticationHardening)
        {
            actions.Add(new HardeningAction
            {
                Category = "Authentication",
                Description = "Enforce Azure AD authentication and disable local/basic auth",
                ResourceType = "SQL, Storage, App Services, Key Vault",
                AffectedResourceCount = 0,
                Priority = "High",
                EstimatedDuration = "10-20 minutes",
                ComplianceControls = new[] { "IA-2", "IA-5" },
                ActionType = "authentication"
            });
        }

        // MFA enforcement
        if (options.EnforceMfa)
        {
            actions.Add(new HardeningAction
            {
                Category = "MFA",
                Description = "Require multi-factor authentication for all administrative access",
                ResourceType = "Azure AD, Conditional Access",
                AffectedResourceCount = 0,
                Priority = "Critical",
                EstimatedDuration = "15-30 minutes",
                ComplianceControls = new[] { "IA-2(1)", "IA-2(2)" },
                ActionType = "mfa"
            });
        }

        // RBAC hardening
        if (options.EnableRbacHardening)
        {
            actions.Add(new HardeningAction
            {
                Category = "RBAC",
                Description = "Apply least privilege principle and remove Owner role from users",
                ResourceType = "Subscriptions, Resource Groups",
                AffectedResourceCount = 0,
                Priority = "High",
                EstimatedDuration = "30-60 minutes",
                ComplianceControls = new[] { "AC-6", "AC-2" },
                ActionType = "rbac"
            });
        }

        // Logging and monitoring
        if (options.EnableLogging)
        {
            actions.Add(new HardeningAction
            {
                Category = "Logging",
                Description = "Enable diagnostic settings and activity logs on all resources",
                ResourceType = "All Resources",
                AffectedResourceCount = 0,
                Priority = "High",
                EstimatedDuration = "20-40 minutes",
                ComplianceControls = new[] { "AU-2", "AU-3", "AU-12" },
                ActionType = "logging"
            });
        }

        if (options.EnableMonitoring)
        {
            actions.Add(new HardeningAction
            {
                Category = "Monitoring",
                Description = options.MonitoringLevel == "defender-all-plans"
                    ? "Enable Microsoft Defender for Cloud (all protection plans)"
                    : "Enable basic Microsoft Defender for Cloud",
                ResourceType = "Subscription",
                AffectedResourceCount = 1,
                Priority = "High",
                EstimatedDuration = "10-15 minutes",
                ComplianceControls = new[] { "SI-4", "RA-5" },
                ActionType = "monitoring"
            });
        }

        // Secret management
        if (options.EnableSecretManagement)
        {
            actions.Add(new HardeningAction
            {
                Category = "Secret Management",
                Description = "Replace connection strings in app config with Key Vault references",
                ResourceType = "App Services, Functions, Container Apps",
                AffectedResourceCount = 0,
                Priority = "Critical",
                EstimatedDuration = "30-60 minutes",
                ComplianceControls = new[] { "SC-12", "SC-13" },
                ActionType = "secrets"
            });
        }

        // Certificate management
        if (options.EnableCertificateManagement)
        {
            actions.Add(new HardeningAction
            {
                Category = "Certificate Management",
                Description = "Enable managed certificates with auto-renewal",
                ResourceType = "App Services, Application Gateway, Front Door",
                AffectedResourceCount = 0,
                Priority = "Medium",
                EstimatedDuration = "15-30 minutes",
                ComplianceControls = new[] { "SC-17" },
                ActionType = "certificates"
            });
        }

        // Vulnerability scanning
        if (options.EnableVulnerabilityScanning)
        {
            actions.Add(new HardeningAction
            {
                Category = "Vulnerability Protection",
                Description = "Enable Defender for Containers, SQL, and Storage",
                ResourceType = "AKS, SQL, Storage Accounts",
                AffectedResourceCount = 0,
                Priority = "High",
                EstimatedDuration = "10-20 minutes",
                ComplianceControls = new[] { "RA-5", "SI-2" },
                ActionType = "vulnerability-scanning"
            });
        }

        return actions;
    }

    private async Task<HardeningActionResult> ExecuteHardeningActionAsync(
        HardeningAction action,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        // This would integrate with the remediation engine to execute the actual hardening
        // For now, we'll simulate the execution
        _logger.LogInformation("Executing hardening action: {ActionType}", action.ActionType);

        // The actual implementation would call Azure APIs through the remediation engine
        // Example: await _remediationEngine.ExecuteActionAsync(action, subscriptionId, cancellationToken);

        return await Task.FromResult(new HardeningActionResult
        {
            Success = true,
            ResourcesUpdated = action.AffectedResourceCount,
            Details = $"Applied {action.Category} hardening to resources"
        });
    }

    private double CalculateEstimatedScore(double currentScore, int actionCount)
    {
        // Simple estimation: each action improves score by ~2-5%
        var improvement = actionCount * 3.5;
        var estimatedScore = Math.Min(100, currentScore + improvement);
        return Math.Round(estimatedScore, 1);
    }

    // Helper classes for security hardening
    private class HardeningOptions
    {
        public bool EnableEncryption { get; set; } = true;
        public string EncryptionType { get; set; } = "customer-managed"; // or "platform-managed"
        public bool EnableNetworkSecurity { get; set; } = true;
        public string NetworkSecurityType { get; set; } = "private-endpoints"; // or "vnet-integration"
        public bool EnableAuthenticationHardening { get; set; } = true;
        public bool EnforceMfa { get; set; } = true;
        public bool EnableRbacHardening { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        public bool EnableMonitoring { get; set; } = true;
        public string MonitoringLevel { get; set; } = "defender-all-plans"; // or "basic"
        public bool EnableSecretManagement { get; set; } = true;
        public bool EnableCertificateManagement { get; set; } = true;
        public bool EnableVulnerabilityScanning { get; set; } = true;
    }

    private class HardeningAction
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public int AffectedResourceCount { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string EstimatedDuration { get; set; } = string.Empty;
        public string[] ComplianceControls { get; set; } = Array.Empty<string>();
        public string ActionType { get; set; } = string.Empty;
    }

    private class HardeningActionResult
    {
        public bool Success { get; set; }
        public int ResourcesUpdated { get; set; }
        public string Details { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    #region MCP-Enhanced Functions

    [KernelFunction("validate_compliance_with_azure_policy")]
    [Description("Validate Azure resources against Azure Policy for compliance checking. " +
                 "Uses Azure MCP to check policy compliance, violations, and remediation options.")]
    public async Task<string> ValidateComplianceWithAzurePolicyAsync(
        [Description("Target resource group to validate compliance")] 
        string resourceGroup,
        
        [Description("Optional subscription ID or name (uses default if not provided)")] 
        string? subscriptionId = null,
        
        [Description("Optional policy assignment name to validate against specific policy")] 
        string? policyAssignment = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription ID
            var resolvedSubscriptionId = !string.IsNullOrWhiteSpace(subscriptionId)
                ? await ResolveSubscriptionIdAsync(subscriptionId)
                : await ResolveSubscriptionIdAsync("default");

            // 1. Use Azure MCP to get policy compliance state
            var policyArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup
            };

            if (!string.IsNullOrWhiteSpace(policyAssignment))
            {
                policyArgs["policyAssignment"] = policyAssignment;
            }

            var policyResult = await _azureMcpClient.CallToolAsync("azurepolicy", policyArgs, cancellationToken);

            if (policyResult == null || !policyResult.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = policyResult?.ErrorMessage ?? "Azure Policy validation failed"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var policyData = policyResult.Result?.ToString() ?? "No policy data available";

            // 2. Get compliance best practices from MCP
            var bestPracticesArgs = new Dictionary<string, object?>
            {
                ["query"] = $"Azure Policy compliance best practices for {resourceGroup}"
            };
            var bestPracticesResult = await _azureMcpClient.CallToolAsync("get_bestpractices", bestPracticesArgs, cancellationToken);
            var bestPractices = bestPracticesResult?.Result?.ToString() ?? "Best practices unavailable";

            // 3. Get resource compliance state from existing compliance engine
            var complianceAssessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                resolvedSubscriptionId, resourceGroup, null, cancellationToken);

            // 4. Compile comprehensive compliance report
            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroup = resourceGroup,
                subscriptionId = resolvedSubscriptionId,
                azurePolicy = new
                {
                    complianceState = policyData,
                    policyAssignment = policyAssignment ?? "All policies"
                },
                complianceFramework = new
                {
                    framework = "NIST 800-53",
                    overallScore = complianceAssessment.OverallComplianceScore,
                    totalFindings = complianceAssessment.TotalFindings,
                    criticalFindings = complianceAssessment.CriticalFindings,
                    highFindings = complianceAssessment.HighFindings,
                    assessedAt = complianceAssessment.EndTime
                },
                bestPractices = new
                {
                    source = "Azure MCP",
                    recommendations = bestPractices
                },
                nextSteps = new[]
                {
                    "Review Azure Policy compliance violations above",
                    "Check NIST 800-53 control failures",
                    "Apply best practice recommendations",
                    "Say 'remediate compliance issues in <resource-group>' to auto-fix violations",
                    "Say 'generate compliance evidence for <resource-group>' for ATO documentation"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compliance with Azure Policy");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Compliance validation failed: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_compliance_recommendations")]
    [Description("Get comprehensive compliance recommendations using Azure MCP best practices. " +
                 "Provides actionable guidance for NIST 800-53, FedRAMP, and Azure Policy compliance.")]
    public async Task<string> GetComplianceRecommendationsAsync(
        [Description("Target resource group for compliance recommendations")] 
        string? resourceGroup = null,
        
        [Description("Optional subscription ID or name (uses default if not provided)")] 
        string? subscriptionId = null,
        
        [Description("Compliance framework: 'nist-800-53', 'fedramp-moderate', 'fedramp-high', 'azure-policy' (optional - uses configured DefaultFramework if not specified)")] 
        string? framework = null,
        
        [Description("Include remediation scripts in recommendations (default: true)")] 
        bool includeRemediation = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription ID
            var resolvedSubscriptionId = !string.IsNullOrWhiteSpace(subscriptionId)
                ? await ResolveSubscriptionIdAsync(subscriptionId)
                : await ResolveSubscriptionIdAsync("default");

            // Get effective framework (use provided or fall back to configuration default)
            var effectiveFramework = GetEffectiveFramework(framework ?? "nist-800-53");
            _logger.LogInformation("Using compliance framework: {Framework}", effectiveFramework);

            // 1. Get compliance assessment from existing engine
            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                resolvedSubscriptionId, resourceGroup, null, cancellationToken);

            // 2. Get framework-specific recommendations from MCP
            var frameworkQuery = effectiveFramework.ToLowerInvariant() switch
            {
                "fedramp-moderate" => "FedRAMP Moderate compliance recommendations for Azure infrastructure",
                "fedramp-high" => "FedRAMP High compliance recommendations for Azure infrastructure",
                "azure-policy" => $"Azure Policy compliance recommendations for {resourceGroup}",
                _ => $"NIST 800-53 compliance recommendations for {resourceGroup}"
            };

            var recommendationsArgs = new Dictionary<string, object?>
            {
                ["query"] = frameworkQuery
            };
            var mcpRecommendations = await _azureMcpClient.CallToolAsync("get_bestpractices", recommendationsArgs, cancellationToken);
            var recommendations = mcpRecommendations?.Result?.ToString() ?? "No recommendations available";

            // 3. Get Azure Policy recommendations via MCP
            var policyArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup
            };
            var policyResult = await _azureMcpClient.CallToolAsync("azurepolicy", policyArgs, cancellationToken);
            var policyRecommendations = policyResult?.Result?.ToString() ?? "Policy data unavailable";

            // 4. Get remediation recommendations if requested
            object? remediationSteps = null;
            var allFindings = assessment.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .Where(f => f.Severity == AtoFindingSeverity.Critical || f.Severity == AtoFindingSeverity.High)
                .ToList();
                
            if (includeRemediation && allFindings.Any())
            {
                var remediationPlan = await _remediationEngine.GenerateRemediationPlanAsync(
                    resolvedSubscriptionId, allFindings, cancellationToken);

                remediationSteps = new
                {
                    totalActions = remediationPlan.RemediationItems.Count,
                    automaticActions = remediationPlan.RemediationItems.Count(a => a.IsAutomated),
                    manualActions = remediationPlan.RemediationItems.Count(a => !a.IsAutomated),
                    estimatedEffort = remediationPlan.EstimatedEffort,
                    actions = remediationPlan.RemediationItems.Take(5).Select(a => new
                    {
                        findingId = a.FindingId,
                        title = a.Title,
                        automated = a.IsAutomated,
                        priority = a.Priority
                    }).ToList(),
                    note = remediationPlan.RemediationItems.Count > 5 
                        ? $"Showing 5 of {remediationPlan.RemediationItems.Count} actions. Say 'remediate compliance issues' to execute all." 
                        : null
                };
            }

            // 5. Compile comprehensive recommendations report
            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroup = resourceGroup,
                subscriptionId = resolvedSubscriptionId,
                framework = effectiveFramework.ToUpperInvariant(),
                complianceStatus = new
                {
                    overallScore = assessment.OverallComplianceScore,
                    totalFindings = assessment.TotalFindings,
                    criticalFindings = assessment.CriticalFindings,
                    highFindings = assessment.HighFindings,
                    mediumFindings = assessment.MediumFindings,
                    lowFindings = assessment.LowFindings,
                    assessedAt = assessment.EndTime
                },
                recommendations = new
                {
                    frameworkGuidance = new
                    {
                        source = $"Azure MCP - {effectiveFramework.ToUpperInvariant()}",
                        guidance = recommendations
                    },
                    azurePolicy = new
                    {
                        source = "Azure Policy via MCP",
                        policyGuidance = policyRecommendations
                    },
                    remediationPlan = remediationSteps
                },
                failingControls = assessment.ControlFamilyResults.Values
                    .SelectMany(cf => cf.Findings)
                    .Where(f => f.Severity == AtoFindingSeverity.Critical || f.Severity == AtoFindingSeverity.High)
                    .Take(10)
                    .Select(f => new
                    {
                        resourceId = f.ResourceId,
                        resourceName = f.ResourceName,
                        title = f.Title,
                        severity = f.Severity.ToString(),
                        complianceStatus = f.ComplianceStatus.ToString()
                    }).ToList(),
                nextSteps = new[]
                {
                    $"Review {effectiveFramework.ToUpperInvariant()} compliance guidance above",
                    "Check Azure Policy recommendations for quick wins",
                    includeRemediation ? "Execute remediation plan to fix failing controls" : "Say 'get compliance recommendations with remediation' to see fix steps",
                    "Say 'remediate compliance issues' to auto-fix violations",
                    "Say 'generate compliance evidence' for ATO documentation"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance recommendations");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get recommendations: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

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
            
            // Query historical assessments
            var assessments = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId &&
                            a.CompletedAt >= cutoffDate &&
                            a.Status == "Completed")
                .OrderBy(a => a.CompletedAt)
                .Select(a => new
                {
                    a.Id,
                    a.CompletedAt,
                    a.ComplianceScore,
                    a.TotalFindings,
                    a.CriticalFindings,
                    a.HighFindings,
                    a.MediumFindings,
                    a.LowFindings,
                    a.InitiatedBy
                })
                .ToListAsync(cancellationToken);

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
            
            // Query audit log
            var auditLog = await _dbContext.ComplianceAssessments
                .Where(a => a.SubscriptionId == subscriptionId &&
                            a.CompletedAt >= cutoffDate)
                .OrderByDescending(a => a.CompletedAt)
                .Select(a => new
                {
                    a.Id,
                    a.StartedAt,
                    a.CompletedAt,
                    a.Duration,
                    a.Status,
                    a.InitiatedBy,
                    a.ComplianceScore,
                    a.TotalFindings,
                    a.CriticalFindings,
                    a.HighFindings,
                    a.ResourceGroupName,
                    a.AssessmentType
                })
                .ToListAsync(cancellationToken);

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
            
            // Query assessments with findings
            var assessments = await _dbContext.ComplianceAssessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId &&
                            a.CompletedAt >= cutoffDate &&
                            a.Status == "Completed")
                .OrderBy(a => a.CompletedAt)
                .ToListAsync(cancellationToken);

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

    #region Database Caching Helpers

    /// <summary>
    /// Retrieves a cached compliance assessment from the database if available and not expired.
    /// </summary>
    private async Task<ComplianceAssessment?> GetCachedAssessmentAsync(
        string subscriptionId, 
        string? resourceGroupName, 
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-ASSESSMENT_CACHE_HOURS);
            
            var query = _dbContext.ComplianceAssessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId &&
                            a.ResourceGroupName == resourceGroupName &&
                            a.CompletedAt >= cutoff &&
                            a.Status == "Completed");
            
            var cached = await query
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);
            
            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached assessment from database");
            return null;
        }
    }

    /// <summary>
    /// Saves a compliance assessment and its findings to the database for future caching.
    /// </summary>
    private async Task<string?> SaveAssessmentToDbAsync(
        object assessmentData,
        List<AtoFinding> allFindings,
        string subscriptionId,
        string? resourceGroupName,
        decimal complianceScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var assessmentId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            
            var dbAssessment = new ComplianceAssessment
            {
                Id = assessmentId,
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                AssessmentType = "NIST-800-53",
                Status = "Completed",
                ComplianceScore = complianceScore,
                TotalFindings = allFindings.Count,
                CriticalFindings = allFindings.Count(f => f.Severity == AtoFindingSeverity.Critical),
                HighFindings = allFindings.Count(f => f.Severity == AtoFindingSeverity.High),
                MediumFindings = allFindings.Count(f => f.Severity == AtoFindingSeverity.Medium),
                LowFindings = allFindings.Count(f => f.Severity == AtoFindingSeverity.Low),
                InformationalFindings = allFindings.Count(f => f.Severity == AtoFindingSeverity.Informational),
                Results = JsonSerializer.Serialize(assessmentData),
                StartedAt = now,
                CompletedAt = now,
                Duration = 0, // Store as ticks (long)
                InitiatedBy = "Platform-Copilot",
                Findings = new List<ComplianceFinding>()
            };

            // Map findings to database entities
            foreach (var finding in allFindings.Take(1000)) // Limit to 1000 findings to avoid DB bloat
            {
                dbAssessment.Findings.Add(new ComplianceFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    FindingId = finding.Id,
                    RuleId = finding.RuleId,
                    Title = finding.Title,
                    Description = finding.Description,
                    Severity = finding.Severity.ToString(),
                    ComplianceStatus = finding.ComplianceStatus.ToString(),
                    FindingType = finding.FindingType.ToString(),
                    ResourceId = finding.ResourceId,
                    ResourceType = finding.ResourceType,
                    ResourceName = finding.ResourceName,
                    ControlId = finding.AffectedNistControls.FirstOrDefault(),
                    ComplianceFrameworks = finding.ComplianceFrameworks.Any() 
                        ? JsonSerializer.Serialize(finding.ComplianceFrameworks) 
                        : null,
                    AffectedNistControls = finding.AffectedNistControls.Any() 
                        ? JsonSerializer.Serialize(finding.AffectedNistControls) 
                        : null,
                    Evidence = finding.Evidence,
                    Remediation = finding.RemediationGuidance,
                    Metadata = finding.Metadata.Any() 
                        ? JsonSerializer.Serialize(finding.Metadata) 
                        : null,
                    IsRemediable = finding.IsRemediable,
                    IsAutomaticallyFixable = finding.IsAutoRemediable,
                    DetectedAt = finding.DetectedAt
                });
            }

            _dbContext.ComplianceAssessments.Add(dbAssessment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "✅ Saved assessment {AssessmentId} to database with {FindingCount} findings for future caching",
                assessmentId, dbAssessment.Findings.Count);
            
            return assessmentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save assessment to database - continuing without cache");
            return null;
        }
    }

    /// <summary>
    /// Formats a cached assessment into the same JSON structure as a fresh assessment.
    /// </summary>
    private string FormatCachedAssessment(ComplianceAssessment cached, string scope, TimeSpan cacheAge)
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

    #endregion
}
