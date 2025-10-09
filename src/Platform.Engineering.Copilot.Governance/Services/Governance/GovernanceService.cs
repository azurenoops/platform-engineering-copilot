using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Governance.Configuration;
using Platform.Engineering.Copilot.Governance.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using GovernanceResult = Platform.Engineering.Copilot.Core.Contracts.GovernanceResult;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Governance service that implements comprehensive policy checks, approval workflows, and ATO compliance validation for platform operations.
/// This service enforces security policies, manages approval requests, and ensures compliance with organizational governance rules.
/// </summary>
public class GovernanceService : IGovernanceService
{
    private readonly ILogger<GovernanceService> _logger;
    private readonly GovernanceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ApprovalRequest> _pendingApprovals;
    private readonly List<AtoRule> _atoRules;

    /// <summary>
    /// Initializes a new instance of the GovernanceService with dependency injection support.
    /// Sets up policy enforcement, approval workflows, and loads ATO compliance rules.
    /// </summary>
    /// <param name="logger">Logger for governance operations and policy enforcement events</param>
    /// <param name="options">Configuration options for governance policies, approval timeouts, and compliance settings</param>
    /// <param name="httpClient">HTTP client for external service integration and Teams notifications</param>
    public GovernanceService(
        ILogger<GovernanceService> logger,
        IOptions<GovernanceOptions> options,
        HttpClient httpClient)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _pendingApprovals = new ConcurrentDictionary<string, ApprovalRequest>();
        _atoRules = new List<AtoRule>();

