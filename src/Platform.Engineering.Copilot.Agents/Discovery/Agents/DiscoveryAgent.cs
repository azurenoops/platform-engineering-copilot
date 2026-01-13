using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Discovery.Configuration;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;
using Platform.Engineering.Copilot.Channels.Abstractions;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.Discovery.Agents;

/// <summary>
/// Main Discovery Agent for Azure resource discovery, inventory management, and health monitoring.
/// Enhanced with State and Channels integration for cross-agent coordination.
/// </summary>
public class DiscoveryAgent : BaseAgent
{
    public override string AgentId => "discovery";
    public override string AgentName => "Discovery Agent";
    public override string Description =>
        "Handles Azure resource discovery, inventory management, resource dependency mapping, " +
        "and health monitoring. Can discover resources across subscriptions, analyze tagging compliance, " +
        "and identify orphaned or unused resources.";

    protected override float Temperature => (float)_options.Temperature;
    protected override int MaxTokens => _options.MaxTokens;

    private readonly DiscoveryStateAccessors _stateAccessors;
    private readonly DiscoveryAgentOptions _options;
    private readonly IChannelManager? _channelManager;
    private readonly IStreamingHandler? _streamingHandler;

    public DiscoveryAgent(
        IChatClient chatClient,
        ILogger<DiscoveryAgent> logger,
        IOptions<DiscoveryAgentOptions> options,
        DiscoveryStateAccessors stateAccessors,
        ResourceDiscoveryTool resourceDiscoveryTool,
        SubscriptionListTool subscriptionListTool,
        ResourceDetailsTool resourceDetailsTool,
        DependencyMappingTool dependencyMappingTool,
        ResourceHealthTool resourceHealthTool,
        ResourceGroupListTool resourceGroupListTool,
        ResourceGroupSummaryTool resourceGroupSummaryTool,
        ResourceTagSearchTool resourceTagSearchTool,
        SubscriptionInventoryTool subscriptionInventoryTool,
        IAgentStateManager? agentStateManager = null,
        ISharedMemory? sharedMemory = null,
        IChannelManager? channelManager = null,
        IStreamingHandler? streamingHandler = null)
        : base(chatClient, logger, agentStateManager, sharedMemory)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _options = options?.Value ?? new DiscoveryAgentOptions();
        _channelManager = channelManager;
        _streamingHandler = streamingHandler;

        // Register tools
        RegisterTool(resourceDiscoveryTool);
        RegisterTool(subscriptionListTool);
        RegisterTool(resourceDetailsTool);
        RegisterTool(dependencyMappingTool);
        RegisterTool(resourceHealthTool);
        RegisterTool(resourceGroupListTool);
        RegisterTool(resourceGroupSummaryTool);
        RegisterTool(resourceTagSearchTool);
        RegisterTool(subscriptionInventoryTool);

