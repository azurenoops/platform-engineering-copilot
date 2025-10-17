using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
// using Azure.ResourceManager.PolicyInsights;
using Azure.ResourceManager.PolicyInsights.Models;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.Compliance;

public class AzurePolicyEngine : IAzurePolicyService
{
    private readonly ILogger<AzurePolicyEngine> _logger;
    private readonly ArmClient _armClient;
    // private readonly PolicyInsightsClient _policyInsightsClient;
    private readonly Dictionary<string, ApprovalWorkflow> _approvalWorkflows;
    private readonly Dictionary<string, List<AzurePolicyEvaluation>> _policyEvaluationCache;

    public AzurePolicyEngine(ILogger<AzurePolicyEngine> logger)
    {
        _logger = logger;
        
        // Initialize Azure clients with default credential
        var credential = new DefaultAzureCredential();
        _armClient = new ArmClient(credential);
        // _policyInsightsClient = new PolicyInsightsClient(credential);
        
        _approvalWorkflows = new Dictionary<string, ApprovalWorkflow>();
        _policyEvaluationCache = new Dictionary<string, List<AzurePolicyEvaluation>>();
    }

    public async Task<PreFlightGovernanceResult> EvaluatePreFlightPoliciesAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evaluating pre-flight policies for tool call: {ToolName}", toolCall.Name);

        var result = new PreFlightGovernanceResult
        {
            RequestId = Guid.NewGuid().ToString(),
            EvaluatedBy = "AzurePolicyEngine"
        };

