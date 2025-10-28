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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Specialized agent for Azure cost analysis, budget management, and cost optimization
/// </summary>
public class CostManagementAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.CostManagement;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<CostManagementAgent> _logger;

    public CostManagementAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<CostManagementAgent> logger,
        CostManagementPlugin costManagementPlugin)
    {
        _logger = logger;
        
        // Create specialized kernel for cost management operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.CostManagement);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Register cost management plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(costManagementPlugin, "CostManagementPlugin"));

        _logger.LogInformation("✅ Cost Management Agent initialized with specialized kernel");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("💰 Cost Management Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.CostManagement,
            Success = false
        };

        try
        {
            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Build system prompt for cost expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context
            var userMessage = BuildUserMessage(task, previousResults);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Execute with moderate temperature for analytical cost assessments
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3, // Low temperature for precise cost analysis
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract cost metadata
            var metadata = ExtractMetadata(result, task);
            response.Metadata = metadata;

            // Extract estimated cost
            response.EstimatedCost = (decimal)ExtractEstimatedCost(result.Content);

            // Check if within budget (extract budget from parameters if provided)
            var budget = ExtractBudget(task.Parameters);
            response.IsWithinBudget = budget == null || response.EstimatedCost <= (decimal)budget.Value;

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.CostManagement,
                AgentType.Orchestrator,
                $"Cost analysis completed. Estimated: ${response.EstimatedCost:N2}/month, Within budget: {response.IsWithinBudget}",
                new Dictionary<string, object>
                {
                    ["estimatedCost"] = response.EstimatedCost,
                    ["isWithinBudget"] = response.IsWithinBudget,
                    ["budget"] = budget ?? 0,
                    ["analysis"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Cost Management Agent completed task: {TaskId}. Estimated cost: ${Cost:N2}/month, Within budget: {WithinBudget}",
                task.TaskId, response.EstimatedCost, response.IsWithinBudget);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Cost Management Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized Azure Cost Management and Optimization expert with deep expertise in:

**Azure Cost Analysis:**
- Cost estimation for all Azure services (VMs, AKS, Storage, Networking, etc.)
- TCO (Total Cost of Ownership) calculations
- Regional pricing variations
- Reserved instance and savings plan benefits
- License optimization (AHUB, Dev/Test pricing)

**Cost Optimization Strategies:**
- Right-sizing recommendations (VMs, storage, databases)
- Auto-scaling and reserved capacity strategies
- Spot instance opportunities
- Storage tier optimization (Hot, Cool, Archive)
- Network cost reduction techniques

**Budget Management:**
- Budget allocation and tracking
- Cost anomaly detection
- Spending forecasts and trend analysis

**🤖 Conversational Requirements Gathering**

When a user asks about costs, optimization, or budgets, use a conversational approach to gather context:

**For Cost Analysis Requests, ask about:**
- **Scope**: ""What would you like me to analyze?""
  - Entire subscription
  - Specific resource group
  - Particular resource types (AKS, VMs, Storage, etc.)
  - Time period for analysis (last month, last 90 days, year-to-date)
- **Breakdown Preference**: ""How would you like costs broken down?""
  - By service type
  - By resource group
  - By location
  - By tags (cost center, environment, etc.)
- **Subscription ID**: If not provided, ask: ""Which subscription should I analyze?""

**For Cost Optimization Requests, ask about:**
- **Focus Area**: ""What type of optimization are you looking for?""
  - Compute (VMs, AKS nodes, App Service plans)
  - Storage (tier optimization, unused disks)
  - Networking (bandwidth, data transfer)
  - Databases (SKU rightsizing, reserved capacity)
  - All of the above
- **Constraints**: ""Are there any constraints I should know about?""
  - Must maintain current performance
  - Can tolerate some downtime for changes
  - Prefer automated recommendations only
  - Need manual review before changes
- **Savings Target**: ""Do you have a target for cost reduction?""
  - Percentage (e.g., reduce by 20%)
  - Dollar amount (e.g., save $5,000/month)
  - Just show all opportunities

**For Budget Management Requests, ask about:**
- **Budget Amount**: ""What's your monthly budget?""
  - Dollar amount
  - Based on current spending + buffer
- **Alert Thresholds**: ""When should I alert you?""
  - At 50%, 75%, 90%, 100% of budget
  - Custom thresholds
- **Scope**: ""What should this budget cover?""
  - Entire subscription
  - Specific resource groups
  - Tagged resources only
- **Actions**: ""What should happen when budget is exceeded?""
  - Email notifications only
  - Automated cost-cutting actions
  - Both

**Example Conversation Flow:**

User: ""How much am I spending on Azure?""
You: ""I'd be happy to help analyze your Azure spending! To give you the most relevant information, I need a few details:

1. Which subscription should I analyze? (You can provide a name or subscription ID)
2. What time period? (last month, last 90 days, year-to-date)
3. Would you like costs broken down by service type, resource group, or location?

Let me know your preferences!""

User: ""subscription 453c..., last month, by service""
You: **[IMMEDIATELY call get_cost_analysis_by_service or similar function - DO NOT ask for confirmation]**

**CRITICAL: One Question Cycle Only!**
- First message: User asks about costs → Ask for missing critical info
- Second message: User provides answers → **IMMEDIATELY call the appropriate cost function**
- DO NOT ask ""Should I proceed?"" or ""Any adjustments needed?""
- DO NOT repeat questions - use smart defaults for minor missing details
- Cost allocation by tags and resource groups
- Showback/chargeback reporting

**FinOps Best Practices:**
- Cost visibility and transparency
- Cost allocation tagging strategies
- Reserved instance coverage optimization
- Commitment-based discounts (Azure Reservations, Savings Plans)
- Waste identification (unused resources, orphaned disks, etc.)

**Response Format:**
When analyzing costs:
1. Estimate monthly costs in USD with itemized breakdown
2. Identify cost drivers (top 3-5 services/resources)
3. Compare with budget if provided
4. Recommend optimization opportunities
5. Provide potential savings percentage

Always format costs as currency (e.g., $1,234.56/month) and be specific about resource SKUs and regions.";
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

        // Add context from previous agent results (especially infrastructure details)
        if (previousResults.Any())
        {
            message += "Context from other agents:\n";
            foreach (var prevResult in previousResults.TakeLast(3)) // Last 3 results for context
            {
                if (prevResult.AgentType == AgentType.Infrastructure && prevResult.Metadata != null)
                {
                    message += $"- Infrastructure resources: ";
                    if (prevResult.Metadata.ContainsKey("resourceTypes"))
                    {
                        message += $"{prevResult.Metadata["resourceTypes"]}\n";
                    }
                }
                var contentLength = prevResult.Content?.Length ?? 0;
                if (contentLength > 0)
                {
                    message += $"- {prevResult.AgentType}: {prevResult.Content?.Substring(0, Math.Min(200, contentLength))}...\n";
                }
            }
            message += "\n";
        }

        message += "Please provide a comprehensive cost analysis with itemized breakdown and optimization recommendations.";

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.CostManagement.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "CostManagementPlugin functions";
        }

        // Extract services mentioned
        var services = ExtractAzureServices(result.Content);
        if (services.Any())
        {
            metadata["azureServices"] = string.Join(", ", services);
        }

        return metadata;
    }

    private List<string> ExtractAzureServices(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new List<string>();

        var services = new List<string>();
        var commonServices = new[]
        {
            "Virtual Machine", "VM", "AKS", "Kubernetes", "Storage", "SQL", "Cosmos",
            "App Service", "Function", "Container", "VNet", "Load Balancer", "Application Gateway",
            "Key Vault", "Monitor", "Log Analytics"
        };

        foreach (var service in commonServices)
        {
            if (content.Contains(service, StringComparison.OrdinalIgnoreCase))
            {
                services.Add(service);
            }
        }

        return services.Distinct().ToList();
    }

    private double ExtractEstimatedCost(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return 0.0;

        // Try to extract cost patterns like "$1,234.56/month", "cost: $500", "estimated: $2,500.00", etc.
        var patterns = new[]
        {
            @"\$\s*([\d,]+\.?\d*)\s*(?:/month|per month|monthly)?",
            @"(?:cost|estimated|total)[:\s]+\$\s*([\d,]+\.?\d*)",
            @"([\d,]+\.?\d*)\s*USD",
            @"approximately\s+\$\s*([\d,]+\.?\d*)"
        };

        var costs = new List<double>();

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var costStr = match.Groups[1].Value.Replace(",", "");
                if (double.TryParse(costStr, out var cost))
                {
                    costs.Add(cost);
                }
            }
        }

        // Return the maximum cost found (likely the total)
        return costs.Any() ? costs.Max() : 0.0;
    }

    private double? ExtractBudget(Dictionary<string, object>? parameters)
    {
        if (parameters == null)
            return null;

        // Look for budget-related parameters
        var budgetKeys = new[] { "budget", "maxCost", "max_cost", "costLimit", "cost_limit" };
        
        foreach (var key in budgetKeys)
        {
            if (parameters.TryGetValue(key, out var budgetObj))
            {
                // Convert to string and remove currency symbols and commas
                var budgetStr = budgetObj?.ToString()?.Replace("$", "").Replace(",", "").Trim();
                
                if (!string.IsNullOrEmpty(budgetStr) && double.TryParse(budgetStr, out var budget))
                {
                    return budget;
                }
            }
        }

        return null;
    }
}
