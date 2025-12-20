using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Generators.Composite;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Semantic Kernel plugin for Azure infrastructure provisioning
/// Enhanced with Azure MCP Server integration for best practices, schema validation, and Azure Developer CLI
/// Uses natural language queries to provision infrastructure via AI-powered service
/// Example: "Create a storage account named mydata in eastus with Standard_LRS"
/// Split into partial classes for maintainability:
/// - InfrastructurePlugin.cs (main plugin, core, networking, context, documentation)
/// - InfrastructurePlugin.Templates.cs (template generation and file handling)
/// - InfrastructurePlugin.Provisioning.cs (resource provisioning operations)
/// - InfrastructurePlugin.Scaling.cs (predictive scaling and performance analysis)
/// - InfrastructurePlugin.AzureArc.cs (Azure Arc onboarding and extension deployment)
/// </summary>
public partial class InfrastructurePlugin : BaseSupervisorPlugin
{
    private readonly IInfrastructureProvisioningService _infrastructureService;
    private readonly IDynamicTemplateGenerator _templateGenerator;
    private readonly INetworkTopologyDesignService? _networkDesignService;
    private readonly IPredictiveScalingEngine? _scalingEngine;
    private readonly IComplianceAwareTemplateEnhancer? _complianceEnhancer;
    private readonly IPolicyEnforcementService _policyEnforcementService;
    private readonly SharedMemory _sharedMemory;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly ITemplateStorageService _templateStorageService;
    private readonly ConfigService _configService;
    private readonly IAzureResourceService _azureResourceService;
    private readonly IMemoryCache _cache;
    private readonly ICompositeInfrastructureGenerator? _compositeGenerator;
    private string? _currentConversationId; // Set by agent before function calls
    private string? _lastGeneratedTemplateName; // Track the last generated template for retrieval
    
    private const string LAST_SUBSCRIPTION_CACHE_KEY = "infrastructure_last_subscription";

    public InfrastructurePlugin(
        ILogger<InfrastructurePlugin> logger,
        Kernel kernel,
        IInfrastructureProvisioningService infrastructureService,
        IDynamicTemplateGenerator templateGenerator,
        INetworkTopologyDesignService? networkDesignService,
        IPredictiveScalingEngine? scalingEngine,
        IComplianceAwareTemplateEnhancer? complianceEnhancer,
        IPolicyEnforcementService policyEnforcementService,
        SharedMemory sharedMemory,
        AzureMcpClient azureMcpClient,
        ITemplateStorageService templateStorageService,
        ConfigService configService,
        IAzureResourceService azureResourceService,
        IMemoryCache cache,
        ICompositeInfrastructureGenerator? compositeGenerator = null)
        : base(logger, kernel)
    {
        _infrastructureService = infrastructureService;
        _templateGenerator = templateGenerator;
        _networkDesignService = networkDesignService;
        _scalingEngine = scalingEngine;
        _complianceEnhancer = complianceEnhancer;
        _policyEnforcementService = policyEnforcementService ?? throw new ArgumentNullException(nameof(policyEnforcementService));
        _sharedMemory = sharedMemory;
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _templateStorageService = templateStorageService ?? throw new ArgumentNullException(nameof(templateStorageService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _compositeGenerator = compositeGenerator;
    }
    
    // ========== SUBSCRIPTION LOOKUP HELPERS ==========
    
    /// <summary>
    /// Stores the last used subscription ID in cache AND persistent config file for session continuity
    /// </summary>
    private void SetLastUsedSubscription(string subscriptionId)
    {
        _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
        try
        {
            _configService.SetDefaultSubscription(subscriptionId);
            _logger.LogInformation("Stored subscription in persistent config: {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist subscription to config file, will only use cache");
        }
    }
    
    /// <summary>
    /// Gets the last used subscription ID from cache, or persistent config file if cache is empty
    /// </summary>
    private string? GetLastUsedSubscription()
    {
        if (_cache.TryGetValue<string>(LAST_SUBSCRIPTION_CACHE_KEY, out var subscriptionId))
        {
            _logger.LogDebug("Retrieved last used subscription from cache: {SubscriptionId}", subscriptionId);
            return subscriptionId;
        }
        
        try
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Retrieved subscription from persistent config: {SubscriptionId}", subscriptionId);
                _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
                return subscriptionId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read subscription from config file");
        }
        
        return null;
    }
    
    /// <summary>
    /// Resolves a subscription identifier to a GUID. Accepts either a GUID or a friendly name.
    /// </summary>
    private async Task<string> ResolveSubscriptionIdAsync(string? subscriptionIdOrName)
    {
        if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
        {
            var lastUsed = GetLastUsedSubscription();
            if (!string.IsNullOrWhiteSpace(lastUsed))
            {
                _logger.LogInformation("Using last used subscription from session: {SubscriptionId}", lastUsed);
                return lastUsed;
            }
            throw new ArgumentException("Subscription ID or name is required. No previous subscription found in session.", nameof(subscriptionIdOrName));
        }
        
        if (Guid.TryParse(subscriptionIdOrName, out _))
        {
            SetLastUsedSubscription(subscriptionIdOrName);
            return subscriptionIdOrName;
        }
        
        try
        {
            var subscription = await _azureResourceService.GetSubscriptionByNameAsync(subscriptionIdOrName);
            _logger.LogInformation("Resolved subscription name '{Name}' to ID '{SubscriptionId}' via Azure API", 
                subscriptionIdOrName, subscription.SubscriptionId);
            SetLastUsedSubscription(subscription.SubscriptionId);
            return subscription.SubscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve subscription '{Name}' via Azure API", subscriptionIdOrName);
        }
        
        throw new ArgumentException($"Subscription '{subscriptionIdOrName}' not found. Provide a valid GUID or subscription name.", nameof(subscriptionIdOrName));
    }

    /// <summary>
    /// Set the current conversation ID for context
    /// </summary>
    public void SetConversationId(string conversationId)
    {
        _currentConversationId = conversationId;
        _logger.LogInformation("🆔 InfrastructurePlugin: ConversationId set to: {ConversationId}", conversationId);
    }
}
