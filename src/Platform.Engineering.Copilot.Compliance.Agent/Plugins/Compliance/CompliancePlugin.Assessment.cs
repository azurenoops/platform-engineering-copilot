using System.Text;
using System.Text.Json;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Constants;
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
/// Partial class containing compliance assessment functions:
/// - run_compliance_assessment
/// - get_control_family_details
/// - get_compliance_status
/// - collect_evidence
/// </summary>
public partial class CompliancePlugin
{
    // ========== COMPLIANCE ASSESSMENT FUNCTIONS ==========

    [KernelFunction("run_compliance_assessment")]
    [Description("🔍 ACTIVE SECURITY SCANNING - Run a comprehensive NIST 800-53 compliance assessment to SCAN your Azure environment for security findings. " +
                 "⚠️ THIS IS AN ACTIVE SCAN that queries Azure resources - only use when user EXPLICITLY requests scanning. " +
                 "✅ USE ONLY when user says: 'run assessment', 'scan for compliance', 'check my compliance', 'assess my environment', 'find security issues', 'run a scan'. " +
                 "🚫 NEVER USE for questions like: 'what is AC-2?', 'explain control', 'define control', 'what does control require?', 'tell me about control' - these are KNOWLEDGE questions handled by KnowledgeBasePlugin.explain_nist_control. " +
                 "🚫 NEVER USE for: 'collect evidence', 'generate evidence', 'evidence package', 'ATO package', 'documentation'. " +
                 "🚫 If user question contains 'what is', 'explain', 'define', 'describe', 'tell me about' followed by a control ID - DO NOT CALL THIS FUNCTION. " +
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
            // Match both "/resourceGroups/{rg}/" (mid-path) and "/resourceGroups/{rg}" (end-of-path)
            if (!string.IsNullOrWhiteSpace(resourceGroupName))
            {
                var rgPatternWithSlash = $"/resourceGroups/{resourceGroupName}/";
                var rgPatternEndOfPath = $"/resourceGroups/{resourceGroupName}";
                
                allFindings = allFindings
                    .Where(f => f.ResourceId != null && (
                        f.ResourceId.Contains(rgPatternWithSlash, StringComparison.OrdinalIgnoreCase) ||
                        f.ResourceId.EndsWith(rgPatternEndOfPath, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                
                _logger.LogInformation("Filtered to {Count} findings in resource group {ResourceGroup}", 
                    allFindings.Count, resourceGroupName);
            }
            
            // Recalculate control family statistics based on filtered findings
            var filteredControlFamilyStats = assessment.ControlFamilyResults
                .ToDictionary(
                    cf => cf.Key,
                    cf => {
                        var rgPatternWithSlash = $"/resourceGroups/{resourceGroupName}/";
                        var rgPatternEndOfPath = $"/resourceGroups/{resourceGroupName}";
                        
                        var filteredFindings = string.IsNullOrWhiteSpace(resourceGroupName) 
                            ? cf.Value.Findings 
                            : cf.Value.Findings.Where(f => f.ResourceId != null && (
                                f.ResourceId.Contains(rgPatternWithSlash, StringComparison.OrdinalIgnoreCase) ||
                                f.ResourceId.EndsWith(rgPatternEndOfPath, StringComparison.OrdinalIgnoreCase))).ToList();
                        
                        // Calculate compliance score: if no findings, 100% compliant for this scope
                        var complianceScore = cf.Value.TotalControls > 0 
                            ? Math.Max(0, ((double)(cf.Value.TotalControls - filteredFindings.Select(f => f.AffectedNistControls).SelectMany(c => c).Distinct().Count()) / cf.Value.TotalControls) * 100)
                            : 100.0;
                            
                        return new {
                            FamilyName = cf.Value.FamilyName,
                            TotalControls = cf.Value.TotalControls,
                            Findings = filteredFindings,
                            ComplianceScore = complianceScore
                        };
                    });

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
            
            // Calculate overall compliance score from filtered results
            var filteredOverallScore = filteredControlFamilyStats.Values.Any() 
                ? filteredControlFamilyStats.Values.Average(cf => cf.ComplianceScore)
                : 100.0;
            var filteredTotalFindings = allFindings.Count;
            var filteredCriticalCount = criticalFindings.Count;
            var filteredHighCount = highFindings.Count;
            
            var formattedOutput = $@"
# 📊 NIST 800-53 COMPLIANCE ASSESSMENT

**Subscription:** `{assessment.SubscriptionId}`
{(string.IsNullOrEmpty(resourceGroupName) ? "" : $"**Resource Group:** `{resourceGroupName}`\n")}
**Compliance Score:** {GenerateScoreBar(filteredOverallScore)} **{Math.Round(filteredOverallScore, 1)}%** (Grade: **{GetComplianceGrade(filteredOverallScore)}**)  
**Status:** {(filteredOverallScore >= 90 ? "✅ Ready for ATO" : filteredOverallScore >= 70 ? "⚠️ Getting there" : "❌ Needs attention")}

---

## 📋 OVERVIEW

| Metric | Count |
|--------|-------|
| **Total Findings** | {filteredTotalFindings} |
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

{string.Join("\n", filteredControlFamilyStats
    .Where(cf => cf.Value.ComplianceScore < 90)
    .OrderBy(cf => cf.Value.ComplianceScore)
    .Take(10)
    .Select(cf => $"- {(cf.Value.ComplianceScore == 0 ? "❌" : "⚠️")} **{cf.Key} ({GetControlFamilyName(cf.Key)})**: {Math.Round(cf.Value.ComplianceScore, 1)}% {new string('█', (int)(cf.Value.ComplianceScore / 5))}{new string('░', 20 - (int)(cf.Value.ComplianceScore / 5))} - {cf.Value.Findings.Count} finding{(cf.Value.Findings.Count > 1 ? "s" : "")}"))}

---

{(filteredCriticalCount > 0 ? $"## 🔴 CRITICAL ALERT\n\n**{filteredCriticalCount} CRITICAL** issue{(filteredCriticalCount > 1 ? "s" : "")} need **immediate** attention!\n\n---\n\n" : "")}
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
                        bar = GenerateScoreBar(filteredOverallScore),
                        percentage = $"{Math.Round(filteredOverallScore, 1)}%",
                        grade = GetComplianceGrade(filteredOverallScore),
                        status = filteredOverallScore >= 90 ? "✅ Ready for ATO" :
                                filteredOverallScore >= 70 ? "⚠️ Getting there - some work needed" : 
                                "❌ Needs attention"
                    },
                    interpretation = filteredOverallScore >= 90 ? 
                        "🎉 Your environment is in great shape! Keep monitoring to maintain this level." :
                        filteredOverallScore >= 80 ? 
                        "👍 You're doing well. Focus on the remaining issues to get ATO-ready." :
                        filteredOverallScore >= 70 ? 
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
                        summary = $"{filteredTotalFindings} total | ✨ {autoRemediableCount} auto-fix | 🔧 {manualCount} manual",
                        total = filteredTotalFindings,
                        autoRemediable = autoRemediableCount,
                        manual = manualCount,
                        message = filteredTotalFindings == 0 ? "🎉 No issues found!" :
                                 filteredCriticalCount > 0 ? $"🔴 {filteredCriticalCount} critical issue{(filteredCriticalCount > 1 ? "s" : "")} need immediate attention" :
                                 filteredHighCount > 0 ? $"🟠 {filteredHighCount} high-priority finding{(filteredHighCount > 1 ? "s" : "")} to address" :
                                 $"✅ {filteredTotalFindings} minor issue{(filteredTotalFindings > 1 ? "s" : "")} to clean up"
                    },
                    controlFamilies = new
                    {
                        total = filteredControlFamilyStats.Count,
                        compliant = filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore >= 90),
                        needsWork = filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore < 90),
                        summary = $"{filteredControlFamilyStats.Count} families assessed | {filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore >= 90)} compliant | {filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore < 90)} need work"
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
                        percentage = filteredTotalFindings > 0 ? Math.Round((double)g.count / filteredTotalFindings * 100, 1) : 0,
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
                    count = filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore < 90),
                    items = filteredControlFamilyStats
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
                    title = filteredCriticalCount > 0 ? "🔴 CRITICAL & HIGH PRIORITY FINDINGS" :
                           filteredHighCount > 0 ? "🟠 HIGH PRIORITY FINDINGS" :
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
                        filteredHighCount > 0 ? new {
                            action = $"📋 Review {filteredHighCount} high-priority findings",
                            command = filteredControlFamilyStats
                                .Where(cf => cf.Value.Findings.Any(f => f.Severity == AtoFindingSeverity.High))
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "show me control family details",
                            explanation = "High-priority findings need attention within 24-48 hours. Review control families with high-severity findings to understand what needs to be fixed."
                        } : null,
                        new {
                            action = "🔍 Drill down into control families",
                            command = filteredControlFamilyStats
                                .Where(cf => cf.Value.ComplianceScore < 70)
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "get control family details for AC",
                            explanation = $"View detailed findings and recommendations for each control family. Focus on families scoring below 70%: {string.Join(", ", filteredControlFamilyStats.Where(cf => cf.Value.ComplianceScore < 70).Select(cf => cf.Key).Take(3))}."
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
                            command = filteredControlFamilyStats
                                .Where(cf => cf.Value.ComplianceScore < 80)
                                .OrderBy(cf => cf.Value.ComplianceScore)
                                .Take(3)
                                .Select(cf => $"get control family details for {cf.Key}")
                                .FirstOrDefault() ?? "get control family details for CM",
                            explanation = $"Focus on control families scoring below 80%. Currently: {string.Join(", ", filteredControlFamilyStats.Where(cf => cf.Value.ComplianceScore < 80).Select(cf => $"{cf.Key} ({cf.Value.ComplianceScore:F0}%)").Take(5))}."
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
                        total = filteredControlFamilyStats.Count,
                        compliant = filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore >= 90),
                        needsWork = filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore < 90),
                        critical = filteredControlFamilyStats.Count(cf => cf.Value.ComplianceScore < 70)
                    },
                    families = filteredControlFamilyStats
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
                    await _complianceEngine.SaveAssessmentAsync(
                        assessment,
                        subscriptionId,
                        resourceGroupName,
                        "Platform-Copilot",
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
            var validFamilies = ComplianceConstants.ControlFamilies.All;
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
            // Match both "/resourceGroups/{rg}/" (mid-path) and "/resourceGroups/{rg}" (end-of-path)
            var findings = familyResult.Findings;
            if (!string.IsNullOrWhiteSpace(resourceGroupName))
            {
                var rgPatternWithSlash = $"/resourceGroups/{resourceGroupName}/";
                var rgPatternEndOfPath = $"/resourceGroups/{resourceGroupName}";
                
                findings = findings
                    .Where(f => f.ResourceId != null && (
                        f.ResourceId.Contains(rgPatternWithSlash, StringComparison.OrdinalIgnoreCase) ||
                        f.ResourceId.EndsWith(rgPatternEndOfPath, StringComparison.OrdinalIgnoreCase)))
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
                { ComplianceConstants.ControlFamilies.AccessControl, "Access Control" },
                { ComplianceConstants.ControlFamilies.AuditAccountability, "Audit and Accountability" },
                { ComplianceConstants.ControlFamilies.AwarenessTraining, "Awareness and Training" },
                { ComplianceConstants.ControlFamilies.ConfigurationManagement, "Configuration Management" },
                { ComplianceConstants.ControlFamilies.ContingencyPlanning, "Contingency Planning" },
                { ComplianceConstants.ControlFamilies.IdentificationAuthentication, "Identification and Authentication" },
                { ComplianceConstants.ControlFamilies.IncidentResponse, "Incident Response" },
                { ComplianceConstants.ControlFamilies.Maintenance, "Maintenance" },
                { ComplianceConstants.ControlFamilies.MediaProtection, "Media Protection" },
                { ComplianceConstants.ControlFamilies.PhysicalEnvironmental, "Physical and Environmental Protection" },
                { ComplianceConstants.ControlFamilies.Planning, "Planning" },
                { ComplianceConstants.ControlFamilies.PersonnelSecurity, "Personnel Security" },
                { ComplianceConstants.ControlFamilies.RiskAssessment, "Risk Assessment" },
                { ComplianceConstants.ControlFamilies.SecurityAssessment, "Security Assessment and Authorization" },
                { ComplianceConstants.ControlFamilies.SystemCommunicationsProtection, "System and Communications Protection" },
                { ComplianceConstants.ControlFamilies.SystemInformationIntegrity, "System and Information Integrity" },
                { ComplianceConstants.ControlFamilies.SystemServicesAcquisition, "System and Services Acquisition" },
                { ComplianceConstants.ControlFamilies.ProgramManagement, "Program Management" }
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
                 "Accepts subscription GUID or friendly name (e.g., 'production', 'dev', 'staging'). " +
                 "RBAC: Requires Compliance.Administrator, Compliance.Auditor, or Compliance.Analyst role.")]
    public async Task<string> CollectEvidenceAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] string subscriptionIdOrName,
        [Description("NIST control family (e.g., AC, AU, CM, IA)")] string controlFamily,
        [Description("Optional resource group name to limit scope")] string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        // Authorization check
        if (!CheckAuthorization(ComplianceRoles.Administrator, ComplianceRoles.Auditor, ComplianceRoles.Analyst))
        {
            var errorResult = JsonSerializer.Serialize(new
            {
                success = false,
                error = "Unauthorized: User must have Compliance.Administrator, Compliance.Auditor, or Compliance.Analyst role to collect evidence",
                required_roles = new[] { ComplianceRoles.Administrator, ComplianceRoles.Auditor, ComplianceRoles.Analyst }
            }, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogWarning("Unauthorized evidence collection attempt");
            return errorResult;
        }

        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            
            // Automatically get the current authenticated Azure user
            string userName;
            try
            {
                userName = _userContextService?.GetCurrentUserName() ?? await _azureResourceService.GetCurrentAzureUserAsync(cancellationToken);
                _logger.LogInformation("Evidence collection initiated by: {UserName}", userName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine current user, using 'Unknown'");
                userName = "Unknown";
            }
            
            // Log audit entry
            await LogAuditAsync(
                eventType: "EvidenceCollected",
                action: "Collect",
                resourceId: $"{subscriptionId}/evidence/{controlFamily}",
                severity: AuditSeverity.Medium, // Evidence collection is sensitive
                description: $"Evidence collection for control family {controlFamily}",
                metadata: new Dictionary<string, object>
                {
                    ["SubscriptionId"] = subscriptionId,
                    ["ControlFamily"] = controlFamily,
                    ["ResourceGroupName"] = resourceGroupName ?? "All",
                    ["Collector"] = userName
                });
            
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

}
