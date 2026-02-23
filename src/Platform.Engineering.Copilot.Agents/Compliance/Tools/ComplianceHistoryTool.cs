using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_history — Assessment history with trend data.
/// No auth required, paginated (default 25, max 100), default 30 days.
/// </summary>
public class ComplianceHistoryTool : BaseTool
{
    public ComplianceHistoryTool(ILogger<ComplianceHistoryTool> logger) : base(logger) { }

    public override string Name => "compliance_history";
    public override string Description => "Get compliance assessment history and trend data";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "subscriptionId": { "type": "string", "description": "Azure subscription ID." },
        "days": { "type": "integer", "default": 30, "description": "Number of days of history." },
        "scanType": { "type": "string", "enum": ["resource", "policy", "combined"] },
        "page": { "type": "integer", "default": 1 },
        "pageSize": { "type": "integer", "default": 25 }
      },
      "required": []
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
        var days = GetOptional<int>(parameters, "days");
        if (days <= 0) days = 30;
        var page = GetOptional<int>(parameters, "page");
        if (page <= 0) page = 1;
        var pageSize = GetOptional<int>(parameters, "pageSize");
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        // Generate sample history entries
        var assessments = Enumerable.Range(0, Math.Min(days, pageSize))
            .Select(i => new
            {
                assessmentId = Guid.NewGuid().ToString(),
                assessedAt = DateTimeOffset.UtcNow.AddDays(-i).ToString("o"),
                complianceScore = Math.Round(80.0 + Random.Shared.NextDouble() * 15, 1),
                totalControls = 421,
                passedControls = 350 + Random.Shared.Next(0, 71),
                framework = "NIST80053"
            })
            .ToList();

        var totalItems = days; // Simplified: 1 assessment per day

        var data = new
        {
            assessments,
            trend = new
            {
                startScore = assessments.LastOrDefault()?.complianceScore ?? 0,
                endScore = assessments.FirstOrDefault()?.complianceScore ?? 0,
                direction = "improving"
            }
        };

        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        sw.Stop();
        var envelope = new
        {
            status = "success",
            data,
            pagination = new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                hasNextPage = page < totalPages
            },
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            }
        };

        return Task.FromResult(JsonSerializer.Serialize(envelope,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
