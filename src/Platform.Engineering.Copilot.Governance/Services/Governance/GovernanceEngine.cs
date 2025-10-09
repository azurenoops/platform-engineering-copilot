using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Governance.Configuration;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Central governance engine that orchestrates policy enforcement, approval workflows,
/// and compliance validation for infrastructure provisioning and platform operations
/// </summary>
public class GovernanceEngine : IGovernanceEngine
{
    private readonly ILogger<GovernanceEngine> _logger;
    private readonly IAzurePolicyService _policyService;
    private readonly GovernanceOptions _options;
    private readonly ConcurrentDictionary<string, ApprovalWorkflow> _approvalWorkflows;

    // Azure naming convention patterns
    private static readonly Dictionary<string, NamingConvention> NamingConventions = new()
    {
        ["vnet"] = new("vnet-{env}-{location}-{project}-{sequence}", 1, 64, new Regex(@"^[a-z0-9\-]+$")),
        ["storage-account"] = new("st{env}{project}{sequence}", 3, 24, new Regex(@"^[a-z0-9]+$")),
        ["key-vault"] = new("kv-{env}-{project}-{sequence}", 3, 24, new Regex(@"^[a-zA-Z0-9\-]+$")),
        ["nsg"] = new("nsg-{env}-{location}-{project}", 1, 80, new Regex(@"^[a-z0-9\-]+$")),
        ["load-balancer"] = new("lb-{env}-{location}-{project}", 1, 80, new Regex(@"^[a-z0-9\-]+$")),
        ["app-gateway"] = new("agw-{env}-{location}-{project}", 1, 80, new Regex(@"^[a-z0-9\-]+$")),
        ["log-analytics"] = new("log-{env}-{project}", 4, 63, new Regex(@"^[a-zA-Z0-9\-]+$")),
    };

