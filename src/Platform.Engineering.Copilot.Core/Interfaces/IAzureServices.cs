using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.ResourceManager;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces
{
    public interface IAzureMetricsService
    {
        Task<List<MetricDataPoint>> GetMetricsAsync(string resourceId, string metricName, DateTime startDate, DateTime endDate);
    }

    /// <summary>
    /// Service interface for Azure resource health monitoring
    /// </summary>
    public interface IAzureResourceHealthService
    {
        Task<ResourceHealthSummary> GetResourceHealthSummaryAsync(string subscriptionId, CancellationToken cancellationToken = default);
        Task<List<ResourceHealthStatus>> GetUnhealthyResourcesAsync(string subscriptionId, CancellationToken cancellationToken = default);
        Task<ResourceHealthStatus?> GetResourceHealthAsync(string resourceId, CancellationToken cancellationToken = default);
        Task<List<ResourceHealthAlert>> GetHealthAlertsAsync(string subscriptionId, CancellationToken cancellationToken = default);
        Task<ResourceHealthDashboard> GenerateHealthDashboardAsync(string subscriptionId, CancellationToken cancellationToken = default);
    }

    public interface IAzureCostManagementService
    {
        Task<CostData> GetCurrentMonthCostsAsync(string subscriptionId);
        Task<decimal> GetResourceMonthlyCostAsync(string resourceId);
        Task<decimal> GetMonthlyTotalAsync(string subscriptionId, DateTime month);
        Task<CostMonitoringDashboard> GetCostDashboardAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
        Task<List<CostTrend>> GetCostTrendsAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
        Task<List<BudgetStatus>> GetBudgetsAsync(string subscriptionId, CancellationToken cancellationToken = default);
        Task<List<CostOptimizationRecommendation>> GetOptimizationRecommendationsAsync(string subscriptionId, CancellationToken cancellationToken = default);
        Task<List<CostAnomaly>> DetectCostAnomaliesAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
        Task<CostForecast> GetCostForecastAsync(string subscriptionId, int forecastDays, CancellationToken cancellationToken = default);
        Task<List<ResourceCostBreakdown>> GetResourceCostBreakdownAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Comprehensive Azure Resource Service for managing Azure resources and operations
    /// </summary>
    public interface IAzureResourceService
    {
        // Original methods
        Task<List<AzureResource>> ListAllResourcesAsync(string subscriptionId);
        Task<List<AzureResource>> ListAllResourcesAsync(string subscriptionId, string resourceGroupName);
        Task<AzureResource?> GetResourceAsync(string resourceId);

        // Resource Group operations
        Task<IEnumerable<object>> ListResourceGroupsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default);
        Task<object?> GetResourceGroupAsync(string resourceGroupName, string? subscriptionId = null, CancellationToken cancellationToken = default);
        Task<object> CreateResourceGroupAsync(string resourceGroupName, string location, string? subscriptionId = null, Dictionary<string, string>? tags = null, CancellationToken cancellationToken = default);

        // Resource operations
        Task<IEnumerable<object>> ListResourcesAsync(string resourceGroupName, string? subscriptionId = null, CancellationToken cancellationToken = default);
        Task<object?> GetResourceAsync(string subscriptionId, string resourceGroupName, string resourceType, string resourceName, CancellationToken cancellationToken = default);
        Task<object> CreateResourceAsync(string resourceGroupName, string resourceType, string resourceName, object properties, string? subscriptionId = null, string location = "eastus", Dictionary<string, string>? tags = null, CancellationToken cancellationToken = default);

        // Subscription operations
        Task<IEnumerable<object>> ListSubscriptionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<object>> ListLocationsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default);
        string GetSubscriptionId(string? subscriptionId = null);

        /// <summary>
        /// Creates a new Azure Government subscription.
        /// </summary>
        /// <param name="subscriptionName">Display name for the subscription</param>
        /// <param name="billingScope">Billing scope ID</param>
        /// <param name="managementGroupId">Management group to assign subscription to</param>
        /// <param name="tags">Resource tags to apply</param>
        /// <returns>Subscription ID</returns>
        Task<string> CreateSubscriptionAsync(
            string subscriptionName,
            string billingScope,
            string managementGroupId,
            Dictionary<string, string> tags);

        /// <summary>
        /// Assigns Owner role to a user for the subscription.
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="userEmail">User email address (UPN)</param>
        /// <returns>Role assignment ID</returns>
        Task<string> AssignOwnerRoleAsync(string subscriptionId, string userEmail);

        /// <summary>
        /// Assigns Contributor role to a user for the subscription.
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="userEmail">User email address (UPN)</param>
        /// <returns>Role assignment ID</returns>
        Task<string> AssignContributorRoleAsync(string subscriptionId, string userEmail);

        /// <summary>
        /// Moves subscription to a management group.
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="managementGroupId">Management group ID</param>
        Task MoveToManagementGroupAsync(string subscriptionId, string managementGroupId);

        /// <summary>
        /// Applies resource tags to a subscription.
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="tags">Tags to apply</param>
        Task ApplySubscriptionTagsAsync(string subscriptionId, Dictionary<string, string> tags);

        /// <summary>
        /// Gets subscription details.
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <returns>Subscription information</returns>
        Task<AzureSubscriptionInfo> GetSubscriptionAsync(string subscriptionId);

        /// <summary>
        /// Gets subscription details by display name.
        /// </summary>
        /// <param name="subscriptionName">Subscription display name</param>
        /// <returns>Subscription information</returns>
        /// <exception cref="InvalidOperationException">Thrown when subscription not found or multiple matches exist</exception>
        Task<AzureSubscriptionInfo> GetSubscriptionByNameAsync(string subscriptionName);

        /// <summary>
        /// Deletes a subscription (for cleanup/rollback).
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        Task DeleteSubscriptionAsync(string subscriptionId);

        /// <summary>
        /// Verifies if a subscription name is available.
        /// </summary>
        /// <param name="subscriptionName">Proposed subscription name</param>
        /// <returns>True if available</returns>
        Task<bool> IsSubscriptionNameAvailableAsync(string subscriptionName);

        // Specialized resource creation
        Task<object> CreateAksClusterAsync(string clusterName, string resourceGroupName, string location, Dictionary<string, object>? aksSettings = null, string? subscriptionId = null, CancellationToken cancellationToken = default);
        Task<object> CreateWebAppAsync(string appName, string resourceGroupName, string location, Dictionary<string, object>? appSettings = null, string? subscriptionId = null, CancellationToken cancellationToken = default);
        Task<object> CreateStorageAccountAsync(string storageAccountName, string resourceGroupName, string location, Dictionary<string, object>? storageSettings = null, string? subscriptionId = null, CancellationToken cancellationToken = default);

        // Resource Health
        Task<IEnumerable<object>> GetResourceHealthEventsAsync(string subscriptionId, CancellationToken cancellationToken = default);
        Task<object?> GetResourceHealthAsync(string resourceId, CancellationToken cancellationToken = default);
        Task<IEnumerable<object>> GetResourceHealthHistoryAsync(string subscriptionId, string? resourceId = null, string timeRange = "24h", CancellationToken cancellationToken = default);

        // Monitoring & Alerts
        Task<object> CreateAlertRuleAsync(string subscriptionId, string resourceGroupName, string alertRuleName, CancellationToken cancellationToken = default);
        Task<IEnumerable<object>> ListAlertRulesAsync(string subscriptionId, string? resourceGroupName = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Lists diagnostic settings for a specific resource.
        /// Used to check for NSG flow logs, activity logs, and other diagnostic configurations.
        /// </summary>
        /// <param name="resourceId">Full Azure resource ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of diagnostic settings with their log categories</returns>
        Task<IEnumerable<DiagnosticSettingInfo>> ListDiagnosticSettingsForResourceAsync(string resourceId, CancellationToken cancellationToken = default);

        // ARM Client access for advanced operations
        ArmClient? GetArmClient();

        // Network 
        /// <summary>
        /// Creates a virtual network with automatic subnet generation.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="vnetName">Virtual network name</param>
        /// <param name="vnetCidr">VNet CIDR block (e.g., "10.100.0.0/16")</param>
        /// <param name="region">Azure region (e.g., "usgovvirginia")</param>
        /// <param name="tags">Resource tags</param>
        /// <returns>VNet resource ID</returns>
        Task<string> CreateVirtualNetworkAsync(
            string subscriptionId,
            string resourceGroupName,
            string vnetName,
            string vnetCidr,
            string region,
            Dictionary<string, string> tags);

        /// <summary>
        /// Creates subnets within a virtual network.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="vnetName">Virtual network name</param>
        /// <param name="subnets">Subnet configurations</param>
        /// <returns>List of subnet resource IDs</returns>
        Task<List<string>> CreateSubnetsAsync(
            string subscriptionId,
            string resourceGroupName,
            string vnetName,
            List<SubnetConfiguration> subnets);

        /// <summary>
        /// Generates subnet configurations from VNet CIDR.
        /// Automatically splits CIDR into equal-sized subnets.
        /// </summary>
        /// <param name="vnetCidr">VNet CIDR (e.g., "10.100.0.0/16")</param>
        /// <param name="subnetPrefix">Subnet prefix size (e.g., 24 for /24)</param>
        /// <param name="subnetCount">Number of subnets to create</param>
        /// <param name="missionName">Mission name for subnet naming</param>
        /// <returns>List of subnet configurations</returns>
        List<SubnetConfiguration> GenerateSubnetConfigurations(
            string vnetCidr,
            int subnetPrefix,
            int subnetCount,
            string missionName);

        /// <summary>
        /// Creates a Network Security Group with default rules.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="nsgName">NSG name</param>
        /// <param name="region">Azure region</param>
        /// <param name="defaultRules">Default NSG rules configuration</param>
        /// <param name="tags">Resource tags</param>
        /// <returns>NSG resource ID</returns>
        Task<string> CreateNetworkSecurityGroupAsync(
            string subscriptionId,
            string resourceGroupName,
            string nsgName,
            string region,
            NsgDefaultRules defaultRules,
            Dictionary<string, string> tags);

        /// <summary>
        /// Associates an NSG with a subnet.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="vnetName">Virtual network name</param>
        /// <param name="subnetName">Subnet name</param>
        /// <param name="nsgId">NSG resource ID</param>
        Task AssociateNsgWithSubnetAsync(
            string subscriptionId,
            string resourceGroupName,
            string vnetName,
            string subnetName,
            string nsgId);

        /// <summary>
        /// Enables DDoS Protection Standard on a VNet.
        /// Required for SECRET and above classifications.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="vnetName">Virtual network name</param>
        /// <param name="ddosPlanId">DDoS Protection Plan resource ID (optional, creates if null)</param>
        Task EnableDDoSProtectionAsync(
            string subscriptionId,
            string resourceGroupName,
            string vnetName,
            string? ddosPlanId = null);

        /// <summary>
        /// Configures custom DNS servers for a VNet.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="vnetName">Virtual network name</param>
        /// <param name="dnsServers">List of DNS server IP addresses</param>
        Task ConfigureDnsServersAsync(
            string subscriptionId,
            string resourceGroupName,
            string vnetName,
            List<string> dnsServers);

        /// <summary>
        /// Creates a resource group for network resources.
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="region">Azure region</param>
        /// <param name="tags">Resource tags</param>
        /// <returns>Resource group ID</returns>
        Task<string> CreateResourceGroupAsync(
            string subscriptionId,
            string resourceGroupName,
            string region,
            Dictionary<string, string> tags);

        /// <summary>
        /// Deletes a virtual network (for cleanup/rollback).
        /// </summary>
        /// <param name="subscriptionId">Target subscription ID</param>
        /// <param name="resourceGroupName">Resource group name</param>
        /// <param name="vnetName">Virtual network name</param>
        Task DeleteVirtualNetworkAsync(
            string subscriptionId,
            string resourceGroupName,
            string vnetName);

        /// <summary>
        /// Validates a CIDR block format.
        /// </summary>
        /// <param name="cidr">CIDR block to validate</param>
        /// <returns>True if valid</returns>
        bool ValidateCidr(string cidr);
    }

    public class MetricDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Unit { get; set; }
    }

    public class CostData
    {
        public decimal TotalCost { get; set; }
        public Dictionary<string, decimal> ServiceCosts { get; set; } = new();
        public Dictionary<string, decimal> ResourceGroupCosts { get; set; } = new();
    }

    /// <summary>
    /// NSG default rules configuration
    /// </summary>
    public class NsgDefaultRules
    {
        public bool AllowRdpFromBastion { get; set; } = true;
        public bool AllowSshFromBastion { get; set; } = true;
        public bool DenyAllInboundInternet { get; set; } = true;
        public bool AllowAzureServices { get; set; } = true;
        public string BastionSubnetCidr { get; set; } = "";
    }

    /// <summary>
    /// Diagnostic setting information for a resource
    /// </summary>
    public class DiagnosticSettingInfo
    {
        public string Name { get; set; } = "";
        public List<string> Categories { get; set; } = new();
        public string? WorkspaceId { get; set; }
        public string? StorageAccountId { get; set; }
        public string? EventHubName { get; set; }
    }

    /// <summary>
    /// Azure subscription information
    /// </summary>
    public class AzureSubscriptionInfo
    {
        public string SubscriptionId { get; set; } = "";
        public string SubscriptionName { get; set; } = "";
        public string State { get; set; } = "";
        public string TenantId { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    // Note: Use Platform.Engineering.Copilot.Core.Models.AzureResource instead of this old class
}