        Logger.LogInformation("✅ Discovery Agent initialized (Temperature: {Temperature}, MaxTokens: {MaxTokens}, " +
            "HealthMonitoring: {HealthMonitoring}, DependencyMapping: {DependencyMapping})",
            _options.Temperature, _options.MaxTokens, 
            _options.EnableHealthMonitoring, _options.EnableDependencyMapping);
    }

    /// <summary>
    /// Override ProcessAsync to add Discovery-specific behavior with Channels integration.
    /// </summary>
    public override async Task<AgentResponse> ProcessAsync(
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        // Notify via channel that discovery is starting
        await NotifyChannelAsync(context.ConversationId, MessageType.AgentThinking, 
            "Analyzing resources discovery request...", cancellationToken);

        try
        {
            // Check for subscription context in shared memory
            var currentSubscription = await _stateAccessors.GetCurrentSubscriptionAsync(
                context.ConversationId, cancellationToken);
            
            if (!string.IsNullOrEmpty(currentSubscription))
            {
                Logger.LogDebug("Using subscription from context: {SubscriptionId}", currentSubscription);
            }
            else if (!string.IsNullOrEmpty(_options.DefaultSubscriptionId))
            {
                // Store default subscription in context
                await _stateAccessors.SetCurrentSubscriptionAsync(
                    context.ConversationId, _options.DefaultSubscriptionId, null, cancellationToken);
                Logger.LogDebug("Using default subscription: {SubscriptionId}", _options.DefaultSubscriptionId);
            }

            // Notify progress
            await NotifyChannelAsync(context.ConversationId, MessageType.ProgressUpdate,
                "Executing resource discovery...", cancellationToken);

            // Call base implementation for actual processing
            var response = await base.ProcessAsync(context, cancellationToken);

            // Track the operation in state
            var duration = DateTime.UtcNow - startTime;
            await _stateAccessors.TrackDiscoveryOperationAsync(
                context.ConversationId,
                "discovery",
                currentSubscription ?? _options.DefaultSubscriptionId ?? "unknown",
                0, // Resource count from response would go here
                duration,
                cancellationToken);

            // Notify completion via channel
            await NotifyChannelAsync(context.ConversationId, MessageType.AgentResponse,
                JsonSerializer.Serialize(new
                {
                    agentName = AgentName,
                    success = response.Success,
                    durationMs = (int)duration.TotalMilliseconds,
                    toolsUsed = response.ToolsExecuted?.Select(t => t.ToolName).ToList() ?? new List<string>()
                }), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Discovery Agent failed");
            
            await NotifyChannelAsync(context.ConversationId, MessageType.Error,
                $"Discovery failed: {ex.Message}", cancellationToken);

            return new AgentResponse
            {
                AgentId = AgentId,
                AgentName = AgentName,
                Content = $"Discovery failed: {ex.Message}",
                Success = false
            };
        }
    }

    protected override string GetSystemPrompt()
    {
        var subscriptionInfo = !string.IsNullOrEmpty(_options.DefaultSubscriptionId)
            ? $@"

## Default Configuration
- **Default Subscription ID**: {_options.DefaultSubscriptionId}
- When users don't specify a subscription, automatically use the default subscription ID
- ALWAYS use the default subscription when available unless user explicitly specifies a different one"
            : @"

## No Default Subscription
- No default subscription is configured
- Ask user for subscription ID when needed, or use list_subscriptions tool first";

        var healthInfo = _options.EnableHealthMonitoring
            ? "- `get_resource_health`: Check health status and alerts for resources"
            : "";

        var dependencyInfo = _options.EnableDependencyMapping
            ? "- `map_resource_dependencies`: Map dependencies between resources (NICs, Disks, VNets)"
            : "";

        return $"""
            You are the Discovery Agent for the Platform Engineering Copilot system. Your expertise is in:

            ## Core Capabilities
            - Comprehensive Azure resource discovery and inventory
            - Resource filtering by type, location, tags, and resource group
            - Resource dependency mapping and visualization
            - Resource health monitoring and troubleshooting
            - Tagging analysis and compliance checking
            - Orphaned resource detection

            ## Available Tools
            - `list_subscriptions`: List all accessible Azure subscriptions
            - `discover_azure_resources`: Discover resources with filtering options
            - `get_resource_details`: Get detailed properties of a specific resource
            {dependencyInfo}
            {healthInfo}
            {subscriptionInfo}

            ## Discovery Workflow
            1. If subscription is unknown, use list_subscriptions to find available subscriptions
            2. Use discover_azure_resources with appropriate filters
            3. For specific resources, use get_resource_details
            4. For architecture understanding, use map_resource_dependencies
            5. For troubleshooting, use get_resource_health

            ## Response Guidelines
            - When listing resources, provide counts and summaries by type/location
            - Include resource IDs in responses for follow-up operations
            - Highlight any issues (missing tags, unhealthy resources, orphaned items)
            - Suggest related operations when helpful (e.g., "I can also map dependencies for this VM")

            ## Discovery Boundaries
            You handle DISCOVERY and INVENTORY operations:
            ✅ List and discover Azure resources
            ✅ Get resource details and properties
            ✅ Map resource dependencies
            ✅ Check resource health status
            ✅ Analyze tagging and compliance

            You do NOT handle:
            ❌ Creating or modifying resources (hand off to Infrastructure Agent)
            ❌ Compliance remediation (hand off to Compliance Agent)
            ❌ Cost analysis and optimization (hand off to Cost Management Agent)

            ## Important
            - Use tools proactively when you have enough information
            - Don't ask for subscription if a default is configured
            - Provide concise summaries with option for more details
            """;
    }

    /// <summary>
    /// Helper to send notifications via channel manager if available.
    /// </summary>
    private async Task NotifyChannelAsync(
        string conversationId, 
        MessageType messageType, 
        string content,
        CancellationToken cancellationToken)
    {
        if (_channelManager == null) return;

        try
        {
            var message = new ChannelMessage
            {
                ConversationId = conversationId,
                Type = messageType,
                Content = content,
                AgentType = AgentId,
                Timestamp = DateTime.UtcNow
            };
            await _channelManager.SendToConversationAsync(conversationId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send channel notification: {MessageType}", messageType);
        }
    }
}
