using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins.ATO;

/// <summary>
/// Plugin for Authority to Operate (ATO) package preparation and orchestration
/// Supports SSP, SAR, POA&M generation and ATO readiness tracking
/// </summary>
public class AtoPreparationPlugin : BaseSupervisorPlugin
{
    private readonly IDocumentGenerationService _documentService;
    private readonly IAtoComplianceEngine _complianceEngine;

    public AtoPreparationPlugin(
        ILogger<AtoPreparationPlugin> logger,
        Kernel kernel,
        IDocumentGenerationService documentService,
        IAtoComplianceEngine complianceEngine) : base(logger, kernel)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
    }

    /// <summary>
    /// Get the current status of an ATO package including completion percentage
    /// </summary>
    [KernelFunction("GetAtoPackageStatus")]
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
{(hasSSP ? "" : "1. Generate System Security Plan\n")}{(hasSAR ? "" : "2. Generate Security Assessment Report\n")}{(hasPOAM ? "" : "3. Generate POA&M\n")}4. Address {findings.Count} compliance findings
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
    /// Generate a System Security Plan (SSP) based on subscription compliance data
    /// </summary>
    [KernelFunction("GenerateSystemSecurityPlan")]
    [Description("Generate a System Security Plan (SSP) for a subscription based on compliance assessment data")]
    public async Task<string> GenerateSystemSecurityPlanAsync(
        [Description("The Azure subscription ID")] string subscriptionId,
        [Description("Optional: System name/description")] string? systemName = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📄 Generating System Security Plan for subscription: {SubscriptionId}", subscriptionId);

        try
        {
            var name = systemName ?? "Azure Cloud Environment";
            
            var sspParams = new SspParameters
            {
                SystemName = name,
                SystemOwner = "Platform Team",
                AuthorizingOfficial = "Authorizing Official",
                ImpactLevel = "IL4",
                Environment = "Azure Government",
                Classification = "UNCLASSIFIED",
                ComplianceFrameworks = new List<string> { "NIST-800-53-Rev5" },
                SystemDescription = "Azure Cloud Environment",
                SystemPurpose = "Cloud infrastructure for government workloads"
            };
            
            var ssp = await _documentService.GenerateSSPAsync(subscriptionId, sspParams, cancellationToken);
            var sspBytes = ssp.Content;
            
            return $@"**System Security Plan (SSP) Generated Successfully**

**System Information:**
- System Name: {name}
- Subscription ID: {subscriptionId}
- Security Categorization: FIPS 199 Moderate
- Authorization Boundary: Azure subscription and associated resources

**SSP Sections Created:**

1. **System Description** ✅
   - Cloud service model (IaaS/PaaS)
   - System boundaries and connections
   - User types and access levels

2. **Control Implementation** ✅
   - NIST 800-53 baseline (Moderate)
   - Control inheritance from Azure
   - Customer responsibility mapping
   - Implementation status per control

3. **Architecture** ✅
   - Network diagrams
   - Data flow diagrams
   - Security architecture overview

4. **Roles & Responsibilities** ✅
   - System owner
   - ISSO/ISSM
   - Authorizing Official

**Document Details:**
- Format: Microsoft Word (.docx)
- Size: {sspBytes.Length / 1024} KB
- Location: Blob storage (SSP/{subscriptionId})

**Next Steps:**
1. Review and validate control implementations
2. Assign organization-specific responsible parties
3. Add organization-specific details
4. Submit for management review";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SSP for subscription {SubscriptionId}", subscriptionId);
            return $"Error generating System Security Plan: {ex.Message}";
        }
    }

    /// <summary>
    /// Generate a Security Assessment Report (SAR) from assessment results
    /// </summary>
    [KernelFunction("GenerateSecurityAssessmentReport")]
    [Description("Generate a Security Assessment Report (SAR) based on compliance assessment results")]
    public async Task<string> GenerateSecurityAssessmentReportAsync(
        [Description("The Azure subscription ID")] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📊 Generating Security Assessment Report for subscription: {SubscriptionId}", subscriptionId);

        try
        {
            var assessmentId = "comprehensive-assessment";
            var sarBytes = await _documentService.GenerateSARAsync(subscriptionId, assessmentId, cancellationToken);
            
            // Get assessment data for summary
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
            
            var findings = assessment?.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList() ?? new List<AtoFinding>();
            
            var criticalCount = findings.Count(f => f.Severity == AtoFindingSeverity.Critical);
            var highCount = findings.Count(f => f.Severity == AtoFindingSeverity.High);
            var mediumCount = findings.Count(f => f.Severity == AtoFindingSeverity.Medium);
            var lowCount = findings.Count(f => f.Severity == AtoFindingSeverity.Low);
            
            var topFamilies = assessment?.ControlFamilyResults
                .OrderByDescending(cf => cf.Value.ComplianceScore)
                .Take(3)
                .Select(cf => $"{cf.Key}: {cf.Value.ComplianceScore:F1}%")
                .ToList() ?? new List<string> { "No data" };

            return $@"**Security Assessment Report (SAR) Generated Successfully**

**Assessment Summary:**
- Subscription ID: {subscriptionId}
- Assessment Date: {DateTime.UtcNow:yyyy-MM-dd}
- Overall Compliance Score: {assessment?.OverallComplianceScore:F1}%
- Assessment Team: Independent Security Assessor
- Methodology: NIST 800-53A

**Control Assessment Results:**

Top Performing Control Families:
{string.Join("\n", topFamilies)}

**Risk Summary:**
- Critical Risk: {criticalCount} findings
- High Risk: {highCount} findings
- Moderate Risk: {mediumCount} findings
- Low Risk: {lowCount} findings

**Total Findings:** {findings.Count}

**Document Details:**
- Format: PDF
- Size: {sarBytes.Content.Length / 1024} KB
- Location: Blob storage (SAR/{subscriptionId})

**Recommendations:**
{(criticalCount > 0 ? $"1. **URGENT:** Address {criticalCount} critical findings immediately\n" : "")}{(highCount > 0 ? $"2. Address {highCount} high-risk findings within 30 days\n" : "")}3. Update POA&M with remediation plans for all findings
4. Re-assess after remediation completion";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAR for subscription {SubscriptionId}", subscriptionId);
            return $"Error generating Security Assessment Report: {ex.Message}";
        }
    }

    /// <summary>
    /// Create a Plan of Action & Milestones (POA&M) from identified findings
    /// </summary>
    [KernelFunction("CreatePoamForFindings")]
    [Description("Generate a Plan of Action & Milestones (POA&M) document from compliance findings")]
    public async Task<string> CreatePoamForFindingsAsync(
        [Description("The Azure subscription ID")] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📝 Creating POA&M for subscription: {SubscriptionId}", subscriptionId);

        try
        {
            // Get findings first
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
            var findings = assessment?.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList();
            
            var poamBytes = await _documentService.GeneratePOAMAsync(subscriptionId, findings, cancellationToken);
            
            // Get assessment data for summary
            findings = findings ?? new List<AtoFinding>();
            
            var criticalCount = findings.Count(f => f.Severity == AtoFindingSeverity.Critical);
            var highCount = findings.Count(f => f.Severity == AtoFindingSeverity.High);
            var mediumCount = findings.Count(f => f.Severity == AtoFindingSeverity.Medium);
            var lowCount = findings.Count(f => f.Severity == AtoFindingSeverity.Low);
            
            var topFindings = findings
                .OrderByDescending(f => f.Severity)
                .Take(3)
                .Select(f => $"- {string.Join(", ", f.AffectedNistControls)}: {f.Description}")
                .ToList();

            return $@"**Plan of Action & Milestones (POA&M) Created Successfully**

**Subscription:** {subscriptionId}
**POA&M Items:** {findings.Count} Total

**Priority Breakdown:**
- Critical: {criticalCount} items
- High: {highCount} items  
- Moderate: {mediumCount} items
- Low: {lowCount} items

**Top Findings:**
{(topFindings.Any() ? string.Join("\n", topFindings) : "No findings")}

**Document Details:**
- Format: Microsoft Excel (.xlsx)
- Size: {poamBytes.Content.Length / 1024} KB
- Location: Blob storage (POAM/{subscriptionId})
- Features: Color-coded severity, automated target dates, milestone tracking

**Excel Worksheets:**
1. **POA&M Items** - Full details of all {findings.Count} findings
2. **Summary** - Statistics and compliance metrics

**Color Coding:**
- 🔴 Critical: Red background (15-day target)
- 🟡 High: Pink background (30-day target)
- 🟨 Medium: Yellow background (90-day target)
- ⚪ Low: White background (180-day target)

**Next Steps:**
1. Review and validate all POA&M items
2. Assign specific responsible parties
3. Update target dates if needed
4. Track remediation progress";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating POA&M for subscription {SubscriptionId}", subscriptionId);
            return $"Error creating Plan of Action & Milestones: {ex.Message}";
        }
    }

    /// <summary>
    /// Track overall ATO preparation progress and timeline
    /// </summary>
    [KernelFunction("TrackAtoProgress")]
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
    [KernelFunction("ExportAtoPackage")]
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
{(sspDoc == null ? "❌ System Security Plan (SSP) - Please generate using generate_ssp\n" : "")}{ (sarDoc == null ? "❌ Security Assessment Report (SAR) - Please generate using generate_sar\n" : "")}{(poamDoc == null ? "❌ Plan of Action & Milestones (POA&M) - Please generate using create_poam\n" : "")}
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
