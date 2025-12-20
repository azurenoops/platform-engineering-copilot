using System.Text;
using System.Text.Json;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Audits;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Authorization;
using Platform.Engineering.Copilot.Core.Models.Audits;
using Platform.Engineering.Copilot.Core.Services.Audits;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Plugin for ATO Compliance operations including assessments, remediation, and evidence collection.
/// Supports NIST 800-53 compliance framework with automated remediation capabilities.
/// Split into partial classes for maintainability:
/// - CompliancePlugin.cs (base class, constructor, helpers)
/// - CompliancePlugin.Assessment.cs (run_compliance_assessment, get_control_family_details, get_compliance_status)
/// - CompliancePlugin.Remediation.cs (generate_remediation_plan, execute_remediation, validate_remediation, get_remediation_progress)
/// - CompliancePlugin.Reporting.cs (perform_risk_assessment, get_compliance_timeline, generate_compliance_certificate)
/// - CompliancePlugin.Evidence.cs (collect_evidence, generate_emass_package, generate_poam)
/// - CompliancePlugin.Security.cs (apply_security_hardening, validate_compliance_with_azure_policy)
/// - CompliancePlugin.Analytics.cs (get_compliance_history, get_assessment_audit_log, get_compliance_trends)
/// - CompliancePlugin.Helpers.cs (private helper methods)
/// </summary>
public partial class CompliancePlugin : BaseSupervisorPlugin
{
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly IRemediationEngine _remediationEngine;
    private readonly IAzureResourceService _azureResourceService;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly IMemoryCache _cache;
    private readonly ConfigService _configService;
    private readonly ComplianceAgentOptions _options;
    private readonly IUserContextService? _userContextService;
    private readonly IAuditLoggingService? _auditLoggingService;
    private readonly IDocumentGenerationService? _documentService;
    
    private const string LAST_SUBSCRIPTION_CACHE_KEY = "compliance_last_subscription";
    private const int ASSESSMENT_CACHE_HOURS = 4; // Cache assessments for 4 hours

    public CompliancePlugin(
        ILogger<CompliancePlugin> logger,
        Kernel kernel,
        IAtoComplianceEngine complianceEngine,
        IRemediationEngine remediationEngine,
        IAzureResourceService azureResourceService,
        AzureMcpClient azureMcpClient,
        IMemoryCache cache,
        ConfigService configService,
        IOptions<ComplianceAgentOptions> options,
        IDocumentGenerationService? documentService = null,
        IUserContextService? userContextService = null,
        IAuditLoggingService? auditLoggingService = null) : base(logger, kernel)
    {
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _remediationEngine = remediationEngine ?? throw new ArgumentNullException(nameof(remediationEngine));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _documentService = documentService; // Optional for document generation utilities
        _userContextService = userContextService; // Optional for HTTP mode
        _auditLoggingService = auditLoggingService; // Optional for HTTP mode
    }
    
    // ========== AUTHORIZATION HELPERS ==========
    
    /// <summary>
    /// Checks if the current user has the required role for an operation.
    /// Returns true if authorization check passes or if running in stdio mode (no auth).
    /// </summary>
    private bool CheckAuthorization(params string[] requiredRoles)
    {
        // If no user context service (stdio mode), allow operation
        if (_userContextService == null)
        {
            _logger.LogDebug("Running in stdio mode - authorization bypass");
            return true;
        }

        // Check if user is authenticated
        if (!_userContextService.IsAuthenticated())
        {
            _logger.LogWarning("Unauthorized access attempt - user not authenticated");
            return false;
        }

        // Check if user has any of the required roles
        foreach (var role in requiredRoles)
        {
            if (_userContextService.IsInRole(role))
            {
                _logger.LogInformation("User authorized with role: {Role}", role);
                return true;
            }
        }

        var userRoles = string.Join(", ", _userContextService.GetUserRoles());
        _logger.LogWarning(
            "User lacks required roles. Required: {RequiredRoles}, User has: {UserRoles}",
            string.Join(", ", requiredRoles),
            userRoles);
        
        return false;
    }

