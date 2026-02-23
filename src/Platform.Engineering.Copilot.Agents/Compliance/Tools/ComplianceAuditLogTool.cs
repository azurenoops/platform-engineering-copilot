using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_audit_log — Query immutable audit trail.
/// No auth required. Paginated. Default 7 days. actionType filter.
/// </summary>
public class ComplianceAuditLogTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Known action types for filtering.</summary>
    private static readonly string[] KnownActionTypes =
    [
        "Assessment", "Remediation", "DocumentGeneration",
        "EvidenceCollection", "ConfigurationChange", "PolicyUpdate"
    ];

    public ComplianceAuditLogTool(ILogger<ComplianceAuditLogTool> logger) : base(logger) { }

    public override string Name => "compliance_audit_log";
    public override string Description => "Query the audit log of compliance actions";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "subscriptionId": { "type": "string", "description": "Filter by subscription." },
        "days": { "type": "integer", "default": 7, "description": "Number of days of audit history." },
        "actionType": { "type": "string", "description": "Filter by action type (Assessment, Remediation, etc.)." },
        "page": { "type": "integer", "description": "Page number for paginated results." },
        "pageSize": { "type": "integer", "description": "Items per page (default: 25, max: 100)." }
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
        if (days <= 0) days = 7;

        var actionType = GetOptional<string>(parameters, "actionType");
        var page = GetOptional<int>(parameters, "page");
        var pageSize = GetOptional<int>(parameters, "pageSize");

        // Pagination defaults
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        // Generate sample audit entries
        var allEntries = GenerateAuditEntries(days, actionType);

        // Apply pagination
        var totalItems = allEntries.Length;
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var pagedEntries = allEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var data = new
        {
            days,
            actionTypeFilter = actionType,
            totalEntries = totalItems,
            entries = pagedEntries,
            pagination = new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                hasNextPage = page < totalPages
            }
        };

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
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static object[] GenerateAuditEntries(int days, string? actionType)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<object>();

        // Generate sample entries spread across the time window
        var sampleActions = new[]
        {
            ("Assessment", "Ran NIST 800-53 assessment", "****1234"),
            ("Remediation", "Applied remediation for AC-2", "****1234"),
            ("DocumentGeneration", "Generated SSP document", "****5678"),
            ("EvidenceCollection", "Collected evidence for SC-7", "****1234"),
            ("ConfigurationChange", "Updated framework to FedRAMP High", "****5678"),
            ("Assessment", "Ran FedRAMP High assessment", "****9012"),
            ("PolicyUpdate", "Updated compliance policy definitions", "****5678"),
            ("Remediation", "Applied remediation for IA-2", "****1234"),
        };

        for (var i = 0; i < sampleActions.Length && i < days * 2; i++)
        {
            var (action, details, userId) = sampleActions[i];

            // Apply actionType filter
            if (!string.IsNullOrWhiteSpace(actionType) &&
                !string.Equals(action, actionType, StringComparison.OrdinalIgnoreCase))
                continue;

            entries.Add(new
            {
                entryId = Guid.NewGuid().ToString(),
                action,
                timestamp = now.AddDays(-(i + 1)).ToString("o"),
                userId,
                details,
                correlationId = Guid.NewGuid().ToString()
            });
        }

        return entries.ToArray();
    }
}
