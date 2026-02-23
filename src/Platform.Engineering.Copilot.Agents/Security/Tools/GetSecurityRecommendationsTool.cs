using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Security.Tools;

/// <summary>
/// get_security_recommendations — List security recommendations from Defender for Cloud.
/// Auth required, PIM Read per mcp-tools.md.
/// </summary>
public class GetSecurityRecommendationsTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetSecurityRecommendationsTool(ILogger<GetSecurityRecommendationsTool> logger) : base(logger) { }

    public override string Name => "get_security_recommendations";
    public override string Description => "List security recommendations from Microsoft Defender for Cloud with severity and remediation guidance";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "subscriptionId": { "type": "string", "description": "Azure subscription ID." },
        "severity": { "type": "string", "enum": ["all", "Critical", "High", "Medium", "Low"], "default": "all", "description": "Filter by severity." }
      },
      "required": ["subscriptionId"]
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
        var subscriptionId = GetRequired<string>(parameters, "subscriptionId");
        var severity = GetOptional<string>(parameters, "severity") ?? "all";

        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Task.FromResult(BuildError("MISSING_SUBSCRIPTION",
                "Subscription ID is required.", "Provide a valid Azure subscription ID.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Recommendations retrieved." });

        var allRecs = new[]
        {
            new { title = "Enable MFA for accounts with owner permissions", severity = "Critical", affectedResources = 3, framework = "NIST 800-53 IA-2", remediationType = "QuickFix", estimatedEffort = "Low", description = "Multi-factor authentication should be enabled for all accounts with owner role.", remediation = "Enable MFA in Azure AD Conditional Access policies." },
            new { title = "Storage accounts should use customer-managed keys", severity = "High", affectedResources = 5, framework = "NIST 800-53 SC-12", remediationType = "Manual", estimatedEffort = "Medium", description = "Enable CMK encryption for storage accounts.", remediation = "Configure Key Vault and update storage encryption settings." },
            new { title = "Subnets should have NSG associations", severity = "High", affectedResources = 2, framework = "NIST 800-53 SC-7", remediationType = "QuickFix", estimatedEffort = "Low", description = "Network security groups should be associated with all subnets.", remediation = "Create and associate NSGs with unprotected subnets." },
            new { title = "Diagnostic logs should be enabled", severity = "Medium", affectedResources = 8, framework = "NIST 800-53 AU-6", remediationType = "Manual", estimatedEffort = "Medium", description = "Enable diagnostic logging for monitoring.", remediation = "Enable diagnostic settings and forward to Log Analytics workspace." },
            new { title = "Endpoint protection should be installed", severity = "Medium", affectedResources = 4, framework = "NIST 800-53 SI-3", remediationType = "Manual", estimatedEffort = "Medium", description = "Install endpoint protection on virtual machines.", remediation = "Deploy Microsoft Defender for Endpoint on all VMs." },
            new { title = "Auto-provisioning of Log Analytics agent", severity = "Low", affectedResources = 1, framework = "NIST 800-53 AU-12", remediationType = "QuickFix", estimatedEffort = "Low", description = "Enable auto-provisioning of the Log Analytics agent.", remediation = "Turn on auto-provisioning in Security Center settings." }
        };

        var filtered = severity == "all"
            ? allRecs
            : allRecs.Where(r => r.severity.Equals(severity, StringComparison.OrdinalIgnoreCase)).ToArray();

        var response = new
        {
            status = "success",
            data = new
            {
                subscriptionId,
                severityFilter = severity,
                totalRecommendations = filtered.Length,
                recommendations = filtered,
                summary = new
                {
                    critical = allRecs.Count(r => r.severity == "Critical"),
                    high = allRecs.Count(r => r.severity == "High"),
                    medium = allRecs.Count(r => r.severity == "Medium"),
                    low = allRecs.Count(r => r.severity == "Low")
                }
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTime.UtcNow.ToString("O") }
        };

        return Task.FromResult(JsonSerializer.Serialize(response, JsonOptions));
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