        // Load ATO rules - simplified for demo
        _ = Task.Run(async () => await LoadAtoRulesAsync());
    }

    public async Task<GovernanceResult> CheckPolicyAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        if (!_options.EnforcePolicies)
        {
            return new GovernanceResult
            {
                IsAllowed = true,
                Violations = Array.Empty<string>(),
                RequiresApproval = false
            };
        }

        var violations = new List<string>();
        var requiresApproval = false;
        string? reason = null;

        try
        {
            // Check ATO rules
            foreach (var rule in _atoRules)
            {
                if (IsRuleMatch(rule, toolCall))
                {
                    _logger.LogInformation("Tool call {ToolName} matches ATO rule {RuleId}", toolCall.Name, rule.RuleId);

                    switch (rule.Action.ToLowerInvariant())
                    {
                        case "block":
                        case "deny":
                            violations.Add($"Blocked by ATO rule {rule.RuleId}: {rule.Description}");
                            break;

                        case "require-approval":
                        case "approval":
                            requiresApproval = true;
                            reason = $"ATO rule {rule.RuleId}: {rule.Description}";
                            break;

                        case "warn":
                            _logger.LogWarning("Tool call {ToolName} triggered warning rule {RuleId}: {Description}", 
                                toolCall.Name, rule.RuleId, rule.Description);
                            break;
                    }
                }
            }

            // Additional Azure Policy checks could be added here
            // For now, we'll just log that we would check Azure Policy
            if (!string.IsNullOrEmpty(_options.AzureSubscriptionId))
            {
                _logger.LogDebug("Would check Azure Policy for subscription {SubscriptionId}", _options.AzureSubscriptionId);
                // TODO: Implement actual Azure Policy API calls
            }

            return new GovernanceResult
            {
                IsAllowed = violations.Count == 0,
                Violations = violations.ToArray(),
                RequiresApproval = requiresApproval && _options.RequireApprovals,
                Reason = reason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking governance policies for tool call {ToolName}", toolCall.Name);
            
            // Fail secure - block the call if we can't determine policy compliance
            return new GovernanceResult
            {
                IsAllowed = false,
                Violations = new[] { "Unable to verify policy compliance due to internal error" },
                RequiresApproval = false,
                Reason = "Policy check failed"
            };
        }
    }

    public async Task<ApprovalResult> RequestApprovalAsync(McpToolCall toolCall, string reason, CancellationToken cancellationToken = default)
    {
        var approvalId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ApprovalTimeoutMinutes);

        var approvalRequest = new ApprovalRequest
        {
            Id = approvalId,
            ToolName = toolCall.Name,
            Arguments = toolCall.Arguments,
            Reason = reason,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            RequestedBy = Environment.UserName // Could be enhanced to get actual user context
        };

        _pendingApprovals.TryAdd(approvalId, approvalRequest);

        try
        {
            // Send Teams notification if configured
            if (!string.IsNullOrEmpty(_options.TeamsWebhookUrl))
            {
                await SendTeamsNotificationAsync(approvalRequest, cancellationToken);
            }

            _logger.LogInformation("Approval request {ApprovalId} created for tool call {ToolName}", 
                approvalId, toolCall.Name);

            // For demo purposes, we'll simulate an immediate approval
            // In a real implementation, this would wait for external approval
            await Task.Delay(1000, cancellationToken); // Simulate processing time

            // Auto-approve for demo (remove in production)
            approvalRequest.IsApproved = true;
            approvalRequest.ApprovedBy = "system";
            approvalRequest.ApprovedAt = DateTime.UtcNow;
            approvalRequest.Comments = "Auto-approved for demo purposes";

            return new ApprovalResult
            {
                IsApproved = approvalRequest.IsApproved,
                ApprovalId = approvalId,
                ApprovedBy = approvalRequest.ApprovedBy,
                ApprovedAt = approvalRequest.ApprovedAt,
                Comments = approvalRequest.Comments
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting approval for tool call {ToolName}", toolCall.Name);
            
            return new ApprovalResult
            {
                IsApproved = false,
                ApprovalId = approvalId,
                Comments = "Error processing approval request"
            };
        }
    }

    private async Task LoadAtoRulesAsync()
    {
        try
        {
            if (!File.Exists(_options.AtoRulesPath))
            {
                _logger.LogWarning("ATO rules file not found at {Path}. Using empty ruleset.", _options.AtoRulesPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(_options.AtoRulesPath);
            var rules = JsonSerializer.Deserialize<AtoRule[]>(jsonContent);
            
            if (rules != null)
            {
                _atoRules.AddRange(rules);
                _logger.LogInformation("Loaded {Count} ATO rules from {Path}", rules.Length, _options.AtoRulesPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading ATO rules from {Path}", _options.AtoRulesPath);
        }
    }

    private bool IsRuleMatch(AtoRule rule, McpToolCall toolCall)
    {
        var match = rule.Match;

        // Check tool name match
        if (!string.IsNullOrEmpty(match.ToolName) && 
            !string.Equals(match.ToolName, toolCall.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check operation match (if specified)
        if (!string.IsNullOrEmpty(match.Operation))
        {
            // This could be enhanced to match specific operations within tools
            // For now, we'll just log it
            _logger.LogDebug("Operation matching not fully implemented: {Operation}", match.Operation);
        }

        // Check argument matches (if specified)
        if (match.Args != null && match.Args.Count > 0)
        {
            foreach (var argMatch in match.Args)
            {
                if (!toolCall.Arguments.ContainsKey(argMatch.Key))
                {
                    return false;
                }

                // Could implement more sophisticated argument matching here
                _logger.LogDebug("Argument matching not fully implemented: {Key}={Value}", argMatch.Key, argMatch.Value);
            }
        }

        return true;
    }

    private async Task SendTeamsNotificationAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var notification = new TeamsNotification
            {
                Summary = $"Approval Required: {request.ToolName}",
                Sections = new List<TeamsSection>
                {
                    new TeamsSection
                    {
                        ActivityTitle = "Tool Execution Approval Required",
                        ActivitySubtitle = $"Tool: {request.ToolName}",
                        Facts = new List<TeamsFact>
                        {
                            new TeamsFact { Name = "Tool Name", Value = request.ToolName },
                            new TeamsFact { Name = "Reason", Value = request.Reason },
                            new TeamsFact { Name = "Requested At", Value = request.RequestedAt.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                            new TeamsFact { Name = "Expires At", Value = request.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                            new TeamsFact { Name = "Approval ID", Value = request.Id }
                        },
                        Text = $"Arguments: {JsonSerializer.Serialize(request.Arguments, new JsonSerializerOptions { WriteIndented = true })}"
                    }
                }
            };

            var json = JsonSerializer.Serialize(notification);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_options.TeamsWebhookUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams notification sent for approval request {ApprovalId}", request.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send Teams notification for approval request {ApprovalId}. Status: {StatusCode}", 
                    request.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams notification for approval request {ApprovalId}", request.Id);
        }
    }
}