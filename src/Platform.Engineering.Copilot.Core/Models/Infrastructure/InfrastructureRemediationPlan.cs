using System;
using System.Collections.Generic;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Models.Infrastructure;

/// <summary>
/// Represents an infrastructure remediation plan for a specific compliance finding
/// </summary>
public class InfrastructureRemediationPlan
{
    /// <summary>
    /// Unique identifier for this remediation plan
    /// </summary>
    public string PlanId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The finding that this plan addresses
    /// </summary>
    public AtoFinding Finding { get; set; } = new();

    /// <summary>
    /// Azure resource that needs remediation
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Type of Azure resource (e.g., Microsoft.Storage/storageAccounts)
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Subscription where the resource is located
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Resource group containing the resource
    /// </summary>
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>
    /// List of specific actions to take for remediation
    /// </summary>
    public List<InfrastructureRemediationAction> Actions { get; set; } = new();

    /// <summary>
    /// Estimated time to complete remediation
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>
    /// Risk level of performing this remediation
    /// </summary>
    public RemediationRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Whether this remediation requires approval before execution
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Prerequisites that must be met before executing this plan
    /// </summary>
    public List<string> Prerequisites { get; set; } = new();

    /// <summary>
    /// Rollback plan in case remediation needs to be undone
    /// </summary>
    public InfrastructureRollbackPlan? RollbackPlan { get; set; }

    /// <summary>
    /// When this plan was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who/what created this plan
    /// </summary>
    public string CreatedBy { get; set; } = "ComplianceRemediationService";
}

/// <summary>
/// Represents a specific action to take during infrastructure remediation
/// </summary>
public class InfrastructureRemediationAction
{
    /// <summary>
    /// Unique identifier for this action
    /// </summary>
    public string ActionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of action (e.g., "UpdateProperty", "AddTag", "EnableFeature")
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Azure REST API operation to perform
    /// </summary>
    public string ApiOperation { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method for the API operation
    /// </summary>
    public string HttpMethod { get; set; } = "PATCH";

    /// <summary>
    /// Request body/payload for the API operation
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Expected HTTP response status codes for success
    /// </summary>
    public List<int> SuccessStatusCodes { get; set; } = new() { 200, 201, 202 };

    /// <summary>
    /// Order in which this action should be executed (1-based)
    /// </summary>
    public int ExecutionOrder { get; set; }

    /// <summary>
    /// Whether this action is critical for compliance
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Estimated time to execute this action
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Actions that must be executed before this one
    /// </summary>
    public List<string> DependsOn { get; set; } = new();

    /// <summary>
    /// Validation to perform after executing this action
    /// </summary>
    public InfrastructureActionValidation? Validation { get; set; }
}

/// <summary>
/// Risk levels for infrastructure remediation
/// </summary>
public enum RemediationRiskLevel
{
    Low,      // Safe changes, minimal impact
    Medium,   // Some impact, requires caution
    High,     // Significant impact, requires approval
    Critical  // Major impact, extensive testing required
}

/// <summary>
/// Validation to perform after an infrastructure action
/// </summary>
public class InfrastructureActionValidation
{
    /// <summary>
    /// Property path to check (e.g., "properties.encryption.keySource")
    /// </summary>
    public string PropertyPath { get; set; } = string.Empty;

    /// <summary>
    /// Expected value after the action
    /// </summary>
    public object? ExpectedValue { get; set; }

    /// <summary>
    /// Validation type (e.g., "Equals", "Contains", "NotNull")
    /// </summary>
    public string ValidationType { get; set; } = "Equals";

    /// <summary>
    /// How long to wait for the change to propagate before validation
    /// </summary>
    public TimeSpan ValidationDelay { get; set; } = TimeSpan.FromSeconds(30);
}