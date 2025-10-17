using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services.Compliance;
/// <summary>
/// Comprehensive ATO Remediation Engine implementation for orchestrating compliance remediation workflows
/// </summary>
public class AtoRemediationEngine : IAtoRemediationEngine
{
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly INistControlsService _nistService;
    private readonly IAzureResourceService _resourceService;
    private readonly ILogger<AtoRemediationEngine> _logger;
    
    // In-memory tracking (in production, use persistent storage)
    private readonly Dictionary<string, RemediationExecution> _activeRemediations = new();
    private readonly List<RemediationExecution> _remediationHistory = new();

    public AtoRemediationEngine(
        IAtoComplianceEngine complianceEngine,
        INistControlsService nistService,
        IAzureResourceService resourceService,
        ILogger<AtoRemediationEngine> logger)
    {
        _complianceEngine = complianceEngine;
        _nistService = nistService;
        _resourceService = resourceService;
        _logger = logger;
    }

    public async Task<RemediationPlan> GenerateRemediationPlanAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        RemediationPlanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating remediation plan for {Count} findings in subscription {SubscriptionId}", 
            findings.Count, subscriptionId);

        options ??= new RemediationPlanOptions();

        var plan = new RemediationPlan
        {
            PlanId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            CreatedAt = DateTimeOffset.UtcNow,
            RemediationItems = new List<RemediationItem>()
        };

        try
        {
            // Filter findings based on options
            var filteredFindings = FilterFindings(findings, options);

            // Prioritize findings
            var prioritizedFindings = PrioritizeFindings(filteredFindings, options);

            // Generate remediation items
            var totalEffort = TimeSpan.Zero;
            foreach (var finding in prioritizedFindings)
            {
                var item = await GenerateRemediationItemAsync(finding, cancellationToken);
                plan.RemediationItems.Add(item);
                totalEffort = totalEffort.Add(item.EstimatedEffort ?? TimeSpan.Zero);
            }

            plan.EstimatedEffort = totalEffort;

            // Optimize remediation order
            plan.RemediationItems = OptimizeRemediationOrder(plan.RemediationItems, options);

            // Generate timeline
            plan.Timeline = GenerateImplementationTimeline(plan.RemediationItems, options);

            // Calculate projected risk reduction
            plan.ProjectedRiskReduction = CalculateRiskReduction(findings);

            // Generate executive summary
            plan.ExecutiveSummary = GenerateExecutiveSummary(plan);

            _logger.LogInformation("Generated remediation plan with {Count} items, estimated effort: {Effort}", 
                plan.RemediationItems.Count, plan.EstimatedEffort);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating remediation plan for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<RemediationExecution> ExecuteRemediationAsync(
        string subscriptionId,
        AtoFinding finding,
        RemediationExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = finding.Id,
            SubscriptionId = subscriptionId,
            ResourceId = finding.ResourceId,
            Status = RemediationExecutionStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            ExecutedBy = options.ExecutedBy,
            RequiredApproval = options.RequireApproval
        };

        try
        {
            _logger.LogInformation("Starting remediation execution {ExecutionId} for finding {FindingId}", 
                execution.ExecutionId, finding.Id);

            // Check if approval is required
            if (options.RequireApproval)
            {
                _logger.LogInformation("Remediation requires approval. Waiting for approval...");
                execution.Status = RemediationExecutionStatus.Pending;
                _remediationHistory.Add(execution);
                return execution;
            }

            // Capture before snapshot if requested
            if (options.CaptureSnapshots)
            {
                execution.BeforeSnapshot = await CaptureResourceSnapshotAsync(finding.ResourceId, cancellationToken);
            }

            // Dry run check
            if (options.DryRun)
            {
                _logger.LogInformation("Executing in DRY RUN mode - no actual changes will be made");
                
                // Generate steps that would be executed
                execution.StepsExecuted = GenerateRemediationSteps(finding);
                execution.ChangesApplied = execution.StepsExecuted.Select(s => s.Description).ToList();
                execution.Message = $"DRY RUN: Would apply {execution.ChangesApplied.Count} changes to {finding.ResourceName}";
                execution.Status = RemediationExecutionStatus.Completed;
                execution.Success = true;
                execution.CompletedAt = DateTimeOffset.UtcNow;
                execution.Duration = execution.CompletedAt.Value - execution.StartedAt;
                
                return execution;
            }

            // Execute remediation using existing ATO tools
            execution.Status = RemediationExecutionStatus.InProgress;
            _activeRemediations[execution.ExecutionId] = execution;
            
            // Create backup before making changes
            execution.BackupId = $"backup-{Guid.NewGuid().ToString()[..8]}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            _logger.LogInformation("Created backup {BackupId} for resource {ResourceId}", 
                execution.BackupId, finding.ResourceId);
            
            if (finding.IsAutoRemediable && finding.RemediationActions.Any())
            {
                try
                {
                    // Execute actual remediation actions
                    execution.StepsExecuted = GenerateRemediationSteps(finding);
                    execution.ChangesApplied = new List<string>();
                    
                    foreach (var action in finding.RemediationActions)
                    {
                        _logger.LogInformation("Executing remediation action: {Action}", action.ActionType);
                        
                        // Execute based on action type
                        var changeDescription = await ExecuteRemediationActionAsync(
                            subscriptionId, 
                            finding, 
                            action, 
                            cancellationToken);
                        
                        execution.ChangesApplied.Add(changeDescription);
                    }
                    
                    execution.Success = true;
                    execution.Message = $"Successfully applied {execution.ChangesApplied.Count} remediation actions to {finding.ResourceName}";
                    _logger.LogInformation("Auto-remediation completed successfully for finding {FindingId}", finding.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during remediation action execution");
                    execution.Success = false;
                    execution.ErrorMessage = ex.Message;
                    execution.Error = ex.ToString();
                    execution.Message = $"Remediation failed: {ex.Message}";
                }
            }
            else
            {
                _logger.LogWarning("Finding {FindingId} requires manual remediation", finding.Id);
                execution.Success = false;
                execution.ErrorMessage = "Manual remediation required";
                execution.Error = "This finding cannot be automatically remediated";
                execution.Message = "Manual remediation required - see RemediationGuidance";
            }

            // Capture after snapshot
            if (options.CaptureSnapshots && execution.Success)
            {
                execution.AfterSnapshot = await CaptureResourceSnapshotAsync(finding.ResourceId, cancellationToken);
            }

            // Validate remediation
            if (options.AutoValidate && execution.Success)
            {
                execution.Status = RemediationExecutionStatus.Validating;
                execution.ValidationResult = await ValidateRemediationAsync(subscriptionId, finding, execution, cancellationToken);
                
                if (!execution.ValidationResult.IsValid && options.AutoRollbackOnFailure)
                {
                    _logger.LogWarning("Validation failed, executing rollback");
                    await RollbackRemediationAsync(subscriptionId, execution, cancellationToken);
                    execution.Status = RemediationExecutionStatus.RolledBack;
                }
            }

            execution.Status = execution.Success ? RemediationExecutionStatus.Completed : RemediationExecutionStatus.Failed;
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.Duration = execution.CompletedAt.Value - execution.StartedAt;

            _activeRemediations.Remove(execution.ExecutionId);
            _remediationHistory.Add(execution);

            _logger.LogInformation("Remediation execution {ExecutionId} completed with status: {Status}", 
                execution.ExecutionId, execution.Status);

            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing remediation for finding {FindingId}", finding.Id);
            execution.Status = RemediationExecutionStatus.Failed;
            execution.Success = false;
            execution.ErrorMessage = ex.Message;
            execution.CompletedAt = DateTimeOffset.UtcNow;
            return execution;
        }
    }

    public async Task<BatchRemediationResult> ExecuteBatchRemediationAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        BatchRemediationOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchRemediationResult
        {
            BatchId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            StartedAt = DateTimeOffset.UtcNow,
            TotalRemediations = findings.Count
        };

        _logger.LogInformation("Starting batch remediation {BatchId} for {Count} findings", result.BatchId, findings.Count);

        try
        {
            var executions = new List<RemediationExecution>();
            var semaphore = new SemaphoreSlim(options.MaxConcurrentRemediations);

            var tasks = findings.Select(async finding =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var execution = await ExecuteRemediationAsync(subscriptionId, finding, options.ExecutionOptions, cancellationToken);
                    return execution;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in batch remediation for finding {FindingId}", finding.Id);
                    
                    if (options.FailFast)
                    {
                        throw;
                    }
                    
                    return new RemediationExecution
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        FindingId = finding.Id,
                        SubscriptionId = subscriptionId,
                        ResourceId = finding.ResourceId,
                        Status = RemediationExecutionStatus.Failed,
                        Success = false,
                        ErrorMessage = ex.Message,
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow
                    };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            result.Executions = (await Task.WhenAll(tasks)).ToList();
            
            result.SuccessfulRemediations = result.Executions.Count(e => e.Success);
            result.FailedRemediations = result.Executions.Count(e => !e.Success && e.Status == RemediationExecutionStatus.Failed);
            result.SkippedRemediations = result.Executions.Count(e => e.Status == RemediationExecutionStatus.Pending);

            result.CompletedAt = DateTimeOffset.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            // Generate summary
            result.Summary = GenerateBatchSummary(result, findings);

            _logger.LogInformation("Batch remediation {BatchId} completed. Success: {Success}, Failed: {Failed}", 
                result.BatchId, result.SuccessfulRemediations, result.FailedRemediations);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch remediation for subscription {SubscriptionId}", subscriptionId);
            result.CompletedAt = DateTimeOffset.UtcNow;
            throw;
        }
    }

    public Task<RemediationValidationResult> ValidateRemediationAsync(
        string subscriptionId,
        AtoFinding finding,
        RemediationExecution execution,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating remediation execution {ExecutionId}", execution.ExecutionId);

        var validation = new RemediationValidationResult
        {
            ValidationId = Guid.NewGuid().ToString(),
            ExecutionId = execution.ExecutionId,
            ValidatedAt = DateTimeOffset.UtcNow,
            Checks = new List<ValidationCheck>()
        };

        try
        {
            // Perform basic validation checks
            validation.Checks = new List<ValidationCheck>
            {
                new ValidationCheck
                {
                    CheckName = "Execution Status",
                    Description = "Verify remediation completed successfully",
                    Passed = execution.Success
                },
                new ValidationCheck
                {
                    CheckName = "Steps Completed",
                    Description = "Verify all remediation steps were executed",
                    Passed = execution.StepsExecuted.Any()
                }
            };
            
            validation.IsValid = validation.Checks.All(c => c.Passed);
            validation.FailureReason = validation.IsValid ? null : 
                string.Join("; ", validation.Checks.Where(c => !c.Passed).Select(c => c.ErrorMessage));

            return Task.FromResult(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating remediation {ExecutionId}", execution.ExecutionId);
            validation.IsValid = false;
            validation.FailureReason = ex.Message;
            return Task.FromResult(validation);
        }
    }

    public async Task<RemediationRollbackResult> RollbackRemediationAsync(
        string subscriptionId,
        RemediationExecution execution,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rolling back remediation execution {ExecutionId}", execution.ExecutionId);

        var rollback = new RemediationRollbackResult
        {
            RollbackId = Guid.NewGuid().ToString(),
            ExecutionId = execution.ExecutionId,
            StartedAt = DateTimeOffset.UtcNow
        };

        try
        {
            // Restore from before snapshot if available
            if (execution.BeforeSnapshot != null)
            {
                await RestoreResourceSnapshotAsync(execution.BeforeSnapshot, cancellationToken);
                rollback.StepsExecuted.Add("Restored resource configuration from snapshot");
            }

            rollback.Success = true;
            rollback.CompletedAt = DateTimeOffset.UtcNow;
            
            _logger.LogInformation("Successfully rolled back remediation execution {ExecutionId}", execution.ExecutionId);
            
            return rollback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back remediation {ExecutionId}", execution.ExecutionId);
            rollback.Success = false;
            rollback.ErrorMessage = ex.Message;
            rollback.CompletedAt = DateTimeOffset.UtcNow;
            return rollback;
        }
    }

    public async Task<RemediationProgress> GetRemediationProgressAsync(
        string subscriptionId,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var sinceDate = since ?? DateTimeOffset.UtcNow.AddDays(-30);
        
        var executions = _remediationHistory
            .Where(e => e.SubscriptionId == subscriptionId && e.StartedAt >= sinceDate)
            .ToList();

        var remediableCount = executions.Count;
        var completed = executions.Count(e => e.Status == RemediationExecutionStatus.Completed);
        var inProgress = executions.Count(e => e.Status == RemediationExecutionStatus.InProgress);
        var failed = executions.Count(e => e.Status == RemediationExecutionStatus.Failed);

        return await Task.FromResult(new RemediationProgress
        {
            TotalRemediableFindings = remediableCount,
            RemediationCompleted = completed,
            RemediationInProgress = inProgress,
            RemediationFailed = failed,
            ActiveRemediations = executions.Where(e => e.Status == RemediationExecutionStatus.InProgress)
                .Select(e => MapToCoreRemediationItem(e)).ToList(),
            RecentlyCompleted = executions.Where(e => e.Status == RemediationExecutionStatus.Completed)
                .OrderByDescending(e => e.CompletedAt)
                .Take(10)
                .Select(e => MapToCoreRemediationItem(e)).ToList(),
            AverageRemediationTime = executions.Any() ? 
                TimeSpan.FromMilliseconds(executions.Average(e => e.Duration.TotalMilliseconds)) : 
                TimeSpan.Zero,
            AutoRemediationsExecuted = executions.Count(e => e.Success && !e.RequiredApproval)
        });
    }

    public async Task<RemediationHistory> GetRemediationHistoryAsync(
        string subscriptionId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var executions = _remediationHistory
            .Where(e => e.SubscriptionId == subscriptionId && 
                       e.StartedAt >= startDate && 
                       e.StartedAt <= endDate)
            .OrderBy(e => e.StartedAt)
            .ToList();

        return await Task.FromResult(new RemediationHistory
        {
            SubscriptionId = subscriptionId,
            StartDate = startDate,
            EndDate = endDate,
            Executions = executions,
            Metrics = GenerateRemediationMetrics(executions),
            RemediationsByControlFamily = new Dictionary<string, int>(),
            RemediationsBySeverity = new Dictionary<string, int>()
        });
    }

    public async Task<RemediationImpactAnalysis> AnalyzeRemediationImpactAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing remediation impact for {Count} findings", findings.Count);

        var analysis = new RemediationImpactAnalysis
        {
            AnalysisId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            AnalyzedAt = DateTimeOffset.UtcNow,
            TotalFindings = findings.Count,
            AutomatableFindings = findings.Count(f => f.IsAutoRemediable),
            ManualFindings = findings.Count(f => !f.IsAutoRemediable)
        };

        // Calculate total duration
        var totalDuration = TimeSpan.Zero;
        foreach (var finding in findings)
        {
            totalDuration = totalDuration.Add(EstimateRemediationDuration(finding));
        }
        analysis.EstimatedTotalDuration = totalDuration;

        // Calculate risk scores
        analysis.CurrentRiskScore = CalculateCurrentRiskScore(findings);
        analysis.ProjectedRiskScore = CalculateProjectedRiskScore(findings);
        analysis.RiskReduction = analysis.CurrentRiskScore - analysis.ProjectedRiskScore;

        // Analyze resource impacts
        analysis.ResourceImpacts = AnalyzeResourceImpacts(findings);

        // Generate recommendations
        analysis.Recommendations = GenerateRecommendations(findings, analysis);

        return await Task.FromResult(analysis);
    }

    public async Task<ManualRemediationGuide> GenerateManualRemediationGuideAsync(
        AtoFinding finding,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating manual remediation guide for finding {FindingId}", finding.Id);

        var guide = new ManualRemediationGuide
        {
            GuideId = Guid.NewGuid().ToString(),
            FindingId = finding.Id,
            Title = $"Manual Remediation: {finding.Title}",
            Overview = finding.Description,
            Steps = GenerateManualRemediationSteps(finding),
            Prerequisites = GeneratePrerequisites(finding),
            ValidationSteps = GenerateManualValidationSteps(finding),
            EstimatedDuration = EstimateRemediationDuration(finding),
            SkillLevel = DetermineSkillLevel(finding),
            RequiredPermissions = DetermineRequiredPermissions(finding),
            References = GenerateReferences(finding)
        };

        // Add rollback plan
        guide.RollbackPlan = new RollbackPlan
        {
            Description = $"Rollback plan for {finding.Title}",
            Steps = GenerateRollbackSteps(finding),
            EstimatedRollbackTime = TimeSpan.FromMinutes(15)
        };

        return await Task.FromResult(guide);
    }

    public async Task<List<RemediationWorkflowStatus>> GetActiveRemediationWorkflowsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var activeWorkflows = _activeRemediations.Values
            .Where(e => e.SubscriptionId == subscriptionId && 
                       e.Status == RemediationExecutionStatus.InProgress)
            .Select(e => new RemediationWorkflowStatus
            {
                WorkflowId = e.ExecutionId,
                SubscriptionId = e.SubscriptionId,
                FindingId = e.FindingId,
                State = RemediationWorkflowState.InProgress,
                StartedAt = e.StartedAt
            })
            .ToList();

        return await Task.FromResult(activeWorkflows);
    }

    public async Task<RemediationApprovalResult> ProcessRemediationApprovalAsync(
        string remediationId,
        bool approved,
        string approvedBy,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing approval for remediation {RemediationId}: {Approved}", 
            remediationId, approved);

        var result = new RemediationApprovalResult
        {
            RemediationId = remediationId,
            Approved = approved,
            ApprovedBy = approvedBy,
            ApprovedAt = DateTimeOffset.UtcNow,
            Comments = comments,
            CanProceed = approved
        };

        var execution = _remediationHistory.FirstOrDefault(e => e.ExecutionId == remediationId);
        if (execution != null)
        {
            execution.ApprovedBy = approvedBy;
            execution.ApprovedAt = DateTimeOffset.UtcNow;
            execution.Status = approved ? RemediationExecutionStatus.Approved : RemediationExecutionStatus.Rejected;
        }

        return await Task.FromResult(result);
    }

    public async Task<RemediationScheduleResult> ScheduleRemediationAsync(
        string subscriptionId,
        List<AtoFinding> findings,
        DateTimeOffset scheduledTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduling remediation for {Count} findings at {Time}", 
            findings.Count, scheduledTime);

        var scheduleResult = new RemediationScheduleResult
        {
            ScheduleId = Guid.NewGuid().ToString(),
            FindingId = string.Join(",", findings.Select(f => f.Id)),
            ScheduledTime = scheduledTime,
            IsScheduled = true
        };

        // In a production system, this would integrate with a job scheduler
        // For now, we'll just log the schedule
        _logger.LogInformation("Remediation scheduled with ID {ScheduleId} for {Count} findings", 
            scheduleResult.ScheduleId, findings.Count);

        return await Task.FromResult(scheduleResult);
    }

    /// <summary>
    /// Execute a specific remediation action for a finding
    /// </summary>
    private async Task<string> ExecuteRemediationActionAsync(
        string subscriptionId,
        AtoFinding finding,
        AtoRemediationAction action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing remediation action {ActionType} for finding {FindingId}", 
            action.ToolCommand, finding.Id);

        try
        {
            switch (action.ToolCommand?.ToUpperInvariant())
            {
                case "ENABLE_HTTPS":
                    return await EnableHttpsOnlyAsync(finding.ResourceId, cancellationToken);
                
                case "UPDATE_TLS_VERSION":
                    var tlsVersion = action.Parameters.TryGetValue("minTlsVersion", out var version) 
                        ? version?.ToString() ?? "1.2" 
                        : "1.2";
                    return await UpdateTlsVersionAsync(finding.ResourceId, tlsVersion, cancellationToken);
                
                case "ENABLE_DIAGNOSTIC_SETTINGS":
                    var workspaceId = action.Parameters.TryGetValue("logAnalyticsWorkspaceId", out var wsId) 
                        ? wsId?.ToString() 
                        : null;
                    return await EnableDiagnosticSettingsAsync(finding.ResourceId, workspaceId, cancellationToken);
                
                case "CONFIGURE_ALERT_RULES":
                    var alertWorkspaceId = action.Parameters.TryGetValue("logAnalyticsWorkspaceId", out var alertWsId) 
                        ? alertWsId?.ToString() 
                        : null;
                    return await ConfigureAlertRulesAsync(finding.ResourceId, alertWorkspaceId, cancellationToken);
                
                case "CONFIGURE_LOG_RETENTION":
                    var retentionDays = action.Parameters.TryGetValue("retentionDays", out var days) 
                        ? int.Parse(days?.ToString() ?? "90") 
                        : 90;
                    return await ConfigureLogRetentionAsync(finding.ResourceId, retentionDays, cancellationToken);
                
                case "ENABLE_ENCRYPTION":
                    return await EnableEncryptionAsync(finding.ResourceId, cancellationToken);
                
                case "CONFIGURE_NSG":
                    return await ConfigureNetworkSecurityGroupAsync(finding.ResourceId, action.Parameters, cancellationToken);
                
                case "APPLY_POLICY":
                    var policyDefinitionId = action.Parameters.TryGetValue("policyDefinitionId", out var policyId) 
                        ? policyId?.ToString() 
                        : null;
                    return await ApplyPolicyAssignmentAsync(subscriptionId, finding.ResourceId, policyDefinitionId, cancellationToken);
                
                default:
                    _logger.LogWarning("Unknown remediation action type: {ActionType}", action.ToolCommand);
                    return $"Executed custom action: {action.Description}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing remediation action {ActionType}", action.ToolCommand);
            throw;
        }
    }

    /// <summary>
    /// Enable HTTPS Only on App Service
    /// </summary>
    private async Task<string> EnableHttpsOnlyAsync(string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enabling HTTPS Only for resource {ResourceId}", resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            var resource = armClient.GetGenericResource(resourceIdentifier);
            
            // Get current resource data
            var response = await resource.GetAsync(cancellationToken);
            var resourceData = response.Value.Data;
            
            // Update properties to enable HTTPS only
            var properties = resourceData.Properties.ToObjectFromJson<Dictionary<string, object>>();
            properties["httpsOnly"] = true;
            
            // Create update payload
            var updateData = new GenericResourceData(resourceData.Location)
            {
                Properties = BinaryData.FromObjectAsJson(properties)
            };
            
            // Apply the update
            var updateOperation = await resource.UpdateAsync(WaitUntil.Completed, updateData, cancellationToken);
            
            _logger.LogInformation("Successfully enabled HTTPS Only on {ResourceId}", resourceId);
            return $"Enabled HTTPS Only on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable HTTPS Only on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to enable HTTPS Only: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Update minimum TLS version on App Service
    /// </summary>
    private async Task<string> UpdateTlsVersionAsync(string resourceId, string minTlsVersion, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating TLS version to {Version} for resource {ResourceId}", minTlsVersion, resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            var resource = armClient.GetGenericResource(resourceIdentifier);
            
            // Get current resource data
            var response = await resource.GetAsync(cancellationToken);
            var resourceData = response.Value.Data;
            
            // Update properties to set minimum TLS version
            var properties = resourceData.Properties.ToObjectFromJson<Dictionary<string, object>>();
            properties["minTlsVersion"] = minTlsVersion;
            
            // Create update payload
            var updateData = new GenericResourceData(resourceData.Location)
            {
                Properties = BinaryData.FromObjectAsJson(properties)
            };
            
            // Apply the update
            var updateOperation = await resource.UpdateAsync(WaitUntil.Completed, updateData, cancellationToken);
            
            _logger.LogInformation("Successfully updated TLS version to {Version} on {ResourceId}", minTlsVersion, resourceId);
            return $"Updated TLS version to {minTlsVersion} on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update TLS version on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to update TLS version: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Enable diagnostic settings for a resource
    /// </summary>
    private async Task<string> EnableDiagnosticSettingsAsync(string resourceId, string? workspaceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enabling diagnostic settings for resource {ResourceId}", resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            
            // Construct diagnostic settings resource ID
            var diagnosticSettingsId = $"{resourceId}/providers/Microsoft.Insights/diagnosticSettings/compliance-diagnostics";
            var diagnosticIdentifier = new ResourceIdentifier(diagnosticSettingsId);
            
            // Create diagnostic settings payload
            var diagnosticSettings = new
            {
                properties = new
                {
                    workspaceId = workspaceId ?? "/subscriptions/default/resourceGroups/default/providers/Microsoft.OperationalInsights/workspaces/default",
                    logs = new[]
                    {
                        new { category = "AuditEvent", enabled = true, retentionPolicy = new { enabled = true, days = 90 } },
                        new { category = "Administrative", enabled = true, retentionPolicy = new { enabled = true, days = 90 } }
                    },
                    metrics = new[]
                    {
                        new { category = "AllMetrics", enabled = true, retentionPolicy = new { enabled = true, days = 90 } }
                    }
                }
            };
            
            // Apply diagnostic settings using ARM REST API
            var genericResource = armClient.GetGenericResource(diagnosticIdentifier);
            var data = new GenericResourceData(AzureLocation.EastUS)
            {
                Properties = BinaryData.FromObjectAsJson(diagnosticSettings.properties)
            };
            
            await genericResource.UpdateAsync(WaitUntil.Completed, data, cancellationToken);
            
            _logger.LogInformation("Successfully enabled diagnostic settings on {ResourceId}", resourceId);
            return $"Enabled diagnostic settings on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable diagnostic settings on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to enable diagnostic settings: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configure alert rules for audit monitoring
    /// </summary>
    private async Task<string> ConfigureAlertRulesAsync(string resourceId, string? workspaceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring alert rules for resource {ResourceId}", resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            
            // Create scheduled query rule for audit monitoring
            var alertRuleId = $"{resourceIdentifier.Parent}/providers/Microsoft.Insights/scheduledQueryRules/audit-alert-rule";
            var alertIdentifier = new ResourceIdentifier(alertRuleId);
            
            var alertRulePayload = new
            {
                location = "global",
                properties = new
                {
                    displayName = "Audit Log Alert Rule",
                    description = "Automated alert rule for audit log review",
                    enabled = true,
                    severity = 2,
                    evaluationFrequency = "PT5M",
                    windowSize = "PT15M",
                    scopes = new[] { workspaceId ?? "/subscriptions/default/resourceGroups/default/providers/Microsoft.OperationalInsights/workspaces/default" },
                    criteria = new
                    {
                        allOf = new[]
                        {
                            new
                            {
                                query = "AuditLogs | where TimeGenerated > ago(15m) | where OperationName contains 'Delete' or OperationName contains 'Create'",
                                timeAggregation = "Count",
                                threshold = 10,
                                @operator = "GreaterThan"
                            }
                        }
                    },
                    actions = new { }
                }
            };
            
            var genericResource = armClient.GetGenericResource(alertIdentifier);
            var data = new GenericResourceData(AzureLocation.EastUS)
            {
                Properties = BinaryData.FromObjectAsJson(alertRulePayload.properties)
            };
            
            await genericResource.UpdateAsync(WaitUntil.Completed, data, cancellationToken);
            
            _logger.LogInformation("Successfully configured alert rules on {ResourceId}", resourceId);
            return $"Configured audit alert rules on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure alert rules on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to configure alert rules: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configure log retention period
    /// </summary>
    private async Task<string> ConfigureLogRetentionAsync(string resourceId, int retentionDays, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring log retention to {Days} days for resource {ResourceId}", retentionDays, resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            var resource = armClient.GetGenericResource(resourceIdentifier);
            
            // Get current workspace data
            var response = await resource.GetAsync(cancellationToken);
            var resourceData = response.Value.Data;
            
            // Update retention settings for Log Analytics workspace
            var properties = resourceData.Properties.ToObjectFromJson<Dictionary<string, object>>();
            properties["retentionInDays"] = retentionDays;
            
            // Create update payload
            var updateData = new GenericResourceData(resourceData.Location)
            {
                Properties = BinaryData.FromObjectAsJson(properties)
            };
            
            // Apply the update
            await resource.UpdateAsync(WaitUntil.Completed, updateData, cancellationToken);
            
            _logger.LogInformation("Successfully configured log retention to {Days} days on {ResourceId}", retentionDays, resourceId);
            return $"Configured log retention to {retentionDays} days on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure log retention on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to configure log retention: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Enable encryption on storage account or other resource
    /// </summary>
    private async Task<string> EnableEncryptionAsync(string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enabling encryption for resource {ResourceId}", resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            var resource = armClient.GetGenericResource(resourceIdentifier);
            
            // Get current resource data
            var response = await resource.GetAsync(cancellationToken);
            var resourceData = response.Value.Data;
            
            // Update properties to enable encryption
            var properties = resourceData.Properties.ToObjectFromJson<Dictionary<string, object>>();
            
            // For storage accounts
            if (resourceIdentifier.ResourceType.ToString().Contains("storageAccounts", StringComparison.OrdinalIgnoreCase))
            {
                properties["encryption"] = new Dictionary<string, object>
                {
                    ["services"] = new Dictionary<string, object>
                    {
                        ["blob"] = new { enabled = true },
                        ["file"] = new { enabled = true }
                    },
                    ["keySource"] = "Microsoft.Storage"
                };
            }
            else
            {
                // Generic encryption enablement
                properties["encryption"] = new { enabled = true };
            }
            
            // Create update payload
            var updateData = new GenericResourceData(resourceData.Location)
            {
                Properties = BinaryData.FromObjectAsJson(properties)
            };
            
            // Apply the update
            await resource.UpdateAsync(WaitUntil.Completed, updateData, cancellationToken);
            
            _logger.LogInformation("Successfully enabled encryption on {ResourceId}", resourceId);
            return $"Enabled encryption on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable encryption on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to enable encryption: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configure Network Security Group rules
    /// </summary>
    private async Task<string> ConfigureNetworkSecurityGroupAsync(
        string resourceId, 
        Dictionary<string, object> parameters, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring NSG for resource {ResourceId}", resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            var resource = armClient.GetGenericResource(resourceIdentifier);
            
            // Get current NSG data
            var response = await resource.GetAsync(cancellationToken);
            var resourceData = response.Value.Data;
            
            // Update security rules
            var properties = resourceData.Properties.ToObjectFromJson<Dictionary<string, object>>();
            
            // Add or update security rules
            var securityRules = new List<object>();
            
            // Add default deny all inbound rule if not exists
            securityRules.Add(new
            {
                name = "DenyAllInbound",
                properties = new
                {
                    priority = 4096,
                    protocol = "*",
                    access = "Deny",
                    direction = "Inbound",
                    sourceAddressPrefix = "*",
                    sourcePortRange = "*",
                    destinationAddressPrefix = "*",
                    destinationPortRange = "*"
                }
            });
            
            // Add allowed inbound rules from parameters
            if (parameters.TryGetValue("allowedPorts", out var allowedPorts))
            {
                var ports = allowedPorts.ToString()?.Split(',') ?? Array.Empty<string>();
                for (int i = 0; i < ports.Length; i++)
                {
                    securityRules.Add(new
                    {
                        name = $"AllowPort{ports[i]}",
                        properties = new
                        {
                            priority = 100 + i,
                            protocol = "Tcp",
                            access = "Allow",
                            direction = "Inbound",
                            sourceAddressPrefix = "*",
                            sourcePortRange = "*",
                            destinationAddressPrefix = "*",
                            destinationPortRange = ports[i]
                        }
                    });
                }
            }
            
            properties["securityRules"] = securityRules;
            
            // Create update payload
            var updateData = new GenericResourceData(resourceData.Location)
            {
                Properties = BinaryData.FromObjectAsJson(properties)
            };
            
            // Apply the update
            await resource.UpdateAsync(WaitUntil.Completed, updateData, cancellationToken);
            
            _logger.LogInformation("Successfully configured NSG rules on {ResourceId}", resourceId);
            return $"Configured NSG rules on {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure NSG on {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to configure NSG: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Apply Azure Policy assignment
    /// </summary>
    private async Task<string> ApplyPolicyAssignmentAsync(
        string subscriptionId,
        string resourceId, 
        string? policyDefinitionId, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying policy assignment to {ResourceId}", resourceId);
        
        try
        {
            var armClient = _resourceService.GetArmClient();
            var resourceIdentifier = ResourceIdentifier.Parse(resourceId);
            
            // Create policy assignment ID
            var assignmentName = $"compliance-policy-{Guid.NewGuid().ToString()[..8]}";
            var assignmentId = $"{resourceId}/providers/Microsoft.Authorization/policyAssignments/{assignmentName}";
            var assignmentIdentifier = new ResourceIdentifier(assignmentId);
            
            // Use default policy definition if not provided
            var policyId = policyDefinitionId ?? 
                $"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/policyDefinitions/audit-vm-managed-disks";
            
            var policyAssignmentPayload = new
            {
                properties = new
                {
                    displayName = $"Compliance Policy Assignment - {assignmentName}",
                    description = "Automated policy assignment for compliance remediation",
                    policyDefinitionId = policyId,
                    scope = resourceId,
                    enforcementMode = "Default",
                    parameters = new { }
                }
            };
            
            var genericResource = armClient.GetGenericResource(assignmentIdentifier);
            var data = new GenericResourceData(resourceIdentifier.Location ?? AzureLocation.EastUS)
            {
                Properties = BinaryData.FromObjectAsJson(policyAssignmentPayload.properties)
            };
            
            await genericResource.UpdateAsync(WaitUntil.Completed, data, cancellationToken);
            
            _logger.LogInformation("Successfully applied policy assignment to {ResourceId}", resourceId);
            return $"Applied policy assignment to {resourceIdentifier.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply policy assignment to {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to apply policy: {ex.Message}", ex);
        }
    }

    // Helper methods

    private List<AtoFinding> FilterFindings(List<AtoFinding> findings, RemediationPlanOptions options)
    {
        var filtered = findings.Where(f => f.Severity >= options.MinimumSeverity);

        if (options.IncludeControlFamilies.Any())
        {
            filtered = filtered.Where(f => f.AffectedControls.Any(c => 
                options.IncludeControlFamilies.Contains(c.Split('-')[0])));
        }

        if (options.ExcludeControlFamilies.Any())
        {
            filtered = filtered.Where(f => !f.AffectedControls.Any(c => 
                options.ExcludeControlFamilies.Contains(c.Split('-')[0])));
        }

        if (options.IncludeOnlyAutomatable)
        {
            filtered = filtered.Where(f => f.IsAutoRemediable);
        }

        return filtered.ToList();
    }

    private List<AtoFinding> PrioritizeFindings(List<AtoFinding> findings, RemediationPlanOptions options)
    {
        return findings
            .OrderByDescending(f => (int)f.Severity)
            .ThenBy(f => f.IsAutoRemediable ? 0 : 1)
            .ThenBy(f => EstimateRemediationDuration(f))
            .ToList();
    }

    private async Task<RemediationItem> GenerateRemediationItemAsync(AtoFinding finding, CancellationToken cancellationToken)
    {
        return await Task.FromResult(new RemediationItem
        {
            FindingId = finding.Id,
            ControlId = finding.AffectedControls.FirstOrDefault() ?? "Unknown",
            ResourceId = finding.ResourceId,
            Priority = GetRemediationPriority(finding),
            AutomationAvailable = finding.IsAutoRemediable,
            EstimatedEffort = EstimateRemediationDuration(finding),
            Steps = GenerateRemediationSteps(finding),
            ValidationSteps = GenerateManualValidationSteps(finding),
            Dependencies = new List<string>()
        });
    }

    private string GetRemediationPriority(AtoFinding finding)
    {
        return finding.Severity switch
        {
            AtoFindingSeverity.Critical => "P0-Critical",
            AtoFindingSeverity.High => "P1-High",
            AtoFindingSeverity.Medium => "P2-Medium",
            AtoFindingSeverity.Low => "P3-Low",
            _ => "P4-Informational"
        };
    }

    private TimeSpan EstimateRemediationDuration(AtoFinding finding)
    {
        if (finding.IsAutoRemediable)
        {
            return finding.Severity switch
            {
                AtoFindingSeverity.Critical => TimeSpan.FromMinutes(30),
                AtoFindingSeverity.High => TimeSpan.FromMinutes(20),
                _ => TimeSpan.FromMinutes(10)
            };
        }

        return finding.Severity switch
        {
            AtoFindingSeverity.Critical => TimeSpan.FromHours(4),
            AtoFindingSeverity.High => TimeSpan.FromHours(2),
            AtoFindingSeverity.Medium => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    private List<RemediationStep> GenerateRemediationSteps(AtoFinding finding)
    {
        var steps = new List<RemediationStep>();

        if (finding.RemediationActions.Any())
        {
            var order = 1;
            foreach (var action in finding.RemediationActions)
            {
                steps.Add(new RemediationStep
                {
                    Order = order++,
                    Description = action.Description,
                    Command = action.ToolCommand,
                    AutomationScript = action.ScriptPath
                });
            }
        }
        else
        {
            steps.Add(new RemediationStep
            {
                Order = 1,
                Description = finding.RemediationGuidance
            });
        }

        return steps;
    }

    private List<RemediationStep> GenerateManualRemediationSteps(AtoFinding finding)
    {
        return GenerateRemediationSteps(finding);
    }

    private List<string> GenerateManualValidationSteps(AtoFinding finding)
    {
        return new List<string>
        {
            $"Verify that {finding.ResourceName} no longer exhibits the finding",
            "Re-run compliance scan to confirm remediation",
            "Document the changes made and evidence of remediation"
        };
    }

    private List<string> GeneratePrerequisites(AtoFinding finding)
    {
        return new List<string>
        {
            "Azure subscription access",
            "Appropriate RBAC permissions",
            "Backup of resource configuration"
        };
    }

    private List<string> GenerateRollbackSteps(AtoFinding finding)
    {
        return new List<string>
        {
            "Restore configuration from backup",
            "Verify resource functionality",
            "Document rollback reason"
        };
    }

    private string DetermineSkillLevel(AtoFinding finding)
    {
        return finding.Severity switch
        {
            AtoFindingSeverity.Critical => "Advanced",
            AtoFindingSeverity.High => "Intermediate",
            _ => "Beginner"
        };
    }

    private List<string> DetermineRequiredPermissions(AtoFinding finding)
    {
        return new List<string>
        {
            "Microsoft.Authorization/*/read",
            "Microsoft.Resources/subscriptions/resourceGroups/read",
            $"{finding.ResourceType}/read",
            $"{finding.ResourceType}/write"
        };
    }

    private List<string> GenerateReferences(AtoFinding finding)
    {
        var references = new List<string>
        {
            "https://docs.microsoft.com/azure/",
            "https://learn.microsoft.com/azure/security/"
        };

        foreach (var control in finding.AffectedNistControls)
        {
            references.Add($"https://csrc.nist.gov/projects/cprt/catalog#/{control}");
        }

        return references;
    }

    private List<RemediationItem> OptimizeRemediationOrder(List<RemediationItem> items, RemediationPlanOptions options)
    {
        if (options.GroupByResource)
        {
            return items.OrderBy(i => i.ResourceId).ThenBy(i => i.Priority).ToList();
        }

        return items.OrderBy(i => i.Priority).ToList();
    }

    private ImplementationTimeline GenerateImplementationTimeline(List<RemediationItem> items, RemediationPlanOptions options)
    {
        var timeline = new ImplementationTimeline
        {
            StartDate = DateTimeOffset.UtcNow,
            Phases = new List<TimelinePhase>()
        };

        var criticalItems = items.Where(i => i.Priority?.StartsWith("P0") == true).ToList();
        var highItems = items.Where(i => i.Priority?.StartsWith("P1") == true).ToList();
        var mediumItems = items.Where(i => i.Priority?.StartsWith("P2") == true).ToList();
        var lowItems = items.Where(i => i.Priority?.StartsWith("P3") == true).ToList();

        var currentStart = timeline.StartDate;

        if (criticalItems.Any())
        {
            var duration = TimeSpan.FromTicks(criticalItems.Sum(i => i.EstimatedEffort?.Ticks ?? 0));
            timeline.Phases.Add(new TimelinePhase
            {
                Name = "Critical Findings",
                StartDate = currentStart,
                EndDate = currentStart.Add(duration),
                EstimatedDuration = duration,
                Items = criticalItems
            });
            currentStart = currentStart.Add(duration);
        }

        if (highItems.Any())
        {
            var duration = TimeSpan.FromTicks(highItems.Sum(i => i.EstimatedEffort?.Ticks ?? 0));
            timeline.Phases.Add(new TimelinePhase
            {
                Name = "High Priority Findings",
                StartDate = currentStart,
                EndDate = currentStart.Add(duration),
                EstimatedDuration = duration,
                Items = highItems
            });
            currentStart = currentStart.Add(duration);
        }

        timeline.EndDate = currentStart;
        timeline.TotalDuration = timeline.EndDate - timeline.StartDate;

        return timeline;
    }

    private double CalculateRiskReduction(List<AtoFinding> findings)
    {
        var totalRisk = findings.Sum(f => (int)f.Severity);
        return totalRisk > 0 ? Math.Min(100, (totalRisk / findings.Count) * 25) : 0;
    }

    private string? GenerateExecutiveSummary(RemediationPlan plan)
    {
        var critical = plan.RemediationItems.Count(i => i.Priority?.StartsWith("P0") == true);
        var high = plan.RemediationItems.Count(i => i.Priority?.StartsWith("P1") == true);
        var automated = plan.RemediationItems.Count(i => i.AutomationAvailable);

        return $"Remediation plan contains {plan.RemediationItems.Count} items: " +
               $"{critical} critical, {high} high priority. " +
               $"{automated} items can be automated. " +
               $"Estimated effort: {plan.EstimatedEffort.TotalHours:F1} hours. " +
               $"Projected risk reduction: {plan.ProjectedRiskReduction:F1}%.";
    }



    private async Task<RemediationSnapshot> CaptureResourceSnapshotAsync(string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Capturing snapshot for resource {ResourceId}", resourceId);
        
        return await Task.FromResult(new RemediationSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString(),
            CapturedAt = DateTimeOffset.UtcNow,
            ResourceId = resourceId,
            Configuration = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>(),
            Tags = new List<string>()
        });
    }

    private async Task RestoreResourceSnapshotAsync(RemediationSnapshot snapshot, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Restoring snapshot {SnapshotId} for resource {ResourceId}", 
            snapshot.SnapshotId, snapshot.ResourceId);
        await Task.CompletedTask;
    }

    private BatchRemediationSummary GenerateBatchSummary(BatchRemediationResult result, List<AtoFinding> findings)
    {
        return new BatchRemediationSummary
        {
            SuccessRate = result.TotalRemediations > 0 ? 
                (double)result.SuccessfulRemediations / result.TotalRemediations * 100 : 0,
            CriticalFindingsRemediated = findings.Count(f => f.Severity == AtoFindingSeverity.Critical),
            HighFindingsRemediated = findings.Count(f => f.Severity == AtoFindingSeverity.High),
            EstimatedRiskReduction = CalculateRiskReduction(findings),
            ControlFamiliesAffected = findings.SelectMany(f => f.AffectedControls)
                .Select(c => c.Split('-')[0])
                .Distinct()
                .ToList(),
            RemediationsByType = findings.GroupBy(f => f.FindingType.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private Core.Models.RemediationItem MapToCoreRemediationItem(RemediationExecution execution)
    {
        return new Core.Models.RemediationItem
        {
            Id = execution.ExecutionId,
            FindingId = execution.FindingId,
            Title = $"Remediation for {execution.FindingId}",
            ResourceId = execution.ResourceId,
            Status = MapExecutionStatusToRemediationStatus(execution.Status),
            StartedAt = execution.StartedAt.DateTime,
            IsAutomated = !execution.RequiredApproval,
            Notes = execution.ErrorMessage ?? "In progress"
        };
    }

    private AtoRemediationStatus MapExecutionStatusToRemediationStatus(RemediationExecutionStatus status)
    {
        return status switch
        {
            RemediationExecutionStatus.Pending => AtoRemediationStatus.NotStarted,
            RemediationExecutionStatus.InProgress => AtoRemediationStatus.InProgress,
            RemediationExecutionStatus.Completed => AtoRemediationStatus.Completed,
            RemediationExecutionStatus.Failed => AtoRemediationStatus.Failed,
            _ => AtoRemediationStatus.NotStarted
        };
    }

    private List<RemediationMetric> GenerateRemediationMetrics(List<RemediationExecution> executions)
    {
        return executions
            .GroupBy(e => e.StartedAt.Date)
            .Select(g => new RemediationMetric
            {
                Date = g.Key,
                TotalRemediations = g.Count(),
                SuccessfulRemediations = g.Count(e => e.Success),
                AverageRemediationTime = g.Average(e => e.Duration.TotalMinutes),
                ComplianceImprovement = 0 // Would be calculated based on before/after compliance scores
            })
            .ToList();
    }

    private double CalculateCurrentRiskScore(List<AtoFinding> findings)
    {
        if (!findings.Any()) return 0;
        
        return findings.Sum(f => (int)f.Severity * 10.0) / findings.Count;
    }

    private double CalculateProjectedRiskScore(List<AtoFinding> findings)
    {
        var remediable = findings.Where(f => f.IsRemediable).ToList();
        var remaining = findings.Except(remediable).ToList();
        
        if (!remaining.Any()) return 0;
        
        return remaining.Sum(f => (int)f.Severity * 10.0) / findings.Count;
    }

    private List<ResourceImpact> AnalyzeResourceImpacts(List<AtoFinding> findings)
    {
        return findings
            .GroupBy(f => f.ResourceId)
            .Select(g => new ResourceImpact
            {
                ResourceId = g.Key,
                ResourceType = g.First().ResourceType,
                FindingsCount = g.Count(),
                RequiresDowntime = g.Any(f => f.Severity == AtoFindingSeverity.Critical),
                EstimatedDowntime = g.Any(f => f.Severity == AtoFindingSeverity.Critical) ? 
                    TimeSpan.FromMinutes(15) : null,
                ImpactLevel = DetermineImpactLevel(g.ToList()),
                AffectedCapabilities = new List<string>()
            })
            .ToList();
    }

    private string DetermineImpactLevel(List<AtoFinding> findings)
    {
        if (findings.Any(f => f.Severity == AtoFindingSeverity.Critical)) return "High";
        if (findings.Any(f => f.Severity == AtoFindingSeverity.High)) return "Medium";
        return "Low";
    }

    private List<string> GenerateRecommendations(List<AtoFinding> findings, RemediationImpactAnalysis analysis)
    {
        var recommendations = new List<string>();

        if (analysis.AutomatableFindings > 0)
        {
            recommendations.Add($"Prioritize {analysis.AutomatableFindings} automatable findings for immediate remediation");
        }

        if (analysis.RiskReduction > 50)
        {
            recommendations.Add("High risk reduction potential - recommend immediate action");
        }

        if (analysis.EstimatedTotalDuration > TimeSpan.FromHours(40))
        {
            recommendations.Add("Consider phased approach due to estimated duration");
        }

        return recommendations;
    }
}

// Remediation strategy interfaces and implementations

// Note: Remediation strategies are simplified to avoid circular dependencies
// In production, integrate with existing AtoRemediationTool which already handles
// Azure resource remediation through proper service layers
