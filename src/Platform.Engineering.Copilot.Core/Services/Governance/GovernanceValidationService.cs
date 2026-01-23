using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;

namespace Platform.Engineering.Copilot.Core.Services.Governance;

/// <summary>
/// Runtime enforcement of governance policies for provisioning operations.
/// Validates against GovernanceOptions configuration before allowing deployments.
/// </summary>
public class GovernanceValidationService : IGovernanceValidationService
{
    private readonly ILogger<GovernanceValidationService> _logger;
    private readonly GovernanceOptions _options;

    // Azure region aliases for normalization
    private static readonly Dictionary<string, string> RegionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "east us", "eastus" },
        { "west us", "westus" },
        { "west us 2", "westus2" },
        { "west us 3", "westus3" },
        { "central us", "centralus" },
        { "south central us", "southcentralus" },
        { "north central us", "northcentralus" },
        { "east us 2", "eastus2" },
        { "usgovvirginia", "usgovvirginia" },
        { "usgov virginia", "usgovvirginia" },
        { "usgovarizona", "usgovarizona" },
        { "usgov arizona", "usgovarizona" },
        { "usgovtexas", "usgovtexas" },
        { "usgov texas", "usgovtexas" },
        { "usdodeast", "usdodeast" },
        { "usdodcentral", "usdodcentral" }
    };

    // Default naming convention patterns
    private static readonly Regex DefaultNamingPattern = new(
        @"^[a-z][a-z0-9-]{1,61}[a-z0-9]$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Resource-specific naming patterns
    private static readonly Dictionary<string, Regex> ResourceNamingPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Microsoft.Compute/virtualMachines", new Regex(@"^[a-zA-Z][a-zA-Z0-9-]{0,14}$", RegexOptions.Compiled) },
        { "Microsoft.Storage/storageAccounts", new Regex(@"^[a-z0-9]{3,24}$", RegexOptions.Compiled) },
        { "Microsoft.KeyVault/vaults", new Regex(@"^[a-zA-Z][a-zA-Z0-9-]{1,22}[a-zA-Z0-9]$", RegexOptions.Compiled) },
        { "Microsoft.ContainerService/managedClusters", new Regex(@"^[a-zA-Z][a-zA-Z0-9_-]{0,62}$", RegexOptions.Compiled) }
    };

    public GovernanceValidationService(
        ILogger<GovernanceValidationService> logger,
        IOptions<GovernanceOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new GovernanceOptions();
    }

    public async Task<GovernanceValidationResult> ValidateAsync(
        GovernanceValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new GovernanceValidationResult();

        // Skip validation if policies are disabled
        if (!_options.EnforcePolicies)
        {
            _logger.LogDebug("Governance policy enforcement is disabled");
            return result;
        }

        _logger.LogInformation("🛡️ Validating governance policies for request from {RequestedBy}",
            request.RequestedBy ?? "unknown");

        // 1. Validate region
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            if (!IsRegionApproved(request.Location))
            {
                result.Violations.Add(new GovernanceViolation
                {
                    PolicyType = GovernancePolicyType.ApprovedRegion,
                    Message = $"Region '{request.Location}' is not in the list of approved regions. " +
                              $"Approved regions: {string.Join(", ", _options.ApprovedRegions)}",
                    Property = "location",
                    ProvidedValue = request.Location,
                    AllowedValue = _options.ApprovedRegions,
                    Severity = GovernanceViolationSeverity.Error,
                    NistControls = "CM-7, AC-3"
                });
            }
        }

        // 2. Validate naming conventions
        if (_options.EnforceNamingConventions)
        {
            // Validate environment name
            if (!string.IsNullOrWhiteSpace(request.EnvironmentName))
            {
                var envNameResult = ValidateResourceName(request.EnvironmentName, "environment");
                if (!envNameResult.IsValid)
                {
                    result.Violations.Add(new GovernanceViolation
                    {
                        PolicyType = GovernancePolicyType.NamingConvention,
                        Message = envNameResult.ViolationMessage ?? $"Environment name '{request.EnvironmentName}' violates naming conventions",
                        Property = "environmentName",
                        ProvidedValue = request.EnvironmentName,
                        AllowedValue = envNameResult.SuggestedName,
                        Severity = GovernanceViolationSeverity.Error,
                        NistControls = "CM-2"
                    });
                }
            }

            // Validate resource group name
            if (!string.IsNullOrWhiteSpace(request.ResourceGroupName))
            {
                var rgNameResult = ValidateResourceName(request.ResourceGroupName, "resourceGroup");
                if (!rgNameResult.IsValid)
                {
                    result.Violations.Add(new GovernanceViolation
                    {
                        PolicyType = GovernancePolicyType.NamingConvention,
                        Message = rgNameResult.ViolationMessage ?? $"Resource group name '{request.ResourceGroupName}' violates naming conventions",
                        Property = "resourceGroupName",
                        ProvidedValue = request.ResourceGroupName,
                        AllowedValue = rgNameResult.SuggestedName,
                        Severity = GovernanceViolationSeverity.Error,
                        NistControls = "CM-2"
                    });
                }
            }
        }

        // 3. Validate required tags
        if (_options.EnforceTagging)
        {
            var tagResult = ValidateRequiredTags(request.Tags);
            if (!tagResult.IsValid)
            {
                result.Violations.Add(new GovernanceViolation
                {
                    PolicyType = GovernancePolicyType.RequiredTags,
                    Message = tagResult.ViolationMessage ?? $"Missing required tags: {string.Join(", ", tagResult.MissingTags)}",
                    Property = "tags",
                    ProvidedValue = request.Tags?.Keys.ToList(),
                    AllowedValue = _options.RequiredTags,
                    Severity = GovernanceViolationSeverity.Error,
                    NistControls = "CM-2, AU-2"
                });
            }
        }

        // Log audit trail
        if (_options.EnableAuditLogging)
        {
            await LogGovernanceAuditAsync(request, result, cancellationToken);
        }

        if (result.IsValid)
        {
            _logger.LogInformation("✅ Governance validation passed for environment: {EnvironmentName}",
                request.EnvironmentName);
        }
        else
        {
            _logger.LogWarning("❌ Governance validation failed with {ViolationCount} violations: {Violations}",
                result.Violations.Count,
                string.Join("; ", result.Errors));
        }

        return result;
    }

    public bool IsRegionApproved(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return false;

        // If no approved regions configured, allow all
        if (_options.ApprovedRegions == null || !_options.ApprovedRegions.Any())
        {
            _logger.LogDebug("No approved regions configured - allowing all regions");
            return true;
        }

        // Normalize the region name
        var normalizedRegion = NormalizeRegion(region);

        // Check if region is in approved list (case-insensitive)
        var isApproved = _options.ApprovedRegions
            .Select(NormalizeRegion)
            .Any(r => r.Equals(normalizedRegion, StringComparison.OrdinalIgnoreCase));

        _logger.LogDebug("Region check: {Region} (normalized: {Normalized}) - Approved: {IsApproved}",
            region, normalizedRegion, isApproved);

        return isApproved;
    }

    public NamingValidationResult ValidateResourceName(string resourceName, string? resourceType = null)
    {
        var result = new NamingValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            result.IsValid = false;
            result.ViolationMessage = "Resource name cannot be empty";
            return result;
        }

        // Check length bounds
        if (resourceName.Length < 2)
        {
            result.IsValid = false;
            result.ViolationMessage = "Resource name must be at least 2 characters";
            return result;
        }

        if (resourceName.Length > 63)
        {
            result.IsValid = false;
            result.ViolationMessage = "Resource name cannot exceed 63 characters";
            result.SuggestedName = resourceName[..63];
            return result;
        }

        // Check for invalid characters
        if (!DefaultNamingPattern.IsMatch(resourceName))
        {
            result.IsValid = false;
            result.ViolationMessage = "Resource name must start with a letter, contain only alphanumeric characters and hyphens, and end with an alphanumeric character";
            result.SuggestedName = SuggestValidName(resourceName);
            return result;
        }

        // Check resource-specific patterns if type provided
        if (!string.IsNullOrWhiteSpace(resourceType) && 
            ResourceNamingPatterns.TryGetValue(resourceType, out var pattern) &&
            !pattern.IsMatch(resourceName))
        {
            result.IsValid = false;
            result.ViolationMessage = $"Resource name does not match naming requirements for {resourceType}";
            return result;
        }

        // Check for forbidden patterns (security)
        var forbiddenPatterns = new[] { "--", "..", "admin", "root", "system" };
        foreach (var forbidden in forbiddenPatterns)
        {
            if (resourceName.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.ViolationMessage = $"Resource name contains forbidden pattern: '{forbidden}'";
                return result;
            }
        }

        return result;
    }

    public TagValidationResult ValidateRequiredTags(Dictionary<string, string>? tags)
    {
        var result = new TagValidationResult { IsValid = true };

        if (_options.RequiredTags == null || !_options.RequiredTags.Any())
        {
            return result;
        }

        tags ??= new Dictionary<string, string>();

        foreach (var requiredTag in _options.RequiredTags)
        {
            // Case-insensitive tag key check
            var hasTag = tags.Keys.Any(k => k.Equals(requiredTag, StringComparison.OrdinalIgnoreCase));
            if (!hasTag)
            {
                result.MissingTags.Add(requiredTag);
            }
        }

        if (result.MissingTags.Any())
        {
            result.IsValid = false;
            result.ViolationMessage = $"Missing required tags: {string.Join(", ", result.MissingTags)}. " +
                                      "All resources must have these tags for cost allocation and compliance tracking.";
        }

        return result;
    }

    #region Private Helpers

    private static string NormalizeRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return region;

        var normalized = region.ToLowerInvariant().Trim();

        // Check if it's an alias and return the normalized form
        if (RegionAliases.TryGetValue(normalized, out var canonical))
            return canonical;

        // Remove spaces and convert to lowercase
        return normalized.Replace(" ", "");
    }

    private static string SuggestValidName(string invalidName)
    {
        // Remove invalid characters and suggest a valid name
        var suggested = Regex.Replace(invalidName.ToLowerInvariant(), @"[^a-z0-9-]", "-");
        suggested = Regex.Replace(suggested, @"-+", "-"); // Collapse multiple hyphens
        suggested = suggested.Trim('-'); // Remove leading/trailing hyphens

        // Ensure it starts with a letter
        if (suggested.Length > 0 && !char.IsLetter(suggested[0]))
        {
            suggested = "r-" + suggested;
        }

        // Ensure it ends with alphanumeric
        if (suggested.Length > 0 && !char.IsLetterOrDigit(suggested[^1]))
        {
            suggested = suggested.TrimEnd('-');
        }

        return suggested.Length >= 2 ? suggested : "resource-" + Guid.NewGuid().ToString("N")[..8];
    }

    private Task LogGovernanceAuditAsync(
        GovernanceValidationRequest request,
        GovernanceValidationResult result,
        CancellationToken cancellationToken)
    {
        // Log governance decision for audit trail (AU-2, AU-3)
        var logLevel = result.IsValid ? LogLevel.Information : LogLevel.Warning;

        _logger.Log(logLevel,
            "GOVERNANCE_AUDIT: RequestedBy={RequestedBy}, Environment={Environment}, Template={Template}, " +
            "Location={Location}, Valid={IsValid}, ViolationCount={ViolationCount}, " +
            "Violations={Violations}",
            request.RequestedBy,
            request.EnvironmentName,
            request.TemplateId,
            request.Location,
            result.IsValid,
            result.Violations.Count,
            result.IsValid ? "None" : string.Join("; ", result.Errors));

        return Task.CompletedTask;
    }

    #endregion
}