    /// <summary>
    /// Logs an audit entry for a compliance operation.
    /// </summary>
    private async Task LogAuditAsync(
        string eventType,
        string action,
        string resourceId,
        AuditSeverity severity = AuditSeverity.Informational,
        string? description = null,
        Dictionary<string, object>? metadata = null)
    {
        if (_auditLoggingService == null || _userContextService == null)
            return;

        try
        {
            await _auditLoggingService.LogAsync(new AuditLogEntry
            {
                EventType = eventType,
                EventCategory = "Compliance",
                ActorId = _userContextService.GetCurrentUserId(),
                ActorName = _userContextService.GetCurrentUserName(),
                ActorType = "User",
                Action = action,
                ResourceId = resourceId,
                ResourceType = "ComplianceOperation",
                Description = description ?? $"{action} on {resourceId}",
                Result = "Success",
                Severity = severity,
                Metadata = metadata?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>(),
                ComplianceContext = new ComplianceContext
                {
                    RequiresReview = severity >= AuditSeverity.High,
                    ControlIds = new List<string> { "AC-2", "AC-6", "AU-2", "AU-3" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry for {EventType}", eventType);
        }
    }
    
    /// <summary>
    /// Logs an audit event with simplified parameters (wrapper for LogAuditAsync).
    /// </summary>
    private async Task LogAuditEventAsync(string action, object data)
    {
        if (_auditLoggingService == null)
            return;
            
        var metadata = new Dictionary<string, object>
        {
            ["data"] = data
        };
        
        await LogAuditAsync(
            eventType: "ComplianceOperation",
            action: action,
            resourceId: action,
            severity: AuditSeverity.Informational,
            description: $"Compliance operation: {action}",
            metadata: metadata);
    }
    
    // ========== CONFIGURATION HELPERS ==========
    
    /// <summary>
    /// Gets the effective compliance framework to use (parameter or configured default)
    /// </summary>
    private string GetEffectiveFramework(string? requestedFramework = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedFramework))
        {
            _logger.LogDebug("Using requested framework: {Framework}", requestedFramework);
            return requestedFramework;
        }
        
        _logger.LogDebug("Using default framework from configuration: {Framework}", _options.DefaultFramework);
        return _options.DefaultFramework;
    }
    
    /// <summary>
    /// Gets the effective compliance baseline to use (parameter or configured default)
    /// </summary>
    private string GetEffectiveBaseline(string? requestedBaseline = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedBaseline))
        {
            _logger.LogDebug("Using requested baseline: {Baseline}", requestedBaseline);
            return requestedBaseline;
        }
        
        _logger.LogDebug("Using default baseline from configuration: {Baseline}", _options.DefaultBaseline);
        return _options.DefaultBaseline;
    }
    
    // ========== SUBSCRIPTION LOOKUP HELPER ==========
    
    /// <summary>
    /// Stores the last used subscription ID in cache AND persistent config file for session continuity
    /// </summary>
    private void SetLastUsedSubscription(string subscriptionId)
    {
        // Store in memory cache for current session
        _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
        
        // ALSO store in persistent config file for cross-session persistence
        try
        {
            _configService.SetDefaultSubscription(subscriptionId);
            _logger.LogInformation("Stored subscription in persistent config: {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist subscription to config file, will only use cache");
        }
    }
    
    /// <summary>
    /// Gets the last used subscription ID from cache, or persistent config file if cache is empty
    /// </summary>
    private string? GetLastUsedSubscription()
    {
        // Try cache first (fastest)
        if (_cache.TryGetValue<string>(LAST_SUBSCRIPTION_CACHE_KEY, out var subscriptionId))
        {
            _logger.LogDebug("Retrieved last used subscription from cache: {SubscriptionId}", subscriptionId);
            return subscriptionId;
        }
        
        // Fall back to persistent config file (survives restarts)
        try
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Retrieved subscription from persistent config: {SubscriptionId}", subscriptionId);
                // Populate cache for future requests in this session
                _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
                return subscriptionId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read subscription from config file");
        }
        
