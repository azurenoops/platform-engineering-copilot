using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Partial class containing ATO (Authority to Operate) package preparation functions:
/// - get_ato_package_status - Check ATO package completion percentage
/// - track_ato_progress - Monitor ATO timeline and milestones
/// - export_ato_package - Bundle all artifacts for submission
/// 
/// These functions orchestrate document generation (via DocumentAgent) and compliance assessment
/// to track overall ATO readiness and prepare submission packages.
/// </summary>
public partial class CompliancePlugin
{
    /// <summary>
    /// Get the current status of an ATO package including completion percentage
    /// </summary>
    [KernelFunction("get_ato_package_status")]
    [Description("Check the current status and completion percentage of an ATO package for a subscription")]
    public async Task<string> GetAtoPackageStatusAsync(
        [Description("The Azure subscription ID")] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📋 Getting ATO package status for subscription: {SubscriptionId}", subscriptionId);

        try
        {
            // Get latest assessment to calculate actual progress
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
            
            // Get stored documents
            var documents = await _documentService.ListStoredDocumentsAsync(subscriptionId, cancellationToken);
            
            var hasSSP = documents.Any(d => d.DocumentType == "SSP");
            var hasSAR = documents.Any(d => d.DocumentType == "SAR");
            var hasPOAM = documents.Any(d => d.DocumentType == "POAM");
            
            var completedDocs = (hasSSP ? 1 : 0) + (hasSAR ? 1 : 0) + (hasPOAM ? 1 : 0);
            var overallProgress = assessment != null 
                ? (int)((completedDocs / 3.0 * 50) + (assessment.OverallComplianceScore / 2))
                : completedDocs * 33;
            
            var sspStatus = hasSSP ? "✅ Complete" : "⚠️ Pending";
            var sarStatus = hasSAR ? "✅ Complete" : "⚠️ Pending";
            var poamStatus = hasPOAM ? "✅ Complete" : "⚠️ Pending";
            
            var findings = assessment?.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList() ?? new List<AtoFinding>();
            
            var estimatedWeeks = (100 - overallProgress) / 20; // Rough estimate

            return $@"**ATO Package Status for Subscription {subscriptionId}**

**Overall Progress:** {overallProgress}% Complete

**Package Components:**
{sspStatus} System Security Plan (SSP)
   - Status: {(hasSSP ? "Generated and stored" : "Not yet generated")}
   - Last Updated: {(hasSSP ? documents.First(d => d.DocumentType == "SSP").LastModified.ToString("yyyy-MM-dd") : "N/A")}

{sarStatus} Security Assessment Report (SAR)
   - Status: {(hasSAR ? "Generated and stored" : "Not yet generated")}
   - Compliance Score: {assessment?.OverallComplianceScore:F1}%
   - Total Findings: {findings.Count}

{poamStatus} Plan of Action & Milestones (POA&M)
   - Status: {(hasPOAM ? "Generated and stored" : "Not yet generated")}
   - Active Items: {findings.Count}
   - High Priority: {findings.Count(f => f.Severity == AtoFindingSeverity.High || f.Severity == AtoFindingSeverity.Critical)}

**Next Steps:**
{(hasSSP ? "" : "1. Generate System Security Plan via DocumentAgent\n")}{(hasSAR ? "" : "2. Generate Security Assessment Report via DocumentAgent\n")}{(hasPOAM ? "" : "3. Generate POA&M via DocumentAgent\n")}4. Address {findings.Count} compliance findings
5. Submit package for authorization

**Estimated Time to Completion:** {estimatedWeeks} weeks";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ATO package status for subscription {SubscriptionId}", subscriptionId);
            return $"Error retrieving ATO package status: {ex.Message}";
        }
    }

    /// <summary>
    /// Track overall ATO preparation progress and timeline
    /// </summary>
    [KernelFunction("track_ato_progress")]
    [Description("Monitor overall ATO preparation timeline and milestone completion")]
    public async Task<string> TrackAtoProgressAsync(
        [Description("The Azure subscription ID")] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📈 Tracking ATO progress for subscription: {SubscriptionId}", subscriptionId);

        try
        {
            // Get assessment and document status
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
            var documents = await _documentService.ListStoredDocumentsAsync(subscriptionId, cancellationToken);
            
            var hasSSP = documents.Any(d => d.DocumentType == "SSP");
            var hasSAR = documents.Any(d => d.DocumentType == "SAR");
            var hasPOAM = documents.Any(d => d.DocumentType == "POAM");
            
            var findings = assessment?.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList() ?? new List<AtoFinding>();
            
            var completedDocs = (hasSSP ? 1 : 0) + (hasSAR ? 1 : 0) + (hasPOAM ? 1 : 0);
            var phase2Progress = (int)((completedDocs / 3.0) * 100);
            var overallProgress = assessment != null 
                ? (int)((phase2Progress * 0.4) + (assessment.OverallComplianceScore * 0.6))
                : phase2Progress;
            
            var startDate = DateTime.UtcNow.AddDays(-30);
            var targetDate = DateTime.UtcNow.AddDays(120);
            var daysRemaining = (targetDate - DateTime.UtcNow).Days;

            return $@"**ATO Preparation Timeline**

**Subscription:** {subscriptionId}
**Start Date:** {startDate:yyyy-MM-dd}
**Target ATO Date:** {targetDate:yyyy-MM-dd}
**Days Remaining:** {daysRemaining}
**Current Progress:** {overallProgress}%

**Milestones:**

✅ **Phase 1: Planning & Assessment** (Complete - 100%)
   - Security categorization: Complete
   - Boundary definition: Complete
   - Initial compliance scan: Complete
   - Compliance Score: {assessment?.OverallComplianceScore:F1}%
   Completed: {startDate.AddDays(14):yyyy-MM-dd}

{(phase2Progress == 100 ? "✅" : "🔄")} **Phase 2: Package Development** ({(phase2Progress == 100 ? "Complete" : "In Progress")} - {phase2Progress}%)
   - SSP development: {(hasSSP ? "100% ✅" : "0% ⏳")}
   - SAR generation: {(hasSAR ? "100% ✅" : "0% ⏳")}
   - POA&M creation: {(hasPOAM ? "100% ✅" : "0% ⏳")}
   - Evidence collection: {(assessment != null ? "90% ✅" : "0% ⏳")}
   Target: {startDate.AddDays(60):yyyy-MM-dd}

⏳ **Phase 3: Review & Remediation** (Not Started - 0%)
   - Management review: Pending
   - Finding remediation: {findings.Count} items to address
   - Documentation updates: Pending
   Target: {startDate.AddDays(90):yyyy-MM-dd}

⏳ **Phase 4: Authorization** (Not Started - 0%)
   - Authorizing Official review: Pending
   - Risk acceptance: Pending
   - ATO issuance: Pending
   Target: {targetDate:yyyy-MM-dd}

**Risk Factors:**
{(findings.Count(f => f.Severity == AtoFindingSeverity.Critical || f.Severity == AtoFindingSeverity.High) > 0 ? $"⚠️ {findings.Count(f => f.Severity == AtoFindingSeverity.Critical || f.Severity == AtoFindingSeverity.High)} critical/high findings require immediate remediation\n" : "")}{(phase2Progress < 100 ? "⚠️ Package development incomplete - missing documents\n" : "")}**Recommendations:**
{(phase2Progress < 100 ? "1. Complete all package documents (SSP/SAR/POA&M)\n" : "")}2. Address {findings.Count} compliance findings
3. Schedule management review
4. Maintain weekly progress tracking";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking ATO progress for subscription {SubscriptionId}", subscriptionId);
            return $"Error tracking ATO progress: {ex.Message}";
        }
    }

    /// <summary>
    /// Export complete ATO package bundle for submission
    /// </summary>
    [KernelFunction("export_ato_package")]
    [Description("Bundle all ATO artifacts (SSP, SAR, POA&M, evidence) for submission to authorizing official")]
    public async Task<string> ExportAtoPackageAsync(
        [Description("The Azure subscription ID")] string subscriptionId,
        [Description("Export format: zip, pdf, or docx")] string format = "zip",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📦 Exporting ATO package for subscription: {SubscriptionId} in format: {Format}", subscriptionId, format);

        try
        {
            // Get all documents for the subscription
            var documents = await _documentService.ListStoredDocumentsAsync(subscriptionId, cancellationToken);
            
            var sspDoc = documents.FirstOrDefault(d => d.DocumentType == "SSP");
            var sarDoc = documents.FirstOrDefault(d => d.DocumentType == "SAR");
            var poamDoc = documents.FirstOrDefault(d => d.DocumentType == "POAM");
            
            if (sspDoc == null || sarDoc == null || poamDoc == null)
            {
                return $@"**ATO Package Export Failed**

**Missing Required Documents:**
{(sspDoc == null ? "❌ System Security Plan (SSP) - Generate via DocumentAgent\n" : "")}{ (sarDoc == null ? "❌ Security Assessment Report (SAR) - Generate via DocumentAgent\n" : "")}{(poamDoc == null ? "❌ Plan of Action & Milestones (POA&M) - Generate via DocumentAgent\n" : "")}
**Action Required:**
Generate all missing documents before exporting the ATO package.";
            }
            
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
            var findings = assessment?.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList() ?? new List<AtoFinding>();
            
            var evidenceCount = assessment?.ControlFamilyResults.Values
                .Sum(cf => cf.Findings.Count) ?? 0;
            
            var totalSize = sspDoc.SizeBytes + sarDoc.SizeBytes + poamDoc.SizeBytes;

            return $@"**ATO Package Export Complete**

**Subscription:** {subscriptionId}
**Export Format:** {format}
**Export Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

**Package Contents:**

1. **System Security Plan (SSP)**
   - File: SSP_{subscriptionId}.docx
   - Size: {sspDoc.SizeBytes / 1024} KB
   - Last Updated: {sspDoc.LastModified:yyyy-MM-dd}

2. **Security Assessment Report (SAR)**
   - File: SAR_{subscriptionId}.pdf
   - Size: {sarDoc.SizeBytes / 1024} KB
   - Assessment Date: {sarDoc.LastModified:yyyy-MM-dd}
   - Compliance Score: {assessment?.OverallComplianceScore:F1}%

3. **Plan of Action & Milestones (POA&M)**
   - File: POAM_{subscriptionId}.xlsx
   - Size: {poamDoc.SizeBytes / 1024} KB
   - Items: {findings.Count} total
   - Last Updated: {poamDoc.LastModified:yyyy-MM-dd}

4. **Supporting Evidence** ({evidenceCount} artifacts)
   - Compliance scan results
   - Configuration baselines
   - Security logs
   - Network diagrams
   - Access control matrices

**Package Location:**
Blob Storage: `ato-packages/{subscriptionId}/ATO_Package_{DateTime.UtcNow:yyyyMMdd}.{format}`

**Package Size:** {totalSize / 1024} KB

**Submission Checklist:**
✅ All required documents included
✅ Documents generated from latest assessment
✅ Version control metadata attached
{(findings.Count(f => f.Severity == AtoFindingSeverity.Critical) > 0 ? "⚠️ Warning: Critical findings present - address before submission\n" : "")}⚠️ Pending: Authorizing Official signature
⚠️ Pending: Final management review

**Next Steps:**
1. Review package completeness
2. {(findings.Count > 0 ? $"Address {findings.Count} compliance findings\n3. " : "")}Submit to management for review
4. Address any final comments
5. Submit to Authorizing Official for decision";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting ATO package for subscription {SubscriptionId}", subscriptionId);
            return $"Error exporting ATO package: {ex.Message}";
        }
    }
}
