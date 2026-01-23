using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Mcp.Tools;
using Platform.Engineering.Copilot.Mcp.Models;
using Platform.Engineering.Copilot.Mcp.Prompts;
using Platform.Engineering.Copilot.Agents.Orchestration;
using Platform.Engineering.Copilot.Agents.Common;
using System.Diagnostics;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Mcp.Server;

/// <summary>
/// MCP server that exposes the Platform Engineering Copilot's tools via stdio/HTTP.
/// Uses domain-specific tool wrappers for granular AI assistant integration.
/// </summary>
public class McpServer
{
    private readonly ConfigurationMcpTools _configurationTools;
    private readonly ComplianceMcpTools _complianceTools;
    private readonly DiscoveryMcpTools _discoveryTools;
    private readonly InfrastructureMcpTools _infrastructureTools;
    private readonly CostManagementMcpTools _costManagementTools;
    private readonly KnowledgeBaseMcpTools _knowledgeBaseTools;
    private readonly PlatformAgentGroupChat _agentGroupChat;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(
        ConfigurationMcpTools configurationTools,
        ComplianceMcpTools complianceTools,
        DiscoveryMcpTools discoveryTools,
        InfrastructureMcpTools infrastructureTools,
        CostManagementMcpTools costManagementTools,
        KnowledgeBaseMcpTools knowledgeBaseTools,
        PlatformAgentGroupChat agentGroupChat,
        ILogger<McpServer> logger)
    {
        _configurationTools = configurationTools;
        _complianceTools = complianceTools;
        _discoveryTools = discoveryTools;
        _infrastructureTools = infrastructureTools;
        _costManagementTools = costManagementTools;
        _knowledgeBaseTools = knowledgeBaseTools;
        _agentGroupChat = agentGroupChat;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Process a chat request through the multi-agent orchestrator.
    /// Used by McpHttpBridge for HTTP mode.
    /// </summary>
    public async Task<McpChatResponse> ProcessChatRequestAsync(
        string message,
        string? conversationId = null,
        Dictionary<string, object>? context = null,
        List<(string Role, string Content)>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        conversationId ??= Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("📨 Processing chat request via McpServer | ConvId: {ConvId} | HistoryCount: {HistoryCount}", 
            conversationId, conversationHistory?.Count ?? 0);

        try
        {
            // Create conversation context
            var agentContext = new AgentConversationContext
            {
                ConversationId = conversationId,
                UserId = "mcp-user"
            };

            // Populate message history from conversation history
            if (conversationHistory != null && conversationHistory.Count > 0)
            {
                foreach (var (role, content) in conversationHistory)
                {
                    agentContext.AddMessage(content, isUser: role.Equals("user", StringComparison.OrdinalIgnoreCase));
                }
                _logger.LogDebug("📜 Loaded {Count} messages from conversation history", conversationHistory.Count);
            }

            // Add any additional context from MCP client
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    agentContext.WorkflowState[kvp.Key] = kvp.Value;
                }
            }

            // Process through agent group chat
            var response = await _agentGroupChat.ProcessAndAggregateAsync(
                message,
                agentContext,
                cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("✅ MCP Chat processed successfully | Time: {TimeMs}ms", stopwatch.ElapsedMilliseconds);

            return new McpChatResponse
            {
                Success = true,
                Response = response,
                ConversationId = conversationId,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "❌ Error processing MCP chat request");

            return new McpChatResponse
            {
                Success = false,
                Response = $"Error processing request: {ex.Message}",
                ConversationId = conversationId,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Start the MCP server in stdio mode
    /// </summary>
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting Platform Engineering MCP Server v2 (domain-specific tools)");

        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                    if (request != null)
                    {
                        var response = await HandleRequestAsync(request);
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON received: {Line}", line);
                    var errorResponse = CreateErrorResponse(0, -32700, "Parse error");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse, _jsonOptions));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MCP server");
            throw;
        }
    }

    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        try
        {
            _logger.LogDebug("Handling MCP request: {Method}", request.Method);

            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolCallAsync(request),
                "prompts/list" => HandlePromptsList(request),
                "prompts/get" => HandlePromptsGet(request),
                "ping" => HandlePing(request),
                _ => CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request {Method}", request.Method);
            return CreateErrorResponse(request.Id, -32603, "Internal error", ex.Message);
        }
    }

    private McpResponse HandleInitialize(McpRequest request)
    {
        _logger.LogInformation("Client initialized MCP connection");

        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = false },
                    prompts = new { listChanged = false }
                },
                serverInfo = new
                {
                    name = "Platform Engineering Copilot",
                    version = "1.0.0"
                }
            }
        };
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new List<McpTool>();

        // Configuration Tools
        tools.Add(CreateTool("configure_subscription", "Configure the default Azure subscription for all Platform Engineering Copilot operations. " +
            "Actions: 'set' to configure, 'get' to show current, 'clear' to remove. This setting persists across sessions.", new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", description = "Action to perform: 'set', 'get', 'clear', or 'show'" },
                subscriptionId = new { type = "string", description = "Azure subscription ID (GUID) when action is 'set'" }
            },
            required = new[] { "action" }
        }));

        // Compliance Tools
        tools.Add(CreateTool("compliance_assess", "Run a NIST 800-53 compliance assessment against Azure resources", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                resource_group = new { type = "string", description = "Optional resource group filter" },
                impact_level = new { type = "string", description = "FIPS 199 impact level: Low, Moderate, or High" },
                control_families = new { type = "array", items = new { type = "string" }, description = "Control families to assess" }
            },
            required = new[] { "subscription_id" }
        }));

        tools.Add(CreateTool("compliance_get_control_family", "Get detailed information about a NIST control family", new
        {
            type = "object",
            properties = new
            {
                family = new { type = "string", description = "Control family (e.g., AC, AU, IA)" },
                impact_level = new { type = "string", description = "FIPS 199 impact level" }
            },
            required = new[] { "family" }
        }));

        tools.Add(CreateTool("compliance_generate_document", "Generate compliance documentation (SSP, POA&M, etc.)", new
        {
            type = "object",
            properties = new
            {
                document_type = new { type = "string", description = "Document type: ssp, poam, sar, or assessment" },
                system_name = new { type = "string", description = "System name" },
                subscription_id = new { type = "string", description = "Azure subscription for evidence" }
            },
            required = new[] { "document_type", "system_name" }
        }));

        tools.Add(CreateTool("compliance_collect_evidence", "Collect compliance evidence from Azure resources", new
        {
            type = "object",
            properties = new
            {
                control_id = new { type = "string", description = "NIST control ID" },
                subscription_id = new { type = "string", description = "Azure subscription ID" }
            },
            required = new[] { "control_id", "subscription_id" }
        }));

        tools.Add(CreateTool("compliance_remediate", "Remediate a compliance finding", new
        {
            type = "object",
            properties = new
            {
                finding_id = new { type = "string", description = "Finding ID to remediate" },
                auto_fix = new { type = "boolean", description = "Apply fix automatically" }
            },
            required = new[] { "finding_id" }
        }));

        // Discovery Tools
        tools.Add(CreateTool("discovery_resources", "Discover Azure resources across subscriptions", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                resource_type = new { type = "string", description = "Filter by resource type" },
                tags = new { type = "object", description = "Filter by tags" }
            },
            required = Array.Empty<string>()
        }));

        tools.Add(CreateTool("discovery_resource_details", "Get detailed information about a specific resource", new
        {
            type = "object",
            properties = new
            {
                resource_id = new { type = "string", description = "Full Azure resource ID" },
                include_metrics = new { type = "boolean", description = "Include performance metrics" }
            },
            required = new[] { "resource_id" }
        }));

        tools.Add(CreateTool("discovery_resource_health", "Check health status of Azure resources", new
        {
            type = "object",
            properties = new
            {
                resource_id = new { type = "string", description = "Azure resource ID" }
            },
            required = new[] { "resource_id" }
        }));

        tools.Add(CreateTool("discovery_dependencies", "Map dependencies between Azure resources", new
        {
            type = "object",
            properties = new
            {
                resource_id = new { type = "string", description = "Starting resource ID" },
                depth = new { type = "integer", description = "Dependency traversal depth" }
            },
            required = new[] { "resource_id" }
        }));

        tools.Add(CreateTool("discovery_subscriptions", "List available Azure subscriptions", new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        }));

        // Infrastructure Tools
        tools.Add(CreateTool("infrastructure_generate_template", "Generate Infrastructure as Code templates (Bicep/Terraform)", new
        {
            type = "object",
            properties = new
            {
                resource_type = new { type = "string", description = "Azure resource type (e.g., aks, vm, storage)" },
                template_format = new { type = "string", description = "Template format: bicep or terraform" },
                compliance_level = new { type = "string", description = "Compliance: commercial, govcloud, or il5" },
                environment = new { type = "string", description = "Target environment: dev, staging, or prod" }
            },
            required = new[] { "resource_type" }
        }));

        tools.Add(CreateTool("infrastructure_provision", "Provision Azure resources from templates", new
        {
            type = "object",
            properties = new
            {
                template = new { type = "string", description = "IaC template content" },
                subscription_id = new { type = "string", description = "Target subscription" },
                resource_group = new { type = "string", description = "Target resource group" },
                dry_run = new { type = "boolean", description = "Preview without deploying" }
            },
            required = new[] { "template", "subscription_id", "resource_group" }
        }));

        tools.Add(CreateTool("infrastructure_delete", "Delete Azure resources", new
        {
            type = "object",
            properties = new
            {
                resource_ids = new { type = "array", items = new { type = "string" }, description = "Resource IDs to delete" },
                force = new { type = "boolean", description = "Force deletion" }
            },
            required = new[] { "resource_ids" }
        }));

        tools.Add(CreateTool("infrastructure_analyze_scaling", "Analyze resource utilization and scaling recommendations", new
        {
            type = "object",
            properties = new
            {
                resource_id = new { type = "string", description = "Azure resource ID" },
                time_range = new { type = "string", description = "Analysis time range (e.g., 7d, 30d)" }
            },
            required = new[] { "resource_id" }
        }));

        tools.Add(CreateTool("infrastructure_azure_arc", "Manage Azure Arc for hybrid resources", new
        {
            type = "object",
            properties = new
            {
                operation = new { type = "string", description = "Operation: onboard, status, or configure" },
                resource_type = new { type = "string", description = "Resource type: server, kubernetes, or data-services" }
            },
            required = new[] { "operation", "resource_type" }
        }));

        // Cost Management Tools
        tools.Add(CreateTool("cost_analyze", "Analyze Azure costs with breakdown", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                resource_group = new { type = "string", description = "Resource group filter" },
                time_range = new { type = "string", description = "Time range: 7d, 30d, 90d" },
                group_by = new { type = "string", description = "Group by: resource, service, location, tag" }
            },
            required = new[] { "subscription_id" }
        }));

        tools.Add(CreateTool("cost_optimize", "Get cost optimization recommendations", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                resource_group = new { type = "string", description = "Resource group filter" },
                minimum_savings = new { type = "number", description = "Minimum monthly savings threshold ($)" }
            },
            required = new[] { "subscription_id" }
        }));

        tools.Add(CreateTool("cost_forecast", "Forecast future Azure costs", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                forecast_months = new { type = "integer", description = "Months to forecast (default: 3)" }
            },
            required = new[] { "subscription_id" }
        }));

        tools.Add(CreateTool("cost_budget", "Manage Azure budgets", new
        {
            type = "object",
            properties = new
            {
                operation = new { type = "string", description = "Operation: create, update, delete, or list" },
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                budget_name = new { type = "string", description = "Budget name" },
                amount = new { type = "number", description = "Budget amount in USD" }
            },
            required = new[] { "operation", "subscription_id" }
        }));

        tools.Add(CreateTool("cost_anomaly", "Detect cost anomalies", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                resource_group = new { type = "string", description = "Resource group filter" },
                lookback_days = new { type = "integer", description = "Days to analyze (default: 30)" }
            },
            required = new[] { "subscription_id" }
        }));

        tools.Add(CreateTool("cost_scenario", "Model cost scenarios", new
        {
            type = "object",
            properties = new
            {
                scenario_type = new { type = "string", description = "Scenario: reserved-instance, spot-vm, or scale-out" },
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                parameters = new { type = "object", description = "Scenario-specific parameters" }
            },
            required = new[] { "scenario_type", "subscription_id" }
        }));

        // Knowledge Base Tools
        tools.Add(CreateTool("kb_explain_nist", "Explain a NIST 800-53 control", new
        {
            type = "object",
            properties = new
            {
                control_id = new { type = "string", description = "NIST control ID (e.g., AC-2, AU-3)" },
                impact_level = new { type = "string", description = "FIPS 199 impact level" },
                include_enhancements = new { type = "boolean", description = "Include control enhancements" }
            },
            required = new[] { "control_id" }
        }));

        tools.Add(CreateTool("kb_search_nist", "Search NIST 800-53 controls", new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Search query" },
                control_family = new { type = "string", description = "Filter by control family" },
                max_results = new { type = "integer", description = "Maximum results (default: 10)" }
            },
            required = new[] { "query" }
        }));

        tools.Add(CreateTool("kb_explain_rmf", "Explain the Risk Management Framework", new
        {
            type = "object",
            properties = new
            {
                rmf_step = new { type = "string", description = "RMF step: prepare, categorize, select, implement, assess, authorize, monitor" },
                topic = new { type = "string", description = "Specific topic within RMF" }
            },
            required = Array.Empty<string>()
        }));

        tools.Add(CreateTool("kb_explain_stig", "Explain STIG requirements", new
        {
            type = "object",
            properties = new
            {
                stig_id = new { type = "string", description = "STIG identifier" },
                rule_id = new { type = "string", description = "Specific rule ID" },
                include_fixes = new { type = "boolean", description = "Include fix guidance" }
            },
            required = new[] { "stig_id" }
        }));

        tools.Add(CreateTool("kb_search_stigs", "Search STIGs", new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Search query" },
                technology = new { type = "string", description = "Technology filter" },
                severity = new { type = "string", description = "Severity: CAT I, CAT II, CAT III" },
                max_results = new { type = "integer", description = "Maximum results (default: 10)" }
            },
            required = new[] { "query" }
        }));

        tools.Add(CreateTool("kb_fedramp_template", "Get FedRAMP templates", new
        {
            type = "object",
            properties = new
            {
                template_type = new { type = "string", description = "Template: ssp, poam, sar, or checklist" },
                impact_level = new { type = "string", description = "Impact level: Low, Moderate, or High" }
            },
            required = new[] { "template_type" }
        }));

        tools.Add(CreateTool("kb_impact_level", "Determine or compare impact levels", new
        {
            type = "object",
            properties = new
            {
                operation = new { type = "string", description = "Operation: determine or compare" },
                system_type = new { type = "string", description = "Type of system" },
                data_types = new { type = "string", description = "Types of data processed" }
            },
            required = new[] { "operation" }
        }));

        // Legacy chat tool for backwards compatibility
        tools.Add(CreateTool("platform_engineering_chat", "Process requests through the multi-agent orchestrator (legacy)", new
        {
            type = "object",
            properties = new
            {
                message = new { type = "string", description = "The user's platform engineering request" },
                conversation_id = new { type = "string", description = "Optional conversation ID for context" }
            },
            required = new[] { "message" }
        }));

        return new McpResponse
        {
            Id = request.Id,
            Result = new { tools }
        };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        try
        {
            var toolCall = JsonSerializer.Deserialize<McpToolCall>(
                JsonSerializer.Serialize(request.Params, _jsonOptions),
                _jsonOptions);

            if (toolCall == null)
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid tool call parameters");
            }

            _logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);

            var args = toolCall.Arguments ?? new Dictionary<string, object>();
            var result = await ExecuteToolAsync(toolCall.Name, args);

            return new McpResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool call");
            return CreateErrorResponse(request.Id, -32603, "Tool execution failed", ex.Message);
        }
    }

    private async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> args)
    {
        try
        {
            string result = toolName switch
            {
                // Configuration Tools
                "configure_subscription" => await _configurationTools.ConfigureSubscriptionAsync(
                    GetArg<string>(args, "action") ?? "get",
                    GetArg<string>(args, "subscriptionId")),

                // Compliance Tools - signatures match ComplianceMcpTools
                "compliance_assess" => await _complianceTools.RunComplianceAssessmentAsync(
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "framework"),
                    GetArg<string>(args, "control_families"),
                    GetArg<string>(args, "resource_types"),
                    GetArg<bool?>(args, "include_passed") ?? false),

                "compliance_get_control_family" => await _complianceTools.GetControlFamilyInfoAsync(
                    GetArg<string>(args, "family") ?? "",
                    GetArg<bool?>(args, "include_controls") ?? true),

                "compliance_generate_document" => await _complianceTools.GenerateComplianceDocumentAsync(
                    GetArg<string>(args, "document_type") ?? "ssp",
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "framework"),
                    GetArg<string>(args, "system_name")),

                "compliance_collect_evidence" => await _complianceTools.CollectComplianceEvidenceAsync(
                    GetArg<string>(args, "control_id") ?? "",
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "resource_group")),

                "compliance_remediate" => await _complianceTools.RemediateComplianceFindingAsync(
                    GetArg<string>(args, "finding_id") ?? "",
                    GetArg<bool?>(args, "apply_remediation") ?? false,
                    GetArg<bool?>(args, "dry_run") ?? true),

                // Discovery Tools - signatures match DiscoveryMcpTools
                "discovery_resources" => await _discoveryTools.DiscoverResourcesAsync(
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<string>(args, "resource_group"),
                    GetArg<string>(args, "resource_type"),
                    GetArg<string>(args, "location"),
                    GetArg<string>(args, "tag_filter")),

                "discovery_resource_details" => await _discoveryTools.GetResourceDetailsAsync(
                    GetArg<string>(args, "resource_id") ?? "",
                    GetArg<bool?>(args, "include_metrics") ?? false,
                    GetArg<bool?>(args, "include_compliance") ?? false),

                "discovery_resource_health" => await _discoveryTools.GetResourceHealthAsync(
                    GetArg<string>(args, "resource_id"),
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "resource_group")),

                "discovery_dependencies" => await _discoveryTools.MapResourceDependenciesAsync(
                    GetArg<string>(args, "resource_id") ?? "",
                    GetArg<int?>(args, "depth") ?? 2,
                    GetArg<bool?>(args, "include_network") ?? true),

                "discovery_subscriptions" => await _discoveryTools.ListSubscriptionsAsync(
                    GetArg<bool?>(args, "include_disabled") ?? false),

                // Infrastructure Tools - signatures match InfrastructureMcpTools
                "infrastructure_generate_template" => await _infrastructureTools.GenerateTemplateAsync(
                    GetArg<string>(args, "resource_type") ?? "",
                    GetArg<string>(args, "template_format") ?? "bicep",
                    GetArg<string>(args, "compliance_level"),
                    GetArg<string>(args, "location"),
                    GetArg<string>(args, "resource_name"),
                    GetArg<string>(args, "additional_requirements")),

                "infrastructure_provision" => await _infrastructureTools.ProvisionResourcesAsync(
                    GetArg<string>(args, "template_path") ?? "",
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<string>(args, "resource_group") ?? "",
                    GetArg<string>(args, "location"),
                    GetArg<Dictionary<string, object>>(args, "parameters"),
                    GetArg<bool?>(args, "validate_only") ?? false),

                "infrastructure_delete" => await _infrastructureTools.DeleteResourcesAsync(
                    GetArg<string>(args, "resource_id") ?? "",
                    GetArg<bool?>(args, "dry_run") ?? true,
                    GetArg<bool?>(args, "force") ?? false,
                    GetArg<bool?>(args, "delete_associated_resources") ?? false),

                "infrastructure_analyze_scaling" => await _infrastructureTools.AnalyzeScalingAsync(
                    GetArg<string>(args, "resource_id") ?? "",
                    GetArg<int?>(args, "analysis_window_days") ?? 30,
                    GetArg<bool?>(args, "include_recommendations") ?? true),

                "infrastructure_azure_arc" => await _infrastructureTools.ManageAzureArcAsync(
                    GetArg<string>(args, "operation") ?? "",
                    GetArg<string>(args, "resource_id"),
                    GetArg<string>(args, "machine_name"),
                    GetArg<string>(args, "subscription_id")),

                // Cost Management Tools - signatures match CostManagementMcpTools
                "cost_analyze" => await _costManagementTools.AnalyzeCostsAsync(
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<string>(args, "resource_group"),
                    GetArg<string>(args, "time_range"),
                    GetArg<string>(args, "group_by")),

                "cost_optimize" => await _costManagementTools.GetOptimizationRecommendationsAsync(
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<string>(args, "resource_group"),
                    GetArg<string>(args, "resource_type"),
                    GetArg<decimal?>(args, "minimum_savings")),

                "cost_forecast" => await _costManagementTools.ForecastCostsAsync(
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<int?>(args, "forecast_months") ?? 3,
                    GetArg<bool?>(args, "include_seasonality") ?? true),

                "cost_budget" => await _costManagementTools.ManageBudgetAsync(
                    GetArg<string>(args, "operation") ?? "",
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<string>(args, "budget_name"),
                    GetArg<decimal?>(args, "amount"),
                    GetArg<string>(args, "time_grain"),
                    GetArg<decimal?>(args, "alert_threshold")),

                "cost_anomaly" => await _costManagementTools.DetectAnomaliesAsync(
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<string>(args, "resource_group"),
                    GetArg<int?>(args, "lookback_days") ?? 30,
                    GetArg<decimal?>(args, "sensitivity_threshold") ?? 0.8m),

                "cost_scenario" => await _costManagementTools.ModelCostScenarioAsync(
                    GetArg<string>(args, "scenario_type") ?? "",
                    GetArg<string>(args, "subscription_id") ?? "",
                    GetArg<Dictionary<string, object>>(args, "scenario_parameters")),

                // Knowledge Base Tools - signatures match KnowledgeBaseMcpTools
                "kb_explain_nist" => await _knowledgeBaseTools.ExplainNistControlAsync(
                    GetArg<string>(args, "control_id") ?? "",
                    GetArg<string>(args, "impact_level"),
                    GetArg<bool?>(args, "include_enhancements") ?? false),

                "kb_search_nist" => await _knowledgeBaseTools.SearchNistControlsAsync(
                    GetArg<string>(args, "query") ?? "",
                    GetArg<string>(args, "control_family"),
                    GetArg<string>(args, "impact_level"),
                    GetArg<int?>(args, "max_results") ?? 10),

                "kb_explain_rmf" => await _knowledgeBaseTools.ExplainRmfAsync(
                    GetArg<string>(args, "rmf_step"),
                    GetArg<string>(args, "topic")),

                "kb_explain_stig" => await _knowledgeBaseTools.ExplainStigAsync(
                    GetArg<string>(args, "stig_id") ?? "",
                    GetArg<string>(args, "rule_id"),
                    GetArg<bool?>(args, "include_fixes") ?? true),

                "kb_search_stigs" => await _knowledgeBaseTools.SearchStigsAsync(
                    GetArg<string>(args, "query") ?? "",
                    GetArg<string>(args, "technology"),
                    GetArg<string>(args, "severity"),
                    GetArg<int?>(args, "max_results") ?? 10),

                "kb_fedramp_template" => await _knowledgeBaseTools.GetFedRampTemplateAsync(
                    GetArg<string>(args, "template_type") ?? "",
                    GetArg<string>(args, "impact_level")),

                "kb_impact_level" => await _knowledgeBaseTools.DetermineImpactLevelAsync(
                    GetArg<string>(args, "operation") ?? "",
                    GetArg<string>(args, "system_type"),
                    GetArg<string>(args, "data_types"),
                    GetArg<string>(args, "compare_level_1"),
                    GetArg<string>(args, "compare_level_2")),

                // Multi-agent chat via PlatformAgentGroupChat
                "platform_engineering_chat" => await ExecuteChatAsync(
                    GetArg<string>(args, "message") ?? "",
                    GetArg<string>(args, "conversation_id")),

                _ => $"Unknown tool: {toolName}"
            };

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = result }
                },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"Error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    private async Task<string> ExecuteChatAsync(string message, string? conversationId)
    {
        var result = await ProcessChatRequestAsync(message, conversationId);
        return result.Response;
    }

    private McpResponse HandlePromptsList(McpRequest request)
    {
        var prompts = PromptRegistry.GetAllPrompts().Select(p => new
        {
            name = p.Name,
            description = p.Description,
            arguments = p.Arguments.Select(a => new
            {
                name = a.Name,
                description = a.Description,
                required = a.Required
            }).ToList()
        }).ToList();

        return new McpResponse
        {
            Id = request.Id,
            Result = new { prompts }
        };
    }

    private McpResponse HandlePromptsGet(McpRequest request)
    {
        var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
        var promptRequest = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson, _jsonOptions);
        var promptName = promptRequest?.GetValueOrDefault("name")?.ToString();

        if (string.IsNullOrEmpty(promptName))
        {
            return CreateErrorResponse(request.Id, -32602, "Prompt name required");
        }

        var prompt = PromptRegistry.FindPrompt(promptName);
        if (prompt == null)
        {
            return CreateErrorResponse(request.Id, -32602, $"Prompt not found: {promptName}");
        }

        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                description = prompt.Description,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new
                        {
                            type = "text",
                            text = $"Execute the {prompt.Name} prompt with the provided arguments."
                        }
                    }
                }
            }
        };
    }

    private McpResponse HandlePing(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new { status = "ok", timestamp = DateTime.UtcNow }
        };
    }

    private static McpTool CreateTool(string name, string description, object inputSchema)
    {
        return new McpTool
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema
        };
    }

    private static McpResponse CreateErrorResponse(object id, int code, string message, string? data = null)
    {
        return new McpResponse
        {
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };
    }

    private static T? GetArg<T>(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
        {
            return default;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        if (value is JsonElement jsonElement)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            catch
            {
                return default;
            }
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}
