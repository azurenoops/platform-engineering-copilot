using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Comprehensive ATO Remediation Engine interface for orchestrating compliance remediation workflows
/// </summary>
public interface IAtoRemediationEngine
{
    /// <summary>
    /// Analyzes findings and generates a comprehensive, prioritized remediation plan
    /// </summary>
    Task<RemediationPlan> GenerateRemediationPlanAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        RemediationPlanOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes automated remediation for a specific finding
    /// </summary>
    Task<RemediationExecution> ExecuteRemediationAsync(
        string subscriptionId,
        AtoFinding finding,
        RemediationExecutionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a batch of remediations in optimal order
    /// </summary>
    Task<BatchRemediationResult> ExecuteBatchRemediationAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        BatchRemediationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a remediation was successful
    /// </summary>
    Task<RemediationValidationResult> ValidateRemediationAsync(
        string subscriptionId,
        AtoFinding finding,
        RemediationExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a remediation if needed
    /// </summary>
    Task<RemediationRollbackResult> RollbackRemediationAsync(
        string subscriptionId,
        RemediationExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status and progress of remediation activities
    /// </summary>
    Task<RemediationProgress> GetRemediationProgressAsync(
        string subscriptionId,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed remediation history for audit purposes
    /// </summary>
    Task<RemediationHistory> GetRemediationHistoryAsync(
        string subscriptionId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the impact and effort of remediation before execution
    /// </summary>
    Task<RemediationImpactAnalysis> AnalyzeRemediationImpactAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates manual remediation guidance for findings that can't be auto-remediated
    /// </summary>
    Task<ManualRemediationGuide> GenerateManualRemediationGuideAsync(
        AtoFinding finding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors ongoing remediations and provides real-time status
    /// </summary>
    Task<List<RemediationWorkflowStatus>> GetActiveRemediationWorkflowsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes approval for remediations that require it
    /// </summary>
    Task<RemediationApprovalResult> ProcessRemediationApprovalAsync(
        string remediationId,
        bool approved,
        string approver,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules remediation for a maintenance window
    /// </summary>
    Task<RemediationScheduleResult> ScheduleRemediationAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        DateTimeOffset scheduledTime,
        CancellationToken cancellationToken = default);
}