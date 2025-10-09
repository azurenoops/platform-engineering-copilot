namespace Platform.Engineering.Copilot.Core.Models;

public class GovernanceResult
{
    public bool IsApproved { get; set; }
    public GovernancePolicyDecision PolicyDecision { get; set; } = GovernancePolicyDecision.Unknown;
    public List<string> Messages { get; set; } = new();
    public List<PolicyViolation> PolicyViolations { get; set; } = new();
    public string RequestId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? ApprovalWorkflowId { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string? EvaluatedBy { get; set; }
}

public class PreFlightGovernanceResult : GovernanceResult
{
    public bool RequiresApproval => PolicyDecision == GovernancePolicyDecision.RequiresApproval;
    public bool IsDenied => PolicyDecision == GovernancePolicyDecision.Deny;
    public bool IsAutoApproved => PolicyDecision == GovernancePolicyDecision.Allow;
    public List<string> RequiredApprovers { get; set; } = new();
}

public class PostFlightGovernanceResult : GovernanceResult
{
    public bool ComplianceViolated { get; set; }
    public List<ComplianceViolation> ComplianceViolations { get; set; } = new();
    public bool RequiresRemediation { get; set; }
    public List<string> RemediationActions { get; set; } = new();
}

public class PolicyViolation
{
    public string PolicyName { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public PolicyViolationSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public Dictionary<string, object> PolicyParameters { get; set; } = new();
}

public class ComplianceViolation
{
    public string ComplianceFramework { get; set; } = string.Empty; // e.g., "ATO", "SOC2", "GDPR"
    public string ControlId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceViolationSeverity Severity { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public List<string> RemediationSteps { get; set; } = new();
}

public class ApprovalWorkflow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ToolCallId { get; set; } = string.Empty;
    public McpToolCall OriginalToolCall { get; set; } = new() { Name = "", Arguments = new Dictionary<string, object?>() };
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public List<string> RequiredApprovers { get; set; } = new();
    public List<ApprovalDecision> Decisions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Justification { get; set; } = string.Empty;
    public int Priority { get; set; } = 1; // 1-5, 5 being highest priority

    // Infrastructure provisioning approval properties
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public string Reason { get; set; } = string.Empty;
    public List<string> PolicyViolations { get; set; } = new();
    public string RequestPayload { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalComments { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class ApprovalDecision
{
    public string ApproverId { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public ApprovalDecisionType Decision { get; set; }
    public string Comments { get; set; } = string.Empty;
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class AzurePolicyEvaluation
{
    public string PolicyDefinitionId { get; set; } = string.Empty;
    public string PolicyAssignmentId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public PolicyComplianceState ComplianceState { get; set; }
    public string PolicyEffect { get; set; } = string.Empty; // "deny", "audit", "append", etc.
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> PolicyParameters { get; set; } = new();
    public string? NonComplianceMessage { get; set; }
}

public enum GovernancePolicyDecision
{
    Unknown,
    Allow,
    Deny,
    RequiresApproval,
    AuditOnly
}

public enum PolicyViolationSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ComplianceViolationSeverity
{
    Informational = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Escalated,
    Expired,
    Cancelled
}

public enum ApprovalDecisionType
{
    Approve,
    Reject,
    RequestMoreInfo,
    Escalate
}

public enum PolicyComplianceState
{
    Unknown,
    Compliant,
    NonCompliant,
    Conflict,
    Exempt
}