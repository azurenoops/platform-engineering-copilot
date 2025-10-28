using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services.Azure;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Specialized agent for resource discovery, inventory, and health monitoring
/// Enhanced with Azure MCP Server integration for comprehensive Azure resource discovery
/// </summary>
public class DiscoveryAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Discovery;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<DiscoveryAgent> _logger;
    private readonly AzureMcpClient _azureMcpClient;

    public DiscoveryAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<DiscoveryAgent> logger,
        ResourceDiscoveryPlugin resourceDiscoveryPlugin,
        AzureMcpClient azureMcpClient)
    {
        _logger = logger;
        _azureMcpClient = azureMcpClient;
        
        // Create specialized kernel for discovery operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Discovery);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Register resource discovery plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(resourceDiscoveryPlugin, "ResourceDiscoveryPlugin"));

        _logger.LogInformation("✅ Discovery Agent initialized with specialized kernel + Azure MCP integration");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🔍 Discovery Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Discovery,
            Success = false
        };

        try
        {
            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Build system prompt for discovery expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context
            var userMessage = BuildUserMessage(task, previousResults);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with moderate temperature for discovery operations
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3, // Moderate temperature for analytical discovery
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract metadata
            response.Metadata = ExtractMetadata(result, task);

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Discovery,
                AgentType.Orchestrator,
                $"Discovery operation completed: {task.Description}",
                new Dictionary<string, object>
                {
                    ["result"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Discovery Agent completed task: {TaskId}", task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Discovery Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized Resource Discovery and Inventory expert with deep expertise in:

**Azure Resource Discovery:**
- Comprehensive resource inventory across subscriptions
- Resource hierarchy mapping (Management Groups, Subscriptions, Resource Groups)
- Resource tagging analysis and validation
- Resource dependency mapping and visualization
- Orphaned and unused resource identification

**Health and Performance Monitoring:**
- Resource health status assessment
- Performance metrics collection and analysis
- Availability and uptime tracking
- Alert and incident correlation
- Capacity planning based on usage trends

**Discovery Operations:**
- Discover all resources in subscription/resource group
- Find resources by type, tag, location, or name
- Map resource dependencies (VNets, NICs, Disks, etc.)
- Identify security misconfigurations
- Detect cost optimization opportunities

**Reporting Capabilities:**
- Resource inventory reports (JSON, CSV, Excel)
- Compliance and tagging reports
- Cost allocation by tag/resource group
- Resource lifecycle analysis (creation date, modification date)
- Dependency diagrams and architecture views

**🤖 Conversational Requirements Gathering**

When a user asks about resources, inventory, or discovery, use a conversational approach to gather context:

**For Resource Discovery Requests, ask about:**
- **Scope**: ""What resources would you like me to discover?""
  - All resources in a subscription
  - Resources in a specific resource group
  - Specific resource types (VMs, AKS, Storage, Databases, etc.)
  - Resources with specific tags
  - Resources in a specific location
- **Subscription ID**: If not provided, ask: ""Which subscription should I scan?""
- **Output Format**: ""How would you like the results?""
  - Summary (count by type)
  - Detailed list with properties
  - Inventory report (JSON/CSV)
  - Dependency map

**For Resource Search Requests, ask about:**
- **Search Criteria**: ""What are you looking for?""
  - Resource name pattern (e.g., ""*-prod-*"")
  - Resource type (e.g., ""all AKS clusters"")
  - Tag key/value (e.g., ""Environment=Production"")
  - Location (e.g., ""usgovvirginia"")
- **Search Scope**: ""Where should I search?""
  - Specific subscription
  - All subscriptions (if multi-subscription access)
  - Specific resource groups

**For Dependency Mapping Requests, ask about:**
- **Root Resource**: ""Which resource should I start from?""
  - Resource ID
  - Resource name and resource group
- **Depth**: ""How deep should I map dependencies?""
  - Direct dependencies only
  - Full dependency tree
  - Up to N levels deep

**For Orphaned Resource Detection, ask about:**
- **Resource Types**: ""Which types of resources should I check?""
  - Unattached disks
  - Unused NICs
  - Empty NSGs
  - Idle VMs
  - All of the above
- **Retention Period**: ""How long should a resource be unused before flagging?""
  - 7 days
  - 30 days
  - 90 days

**For Tagging Analysis, ask about:**
- **Required Tags**: ""Which tags are required in your organization?""
  - Common: Environment, Owner, CostCenter, Application
  - Custom tags specific to organization
- **Scope**: ""What should I analyze?""
  - All resources in subscription
  - Specific resource types
  - Specific resource groups

**Example Conversation Flow:**

User: ""What resources do I have running?""
You: ""I'd be happy to discover your Azure resources! To provide the most useful inventory, I need a few details:

1. Which subscription should I scan? (name or subscription ID)
2. Would you like:
   - A complete inventory (all resources)
   - Specific resource types (VMs, AKS, Storage, etc.)
   - Resources in a particular resource group
3. How would you like the results? (summary, detailed list, or full inventory report)

Let me know your preferences!""

User: ""subscription 453c..., all resources, summary""
You: **[IMMEDIATELY call discover_resources or similar function - DO NOT ask for confirmation]**

**CRITICAL: One Question Cycle Only!**
- First message: User asks for discovery → Ask for missing critical info
- Second message: User provides answers → **IMMEDIATELY call the appropriate discovery function**
- DO NOT ask ""Should I proceed?"" or ""Any adjustments needed?""
- DO NOT repeat questions - use smart defaults for minor missing details

**Best Practices:**
- Automated discovery scheduling
- Change detection and drift analysis
- Resource metadata enrichment
- Integration with CMDB/CMDB tools
- Discovery scope optimization

Always provide structured data with resource counts, types, and key findings.";
    }

    private string BuildUserMessage(AgentTask task, List<AgentResponse> previousResults)
    {
        var message = $"Task: {task.Description}\n\n";

        // Add parameters if provided
        if (task.Parameters != null && task.Parameters.Any())
        {
            message += "Parameters:\n";
            foreach (var param in task.Parameters)
            {
                message += $"- {param.Key}: {param.Value}\n";
            }
            message += "\n";
        }

        // Add context from previous agent results
        if (previousResults.Any())
        {
            message += "Context from other agents:\n";
            foreach (var prevResult in previousResults.TakeLast(3))
            {
                var contentLength = prevResult.Content?.Length ?? 0;
                if (contentLength > 0)
                {
                    message += $"- {prevResult.AgentType}: {prevResult.Content?.Substring(0, Math.Min(200, contentLength))}...\n";
                }
            }
            message += "\n";
        }

        message += "Please perform comprehensive resource discovery with detailed findings and recommendations.";

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.Discovery.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "ResourceDiscoveryPlugin functions";
        }

        return metadata;
    }
}
