using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Governance.Configuration;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Health check for NIST controls service availability and data freshness
/// </summary>
public class NistControlsHealthCheck : IHealthCheck
{
    private readonly INistControlsService _nistControlsService;
    private readonly ILogger<NistControlsHealthCheck> _logger;
    private readonly NistControlsOptions _options;

    public NistControlsHealthCheck(
        INistControlsService nistControlsService,
        ILogger<NistControlsHealthCheck> logger,
        IOptions<NistControlsOptions> options)
    {
        _nistControlsService = nistControlsService ?? throw new ArgumentNullException(nameof(nistControlsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing NIST controls service health check");

            var startTime = DateTime.UtcNow;
            
            // Test basic connectivity and data availability
            var version = await _nistControlsService.GetVersionAsync(cancellationToken);
            
            if (string.IsNullOrEmpty(version) || version == "Unknown")
            {
                return HealthCheckResult.Degraded("NIST controls service is available but version information is unavailable", null,
                    new Dictionary<string, object>
                    {
                        ["version"] = version ?? "null",
                        ["timestamp"] = DateTime.UtcNow,
                        ["response_time_ms"] = (DateTime.UtcNow - startTime).TotalMilliseconds
                    });
            }

            // Test a few key controls to ensure data integrity
            var testControls = new[] { "AC-3", "SC-13", "AU-2" };
            var validControls = 0;

            foreach (var controlId in testControls)
            {
                try
                {
                    var isValid = await _nistControlsService.ValidateControlIdAsync(controlId, cancellationToken);
                    if (isValid) validControls++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to validate test control {ControlId}", controlId);
                }
            }

            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var healthData = new Dictionary<string, object>
            {
                ["version"] = version,
                ["valid_test_controls"] = $"{validControls}/{testControls.Length}",
                ["response_time_ms"] = responseTime,
                ["timestamp"] = DateTime.UtcNow,
                ["cache_duration_hours"] = _options.CacheDurationHours,
                ["offline_fallback_enabled"] = _options.EnableOfflineFallback
            };

            if (validControls == testControls.Length && responseTime < 5000) // 5 second threshold
            {
                _logger.LogDebug("NIST controls service health check passed");
                return HealthCheckResult.Healthy("NIST controls service is fully operational", healthData);
            }
            
            if (validControls > 0)
            {
                _logger.LogWarning("NIST controls service health check shows degraded performance");
                return HealthCheckResult.Degraded($"NIST controls service is partially operational ({validControls}/{testControls.Length} test controls valid)", null, healthData);
            }

            _logger.LogError("NIST controls service health check failed - no test controls validated");
            return HealthCheckResult.Unhealthy("NIST controls service is not operational", null, healthData);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("NIST controls service health check timed out");
            return HealthCheckResult.Degraded("NIST controls service health check timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NIST controls service health check failed with exception");
            return HealthCheckResult.Unhealthy("NIST controls service health check failed", ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                });
        }
    }
}

/// <summary>
/// Validation service for ATO compliance data and configuration
/// </summary>
public interface IComplianceValidationService
{
    Task<ValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateControlMappingsAsync(CancellationToken cancellationToken = default);
    ValidationResult ValidateAtoScanRequest(AtoScanRequest request);
    ValidationResult ValidateAtoFinding(AtoFinding finding);
}