    // Approved Azure regions for infrastructure
    private static readonly HashSet<string> ApprovedRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        "eastus", "eastus2", "westus", "westus2", "centralus",
        "usgovvirginia", "usgovarizona", "usdodeast", "usdodcentral" // Azure Government
    };

    // Region aliases for flexibility
    private static readonly Dictionary<string, string> RegionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["east"] = "eastus",
        ["west"] = "westus",
        ["central"] = "centralus",
        ["virginia"] = "usgovvirginia",
        ["arizona"] = "usgovarizona"
    };

    public GovernanceEngine(
        ILogger<GovernanceEngine> logger,
        IAzurePolicyService policyService,
        IOptions<GovernanceOptions> options)
    {
        _logger = logger;
        _policyService = policyService;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _approvalWorkflows = new ConcurrentDictionary<string, ApprovalWorkflow>();
    }

    public async Task<PreFlightGovernanceResult> EvaluatePreFlightChecksAsync(
        InfrastructureProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting pre-flight governance evaluation for {ResourceType} '{ResourceName}'",
            request.ResourceType, request.ResourceName);

        var result = new PreFlightGovernanceResult
        {
            RequestId = Guid.NewGuid().ToString(),
            EvaluatedBy = "GovernanceEngine"
        };

        try
        {
            var violations = new List<string>();
            var warnings = new List<string>();

            // 1. Validate resource naming conventions
            var namingResult = await ValidateResourceNamingAsync(
                request.ResourceType,
                request.ResourceName,
                request.Environment);

            if (!namingResult.IsValid)
            {
                violations.AddRange(namingResult.Errors.Select(e => $"Naming: {e}"));
                _logger.LogWarning("Naming validation failed for {ResourceName}: {Errors}",
                    request.ResourceName, string.Join(", ", namingResult.Errors));
            }

            warnings.AddRange(namingResult.Warnings.Select(w => $"Naming: {w}"));

            // 2. Validate region availability
            var regionResult = await ValidateRegionAvailabilityAsync(
                request.Location,
                request.ResourceType,
                cancellationToken);

            if (!regionResult.IsAvailable)
            {
                violations.Add($"Region '{request.Location}' is not available for {request.ResourceType}");
            }
            else if (!regionResult.IsApproved)
            {
                violations.Add($"Region '{request.Location}' is not in the approved regions list. Approved: {string.Join(", ", ApprovedRegions.Take(5))}");
            }

            // 3. Validate required tags
            if (!ValidateRequiredTags(request.Tags, out var tagErrors))
            {
                violations.AddRange(tagErrors.Select(e => $"Tagging: {e}"));
            }

            // 4. Check Azure Policy compliance (delegate to policy service)
            var toolCall = ConvertToToolCall(request);
            var policyResult = await _policyService.EvaluatePreFlightPoliciesAsync(toolCall, cancellationToken);

            if (policyResult.PolicyViolations.Any())
            {
                violations.AddRange(policyResult.PolicyViolations.Select(v =>
                    $"Policy: {v.PolicyName} - {v.Description}"));
            }

            result.PolicyViolations.AddRange(policyResult.PolicyViolations);

            // 5. Determine overall decision
            if (violations.Any())
            {
                result.PolicyDecision = GovernancePolicyDecision.Deny;
                result.IsApproved = false;
                result.Messages.AddRange(violations);
            }
            else if (policyResult.PolicyDecision == GovernancePolicyDecision.RequiresApproval ||
                     request.Environment.Equals("prod", StringComparison.OrdinalIgnoreCase))
            {
                result.PolicyDecision = GovernancePolicyDecision.RequiresApproval;
                result.IsApproved = false;
                result.RequiredApprovers = await GetRequiredApproversForEnvironmentAsync(request.Environment);
                result.Messages.Add("Manual approval required for production infrastructure changes");

                // Create approval workflow
                var workflow = await CreateApprovalWorkflowAsync(
                    request,
                    "Production infrastructure change requires approval",
                    violations,
                    cancellationToken);

                result.ApprovalWorkflowId = workflow.Id;
            }
            else
            {
                result.PolicyDecision = GovernancePolicyDecision.Allow;
                result.IsApproved = true;
                result.Messages.Add("All pre-flight checks passed");
            }

            if (warnings.Any())
            {
                result.Messages.AddRange(warnings.Select(w => $"Warning: {w}"));
            }

            _logger.LogInformation("Pre-flight evaluation completed: Decision={Decision}, Approved={Approved}, Violations={ViolationCount}",
                result.PolicyDecision, result.IsApproved, violations.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pre-flight governance evaluation");

            return new PreFlightGovernanceResult
            {
                RequestId = Guid.NewGuid().ToString(),
                EvaluatedBy = "GovernanceEngine",
                PolicyDecision = GovernancePolicyDecision.Deny,
                IsApproved = false,
                Messages = new List<string> { $"Governance evaluation failed: {ex.Message}" }
            };
        }
    }

    public async Task<PostFlightGovernanceResult> EvaluatePostFlightChecksAsync(
        InfrastructureProvisioningRequest request,
        InfrastructureProvisionResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting post-flight governance evaluation for {ResourceId}",
            result.ResourceId);

        var postFlightResult = new PostFlightGovernanceResult
        {
            IsCompliant = true,
            AuditLogged = true
        };

        try
        {
            // 1. Verify resource was created successfully
            if (!result.Success)
            {
                postFlightResult.IsCompliant = false;
                postFlightResult.ComplianceIssues.Add("Resource provisioning failed");
                return postFlightResult;
            }

            // 2. Validate tagging compliance
            if (request.Tags == null || !request.Tags.Any())
            {
                postFlightResult.TaggingCompliant = false;
                postFlightResult.ComplianceIssues.Add("Resource created without required tags");
            }
            else
            {
                postFlightResult.TaggingCompliant = ValidateRequiredTags(request.Tags, out _);
            }

            // 3. Validate security configuration (resource-specific checks)
            postFlightResult.SecurityCompliant = await ValidateSecurityConfigurationAsync(
                request.ResourceType,
                result.Properties,
                cancellationToken);

            if (!postFlightResult.SecurityCompliant)
            {
                postFlightResult.SecurityFindings.Add("Resource does not meet security baseline requirements");
            }

            // 4. Check for immediate remediation needs
            if (!postFlightResult.TaggingCompliant || !postFlightResult.SecurityCompliant)
            {
                postFlightResult.IsCompliant = false;
                postFlightResult.RemediationRequired = "Apply tags and security configurations post-deployment";
            }

            // 5. Log audit trail
            await LogAuditTrailAsync(request, result, postFlightResult, cancellationToken);

            _logger.LogInformation("Post-flight evaluation completed: Compliant={IsCompliant}, Security={SecurityCompliant}, Tagging={TaggingCompliant}",
                postFlightResult.IsCompliant, postFlightResult.SecurityCompliant, postFlightResult.TaggingCompliant);

            return postFlightResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during post-flight governance evaluation");

            return new PostFlightGovernanceResult
            {
                IsCompliant = false,
                AuditLogged = false,
                ComplianceIssues = new List<string> { $"Post-flight evaluation failed: {ex.Message}" }
            };
        }
    }

    public Task<NamingValidationResult> ValidateResourceNamingAsync(
        string resourceType,
        string resourceName,
        string environment)
    {
        var result = new NamingValidationResult();

        var normalizedType = resourceType.ToLowerInvariant()
            .Replace("microsoft.", "")
            .Replace("/", "-");

        if (!NamingConventions.TryGetValue(normalizedType, out var convention))
        {
            result.Warnings.Add($"No naming convention defined for resource type '{resourceType}'. Using generic validation.");
            convention = new NamingConvention("{env}-{project}-{resource}", 1, 80, new Regex(@"^[a-zA-Z0-9\-_]+$"));
        }

        // Validate length
        if (resourceName.Length < convention.MinLength)
        {
            result.Errors.Add($"Name too short (minimum {convention.MinLength} characters)");
            result.IsValid = false;
        }

        if (resourceName.Length > convention.MaxLength)
        {
            result.Errors.Add($"Name too long (maximum {convention.MaxLength} characters)");
            result.IsValid = false;
        }

        // Validate pattern
        if (!convention.Pattern.IsMatch(resourceName))
        {
            result.Errors.Add($"Name contains invalid characters. Pattern: {convention.Pattern}");
            result.IsValid = false;
        }

        // Validate environment prefix/infix if required
        if (!resourceName.Contains(environment, StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Resource name should include environment identifier '{environment}'");
        }

        // Generate suggested name if validation failed
        if (!result.IsValid && !result.Errors.Any(e => e.Contains("too short")))
        {
            result.SuggestedName = GenerateSuggestedName(resourceType, environment, resourceName);
        }

        result.IsValid = !result.Errors.Any();

        return Task.FromResult(result);
    }

    public Task<RegionValidationResult> ValidateRegionAvailabilityAsync(
        string location,
        string resourceType,
        CancellationToken cancellationToken = default)
    {
        var result = new RegionValidationResult();

        // Normalize region name
        var normalizedLocation = NormalizeRegionName(location);

        // Check if region is in approved list
        result.IsApproved = ApprovedRegions.Contains(normalizedLocation);
        result.IsAvailable = true; // Assume available unless specific check fails

        if (!result.IsApproved)
        {
            result.ReasonUnavailable = $"Region '{location}' is not in the approved regions list for this organization";
            result.AlternativeRegions = ApprovedRegions.Take(5).ToList();
        }

        // TODO: Add actual Azure availability check using Azure SDK
        // For now, we do basic checks

        _logger.LogDebug("Region validation for '{Location}': Approved={Approved}, Available={Available}",
            normalizedLocation, result.IsApproved, result.IsAvailable);

        return Task.FromResult(result);
    }

    public async Task<ApprovalWorkflow> CreateApprovalWorkflowAsync(
        InfrastructureProvisioningRequest request,
        string reason,
        List<string> violations,
        CancellationToken cancellationToken = default)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            ResourceType = request.ResourceType,
            ResourceName = request.ResourceName,
            ResourceGroupName = request.ResourceGroupName,
            Location = request.Location,
            Environment = request.Environment,
            RequestedBy = request.RequestedBy,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Status = ApprovalStatus.Pending,
            Reason = reason,
            PolicyViolations = violations,
            RequiredApprovers = await GetRequiredApproversForEnvironmentAsync(request.Environment),
            RequestPayload = System.Text.Json.JsonSerializer.Serialize(request)
        };

        _approvalWorkflows.TryAdd(workflow.Id, workflow);

        _logger.LogInformation("Created approval workflow {WorkflowId} for {ResourceType} '{ResourceName}'",
            workflow.Id, request.ResourceType, request.ResourceName);

        return workflow;
    }

    public Task<ApprovalWorkflow?> GetApprovalWorkflowAsync(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        _approvalWorkflows.TryGetValue(workflowId, out var workflow);
        return Task.FromResult(workflow);
    }

    public Task<List<ApprovalWorkflow>> ListPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = _approvalWorkflows.Values
            .Where(w => w.Status == ApprovalStatus.Pending && w.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(w => w.RequestedAt)
            .ToList();

        return Task.FromResult(pending);
    }

    public async Task<bool> ApproveWorkflowAsync(
        string workflowId,
        string approvedBy,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        if (!_approvalWorkflows.TryGetValue(workflowId, out var workflow))
        {
            _logger.LogWarning("Approval workflow {WorkflowId} not found", workflowId);
            return false;
        }

        if (workflow.Status != ApprovalStatus.Pending)
        {
            _logger.LogWarning("Workflow {WorkflowId} is not in pending status: {Status}",
                workflowId, workflow.Status);
            return false;
        }

        if (workflow.ExpiresAt < DateTime.UtcNow)
        {
            workflow.Status = ApprovalStatus.Expired;
            _logger.LogWarning("Workflow {WorkflowId} has expired", workflowId);
            return false;
        }

        workflow.Status = ApprovalStatus.Approved;
        workflow.ApprovedBy = approvedBy;
        workflow.ApprovedAt = DateTime.UtcNow;
        workflow.ApprovalComments = comments;

        _logger.LogInformation("Workflow {WorkflowId} approved by {ApprovedBy}", workflowId, approvedBy);

        return true;
    }

    public async Task<bool> RejectWorkflowAsync(
        string workflowId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_approvalWorkflows.TryGetValue(workflowId, out var workflow))
        {
            _logger.LogWarning("Approval workflow {WorkflowId} not found", workflowId);
            return false;
        }

        if (workflow.Status != ApprovalStatus.Pending)
        {
            _logger.LogWarning("Workflow {WorkflowId} is not in pending status: {Status}",
                workflowId, workflow.Status);
            return false;
        }

        workflow.Status = ApprovalStatus.Rejected;
        workflow.RejectedBy = rejectedBy;
        workflow.RejectedAt = DateTime.UtcNow;
        workflow.RejectionReason = reason;

        _logger.LogInformation("Workflow {WorkflowId} rejected by {RejectedBy}: {Reason}",
            workflowId, rejectedBy, reason);

        return true;
    }

    #region Private Helper Methods

    private bool ValidateRequiredTags(Dictionary<string, string>? tags, out List<string> errors)
    {
        errors = new List<string>();

        if (tags == null || !tags.Any())
        {
            errors.Add("No tags provided");
            return false;
        }

        var requiredTags = new[] { "environment", "project", "owner", "cost-center" };

        foreach (var requiredTag in requiredTags)
        {
            if (!tags.ContainsKey(requiredTag) || string.IsNullOrWhiteSpace(tags[requiredTag]))
            {
                errors.Add($"Required tag '{requiredTag}' is missing or empty");
            }
        }

        return !errors.Any();
    }

    private Task<bool> ValidateSecurityConfigurationAsync(
        string resourceType,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken)
    {
        // TODO: Implement resource-specific security validation
        // For now, return true (placeholder)
        return Task.FromResult(true);
    }

    private Task LogAuditTrailAsync(
        InfrastructureProvisioningRequest request,
        InfrastructureProvisionResult result,
        PostFlightGovernanceResult postFlightResult,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AUDIT: Resource provisioned - Type={ResourceType}, Name={ResourceName}, RG={ResourceGroup}, " +
            "Location={Location}, RequestedBy={RequestedBy}, Compliant={IsCompliant}",
            request.ResourceType, request.ResourceName, request.ResourceGroupName,
            request.Location, request.RequestedBy, postFlightResult.IsCompliant);

        // TODO: Send to audit log storage (Log Analytics, Event Hub, etc.)

        return Task.CompletedTask;
    }

    private Task<List<string>> GetRequiredApproversForEnvironmentAsync(string environment)
    {
        // Map environments to required approvers
        var approvers = environment.ToLowerInvariant() switch
        {
            "prod" or "production" => new List<string> { "platform-admin", "security-team" },
            "staging" or "stage" => new List<string> { "platform-admin" },
            _ => new List<string>()
        };

        return Task.FromResult(approvers);
    }

    private string NormalizeRegionName(string location)
    {
        var normalized = location.ToLowerInvariant().Replace(" ", "");

        // Check for aliases
        if (RegionAliases.TryGetValue(normalized, out var officialName))
        {
            return officialName;
        }

        return normalized;
    }

    private string GenerateSuggestedName(string resourceType, string environment, string baseName)
    {
        var sanitized = Regex.Replace(baseName, @"[^a-zA-Z0-9\-]", "");
        var envPrefix = environment.ToLowerInvariant().Substring(0, Math.Min(4, environment.Length));

        return resourceType.ToLowerInvariant() switch
        {
            "storage-account" => $"st{envPrefix}{sanitized}".ToLowerInvariant().Substring(0, Math.Min(24, $"st{envPrefix}{sanitized}".Length)),
            "key-vault" => $"kv-{envPrefix}-{sanitized}".ToLowerInvariant().Substring(0, Math.Min(24, $"kv-{envPrefix}-{sanitized}".Length)),
            _ => $"{envPrefix}-{sanitized}".ToLowerInvariant()
        };
    }

    private McpToolCall ConvertToToolCall(InfrastructureProvisioningRequest request)
    {
        return new McpToolCall
        {
            Name = "provision_infrastructure",
            Arguments = new Dictionary<string, object?>
            {
                ["resource_type"] = request.ResourceType,
                ["resource_name"] = request.ResourceName,
                ["resource_group"] = request.ResourceGroupName,
                ["location"] = request.Location,
                ["environment"] = request.Environment
            }
        };
    }

    #endregion
}

/// <summary>
/// Naming convention rule for Azure resources
/// </summary>
internal record NamingConvention(
    string Template,
    int MinLength,
    int MaxLength,
    Regex Pattern);
