using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Configuration.Tools;

/// <summary>
/// configuration_manage — Single tool with 5 sub-actions for managing ATO Copilot settings.
/// Sub-actions: get_configuration, set_subscription, set_framework, set_baseline, set_preference.
/// Writes to IAgentStateManager with config: prefix.
/// </summary>
public class ConfigurationManageTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> ValidFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "NIST80053", "FedRAMPHigh", "FedRAMPModerate", "DoDIL5"
    };

    private static readonly HashSet<string> ValidBaselines = new(StringComparer.OrdinalIgnoreCase)
    {
        "High", "Moderate", "Low"
    };

    private static readonly Dictionary<string, string[]> ValidPreferences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dryRunDefault"] = ["true", "false"],
        ["defaultScanType"] = ["resource", "policy", "combined"],
        ["cloudEnvironment"] = ["AzureGovernment", "AzureCloud"],
        ["region"] = [] // any valid Azure region string
    };

    private static readonly Regex GuidRegex = new(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IAgentStateManager _stateManager;

    public ConfigurationManageTool(
        ILogger<ConfigurationManageTool> logger,
        IAgentStateManager stateManager)
        : base(logger)
    {
        _stateManager = stateManager;
    }

    public override string Name => "configuration_manage";
    public override string Description =>
        "Manage ATO Copilot settings: subscription, framework, baseline, environment, and preferences";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "action": {
          "type": "string",
          "enum": ["get_configuration", "set_subscription", "set_framework", "set_baseline", "set_preference"],
          "description": "Configuration action to perform."
        },
        "subscriptionId": { "type": "string", "description": "Azure subscription ID (for set_subscription)." },
        "framework": { "type": "string", "enum": ["NIST80053", "FedRAMPHigh", "FedRAMPModerate", "DoDIL5"], "description": "Compliance framework (for set_framework)." },
        "baseline": { "type": "string", "enum": ["High", "Moderate", "Low"], "description": "Baseline level (for set_baseline)." },
        "preferenceName": { "type": "string", "description": "Preference name (for set_preference)." },
        "preferenceValue": { "type": "string", "description": "Preference value (for set_preference)." }
      },
      "required": ["action"]
    }
    """;

    public override bool RequiresAuthentication => false;
    public override PimTier PimTierRequired => PimTier.None;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var action = GetRequired<string>(parameters, "action").ToLowerInvariant();

        var result = action switch
        {
            "get_configuration" => HandleGetConfiguration(sw),
            "set_subscription" => HandleSetSubscription(parameters, sw),
            "set_framework" => HandleSetFramework(parameters, sw),
            "set_baseline" => HandleSetBaseline(parameters, sw),
            "set_preference" => HandleSetPreference(parameters, sw),
            _ => BuildError("MISSING_REQUIRED_PARAM",
                $"Unknown action: '{action}'.",
                "Use one of: get_configuration, set_subscription, set_framework, set_baseline, set_preference",
                sw)
        };

        return Task.FromResult(result);
    }

    private string HandleGetConfiguration(Stopwatch sw)
    {
        var data = new
        {
            subscriptionId = _stateManager.GetString("config:subscriptionId"),
            framework = _stateManager.GetString("config:framework"),
            baseline = _stateManager.GetString("config:baseline"),
            cloudEnvironment = _stateManager.GetString("config:cloudEnvironment") ?? "AzureGovernment",
            dryRunDefault = _stateManager.GetString("config:dryRunDefault") ?? "true",
            defaultScanType = _stateManager.GetString("config:defaultScanType") ?? "combined",
            region = _stateManager.GetString("config:region") ?? "usgovvirginia",
            lastUpdated = _stateManager.GetString("config:lastUpdated")
        };

        sw.Stop();
        return BuildSuccess(data, sw);
    }

    private string HandleSetSubscription(Dictionary<string, object?> parameters, Stopwatch sw)
    {
        var subscriptionId = GetOptional<string>(parameters, "subscriptionId");
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return BuildError("MISSING_REQUIRED_PARAM",
                "The 'set_subscription' action requires 'subscriptionId' parameter.",
                "Provide subscriptionId as a valid GUID", sw);

        if (!GuidRegex.IsMatch(subscriptionId))
            return BuildError("INVALID_SUBSCRIPTION_ID",
                "Invalid subscription ID format.",
                "Provide a valid GUID-format subscription ID (e.g., 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx')", sw);

        var previousValue = _stateManager.GetString("config:subscriptionId");
        _stateManager.Set("config:subscriptionId", subscriptionId);
        _stateManager.Set("config:lastUpdated", DateTimeOffset.UtcNow.ToString("o"));

        var data = new
        {
            message = $"Default subscription set to {subscriptionId}",
            subscriptionId,
            previousValue
        };

        sw.Stop();
        return BuildSuccess(data, sw);
    }

    private string HandleSetFramework(Dictionary<string, object?> parameters, Stopwatch sw)
    {
        var framework = GetOptional<string>(parameters, "framework");
        if (string.IsNullOrWhiteSpace(framework))
            return BuildError("MISSING_REQUIRED_PARAM",
                "The 'set_framework' action requires 'framework' parameter.",
                "Use one of: NIST80053, FedRAMPHigh, FedRAMPModerate, DoDIL5", sw);

        if (!ValidFrameworks.Contains(framework))
            return BuildError("INVALID_FRAMEWORK",
                $"Framework value '{framework}' is not recognized.",
                "Use one of: NIST80053, FedRAMPHigh, FedRAMPModerate, DoDIL5", sw);

        // Normalize to canonical form
        var canonical = ValidFrameworks.First(f =>
            string.Equals(f, framework, StringComparison.OrdinalIgnoreCase));

        var previousValue = _stateManager.GetString("config:framework");
        _stateManager.Set("config:framework", canonical);
        _stateManager.Set("config:lastUpdated", DateTimeOffset.UtcNow.ToString("o"));

        var data = new
        {
            message = $"Default framework set to {canonical}",
            framework = canonical,
            previousValue
        };

        sw.Stop();
        return BuildSuccess(data, sw);
    }

    private string HandleSetBaseline(Dictionary<string, object?> parameters, Stopwatch sw)
    {
        var baseline = GetOptional<string>(parameters, "baseline");
        if (string.IsNullOrWhiteSpace(baseline))
            return BuildError("MISSING_REQUIRED_PARAM",
                "The 'set_baseline' action requires 'baseline' parameter.",
                "Use one of: High, Moderate, Low", sw);

        if (!ValidBaselines.Contains(baseline))
            return BuildError("INVALID_BASELINE",
                $"Baseline value '{baseline}' is not recognized.",
                "Use one of: High, Moderate, Low", sw);

        var canonical = ValidBaselines.First(b =>
            string.Equals(b, baseline, StringComparison.OrdinalIgnoreCase));

        var previousValue = _stateManager.GetString("config:baseline");
        _stateManager.Set("config:baseline", canonical);
        _stateManager.Set("config:lastUpdated", DateTimeOffset.UtcNow.ToString("o"));

        var data = new
        {
            message = $"Default baseline set to {canonical}",
            baseline = canonical,
            previousValue
        };

        sw.Stop();
        return BuildSuccess(data, sw);
    }

    private string HandleSetPreference(Dictionary<string, object?> parameters, Stopwatch sw)
    {
        var preferenceName = GetOptional<string>(parameters, "preferenceName");
        var preferenceValue = GetOptional<string>(parameters, "preferenceValue");

        if (string.IsNullOrWhiteSpace(preferenceName))
            return BuildError("MISSING_REQUIRED_PARAM",
                "The 'set_preference' action requires 'preferenceName' parameter.",
                "Valid preferences: dryRunDefault, defaultScanType, cloudEnvironment, region", sw);

        if (!ValidPreferences.ContainsKey(preferenceName))
            return BuildError("INVALID_PREFERENCE_NAME",
                $"Preference name '{preferenceName}' is not recognized.",
                "Valid preferences: dryRunDefault, defaultScanType, cloudEnvironment, region", sw);

        if (string.IsNullOrWhiteSpace(preferenceValue))
            return BuildError("MISSING_REQUIRED_PARAM",
                "The 'set_preference' action requires 'preferenceValue' parameter.",
                $"Provide a value for '{preferenceName}'", sw);

        // Validate value for named preferences (region is free-form)
        var validValues = ValidPreferences[preferenceName];
        if (validValues.Length > 0 &&
            !validValues.Any(v => string.Equals(v, preferenceValue, StringComparison.OrdinalIgnoreCase)))
        {
            return BuildError("INVALID_PREFERENCE_VALUE",
                $"Value '{preferenceValue}' is not valid for preference '{preferenceName}'.",
                $"Valid values: {string.Join(", ", validValues)}", sw);
        }

        var key = $"config:{preferenceName}";
        var previousValue = _stateManager.GetString(key);
        _stateManager.Set(key, preferenceValue);
        _stateManager.Set("config:lastUpdated", DateTimeOffset.UtcNow.ToString("o"));

        var data = new
        {
            message = $"{preferenceName} set to {preferenceValue}",
            preferenceName,
            preferenceValue,
            previousValue
        };

        sw.Stop();
        return BuildSuccess(data, sw);
    }

    private string BuildSuccess(object data, Stopwatch sw)
    {
        sw.Stop();
        var envelope = new
        {
            status = "success",
            data,
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            }
        };
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private string BuildError(string errorCode, string message, string suggestion, Stopwatch sw)
    {
        sw.Stop();
        var envelope = new
        {
            status = "error",
            data = (object?)null,
            error = new { message, errorCode, suggestion },
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            }
        };
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }
}
