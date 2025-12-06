using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Partial class containing reporting functions:
/// - perform_risk_assessment
/// - get_compliance_timeline
/// - generate_compliance_certificate
/// - get_remediation_guide
/// </summary>
public partial class CompliancePlugin
{
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

}
