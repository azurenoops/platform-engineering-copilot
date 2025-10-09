using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure security hardening and baseline assessment
/// </summary>
public class SecurityPlugin : BaseSupervisorPlugin
{
    private readonly IMcpToolHandler _securityToolHandler;

    public SecurityPlugin(
        IMcpToolHandler securityToolHandler,
        ILogger<SecurityPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _securityToolHandler = securityToolHandler ?? throw new ArgumentNullException(nameof(securityToolHandler));
    }

    [KernelFunction("apply_security_hardening")]
    [Description("Apply security hardening to Azure resources. Implements security best practices, removes public access, enables encryption, configures firewalls, and applies security baselines. Use when user wants to: harden resources, apply security, secure infrastructure, or implement security controls.")]
    public async Task<string> ApplySecurityHardeningAsync(
        [Description("Azure subscription ID containing resources to harden")] string subscriptionId,
        [Description("Resource group to apply hardening to. Optional - hardens entire subscription if not specified.")] string? resourceGroup = null,
        [Description("Specific resource type to harden (e.g., 'storage_account', 'key_vault', 'all'). Optional - hardens all resource types if not specified.")] string? resourceType = null,
        [Description("Security baseline to apply (e.g., 'CIS', 'NIST', 'Azure Security Benchmark'). Optional - applies Azure Security Benchmark if not specified.")] string? baseline = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Apply security hardening to subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(resourceGroup))
                query += $" in resource group {resourceGroup}";
            if (!string.IsNullOrEmpty(resourceType))
                query += $" for {resourceType}";
            if (!string.IsNullOrEmpty(baseline))
                query += $" using {baseline} baseline";

            var toolCall = new McpToolCall
            {
                Name = "infrastructure_provisioning",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["resource_group"] = resourceGroup,
                    ["resource_type"] = resourceType,
                    ["baseline"] = baseline,
                    ["operation"] = "security_hardening"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _securityToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("apply security hardening", ex);
        }
    }

    [KernelFunction("assess_security_baseline")]
    [Description("Assess Azure resources against security baselines. Compares current configuration against CIS, NIST, or Azure Security Benchmark. Identifies gaps and provides remediation guidance. Use when user wants to: check baseline compliance, assess security posture, or find security gaps.")]
    public async Task<string> AssessSecurityBaselineAsync(
        [Description("Azure subscription ID to assess")] string subscriptionId,
        [Description("Security baseline to assess against (e.g., 'CIS Azure Foundations', 'Azure Security Benchmark', 'NIST 800-53')")] string baseline,
        [Description("Resource group to assess. Optional - assesses entire subscription if not specified.")] string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Assess security baseline {baseline} for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(resourceGroup))
                query += $" in resource group {resourceGroup}";

            var toolCall = new McpToolCall
            {
                Name = "ato_compliance",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["baseline"] = baseline,
                    ["resource_group"] = resourceGroup
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _securityToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("assess security baseline", ex);
        }
    }

    [KernelFunction("configure_network_security")]
    [Description("Configure network security for Azure resources. Sets up NSGs, firewalls, private endpoints, service endpoints, and network isolation. Use when user wants to: secure network, configure firewall, set up private connectivity, or isolate resources.")]
    public async Task<string> ConfigureNetworkSecurityAsync(
        [Description("Azure subscription ID containing resources")] string subscriptionId,
        [Description("Resource group containing resources to secure")] string resourceGroup,
        [Description("Network security configuration (e.g., 'enable private endpoints', 'restrict public access', 'configure NSG')")] string securityConfig,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Configure network security in resource group {resourceGroup}: {securityConfig}";

            var toolCall = new McpToolCall
            {
                Name = "infrastructure_provisioning",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["resource_group"] = resourceGroup,
                    ["security_config"] = securityConfig,
                    ["operation"] = "network_security"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _securityToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("configure network security", ex);
        }
    }

    [KernelFunction("enable_security_monitoring")]
    [Description("Enable security monitoring and alerting for Azure resources. Configures Microsoft Defender, Security Center, Log Analytics, and security alerts. Use when user wants to: enable monitoring, set up security alerts, configure Defender, or enable threat detection.")]
    public async Task<string> EnableSecurityMonitoringAsync(
        [Description("Azure subscription ID to enable monitoring for")] string subscriptionId,
        [Description("Monitoring scope (e.g., 'all resources', 'specific resource group', 'critical resources only'). Optional - monitors all resources if not specified.")] string? scope = null,
        [Description("Alert severity threshold (e.g., 'High', 'Medium', 'Low'). Optional - alerts on all severities if not specified.")] string? alertThreshold = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Enable security monitoring for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(scope))
                query += $" for {scope}";
            if (!string.IsNullOrEmpty(alertThreshold))
                query += $" with {alertThreshold} severity threshold";

            var toolCall = new McpToolCall
            {
                Name = "infrastructure_provisioning",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["scope"] = scope,
                    ["alert_threshold"] = alertThreshold,
                    ["operation"] = "enable_monitoring"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _securityToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("enable security monitoring", ex);
        }
    }

    [KernelFunction("configure_encryption")]
    [Description("Configure encryption for Azure resources. Enables encryption at rest, encryption in transit, key management, and customer-managed keys. Use when user wants to: enable encryption, configure keys, secure data, or implement cryptographic controls.")]
    public async Task<string> ConfigureEncryptionAsync(
        [Description("Azure subscription ID containing resources")] string subscriptionId,
        [Description("Resource group containing resources to encrypt")] string resourceGroup,
        [Description("Encryption configuration (e.g., 'enable all encryption', 'customer-managed keys', 'TLS 1.2 minimum')")] string encryptionConfig,
        [Description("Key Vault name for customer-managed keys. Optional - uses Microsoft-managed keys if not specified.")] string? keyVaultName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Configure encryption in resource group {resourceGroup}: {encryptionConfig}";
            if (!string.IsNullOrEmpty(keyVaultName))
                query += $" using Key Vault {keyVaultName}";

            var toolCall = new McpToolCall
            {
                Name = "infrastructure_provisioning",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["resource_group"] = resourceGroup,
                    ["encryption_config"] = encryptionConfig,
                    ["key_vault_name"] = keyVaultName,
                    ["operation"] = "configure_encryption"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _securityToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("configure encryption", ex);
        }
    }

    [KernelFunction("setup_incident_response")]
    [Description("Set up security incident response capabilities. Configures Azure Sentinel, playbooks, automation, and incident workflows. Use when user wants to: set up SIEM, enable incident response, configure automation, or prepare for security events.")]
    public async Task<string> SetupIncidentResponseAsync(
        [Description("Azure subscription ID to configure incident response for")] string subscriptionId,
        [Description("Incident response configuration (e.g., 'enable Sentinel', 'create playbooks', 'configure automation')")] string configuration,
        [Description("Log Analytics workspace name to use. Optional - creates new workspace if not specified.")] string? workspaceName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Set up incident response for subscription {subscriptionId}: {configuration}";
            if (!string.IsNullOrEmpty(workspaceName))
                query += $" using workspace {workspaceName}";

            var toolCall = new McpToolCall
            {
                Name = "infrastructure_provisioning",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["configuration"] = configuration,
                    ["workspace_name"] = workspaceName,
                    ["operation"] = "incident_response"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _securityToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("setup incident response", ex);
        }
    }
}