        return null;
    }
    
    /// <summary>
    /// Resolves a subscription identifier to a GUID. Accepts either a GUID or a friendly name.
    /// If null/empty, tries to use the last used subscription from session context.
    /// First queries Azure for subscription by name, then falls back to static dictionary.
    /// </summary>
    private async Task<string> ResolveSubscriptionIdAsync(string? subscriptionIdOrName)
    {
        // If no subscription provided, try to use last used subscription
        if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
        {
            var lastUsed = GetLastUsedSubscription();
            if (!string.IsNullOrWhiteSpace(lastUsed))
            {
                _logger.LogInformation("Using last used subscription from session: {SubscriptionId}", lastUsed);
                return lastUsed;
            }
            throw new ArgumentException("Subscription ID or name is required. No previous subscription found in session.", nameof(subscriptionIdOrName));
        }
        
        // Check if it's already a valid GUID
        if (Guid.TryParse(subscriptionIdOrName, out _))
        {
            SetLastUsedSubscription(subscriptionIdOrName);
            return subscriptionIdOrName;
        }
        
        // Try to query Azure for subscription by name
        try
        {
            var subscription = await _azureResourceService.GetSubscriptionByNameAsync(subscriptionIdOrName);
            _logger.LogInformation("Resolved subscription name '{Name}' to ID '{SubscriptionId}' via Azure API", 
                subscriptionIdOrName, subscription.SubscriptionId);
            SetLastUsedSubscription(subscription.SubscriptionId);
            return subscription.SubscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve subscription '{Name}' via Azure API, trying static lookup", 
                subscriptionIdOrName);
        }
               
        // If not found, throw with helpful message
        throw new ArgumentException(
            $"Subscription '{subscriptionIdOrName}' not found. " +
            $"Or provide a valid GUID.", 
            nameof(subscriptionIdOrName));
    }

    #region Database Caching Helpers

    /// <summary>
    /// Retrieves a cached compliance assessment from the database if available and not expired.
    /// </summary>
    private async Task<ComplianceAssessmentWithFindings?> GetCachedAssessmentAsync(
        string subscriptionId, 
        string? resourceGroupName, 
        CancellationToken cancellationToken)
    {
        return await _complianceEngine.GetCachedAssessmentAsync(
            subscriptionId,
            resourceGroupName,
            ASSESSMENT_CACHE_HOURS,
            cancellationToken);
    }

    /// <summary>
    /// Formats a cached assessment into the same JSON structure as a fresh assessment.
    /// </summary>
    private string FormatCachedAssessment(ComplianceAssessmentWithFindings cached, string scope, TimeSpan cacheAge)
    {
        try
        {
            // Deserialize the stored assessment data
            var assessmentData = JsonSerializer.Deserialize<JsonElement>(cached.Results ?? "{}");
            
            // Add cache metadata to the response
            var response = new Dictionary<string, object?>
            {
                ["success"] = true,
                ["cached"] = true,
                ["cacheAge"] = $"{Math.Round(cacheAge.TotalMinutes, 1)} minutes",
                ["cachedAt"] = cached.CompletedAt,
                ["assessmentId"] = cached.Id,
                ["subscriptionId"] = cached.SubscriptionId,
                ["resourceGroupName"] = cached.ResourceGroupName,
                ["scope"] = scope,
                ["timestamp"] = cached.CompletedAt,
                ["duration"] = cached.Duration
            };

            // Merge the original assessment data
            if (assessmentData.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in assessmentData.EnumerateObject())
                {
                    if (!response.ContainsKey(property.Name))
                    {
                        response[property.Name] = property.Value;
                    }
                }
            }

            // Add cache notice to formatted_output if it exists
            if (response.TryGetValue("formatted_output", out var formattedOutput) && formattedOutput is JsonElement elem)
            {
                var output = elem.GetString() ?? "";
                var cacheNotice = $"\n\n---\n\n🔄 **CACHED RESULTS** (Age: {Math.Round(cacheAge.TotalMinutes, 1)} minutes, expires in {Math.Round((ASSESSMENT_CACHE_HOURS * 60) - cacheAge.TotalMinutes, 1)} minutes)\n";
                response["formatted_output"] = output.Replace("# 📊 NIST 800-53 COMPLIANCE ASSESSMENT", 
                    $"# 📊 NIST 800-53 COMPLIANCE ASSESSMENT{cacheNotice}");
            }

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to format cached assessment - will run fresh assessment");
            throw; // This will cause the caller to run a fresh assessment
        }
    }

    /// <summary>
    /// Calculates trend direction and rate of change from a list of values over time.
    /// </summary>
    /// <param name="values">List of values in chronological order</param>
    /// <returns>Trend analysis with direction and change rate</returns>
    private (string Direction, double ChangeRate) CalculateTrend(List<double> values)
    {
        if (values.Count < 2)
        {
            return ("stable", 0);
        }

        // Calculate simple linear regression slope
        var n = values.Count;
        var xValues = Enumerable.Range(0, n).Select(i => (double)i).ToList();
        
        var xMean = xValues.Average();
        var yMean = values.Average();
        
        var numerator = xValues.Zip(values, (x, y) => (x - xMean) * (y - yMean)).Sum();
        var denominator = xValues.Sum(x => Math.Pow(x - xMean, 2));
        
        var slope = denominator != 0 ? numerator / denominator : 0;
        
        // Determine direction based on slope
        // Threshold: 0.5% change per assessment is considered "stable"
        var direction = Math.Abs(slope) < 0.5 ? "stable" :
                       slope > 0 ? "improving" : "declining";
        
        return (direction, slope);
    }

    #endregion
}