        try
        {
            // Extract resource information from tool call
            var resourceContext = await ExtractResourceContextAsync(toolCall);
            
            if (resourceContext == null)
            {
                // No Azure resource context, apply default governance rules
                result.PolicyDecision = GovernancePolicyDecision.Allow;
                result.IsApproved = true;
                result.Messages.Add("No Azure resource context found - allowing tool execution");
                return result;
            }

            // Evaluate policies based on tool type and resource context
            var policyViolations = await EvaluatePoliciesAsync(toolCall, resourceContext, cancellationToken);
            result.PolicyViolations.AddRange(policyViolations);

            // Determine overall decision based on policy violations
            var decision = DeterminePolicyDecision(policyViolations);
            result.PolicyDecision = decision;

            switch (decision)
            {
                case GovernancePolicyDecision.Allow:
                    result.IsApproved = true;
                    result.Messages.Add("All policies passed - tool execution approved");
                    break;

                case GovernancePolicyDecision.Deny:
                    result.IsApproved = false;
                    result.Messages.Add("Policy violation detected - tool execution denied");
                    break;

                case GovernancePolicyDecision.RequiresApproval:
                    result.IsApproved = false;
                    result.RequiredApprovers = await GetRequiredApproversAsync(policyViolations);
                    result.Messages.Add("Manual approval required due to policy constraints");
                    
                    // Create approval workflow
                    var workflow = await CreateApprovalWorkflowAsync(toolCall, policyViolations, cancellationToken);
                    result.ApprovalWorkflowId = workflow.Id;
                    break;

                case GovernancePolicyDecision.AuditOnly:
                    result.IsApproved = true;
                    result.Messages.Add("Audit-only policy - tool execution allowed with logging");
                    break;
            }

            _logger.LogInformation("Pre-flight evaluation completed. Decision: {Decision}, Approved: {Approved}", 
                decision, result.IsApproved);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pre-flight policy evaluation");
            
            // Fail-safe: deny execution on policy evaluation errors
            result.PolicyDecision = GovernancePolicyDecision.Deny;
            result.IsApproved = false;
            result.Messages.Add($"Policy evaluation failed: {ex.Message}");
            
            return result;
        }
    }

    public async Task<PostFlightGovernanceResult> EvaluatePostFlightComplianceAsync(McpToolCall toolCall, McpToolResult result, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evaluating post-flight compliance for tool call: {ToolName}", toolCall.Name);

        var governanceResult = new PostFlightGovernanceResult
        {
            RequestId = Guid.NewGuid().ToString(),
            EvaluatedBy = "AzurePolicyEngine",
            PolicyDecision = GovernancePolicyDecision.Allow,
            IsApproved = true
        };

        try
        {
            // Extract resource information from tool call result
            var resourceContext = await ExtractResourceContextFromResultAsync(toolCall, result);
            
            if (resourceContext == null)
            {
                governanceResult.Messages.Add("No Azure resource context found in result - compliance check skipped");
                return governanceResult;
            }

            // Check for compliance violations
            var complianceViolations = await EvaluateComplianceAsync(toolCall, result, resourceContext, cancellationToken);
            governanceResult.ComplianceViolations.AddRange(complianceViolations);

            if (complianceViolations.Any())
            {
                governanceResult.ComplianceViolated = true;
                governanceResult.RequiresRemediation = complianceViolations.Any(v => v.Severity >= ComplianceViolationSeverity.High);
                
                // Generate remediation actions
                governanceResult.RemediationActions = GenerateRemediationActions(complianceViolations);
                
                governanceResult.Messages.Add($"Found {complianceViolations.Count} compliance violations");
            }
            else
            {
                governanceResult.Messages.Add("No compliance violations detected");
            }

            return governanceResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during post-flight compliance evaluation");
            
            governanceResult.ComplianceViolated = true;
            governanceResult.Messages.Add($"Compliance evaluation failed: {ex.Message}");
            
            return governanceResult;
        }
    }

    public Task<List<AzurePolicyEvaluation>> GetPolicyEvaluationsForResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        if (_policyEvaluationCache.TryGetValue(resourceId, out var cachedEvaluations))
        {
            return Task.FromResult(cachedEvaluations);
        }

        try
        {
            var evaluations = new List<AzurePolicyEvaluation>();
            
            // Query policy insights for the resource
            // Note: This is a simplified implementation - in production you would use the actual Azure Policy Insights API
            var mockEvaluation = new AzurePolicyEvaluation
            {
                PolicyDefinitionId = "/providers/Microsoft.Authorization/policyDefinitions/mock-policy",
                PolicyAssignmentId = "/subscriptions/mock-sub/providers/Microsoft.Authorization/policyAssignments/mock-assignment",
                ResourceId = resourceId,
                ComplianceState = Core.Models.PolicyComplianceState.Compliant,
                PolicyEffect = "audit",
                EvaluatedAt = DateTime.UtcNow
            };
            
            evaluations.Add(mockEvaluation);
            
            _policyEvaluationCache[resourceId] = evaluations;
            return Task.FromResult(evaluations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy evaluations for resource: {ResourceId}", resourceId);
            return Task.FromResult(new List<AzurePolicyEvaluation>());
        }
    }

    public async Task<ApprovalWorkflow> CreateApprovalWorkflowAsync(McpToolCall toolCall, List<PolicyViolation> violations, CancellationToken cancellationToken = default)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            ToolCallId = toolCall.RequestId ?? Guid.NewGuid().ToString(),
            OriginalToolCall = toolCall,
            Status = ApprovalStatus.Pending,
            RequiredApprovers = await GetRequiredApproversAsync(violations),
            Justification = $"Tool call '{toolCall.Name}' requires approval due to policy violations: {string.Join(", ", violations.Select(v => v.PolicyName))}",
            Priority = violations.Any(v => v.Severity == PolicyViolationSeverity.Critical) ? 5 :
                      violations.Any(v => v.Severity == PolicyViolationSeverity.High) ? 4 : 3
        };

        _approvalWorkflows[workflow.Id] = workflow;
        
        _logger.LogInformation("Created approval workflow {WorkflowId} for tool call {ToolCall}", 
            workflow.Id, toolCall.Name);

        return workflow;
    }

    public Task<ApprovalWorkflow?> GetApprovalWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_approvalWorkflows.TryGetValue(workflowId, out var workflow) ? workflow : null);
    }

    public Task<ApprovalWorkflow> UpdateApprovalWorkflowAsync(ApprovalWorkflow workflow, CancellationToken cancellationToken = default)
    {
        _approvalWorkflows[workflow.Id] = workflow;
        
        _logger.LogInformation("Updated approval workflow {WorkflowId} with status {Status}", 
            workflow.Id, workflow.Status);

        return Task.FromResult(workflow);
    }

    private Task<ResourceContext?> ExtractResourceContextAsync(McpToolCall toolCall)
    {
        // Extract Azure resource information from tool call parameters
        // This is a simplified implementation - in production you would parse the actual tool parameters
        if (toolCall.Arguments?.ContainsKey("resourceId") == true)
        {
            var resourceId = toolCall.Arguments["resourceId"]?.ToString() ?? string.Empty;
            return Task.FromResult<ResourceContext?>(new ResourceContext
            {
                ResourceId = resourceId,
                ResourceType = ExtractResourceTypeFromId(resourceId),
                SubscriptionId = ExtractSubscriptionFromId(resourceId)
            });
        }

        return Task.FromResult<ResourceContext?>(null);
    }

    private Task<ResourceContext?> ExtractResourceContextFromResultAsync(McpToolCall toolCall, McpToolResult result)
    {
        // Extract resource information from the tool call result
        if (result.Content is Dictionary<string, object> contentDict && contentDict.ContainsKey("resourceId"))
        {
            var resourceId = contentDict["resourceId"]?.ToString() ?? string.Empty;
            return Task.FromResult<ResourceContext?>(new ResourceContext
            {
                ResourceId = resourceId,
                ResourceType = ExtractResourceTypeFromId(resourceId),
                SubscriptionId = ExtractSubscriptionFromId(resourceId)
            });
        }

        return Task.FromResult<ResourceContext?>(null);
    }

    private Task<List<PolicyViolation>> EvaluatePoliciesAsync(McpToolCall toolCall, ResourceContext resourceContext, CancellationToken cancellationToken)
    {
        var violations = new List<PolicyViolation>();

        // Simulate policy evaluation based on tool call and resource context
        // In production, this would integrate with Azure Policy service
        
        if (toolCall.Name.Contains("delete", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(new PolicyViolation
            {
                PolicyName = "Prevent Resource Deletion",
                PolicyId = "prevent-deletion-policy",
                Severity = PolicyViolationSeverity.High,
                Description = "Deletion operations require manual approval",
                RecommendedAction = "Request approval from resource owner"
            });
        }

        if (resourceContext.ResourceType?.Contains("Microsoft.Storage") == true)
        {
            violations.Add(new PolicyViolation
            {
                PolicyName = "Storage Account Security",
                PolicyId = "storage-security-policy",
                Severity = PolicyViolationSeverity.Medium,
                Description = "Storage operations require security review",
                RecommendedAction = "Ensure encryption and access controls are properly configured"
            });
        }

        return Task.FromResult(violations);
    }

    private Task<List<ComplianceViolation>> EvaluateComplianceAsync(McpToolCall toolCall, McpToolResult result, ResourceContext resourceContext, CancellationToken cancellationToken)
    {
        var violations = new List<ComplianceViolation>();

        // Simulate compliance evaluation based on tool execution result
        // In production, this would check against compliance frameworks
        
        if (!result.IsSuccess)
        {
            violations.Add(new ComplianceViolation
            {
                ComplianceFramework = "ATO",
                ControlId = "SI-2",
                Description = "Tool execution resulted in error - may indicate security issue",
                Severity = ComplianceViolationSeverity.Medium,
                Evidence = result.Error ?? result.Content?.ToString() ?? "Tool execution failed",
                RemediationSteps = new List<string> { "Review error details", "Check security logs", "Validate tool permissions" }
            });
        }

        return Task.FromResult(violations);
    }

    private GovernancePolicyDecision DeterminePolicyDecision(List<PolicyViolation> violations)
    {
        if (!violations.Any())
            return GovernancePolicyDecision.Allow;

        if (violations.Any(v => v.Severity == PolicyViolationSeverity.Critical))
            return GovernancePolicyDecision.Deny;

        if (violations.Any(v => v.Severity >= PolicyViolationSeverity.High))
            return GovernancePolicyDecision.RequiresApproval;

        return GovernancePolicyDecision.AuditOnly;
    }

    private Task<List<string>> GetRequiredApproversAsync(List<PolicyViolation> violations)
    {
        var approvers = new List<string>();

        // Determine required approvers based on violation severity and type
        if (violations.Any(v => v.Severity == PolicyViolationSeverity.Critical))
        {
            approvers.Add("security-admin@company.com");
            approvers.Add("compliance-officer@company.com");
        }
        else if (violations.Any(v => v.Severity == PolicyViolationSeverity.High))
        {
            approvers.Add("resource-owner@company.com");
        }
        else
        {
            approvers.Add("team-lead@company.com");
        }

        return Task.FromResult(approvers);
    }

    private List<string> GenerateRemediationActions(List<ComplianceViolation> violations)
    {
        var actions = new List<string>();

        foreach (var violation in violations)
        {
            actions.AddRange(violation.RemediationSteps);
        }

        return actions.Distinct().ToList();
    }

    private string ExtractResourceTypeFromId(string resourceId)
    {
        // Extract resource type from Azure resource ID
        var parts = resourceId.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("providers", StringComparison.OrdinalIgnoreCase) && i + 2 < parts.Length)
            {
                return $"{parts[i + 1]}/{parts[i + 2]}";
            }
        }
        return "Unknown";
    }

    private string ExtractSubscriptionFromId(string resourceId)
    {
        // Extract subscription ID from Azure resource ID
        var parts = resourceId.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("subscriptions", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
            {
                return parts[i + 1];
            }
        }
        return "Unknown";
    }
}

public class ResourceContext
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}