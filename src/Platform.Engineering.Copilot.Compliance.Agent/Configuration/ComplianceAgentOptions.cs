using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Compliance.Core.Configuration;

/// <summary>
/// Consolidated configuration options for the Compliance Agent
/// </summary>
public class ComplianceAgentOptions
{
    public const string SectionName = "ComplianceAgent";

    /// <summary>
    /// Azure OpenAI configuration for compliance AI capabilities
    /// </summary>
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();

    /// <summary>
    /// Gateway configuration for external integrations
    /// </summary>
    public GatewayOptions Gateway { get; set; } = new();

    /// <summary>
    /// Governance policy enforcement configuration
    /// </summary>
    public GovernanceOptions Governance { get; set; } = new();

    /// <summary>
    /// NIST controls service configuration
    /// </summary>
    public NistControlsOptions NistControls { get; set; } = new();
}

/// <summary>
/// Azure OpenAI configuration options for specialized compliance AI capabilities
/// </summary>
public class AzureOpenAIOptions
{
    /// <summary>
    /// Azure OpenAI API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI Endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Default deployment name for general chat
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Whether to use Azure Managed Identity for authentication
    /// </summary>
    public bool UseManagedIdentity { get; set; } = false;

    /// <summary>
    /// Chat completion deployment name
    /// </summary>
    public string ChatDeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Text embedding deployment name
    /// </summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// Specialized deployment for compliance analysis tasks
    /// </summary>
    public string ComplianceAnalysisDeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Specialized deployment for code analysis tasks
    /// </summary>
    public string CodeAnalysisDeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Specialized deployment for document processing and analysis
    /// </summary>
    public string DocumentAnalysisDeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Maximum tokens for chat completion requests
    /// </summary>
    [Range(1, 128000)]
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Temperature for AI responses (0.0 - 2.0)
    /// Lower = more focused, Higher = more creative
    /// </summary>
    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(10, 600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Enable retry logic for transient failures
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    [Range(1, 5)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Enable response caching
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes
    /// </summary>
    [Range(1, 1440)]
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Enable detailed logging of AI requests/responses
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Enable token usage tracking
    /// </summary>
    public bool EnableTokenTracking { get; set; } = true;

    /// <summary>
    /// Cost per 1K tokens for input (for cost tracking)
    /// </summary>
    public decimal InputTokenCostPer1K { get; set; } = 0.01m;

    /// <summary>
    /// Cost per 1K tokens for output (for cost tracking)
    /// </summary>
    public decimal OutputTokenCostPer1K { get; set; } = 0.03m;
}

/// <summary>
/// Gateway configuration options
/// </summary>
public class GatewayOptions
{
    /// <summary>
    /// Azure configuration
    /// </summary>
    public AzureGatewayOptions Azure { get; set; } = new();

    /// <summary>
    /// GitHub configuration
    /// </summary>
    public GitHubGatewayOptions GitHub { get; set; } = new();

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Azure gateway configuration
/// </summary>
public class AzureGatewayOptions
{
    /// <summary>
    /// Azure subscription ID
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// Azure tenant ID
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Whether to use managed identity
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Azure cloud environment (AzurePublic, AzureGovernment, etc.)
    /// </summary>
    public string CloudEnvironment { get; set; } = "AzureGovernment";

    /// <summary>
    /// Maximum retry attempts for Azure API calls
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 2;
}

/// <summary>
/// GitHub gateway configuration
/// </summary>
public class GitHubGatewayOptions
{
    /// <summary>
    /// GitHub Personal Access Token
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// GitHub organization name
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Default repository for operations
    /// </summary>
    public string? DefaultRepository { get; set; }

    /// <summary>
    /// Whether to use GitHub App authentication
    /// </summary>
    public bool UseAppAuthentication { get; set; } = false;

    /// <summary>
    /// GitHub App ID (if using app authentication)
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// GitHub App private key (if using app authentication)
    /// </summary>
    public string? PrivateKey { get; set; }
}

/// <summary>
/// Governance policy enforcement and compliance configuration
/// </summary>
public class GovernanceOptions
{
    /// <summary>
    /// Whether to enforce governance policies
    /// </summary>
    public bool EnforcePolicies { get; set; } = true;

    /// <summary>
    /// Whether to require approval for high-risk operations
    /// </summary>
    public bool RequireApproval { get; set; } = true;

    /// <summary>
    /// Approval timeout in minutes
    /// </summary>
    public int ApprovalTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// List of approved Azure regions
    /// </summary>
    public List<string> ApprovedRegions { get; set; } = new();

    /// <summary>
    /// Whether to enforce naming conventions
    /// </summary>
    public bool EnforceNamingConventions { get; set; } = true;

    /// <summary>
    /// Whether to enforce tagging requirements
    /// </summary>
    public bool EnforceTagging { get; set; } = true;

    /// <summary>
    /// Required tags for all resources
    /// </summary>
    public List<string> RequiredTags { get; set; } = new() { "Environment", "Owner", "CostCenter" };

    /// <summary>
    /// Whether to log all governance decisions for audit
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Whether to block operations that violate policies (vs. warn only)
    /// </summary>
    public bool BlockViolations { get; set; } = true;
}

/// <summary>
/// NIST controls service configuration
/// </summary>
public class NistControlsOptions
{
    public const string SectionName = "NistControls";

    /// <summary>
    /// Base URL for NIST OSCAL content repository
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json";

    /// <summary>
    /// Target NIST version (e.g., "rev5", null for latest)
    /// </summary>
    public string? TargetVersion { get; set; }

    /// <summary>
    /// HTTP client timeout in seconds
    /// </summary>
    [Range(10, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Cache duration in hours
    /// </summary>
    [Range(1, 168)] // 1 hour to 1 week
    public int CacheDurationHours { get; set; } = 24;

    /// <summary>
    /// Maximum retry attempts for failed requests
    /// </summary>
    [Range(1, 5)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds (exponential backoff base)
    /// </summary>
    [Range(1, 60)]
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Enable offline fallback mode
    /// </summary>
    public bool EnableOfflineFallback { get; set; } = true;

    /// <summary>
    /// Path to offline NIST controls JSON file (relative to content root)
    /// </summary>
    public string? OfflineFallbackPath { get; set; } = "Data/nist-800-53-fallback.json";

    /// <summary>
    /// Enable control caching in memory
    /// </summary>
    public bool EnableMemoryCache { get; set; } = true;

    /// <summary>
    /// Enable detailed logging of control lookups
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
