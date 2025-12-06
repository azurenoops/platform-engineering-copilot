using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Models.Infrastructure;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance.Remediation;

/// <summary>
/// Interface for compliance-based auto-remediation of compliance findings
/// Handles automated fixes for Azure resources, policies, and configurations
/// </summary>
public interface IComplianceRemediationService
{
    /// <summary>
    /// Determines if a finding can be automatically remediated through configuration changes
    /// </summary>
    /// <param name="finding">The compliance finding to evaluate</param>
    /// <returns>True if the finding can be auto-remediated</returns>
    Task<bool> CanAutoRemediateAsync(AtoFinding finding);

    /// <summary>
    /// Generates an infrastructure remediation plan for a specific finding
    /// </summary>
    /// <param name="finding">The compliance finding to remediate</param>
    /// <param name="options">Options for remediation planning</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Infrastructure remediation plan with specific actions</returns>
    Task<InfrastructureRemediationPlan> GenerateRemediationPlanAsync(
        AtoFinding finding,
        InfrastructureRemediationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes automated infrastructure remediation for a finding
    /// </summary>
    /// <param name="plan">The remediation plan to execute</param>
    /// <param name="dryRun">If true, only simulate the remediation without making changes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the remediation execution</returns>
    Task<InfrastructureRemediationResult> ExecuteRemediationAsync(
        InfrastructureRemediationPlan plan,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that an infrastructure remediation was successful
    /// </summary>
    /// <param name="result">The remediation result to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with compliance status</returns>
    Task<InfrastructureRemediationValidation> ValidateRemediationAsync(
        InfrastructureRemediationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back an infrastructure remediation if something goes wrong
    /// </summary>
    /// <param name="result">The remediation result to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<InfrastructureRollbackResult> RollbackRemediationAsync(
        InfrastructureRemediationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supported remediation actions for a specific Azure resource type
    /// </summary>
    /// <param name="resourceType">The Azure resource type (e.g., Microsoft.Storage/storageAccounts)</param>
    /// <returns>List of supported remediation actions</returns>
    Task<IEnumerable<InfrastructureRemediationAction>> GetSupportedActionsAsync(string resourceType);

    /// <summary>
    /// Estimates the impact and risks of performing infrastructure remediation
    /// </summary>
    /// <param name="plan">The remediation plan to assess</param>
    /// <returns>Impact assessment with risks and recommendations</returns>
    Task<InfrastructureRemediationImpactAssessment> AssessRemediationImpactAsync(
        InfrastructureRemediationPlan plan);
}