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
/// Partial class containing evidence download functions:
/// - generate_emass_package
/// - generate_poam
/// </summary>
public partial class CompliancePlugin
{
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

}
