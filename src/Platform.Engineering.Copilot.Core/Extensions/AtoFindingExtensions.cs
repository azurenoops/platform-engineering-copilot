using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Compliance;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for AtoFinding to enrich with auto-remediation information
/// </summary>
public static class AtoFindingExtensions
{
    /// <summary>
    /// Enriches a finding with auto-remediation information including:
    /// - IsAutoRemediable flag
    /// - RemediationActions list
    /// - Appropriate RemediationGuidance
    /// </summary>
    public static AtoFinding WithAutoRemediationInfo(this AtoFinding finding)
    {
        if (finding == null)
            return finding;
        
        // Set auto-remediable flag
        finding.IsAutoRemediable = FindingAutoRemediationService.IsAutoRemediable(finding);
        
        // Add remediation actions if auto-remediable
        if (finding.IsAutoRemediable && (finding.RemediationActions == null || !finding.RemediationActions.Any()))
        {
            finding.RemediationActions = FindingAutoRemediationService.GetRemediationActions(finding);
        }
        
        // Enhance remediation guidance based on auto-remediation capability
        if (finding.IsAutoRemediable && string.IsNullOrEmpty(finding.RemediationGuidance))
        {
            var complexity = FindingAutoRemediationService.GetRemediationComplexity(finding);
            var duration = FindingAutoRemediationService.GetEstimatedDuration(finding);
            
            finding.RemediationGuidance = $"✨ This finding can be automatically remediated. " +
                $"Complexity: {complexity}, Estimated time: {duration.TotalMinutes:F0} minutes. " +
                $"Use the auto-remediation feature or apply changes manually following the recommendation.";
        }
        else if (!finding.IsAutoRemediable && string.IsNullOrEmpty(finding.RemediationGuidance))
        {
            finding.RemediationGuidance = "🔧 Manual remediation required. Follow the recommendation guidance to address this finding. " +
                "Review the affected controls and compliance frameworks to understand the security implications.";
        }
        
        return finding;
    }
    
    /// <summary>
    /// Enriches a list of findings with auto-remediation information
    /// </summary>
    public static List<AtoFinding> WithAutoRemediationInfo(this List<AtoFinding> findings)
    {
        if (findings == null)
            return findings;
            
        foreach (var finding in findings)
        {
            finding.WithAutoRemediationInfo();
        }
        
        return findings;
    }
}
