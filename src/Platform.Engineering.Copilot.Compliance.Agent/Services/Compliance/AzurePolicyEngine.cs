using Platform.Engineering.Copilot.Core.Models.Mcp;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Text.Json;
using System.Text.Json.Serialization;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

public class AzurePolicyEngine : IAzurePolicyService
{
    private readonly ILogger<AzurePolicyEngine> _logger;
    private readonly IAzureClientFactory _clientFactory;
    private readonly ArmClient _armClient;
    private readonly Dictionary<string, List<AzurePolicyEvaluation>> _policyEvaluationCache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, DateTime> _cacheTimestamps;
    private readonly Dictionary<string, ApprovalWorkflow> _approvalWorkflows;

    public AzurePolicyEngine(
        ILogger<AzurePolicyEngine> logger,
        IAzureClientFactory clientFactory)
    {
        _logger = logger;
        _clientFactory = clientFactory;
        
        // Get ARM client from factory (centralized credential management)
        _armClient = _clientFactory.GetArmClient();
        
        _policyEvaluationCache = new Dictionary<string, List<AzurePolicyEvaluation>>();
        _cacheTimestamps = new Dictionary<string, DateTime>();
        _approvalWorkflows = new Dictionary<string, ApprovalWorkflow>();
        
        _logger.LogInformation("AzurePolicyEngine initialized with Azure Policy Insights integration using {CloudEnvironment}", 
            _clientFactory.CloudEnvironment);
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
                    result.Messages.Add("All policies passed - approved");
                    break;

                case GovernancePolicyDecision.Deny:
                    result.IsApproved = false;
                    result.Messages.Add("Policy violation detected - denied");
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
                    result.Messages.Add("Audit-only policy - allowed with logging");
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

    public async Task<List<AzurePolicyEvaluation>> GetPolicyEvaluationsForResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_policyEvaluationCache.TryGetValue(resourceId, out var cachedEvaluations) &&
            _cacheTimestamps.TryGetValue(resourceId, out var cacheTime) &&
            DateTime.UtcNow - cacheTime < _cacheExpiration)
        {
            _logger.LogDebug("Returning cached policy evaluations for resource: {ResourceId}", resourceId);
            return cachedEvaluations;
        }

        try
        {
            _logger.LogInformation("Fetching policy evaluations from Azure for resource: {ResourceId}", resourceId);
            
            var evaluations = new List<AzurePolicyEvaluation>();
            
            // Parse resource ID to get subscription
            var subscriptionId = ExtractSubscriptionFromId(resourceId);
            if (string.IsNullOrEmpty(subscriptionId) || subscriptionId == "Unknown")
            {
                _logger.LogWarning("Could not extract subscription ID from resource ID: {ResourceId}", resourceId);
                return evaluations;
            }

            // Get subscription resource
            var subscriptionResourceId = new global::Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}");
            var subscription = _armClient.GetSubscriptionResource(subscriptionResourceId);
            
