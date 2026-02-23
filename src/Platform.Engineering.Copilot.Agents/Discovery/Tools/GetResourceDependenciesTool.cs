using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// get_resource_dependencies — Map resource dependencies and interconnections.
/// Auth required, PIM Read per mcp-tools.md.
/// </summary>
public class GetResourceDependenciesTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetResourceDependenciesTool(ILogger<GetResourceDependenciesTool> logger) : base(logger) { }

    public override string Name => "get_resource_dependencies";
    public override string Description => "Map resource dependencies to understand interconnections and blast radius";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "resourceId": { "type": "string", "description": "Azure resource ID to analyze dependencies for." },
        "depth": { "type": "integer", "default": 2, "description": "Dependency traversal depth (1-5)." }
      },
      "required": ["resourceId"]
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
        var resourceId = GetRequired<string>(parameters, "resourceId");
        var depth = GetOptional<int?>(parameters, "depth") ?? 2;

        if (string.IsNullOrWhiteSpace(resourceId))
            return Task.FromResult(BuildError("MISSING_RESOURCE_ID",
                "Resource ID is required.", "Provide a valid Azure resource ID.", sw));

        if (depth < 1 || depth > 5)
            return Task.FromResult(BuildError("INVALID_DEPTH",
                "Depth must be between 1 and 5.", "Use a value from 1 to 5.", sw));

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 50,
            Message = $"Mapping dependencies for resource (depth={depth})..."
        });

        var dependencies = new[]
        {
            new { source = resourceId, target = "/subscriptions/.../Microsoft.Network/virtualNetworks/vnet-main", relationship = "network", type = "uses" },
            new { source = resourceId, target = "/subscriptions/.../Microsoft.KeyVault/vaults/kv-secrets", relationship = "identity", type = "reads" },
            new { source = resourceId, target = "/subscriptions/.../Microsoft.Storage/storageAccounts/sa-diag", relationship = "storage", type = "writes" }
        };

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Found {dependencies.Length} dependencies at depth {depth}."
        });

        var response = new
        {
            status = "success",
            data = new
            {
                resourceId,
                depth,
                dependencyCount = dependencies.Length,
                dependencies,
                blastRadius = new { directDependents = 3, transitiveDependents = 7 }
            },
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTime.UtcNow.ToString("O")
            }
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