/// <summary>
/// Compliance validation service that provides comprehensive validation of NIST controls configuration, ATO findings, and security compliance rules.
/// This service ensures data integrity and validates compliance configurations across the platform.
/// </summary>
public class ComplianceValidationService : IComplianceValidationService
{
    private readonly INistControlsService _nistControlsService;
    private readonly ILogger<ComplianceValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the ComplianceValidationService with dependency injection support.
    /// Sets up validation capabilities for NIST controls and ATO compliance requirements.
    /// </summary>
    /// <param name="nistControlsService">Service for accessing NIST controls catalog and validation data</param>
    /// <param name="logger">Logger for validation operations and compliance checking events</param>
    public ComplianceValidationService(
        INistControlsService nistControlsService,
        ILogger<ComplianceValidationService> logger)
    {
        _nistControlsService = nistControlsService ?? throw new ArgumentNullException(nameof(nistControlsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            // Test NIST service connectivity
            var version = await _nistControlsService.GetVersionAsync(cancellationToken);
            if (string.IsNullOrEmpty(version) || version == "Unknown")
            {
                result.AddError("NIST controls service is not available or not returning version information");
            }
            else
            {
                result.AddInfo($"NIST controls version: {version}");
            }

            // Test catalog availability
            var catalog = await _nistControlsService.GetCatalogAsync(cancellationToken);
            if (catalog?.Groups == null || !catalog.Groups.Any())
            {
                result.AddError("NIST catalog is not available or empty");
            }
            else
            {
                var controlCount = catalog.Groups.SelectMany(g => g.Controls ?? []).Count();
                result.AddInfo($"NIST catalog contains {controlCount} controls");
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Configuration validation failed: {ex.Message}");
            _logger.LogError(ex, "Configuration validation failed");
        }

        return result;
    }

    public async Task<ValidationResult> ValidateControlMappingsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };
        
        // Define all control IDs used in the system
        var systemControlIds = new[]
        {
            "SC-13", "SC-28", "AC-3", "AC-6", "SC-7", "AC-4",
            "AU-2", "SI-4", "CP-9", "CP-10", "IA-5"
        };

        try
        {
            foreach (var controlId in systemControlIds)
            {
                var isValid = await _nistControlsService.ValidateControlIdAsync(controlId, cancellationToken);
                if (!isValid)
                {
                    result.AddWarning($"Control ID '{controlId}' is not found in current NIST catalog");
                }
                else
                {
                    result.AddInfo($"Control ID '{controlId}' validated successfully");
                }
            }

            if (result.Warnings.Any())
            {
                _logger.LogWarning("Some control IDs are not valid in current NIST catalog: {InvalidControls}",
                    string.Join(", ", result.Warnings.Where(w => w.Contains("not found")).Select(w => w.Split('\'')[1])));
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Control mapping validation failed: {ex.Message}");
            _logger.LogError(ex, "Control mapping validation failed");
        }

        return result;
    }

    public ValidationResult ValidateAtoScanRequest(AtoScanRequest request)
    {
        var result = new ValidationResult { IsValid = true };

        if (request == null)
        {
            result.AddError("ATO scan request cannot be null");
            return result;
        }

        if (string.IsNullOrWhiteSpace(request.SubscriptionId))
        {
            result.AddError("Subscription ID is required");
        }
        else if (!Guid.TryParse(request.SubscriptionId, out _))
        {
            result.AddError("Subscription ID must be a valid GUID");
        }

        if (string.IsNullOrWhiteSpace(request.ResourceGroupName))
        {
            result.AddError("Resource group name is required");
        }
        else if (request.ResourceGroupName.Length > 90)
        {
            result.AddError("Resource group name cannot exceed 90 characters");
        }

        if (request.Configuration == null)
        {
            result.AddError("Scan configuration is required");
        }
        else
        {
            if (request.Configuration.ComplianceFrameworks == null || !request.Configuration.ComplianceFrameworks.Any())
            {
                result.AddError("At least one compliance framework must be specified");
            }
            else
            {
                var validFrameworks = new[] { "NIST-800-53", "SOC2", "ISO-27001", "GDPR" };
                var invalidFrameworks = request.Configuration.ComplianceFrameworks
                    .Where(f => !validFrameworks.Contains(f, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (invalidFrameworks.Any())
                {
                    result.AddWarning($"Unknown compliance frameworks: {string.Join(", ", invalidFrameworks)}");
                }
            }
        }

        return result;
    }

    public ValidationResult ValidateAtoFinding(AtoFinding finding)
    {
        var result = new ValidationResult { IsValid = true };

        if (finding == null)
        {
            result.AddError("ATO finding cannot be null");
            return result;
        }

        if (string.IsNullOrWhiteSpace(finding.Id))
        {
            result.AddError("Finding ID is required");
        }

        if (string.IsNullOrWhiteSpace(finding.ResourceId))
        {
            result.AddError("Resource ID is required");
        }

        if (string.IsNullOrWhiteSpace(finding.Title))
        {
            result.AddError("Finding title is required");
        }

        if (string.IsNullOrWhiteSpace(finding.Description))
        {
            result.AddError("Finding description is required");
        }

        if (finding.AffectedControls == null || !finding.AffectedControls.Any())
        {
            result.AddWarning("Finding should specify affected controls");
        }

        if (finding.ComplianceFrameworks == null || !finding.ComplianceFrameworks.Any())
        {
            result.AddWarning("Finding should specify applicable compliance frameworks");
        }

        if (finding.Severity == AtoFindingSeverity.Critical && !finding.IsRemediable)
        {
            result.AddWarning("Critical findings should typically be remediable");
        }

        return result;
    }
}

/// <summary>
/// Validation result container
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();

    public void AddError(string error)
    {
        Errors.Add(error);
        IsValid = false;
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    public void AddInfo(string info)
    {
        Info.Add(info);
    }

    public bool HasIssues => Errors.Any() || Warnings.Any();
}