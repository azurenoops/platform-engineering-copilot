using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Helpers;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Partial class containing helper methods:
/// - GetComplianceGrade
/// - GetControlFamilyName
/// - GetResourceTypeDisplayName
/// - GenerateScoreBar
/// - CreateErrorResponse
/// - GetCachedAssessmentAsync
/// - FormatCachedAssessment
/// - Download helpers (JSON, CSV, Markdown)
/// </summary>
public partial class CompliancePlugin
{
    // ========== HELPER METHODS (delegating to ComplianceHelpers) ==========

    private string GetComplianceGrade(double score) => ComplianceHelpers.GetComplianceGrade(score);

    private string GenerateScoreBar(double score) => ComplianceHelpers.GenerateScoreBar(score);

    private string GetResourceTypeDisplayName(string resourceType) => ComplianceHelpers.GetResourceTypeDisplayName(resourceType);

    private string GetControlFamilyName(string familyCode) => ComplianceHelpers.GetControlFamilyName(familyCode);

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

}
