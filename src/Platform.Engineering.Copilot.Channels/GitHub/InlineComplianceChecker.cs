// Placeholder: Inline Compliance Checker for GitHub Copilot (FR-064)
// Provides real-time compliance annotations on IaC files.
//
// Features:
// - Scan Bicep/Terraform/ARM files for compliance gaps
// - Show inline annotations mapping properties to NIST controls
// - Hover tooltips with control descriptions
// - Quick-fix suggestions for non-compliant configurations

namespace Platform.Engineering.Copilot.Channels.GitHub;

/// <summary>
/// Inline compliance checker scaffold per FR-064.
/// Analyzes IaC files and provides compliance annotations.
/// </summary>
public class InlineComplianceChecker
{
    /// <summary>
    /// Analyze an IaC file for compliance gaps.
    /// </summary>
    /// <param name="fileContent">The content of the IaC file.</param>
    /// <param name="fileType">File type: bicep, terraform, arm.</param>
    /// <returns>List of compliance annotations.</returns>
    public Task<List<ComplianceAnnotation>> AnalyzeAsync(string fileContent, string fileType)
    {
        // TODO: Parse file content based on fileType
        // TODO: Map resource properties to NIST 800-53 controls
        // TODO: Identify missing compliance configurations
        // TODO: Generate fix suggestions

        var annotations = new List<ComplianceAnnotation>
        {
            new()
            {
                Line = 1,
                ControlId = "SC-28",
                ControlName = "Protection of Information at Rest",
                Status = "info",
                Message = "[Scaffold] Inline compliance checking is not yet implemented."
            }
        };

        return Task.FromResult(annotations);
    }
}

/// <summary>
/// Represents a compliance annotation on a specific line of an IaC file.
/// </summary>
public class ComplianceAnnotation
{
    public int Line { get; set; }
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Status { get; set; } = "info"; // info, warning, error
    public string Message { get; set; } = string.Empty;
    public string? FixSuggestion { get; set; }
}
