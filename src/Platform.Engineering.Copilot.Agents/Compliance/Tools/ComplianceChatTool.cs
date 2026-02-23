using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_chat — Natural language compliance interaction with conversation memory.
/// No auth required per mcp-tools.md. Enables board-related conversational workflows (US9).
/// </summary>
public class ComplianceChatTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>In-memory conversation store.</summary>
    private readonly Dictionary<string, List<ConversationMessage>> _conversations = new();

    public ComplianceChatTool(ILogger<ComplianceChatTool> logger) : base(logger) { }

    public override string Name => "compliance_chat";
    public override string Description => "Natural language compliance interaction with conversation memory";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "message": { "type": "string", "description": "Natural language compliance question or command." },
        "conversationId": { "type": "string", "description": "Conversation ID for context continuity." }
      },
      "required": ["message"]
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
        var message = GetRequired<string>(parameters, "message");
        var conversationId = GetOptional<string>(parameters, "conversationId")
            ?? Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(message))
        {
            sw.Stop();
            return Task.FromResult(BuildError("MISSING_MESSAGE",
                "Message is required.", "Provide a compliance question or command.", sw));
        }

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 30,
            Message = "Processing compliance query..."
        });

        // Get or create conversation
        if (!_conversations.ContainsKey(conversationId))
            _conversations[conversationId] = [];

        var history = _conversations[conversationId];
        history.Add(new ConversationMessage("user", message, DateTimeOffset.UtcNow));

        var response = GenerateResponse(message, history);
        history.Add(new ConversationMessage("assistant", response, DateTimeOffset.UtcNow));

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = "Response generated."
        });

        sw.Stop();
        var result = new
        {
            conversationId,
            response,
            messageCount = history.Count,
            context = new
            {
                topic = InferTopic(message),
                historyLength = history.Count
            }
        };

        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static string GenerateResponse(string message, List<ConversationMessage> history)
    {
        var lower = message.ToLowerInvariant();

        if (lower.Contains("remediation") && lower.Contains("board"))
            return "I can help you create a remediation board from your assessment findings. " +
                   "Each finding becomes a task card with a REM-### ID, severity badge, and SLA-based due date. " +
                   "Would you like to create a board from your most recent assessment?";

        if (lower.Contains("sla") || lower.Contains("due date"))
            return "SLA-based due dates are set by severity: Critical = 24 hours, High = 7 days, " +
                   "Medium = 30 days, Low = 90 days. Overdue tasks are automatically highlighted.";

        if (lower.Contains("nist") || lower.Contains("800-53"))
            return "NIST 800-53 Rev 5 provides security and privacy controls for federal information systems. " +
                   "Use the Knowledge Base tools for detailed control explanations.";

        if (lower.Contains("fedramp"))
            return "FedRAMP provides a standardized approach to security assessment for cloud products. " +
                   "Baselines include Low, Moderate, and High impact levels.";

        if (lower.Contains("assess") || lower.Contains("compliance"))
            return "I can help you understand your compliance posture. Available assessments include " +
                   "NIST 800-53 Rev 5, FedRAMP High, FedRAMP Moderate, and DoD IL5.";

        if (history.Count > 2)
            return $"Based on our conversation ({history.Count / 2} exchanges so far), " +
                   "I'll continue assisting with your compliance questions.";

        return "I'm your compliance assistant. I can help with NIST 800-53 controls, " +
               "FedRAMP assessments, remediation tracking, SLA management, and more.";
    }

    private static string InferTopic(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("remediation") || lower.Contains("board")) return "remediation";
        if (lower.Contains("sla") || lower.Contains("due")) return "sla";
        if (lower.Contains("nist") || lower.Contains("800-53")) return "nist";
        if (lower.Contains("fedramp")) return "fedramp";
        if (lower.Contains("assess")) return "assessment";
        return "general";
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

    private record ConversationMessage(string Role, string Content, DateTimeOffset Timestamp);
}
