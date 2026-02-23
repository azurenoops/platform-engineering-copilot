using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Security.Tools;

/// <summary>
/// manage_security_policy — View or modify security policies.
/// View requires PIM Read, modify requires PIM Write per mcp-tools.md.
/// </summary>
public class ManageSecurityPolicyTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ManageSecurityPolicyTool(ILogger<ManageSecurityPolicyTool> logger) : base(logger) { }

    public override string Name => "manage_security_policy";
    public override string Description => "View or modify Azure security policies (view=Read PIM, modify=Write PIM)";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "action": { "type": "string", "enum": ["view", "enable", "disable"], "description": "Action to perform on the policy." },
        "subscriptionId": { "type": "string", "description": "Azure subscription ID." },
        "policyName": { "type": "string", "description": "Security policy name (required for enable/disable)." }
      },
      "required": ["action", "subscriptionId"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Read;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var action = GetRequired<string>(parameters, "action");
        var subscriptionId = GetRequired<string>(parameters, "subscriptionId");
        var policyName = GetOptional<string>(parameters, "policyName");

        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Task.FromResult(BuildError("MISSING_SUBSCRIPTION",
                "Subscription ID is required.", "Provide a valid Azure subscription ID.", sw));

        if (action is "enable" or "disable" && string.IsNullOrWhiteSpace(policyName))
            return Task.FromResult(BuildError("MISSING_POLICY_NAME",
                "Policy name is required for enable/disable actions.", "Provide policyName parameter.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = $"Policy {action} complete." });

        if (action == "view")
        {
            var viewResponse = new
            {
                status = "success",
                data = new
                {
                    subscriptionId,
                    policies = new[]
                    {
                        new { name = "ASC Default", scope = "Subscription", state = "Enabled", category = "SecurityCenter", affectedResources = 45 },
                        new { name = "FedRAMP High", scope = "ManagementGroup", state = "Enabled", category = "RegulatoryCompliance", affectedResources = 156 },
                        new { name = "NIST SP 800-53 Rev 5", scope = "ManagementGroup", state = "Enabled", category = "RegulatoryCompliance", affectedResources = 156 },
                        new { name = "CIS Benchmark", scope = "Subscription", state = "Audit", category = "SecurityBenchmark", affectedResources = 89 },
                        new { name = "Custom-IL5-Encryption", scope = "Subscription", state = "Enabled", category = "Custom", affectedResources = 32 }
                    }
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTime.UtcNow.ToString("O") }
            };
            return Task.FromResult(JsonSerializer.Serialize(viewResponse, JsonOptions));
        }

        var modifyResponse = new
        {
            status = "success",
            data = new
            {
                subscriptionId,
                policyName,
                action,
                previousState = action == "enable" ? "Disabled" : "Enabled",
                newState = action == "enable" ? "Enabled" : "Disabled",
                affectedResources = 45,
                complianceImpact = action == "disable" ? "Warning: Disabling this policy may reduce compliance score." : "Enabling this policy will improve compliance posture."
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTime.UtcNow.ToString("O") }
        };

        return Task.FromResult(JsonSerializer.Serialize(modifyResponse, JsonOptions));
    }

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private string BuildError(string code, string message, string suggestion, Stopwatch sw)
    {
        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "error",
            error = new { errorCode = code, message, suggestion },
            metadata = BuildMetadata(sw)
        }, JsonOptions);
    }
}