            // Query policy compliance using Azure Resource Manager REST API
            // This approach works consistently across SDK versions
            try
            {
                // Build the policy states query URL
                var policyStatesUrl = $"{subscriptionResourceId}/providers/Microsoft.PolicyInsights/policyStates/latest/queryResults?api-version=2019-10-01&$filter=ResourceId eq '{resourceId}'";
                
                // Use HttpClient to query the REST API
                using var httpClient = new HttpClient();
                var credential = _clientFactory.GetCredential();
                var token = await credential.GetTokenAsync(
                    new global::Azure.Core.TokenRequestContext(new[] { _clientFactory.GetManagementScope().Replace("/.default", "") + "/.default" }),
                    cancellationToken);
                
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
                
                var response = await httpClient.PostAsync(policyStatesUrl, null, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var policyStates = JsonSerializer.Deserialize<PolicyStatesResponse>(content);
                    
                    if (policyStates?.Value != null)
                    {
                        foreach (var policyState in policyStates.Value)
                        {
                            var evaluation = new AzurePolicyEvaluation
                            {
                                PolicyDefinitionId = policyState.PolicyDefinitionId ?? "unknown",
                                PolicyAssignmentId = policyState.PolicyAssignmentId ?? "unknown",
                                ResourceId = resourceId,
                                ComplianceState = MapComplianceState(policyState.ComplianceState),
                                PolicyEffect = policyState.PolicyDefinitionAction ?? "unknown",
                                EvaluatedAt = policyState.Timestamp
                            };
                            
                            evaluations.Add(evaluation);
                            
                            _logger.LogDebug("Policy evaluation: {PolicyId} - {ComplianceState}", 
                                evaluation.PolicyDefinitionId, evaluation.ComplianceState);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Policy states API returned {StatusCode} for resource: {ResourceId}", 
                        response.StatusCode, resourceId);
                }
            }
            catch (global::Azure.RequestFailedException ex) when (ex.Status == 404 || ex.Status == 403)
            {
                _logger.LogWarning(ex, "Unable to query policy states (permission or resource not found): {ResourceId}", resourceId);
                // Return empty list - this is expected for resources without policies or insufficient permissions
            }

            // Cache the results
            _policyEvaluationCache[resourceId] = evaluations;
            _cacheTimestamps[resourceId] = DateTime.UtcNow;
            
            _logger.LogInformation("Retrieved {Count} policy evaluations for resource: {ResourceId}", 
                evaluations.Count, resourceId);
            
            return evaluations;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Resource not found or no policy states available: {ResourceId}", resourceId);
            return new List<AzurePolicyEvaluation>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy evaluations for resource: {ResourceId}", resourceId);
            return new List<AzurePolicyEvaluation>();
        }
    }

    private PolicyComplianceState MapComplianceState(string? azureComplianceState)
    {
        return azureComplianceState?.ToLowerInvariant() switch
        {
            "compliant" => PolicyComplianceState.Compliant,
            "noncompliant" => PolicyComplianceState.NonCompliant,
            "unknown" => PolicyComplianceState.Unknown,
            _ => PolicyComplianceState.Unknown
        };
    }

    public async Task<ApprovalWorkflow> CreateApprovalWorkflowAsync(McpToolCall toolCall, List<PolicyViolation> violations, CancellationToken cancellationToken = default)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            ToolCallId = Guid.NewGuid().ToString(), // MCP protocol doesn't have RequestId
            OriginalToolCall = toolCall,
            Status = ApprovalStatus.Pending,
            RequiredApprovers = await GetRequiredApproversAsync(violations),
            Justification = $"Tool call '{toolCall.Name}' requires approval due to policy violations: {string.Join(", ", violations.Select(v => v.PolicyName))}",
            Priority = violations.Any(v => v.Severity == PolicyViolationSeverity.Critical) ? 5 :
                      violations.Any(v => v.Severity == PolicyViolationSeverity.High) ? 4 : 3
        };

        // Store in memory
        _approvalWorkflows[workflow.Id] = workflow;
        
        _logger.LogInformation("Created and persisted approval workflow {WorkflowId} for tool call {ToolCall}", 
            workflow.Id, toolCall.Name);

        return workflow;
    }

    public Task<ApprovalWorkflow?> GetApprovalWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        _approvalWorkflows.TryGetValue(workflowId, out var workflow);
        return Task.FromResult(workflow);
    }

    public Task<ApprovalWorkflow> UpdateApprovalWorkflowAsync(ApprovalWorkflow workflow, CancellationToken cancellationToken = default)
    {
        if (!_approvalWorkflows.ContainsKey(workflow.Id))
        {
            throw new InvalidOperationException($"Approval workflow {workflow.Id} not found");
        }

        // Update in-memory storage
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
        // McpToolResult.Content is a List<McpContent>, try to find resourceId in text content
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        if (!string.IsNullOrEmpty(textContent))
        {
            // Try to parse JSON or extract resource ID from text
            try
            {
                var contentDict = JsonSerializer.Deserialize<Dictionary<string, object>>(textContent);
                if (contentDict != null && contentDict.ContainsKey("resourceId"))
                {
                    var resourceId = contentDict["resourceId"]?.ToString() ?? string.Empty;
                    return Task.FromResult<ResourceContext?>(new ResourceContext
                    {
                        ResourceId = resourceId,
                        ResourceType = ExtractResourceTypeFromId(resourceId),
                        SubscriptionId = ExtractSubscriptionFromId(resourceId)
                    });
                }
            }
            catch
            {
                // Not JSON, ignore
            }
        }

        return Task.FromResult<ResourceContext?>(null);
    }

    private async Task<List<PolicyViolation>> EvaluatePoliciesAsync(McpToolCall toolCall, ResourceContext resourceContext, CancellationToken cancellationToken)
    {
        var violations = new List<PolicyViolation>();

        try
        {
            // Use real Azure Policy Insights API to check for policy compliance
            if (!string.IsNullOrEmpty(resourceContext.ResourceId))
            {
                _logger.LogInformation("Evaluating Azure policies for resource: {ResourceId}", resourceContext.ResourceId);
                
                var policyEvaluations = await GetPolicyEvaluationsForResourceAsync(resourceContext.ResourceId, cancellationToken);
                
                // Convert non-compliant policy evaluations to violations
                foreach (var evaluation in policyEvaluations.Where(e => e.ComplianceState == PolicyComplianceState.NonCompliant))
                {
                    var severity = DetermineSeverityFromPolicyEffect(evaluation.PolicyEffect);
                    
                    violations.Add(new PolicyViolation
                    {
                        PolicyName = ExtractPolicyName(evaluation.PolicyDefinitionId),
                        PolicyId = evaluation.PolicyDefinitionId,
                        Severity = severity,
                        Description = $"Resource violates policy: {ExtractPolicyName(evaluation.PolicyDefinitionId)}",
                        RecommendedAction = GetRecommendedActionForPolicy(evaluation.PolicyEffect, evaluation.PolicyDefinitionId)
                    });
                    
                    _logger.LogWarning("Policy violation detected: {PolicyId} - {Effect}", 
                        evaluation.PolicyDefinitionId, evaluation.PolicyEffect);
                }
                
                _logger.LogInformation("Found {Count} policy violations for resource {ResourceId}", 
                    violations.Count, resourceContext.ResourceId);
            }
            else
            {
                _logger.LogWarning("No resource ID provided - using fallback policy checks");
                
                // Fallback to basic policy checks for operations without a specific resource ID
                violations.AddRange(EvaluateFallbackPolicies(toolCall, resourceContext));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating Azure policies for resource: {ResourceId}", resourceContext.ResourceId);
            
            // Fallback to basic policy checks on error
            violations.AddRange(EvaluateFallbackPolicies(toolCall, resourceContext));
        }

        return violations;
    }

    private List<PolicyViolation> EvaluateFallbackPolicies(McpToolCall toolCall, ResourceContext resourceContext)
    {
        var violations = new List<PolicyViolation>();
        
        // Basic policy checks when Azure Policy API is unavailable or resource doesn't exist yet
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
        
        return violations;
    }

    private PolicyViolationSeverity DetermineSeverityFromPolicyEffect(string policyEffect)
    {
        return policyEffect?.ToLowerInvariant() switch
        {
            "deny" => PolicyViolationSeverity.Critical,
            "deployifnotexists" => PolicyViolationSeverity.High,
            "modify" => PolicyViolationSeverity.Medium,
            "audit" => PolicyViolationSeverity.Low,
            "auditifnotexists" => PolicyViolationSeverity.Low,
            _ => PolicyViolationSeverity.Medium
        };
    }

    private string GetRecommendedActionForPolicy(string policyEffect, string policyId)
    {
        var policyName = ExtractPolicyName(policyId);
        
        return policyEffect?.ToLowerInvariant() switch
        {
            "deny" => $"This resource violates the '{policyName}' policy. The operation will be blocked by Azure. Review policy requirements and adjust resource configuration.",
            "deployifnotexists" => $"The '{policyName}' policy requires additional resources to be deployed. Review policy definition and ensure required resources are created.",
            "modify" => $"The '{policyName}' policy will automatically modify this resource. Review the changes that will be applied.",
            "audit" => $"This resource violates the '{policyName}' audit policy. The operation is allowed but will be logged for compliance review.",
            _ => $"Review the '{policyName}' policy requirements and ensure resource compliance."
        };
    }

    private Task<List<ComplianceViolation>> EvaluateComplianceAsync(McpToolCall toolCall, McpToolResult result, ResourceContext resourceContext, CancellationToken cancellationToken)
    {
        var violations = new List<ComplianceViolation>();

        // Simulate compliance evaluation based on tool execution result
        // In production, this would check against compliance frameworks
        
        // McpToolResult uses IsError property, not IsSuccess
        if (result.IsError)
        {
            var errorMessage = result.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? "Tool execution failed";
            violations.Add(new ComplianceViolation
            {
                ComplianceFramework = "ATO",
                ControlId = "SI-2",
                Description = "Tool execution resulted in error - may indicate security issue",
                Severity = ComplianceViolationSeverity.Medium,
                Evidence = errorMessage,
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

    private string ExtractPolicyName(string policyDefinitionId)
    {
        // Extract policy name from policy definition ID
        // Format: /providers/Microsoft.Authorization/policyDefinitions/{name}
        // or /subscriptions/{sub}/providers/Microsoft.Authorization/policyDefinitions/{name}
        if (string.IsNullOrEmpty(policyDefinitionId))
        {
            return "Unknown Policy";
        }

        var parts = policyDefinitionId.Split('/');
        if (parts.Length > 0)
        {
            // Get the last part which is typically the policy name
            var name = parts[^1];
            
            // Clean up common Azure naming patterns
            return name
                .Replace("-", " ")
                .Replace("_", " ")
                .Trim();
        }
        
        return policyDefinitionId;
    }

    



    // Helper classes for Azure Policy Insights REST API deserialization
    private class PolicyStatesResponse
    {
        [JsonPropertyName("value")]
        public List<PolicyStateData>? Value { get; set; }
    }

    private class PolicyStateData
    {
        [JsonPropertyName("policyDefinitionId")]
        public string? PolicyDefinitionId { get; set; }

        [JsonPropertyName("policyAssignmentId")]
        public string? PolicyAssignmentId { get; set; }

        [JsonPropertyName("complianceState")]
        public string? ComplianceState { get; set; }

        [JsonPropertyName("policyDefinitionAction")]
        public string? PolicyDefinitionAction { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("resourceId")]
        public string? ResourceId { get; set; }

        [JsonPropertyName("policyDefinitionName")]
        public string? PolicyDefinitionName { get; set; }
    }
}

public class ResourceContext
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}
