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

}
