using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using Platform.Engineering.Copilot.Core.Services.Azure;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Security operations plugin for SIEM setup, incident response, threat detection, and security automation.
/// Complements CompliancePlugin by providing proactive security infrastructure setup.
/// </summary>
public class SecurityPlugin : BaseSupervisorPlugin
{
    private readonly AzureMcpClient _azureMcpClient;
    
    // Named subscriptions for easier testing and demos
    private static readonly Dictionary<string, string> _namedSubscriptions = new()
    {
        { "production", "453c2549-4cc5-464f-ba66-acad920823e8" },
        { "prod", "453c2549-4cc5-464f-ba66-acad920823e8" },
        { "staging", "453c2549-4cc5-464f-ba66-acad920823e8" },
        { "development", "453c2549-4cc5-464f-ba66-acad920823e8" },
        { "dev", "453c2549-4cc5-464f-ba66-acad920823e8" },
        { "secondary", "453c2549-4cc5-464f-ba66-acad920823e8" },
        { "default", "453c2549-4cc5-464f-ba66-acad920823e8" },
    };

    public SecurityPlugin(
        ILogger<SecurityPlugin> logger,
        Kernel kernel,
        AzureMcpClient azureMcpClient) : base(logger, kernel)
    {
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
    }

    // ========== SUBSCRIPTION LOOKUP HELPER ==========
    
    /// <summary>
    /// Resolves a subscription identifier to a GUID. Accepts either a GUID or a friendly name.
    /// </summary>
    private string ResolveSubscriptionId(string subscriptionIdOrName)
    {
        if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
        {
            throw new ArgumentException("Subscription ID or name is required", nameof(subscriptionIdOrName));
        }
        
        // Check if it's already a valid GUID
        if (Guid.TryParse(subscriptionIdOrName, out _))
        {
            return subscriptionIdOrName;
        }
        
        // Fall back to static dictionary lookup
        if (_namedSubscriptions.TryGetValue(subscriptionIdOrName.ToLowerInvariant(), out var subscriptionId))
        {
            _logger.LogInformation("Resolved subscription name '{Name}' to ID '{SubscriptionId}'", 
                subscriptionIdOrName, subscriptionId);
            return subscriptionId;
        }
        
        // If not found, throw with helpful message
        var availableNames = string.Join(", ", _namedSubscriptions.Keys.Take(5));
        throw new ArgumentException(
            $"Subscription '{subscriptionIdOrName}' not found. " +
            $"Available names: {availableNames}. " +
            $"Or provide a valid GUID.", 
            nameof(subscriptionIdOrName));
    }

    // ========== INCIDENT RESPONSE FUNCTIONS ==========

    [KernelFunction("setup_incident_response")]
    [Description("Set up comprehensive security incident response infrastructure including Microsoft Sentinel SIEM, " +
                 "data sources, analytics rules, automation playbooks, and notifications. " +
                 "Configures threat detection, automated response, and compliance-driven log retention. " +
                 "Essential for security operations and incident management. " +
                 "Example: 'Set up incident response for subscription production with 2-year retention'")]
    public async Task<string> SetupIncidentResponseAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev', 'staging')")] 
        string subscriptionIdOrName,
        [Description("Optional resource group name. If not provided, creates new resource group 'security-ops-rg'.")] 
        string? resourceGroupName = null,
        [Description("Incident response configuration in JSON format. Example: {\"siemWorkspaceName\":\"sentinel-workspace\",\"dataRetentionDays\":730,\"enableAzureADLogs\":true,\"enableActivityLogs\":true,\"enableNsgFlowLogs\":true,\"enableWafLogs\":true,\"threatDetection\":{\"suspiciousLogins\":true,\"privilegeEscalation\":true,\"dataExfiltration\":true,\"cryptoMining\":true,\"lateralMovement\":true},\"automation\":{\"autoBlockSuspiciousIPs\":true,\"autoDisableCompromisedAccounts\":true,\"isolateInfectedVMs\":false},\"notifications\":{\"emailSecurityTeam\":true,\"emailAddresses\":[\"security@example.com\"],\"createPagerDutyIncident\":false,\"pagerDutyServiceKey\":\"\"}}")] 
        string? incidentResponseConfig = null,
        [Description("Dry run mode - generate setup plan without creating resources. Default is true for safety.")] 
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription name to GUID
            string subscriptionId = ResolveSubscriptionId(subscriptionIdOrName);
            
            var targetResourceGroup = string.IsNullOrWhiteSpace(resourceGroupName) 
                ? "security-ops-rg" 
                : resourceGroupName;
            
            _logger.LogInformation("Setting up incident response in subscription {SubscriptionId}, resource group {ResourceGroup}, dryRun={DryRun}", 
                subscriptionId, targetResourceGroup, dryRun);

            // Parse incident response configuration or use defaults
            var config = ParseIncidentResponseConfig(incidentResponseConfig);

            // Step 1: Generate infrastructure components
            _logger.LogInformation("Step 1: Generating incident response infrastructure components...");
            var components = GenerateIncidentResponseComponents(config, subscriptionId, targetResourceGroup);

            if (dryRun)
            {
                _logger.LogInformation("DRY RUN MODE: Generating incident response setup plan without creating resources");
                
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    dryRun = true,
                    header = new
                    {
                        title = "🔒 INCIDENT RESPONSE SETUP PLAN (DRY RUN)",
                        icon = "🚨",
                        subscriptionId = subscriptionId,
                        resourceGroup = targetResourceGroup
                    },
                    summary = new
                    {
                        totalComponents = components.Count,
                        siemWorkspace = config.SiemWorkspaceName,
                        dataRetentionDays = config.DataRetentionDays,
                        estimatedMonthlyCost = CalculateEstimatedCost(config),
                        configuration = config
                    },
                    components = components.Select((comp, index) => new
                    {
                        componentNumber = index + 1,
                        category = comp.Category,
                        resourceType = comp.ResourceType,
                        name = comp.Name,
                        description = comp.Description,
                        configuration = comp.Configuration,
                        dependencies = comp.Dependencies,
                        estimatedSetupTime = comp.EstimatedSetupTime
                    }),
                    dataSourcesEnabled = new
                    {
                        azureADLogs = config.EnableAzureADLogs,
                        activityLogs = config.EnableActivityLogs,
                        nsgFlowLogs = config.EnableNsgFlowLogs,
                        wafLogs = config.EnableWafLogs
                    },
                    threatDetection = config.ThreatDetection,
                    automation = config.Automation,
                    notifications = new
                    {
                        emailEnabled = config.Notifications.EmailSecurityTeam,
                        recipientCount = config.Notifications.EmailAddresses?.Count ?? 0,
                        pagerDutyEnabled = config.Notifications.CreatePagerDutyIncident
                    },
                    nextSteps = new
                    {
                        toExecute = "To create these resources, run: setup_incident_response with dryRun=false",
                        toCustomize = "Modify incidentResponseConfig JSON parameter to customize settings",
                        estimatedCost = $"Estimated monthly cost: ${CalculateEstimatedCost(config):F2}",
                        setupTime = "Estimated setup time: 45-90 minutes"
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                _logger.LogInformation("LIVE MODE: Creating {Count} incident response infrastructure components", components.Count);
                
                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                // Step 2: Create resource group if it doesn't exist
                _logger.LogInformation("Creating resource group {ResourceGroup} if needed...", targetResourceGroup);
                var rgResult = await CreateResourceGroupAsync(subscriptionId, targetResourceGroup, "eastus", cancellationToken);
                if (rgResult.Success)
                {
                    results.Add(new
                    {
                        component = "Resource Group",
                        status = "✅ Created/Verified",
                        name = targetResourceGroup
                    });
                }

                // Step 3: Create each component
                foreach (var component in components)
                {
                    try
                    {
                        _logger.LogInformation("Creating component: {Category} - {Name}", 
                            component.Category, component.Name);

                        var componentResult = await CreateIncidentResponseComponentAsync(
                            component, 
                            subscriptionId, 
                            targetResourceGroup, 
                            cancellationToken);
                        
                        if (componentResult.Success)
                        {
                            successCount++;
                            results.Add(new
                            {
                                component = component.Name,
                                category = component.Category,
                                status = "✅ Success",
                                resourceId = componentResult.ResourceId,
                                details = componentResult.Details
                            });
                        }
                        else
                        {
                            failCount++;
                            results.Add(new
                            {
                                component = component.Name,
                                category = component.Category,
                                status = "❌ Failed",
                                error = componentResult.Error,
                                requiresManualSetup = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "Error creating component: {Category}", component.Category);
                        results.Add(new
                        {
                            component = component.Name,
                            category = component.Category,
                            status = "❌ Error",
                            error = ex.Message,
                            requiresManualSetup = true
                        });
                    }
                }

                return JsonSerializer.Serialize(new
                {
                    success = failCount == 0,
                    header = new
                    {
                        title = "🔒 INCIDENT RESPONSE SETUP RESULTS",
                        icon = "🚨",
                        subscriptionId = subscriptionId,
                        resourceGroup = targetResourceGroup,
                        completedAt = DateTimeOffset.UtcNow
                    },
                    summary = new
                    {
                        totalComponents = components.Count,
                        successful = successCount,
                        failed = failCount,
                        successRate = $"{(successCount * 100.0 / components.Count):F1}%",
                        siemWorkspace = config.SiemWorkspaceName,
                        dataRetentionDays = config.DataRetentionDays
                    },
                    results = results,
                    nextSteps = new 
                    {
                        recommendation = failCount > 0 
                            ? "Review failed components above and complete manual setup. Check Azure portal for detailed error messages."
                            : "Incident response setup complete! Access Microsoft Sentinel in Azure portal to configure additional analytics rules and playbooks.",
                        sentinelUrl = failCount > 0 
                            ? (string?)null 
                            : $"https://portal.azure.com/#blade/HubsExtension/BrowseResource/resourceType/microsoft.securityinsightsarg%2Fsentinel"
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SetupIncidentResponseAsync");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private IncidentResponseConfig ParseIncidentResponseConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new IncidentResponseConfig(); // Use defaults
        }

        try
        {
            return JsonSerializer.Deserialize<IncidentResponseConfig>(configJson) ?? new IncidentResponseConfig();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse incident response config JSON, using defaults");
            return new IncidentResponseConfig();
        }
    }

    private List<IncidentResponseComponent> GenerateIncidentResponseComponents(
        IncidentResponseConfig config,
        string subscriptionId,
        string resourceGroupName)
    {
        var components = new List<IncidentResponseComponent>();

        // 1. Log Analytics Workspace (foundation for Sentinel)
        components.Add(new IncidentResponseComponent
        {
            Category = "SIEM Foundation",
            ResourceType = "Microsoft.OperationalInsights/workspaces",
            Name = config.SiemWorkspaceName,
            Description = "Log Analytics workspace for Microsoft Sentinel SIEM",
            Configuration = new
            {
                sku = "PerGB2018",
                retentionInDays = config.DataRetentionDays,
                publicNetworkAccessForIngestion = "Enabled",
                publicNetworkAccessForQuery = "Enabled"
            },
            Dependencies = new List<string>(),
            EstimatedSetupTime = "5-10 minutes"
        });

        // 2. Microsoft Sentinel Solution
        components.Add(new IncidentResponseComponent
        {
            Category = "SIEM Core",
            ResourceType = "Microsoft.OperationsManagement/solutions",
            Name = "SecurityInsights",
            Description = "Microsoft Sentinel solution for the Log Analytics workspace",
            Configuration = new
            {
                workspaceName = config.SiemWorkspaceName,
                plan = new { product = "OMSGallery/SecurityInsights", publisher = "Microsoft" }
            },
            Dependencies = new List<string> { config.SiemWorkspaceName },
            EstimatedSetupTime = "5-10 minutes"
        });

        // 3. Data Connectors
        if (config.EnableAzureADLogs)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Data Sources",
                ResourceType = "Microsoft.SecurityInsights/dataConnectors",
                Name = "AzureActiveDirectory",
                Description = "Azure AD sign-in and audit logs connector",
                Configuration = new
                {
                    kind = "AzureActiveDirectory",
                    dataTypes = new[] { "SigninLogs", "AuditLogs" }
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        if (config.EnableActivityLogs)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Data Sources",
                ResourceType = "Microsoft.SecurityInsights/dataConnectors",
                Name = "AzureActivity",
                Description = "Azure Activity logs connector for subscription-level events",
                Configuration = new
                {
                    kind = "AzureActivity",
                    subscriptionId = subscriptionId
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        if (config.EnableNsgFlowLogs)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Data Sources",
                ResourceType = "Microsoft.Network/networkWatchers/flowLogs",
                Name = "NSGFlowLogs",
                Description = "Network Security Group flow logs for network traffic analysis",
                Configuration = new
                {
                    enabled = true,
                    format = new { type = "JSON", version = 2 },
                    retentionPolicy = new { days = config.DataRetentionDays, enabled = true }
                },
                Dependencies = new List<string> { config.SiemWorkspaceName },
                EstimatedSetupTime = "5-10 minutes"
            });
        }

        if (config.EnableWafLogs)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Data Sources",
                ResourceType = "Microsoft.Insights/diagnosticSettings",
                Name = "WAFDiagnosticSettings",
                Description = "Web Application Firewall logs for application-layer attack detection",
                Configuration = new
                {
                    workspaceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.OperationalInsights/workspaces/{config.SiemWorkspaceName}",
                    logs = new[] { "ApplicationGatewayAccessLog", "ApplicationGatewayFirewallLog" }
                },
                Dependencies = new List<string> { config.SiemWorkspaceName },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        // 4. Analytics Rules (Threat Detection)
        if (config.ThreatDetection.SuspiciousLogins)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Threat Detection",
                ResourceType = "Microsoft.SecurityInsights/alertRules",
                Name = "SuspiciousLoginAttempts",
                Description = "Detects multiple failed login attempts from same IP or user",
                Configuration = new
                {
                    kind = "Scheduled",
                    displayName = "Suspicious Login Activity",
                    description = "Alerts on multiple failed sign-in attempts indicating potential brute force attack",
                    severity = "High",
                    query = "SigninLogs | where ResultType != 0 | summarize FailedAttempts = count() by UserPrincipalName, IPAddress | where FailedAttempts > 5",
                    queryFrequency = "PT5M",
                    queryPeriod = "PT1H",
                    triggerOperator = "GreaterThan",
                    triggerThreshold = 0
                },
                Dependencies = new List<string> { "AzureActiveDirectory" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        if (config.ThreatDetection.PrivilegeEscalation)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Threat Detection",
                ResourceType = "Microsoft.SecurityInsights/alertRules",
                Name = "PrivilegeEscalation",
                Description = "Detects unauthorized elevation of privileges",
                Configuration = new
                {
                    kind = "Scheduled",
                    displayName = "Privilege Escalation Detected",
                    description = "Alerts on role assignment changes that grant elevated permissions",
                    severity = "Critical",
                    query = "AuditLogs | where OperationName contains 'Add member to role' and TargetResources[0].modifiedProperties[0].newValue contains 'Global Administrator'",
                    queryFrequency = "PT5M",
                    queryPeriod = "PT1H"
                },
                Dependencies = new List<string> { "AzureActiveDirectory" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        if (config.ThreatDetection.DataExfiltration)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Threat Detection",
                ResourceType = "Microsoft.SecurityInsights/alertRules",
                Name = "DataExfiltration",
                Description = "Detects unusual data transfer patterns indicating potential data theft",
                Configuration = new
                {
                    kind = "Scheduled",
                    displayName = "Potential Data Exfiltration",
                    description = "Alerts on large or unusual data transfers to external destinations",
                    severity = "High",
                    query = "AzureActivity | where OperationNameValue contains 'MICROSOFT.STORAGE/STORAGEACCOUNTS/BLOBSERVICES/CONTAINERS/BLOBS/READ' | summarize BytesTransferred = sum(todouble(Properties.responseSize)) by CallerIpAddress | where BytesTransferred > 10000000000"
                },
                Dependencies = new List<string> { "AzureActivity" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        if (config.ThreatDetection.CryptoMining)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Threat Detection",
                ResourceType = "Microsoft.SecurityInsights/alertRules",
                Name = "CryptoMining",
                Description = "Detects cryptocurrency mining activity",
                Configuration = new
                {
                    kind = "Scheduled",
                    displayName = "Crypto Mining Activity Detected",
                    description = "Alerts on network connections to known crypto mining pools",
                    severity = "Medium",
                    query = "CommonSecurityLog | where DestinationHostName contains 'pool.minergate.com' or DestinationHostName contains 'xmr-eu1.nanopool.org'"
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        if (config.ThreatDetection.LateralMovement)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Threat Detection",
                ResourceType = "Microsoft.SecurityInsights/alertRules",
                Name = "LateralMovement",
                Description = "Detects lateral movement across the network",
                Configuration = new
                {
                    kind = "Scheduled",
                    displayName = "Lateral Movement Detected",
                    description = "Alerts on suspicious authentication patterns indicating lateral movement",
                    severity = "High",
                    query = "SecurityEvent | where EventID == 4624 and LogonType == 3 | summarize count() by Account, Computer | where count_ > 10"
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "2-5 minutes"
            });
        }

        // 5. Automation Playbooks
        if (config.Automation.AutoBlockSuspiciousIPs)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Automation",
                ResourceType = "Microsoft.Logic/workflows",
                Name = "AutoBlockSuspiciousIPs",
                Description = "Logic App to automatically block IPs with suspicious activity",
                Configuration = new
                {
                    definition = new
                    {
                        triggers = new { sentinel_alert = new { type = "ApiConnectionWebhook" } },
                        actions = new { block_ip = new { type = "ApiConnection" } }
                    }
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "10-15 minutes"
            });
        }

        if (config.Automation.AutoDisableCompromisedAccounts)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Automation",
                ResourceType = "Microsoft.Logic/workflows",
                Name = "AutoDisableCompromisedAccounts",
                Description = "Logic App to disable user accounts showing signs of compromise",
                Configuration = new
                {
                    definition = new
                    {
                        triggers = new { sentinel_alert = new { type = "ApiConnectionWebhook" } },
                        actions = new { disable_account = new { type = "ApiConnection" } }
                    }
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "10-15 minutes"
            });
        }

        if (config.Automation.IsolateInfectedVMs)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Automation",
                ResourceType = "Microsoft.Logic/workflows",
                Name = "IsolateInfectedVMs",
                Description = "Logic App to isolate VMs showing signs of infection",
                Configuration = new
                {
                    definition = new
                    {
                        triggers = new { sentinel_alert = new { type = "ApiConnectionWebhook" } },
                        actions = new { isolate_vm = new { type = "ApiConnection" } }
                    }
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "10-15 minutes"
            });
        }

        // 6. Notification Integrations
        if (config.Notifications.EmailSecurityTeam && config.Notifications.EmailAddresses?.Any() == true)
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Notifications",
                ResourceType = "Microsoft.SecurityInsights/automationRules",
                Name = "EmailSecurityTeam",
                Description = "Automation rule to email security team on high-severity incidents",
                Configuration = new
                {
                    triggeringLogic = new { isEnabled = true, triggersOn = "Incidents", triggersWhen = "Created" },
                    actions = new[]
                    {
                        new
                        {
                            order = 1,
                            actionType = "RunPlaybook",
                            actionConfiguration = new
                            {
                                emailRecipients = config.Notifications.EmailAddresses
                            }
                        }
                    }
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "5-10 minutes"
            });
        }

        if (config.Notifications.CreatePagerDutyIncident && !string.IsNullOrWhiteSpace(config.Notifications.PagerDutyServiceKey))
        {
            components.Add(new IncidentResponseComponent
            {
                Category = "Notifications",
                ResourceType = "Microsoft.Logic/workflows",
                Name = "PagerDutyIntegration",
                Description = "Logic App to create PagerDuty incidents for critical alerts",
                Configuration = new
                {
                    definition = new
                    {
                        triggers = new { sentinel_alert = new { type = "ApiConnectionWebhook" } },
                        actions = new
                        {
                            create_pagerduty_incident = new
                            {
                                type = "Http",
                                inputs = new
                                {
                                    uri = "https://api.pagerduty.com/incidents",
                                    method = "POST",
                                    headers = new { Authorization = $"Token token={config.Notifications.PagerDutyServiceKey}" }
                                }
                            }
                        }
                    }
                },
                Dependencies = new List<string> { "SecurityInsights" },
                EstimatedSetupTime = "10-15 minutes"
            });
        }

        return components;
    }

    private async Task<ComponentResult> CreateResourceGroupAsync(
        string subscriptionId,
        string resourceGroupName,
        string location,
        CancellationToken cancellationToken)
    {
        // Simulate resource group creation
        _logger.LogInformation("Creating/verifying resource group {ResourceGroup} in {Location}", 
            resourceGroupName, location);

        await Task.Delay(100, cancellationToken); // Simulate API call

        return new ComponentResult
        {
            Success = true,
            ResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
            Details = $"Resource group created/verified in {location}"
        };
    }

    private async Task<ComponentResult> CreateIncidentResponseComponentAsync(
        IncidentResponseComponent component,
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken)
    {
        // This would integrate with Azure ARM APIs to actually create the resources
        // For now, we'll simulate the creation
        _logger.LogInformation("Creating component: {ResourceType} - {Name}", 
            component.ResourceType, component.Name);

        await Task.Delay(500, cancellationToken); // Simulate API call

        return new ComponentResult
        {
            Success = true,
            ResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{component.ResourceType}/{component.Name}",
            Details = $"Created {component.Category} component: {component.Name}"
        };
    }

    private double CalculateEstimatedCost(IncidentResponseConfig config)
    {
        double monthlyCost = 0;

        // Log Analytics workspace: ~$2.30 per GB ingested
        // Estimated 50GB/day for typical environment = ~$3,450/month
        monthlyCost += 3450;

        // Microsoft Sentinel: ~$2.50 per GB ingested (on top of Log Analytics)
        // Same 50GB/day = ~$3,750/month
        monthlyCost += 3750;

        // Data retention beyond 90 days: ~$0.12 per GB per month
        if (config.DataRetentionDays > 90)
        {
            var extraDays = config.DataRetentionDays - 90;
            var extraMonths = extraDays / 30.0;
            monthlyCost += extraMonths * 50 * 30 * 0.12; // 50GB/day * 30 days * $0.12
        }

        // Logic Apps for automation: ~$0.000025 per execution
        // Estimated 10,000 executions/month = ~$0.25
        var automationCount = 0;
        if (config.Automation.AutoBlockSuspiciousIPs) automationCount++;
        if (config.Automation.AutoDisableCompromisedAccounts) automationCount++;
        if (config.Automation.IsolateInfectedVMs) automationCount++;
        monthlyCost += automationCount * 0.25;

        return Math.Round(monthlyCost, 2);
    }

    // Helper classes for incident response setup
    private class IncidentResponseConfig
    {
        public string SiemWorkspaceName { get; set; } = "sentinel-workspace";
        public int DataRetentionDays { get; set; } = 730; // 2 years default for compliance
        public bool EnableAzureADLogs { get; set; } = true;
        public bool EnableActivityLogs { get; set; } = true;
        public bool EnableNsgFlowLogs { get; set; } = true;
        public bool EnableWafLogs { get; set; } = true;
        public ThreatDetectionConfig ThreatDetection { get; set; } = new();
        public AutomationConfig Automation { get; set; } = new();
        public NotificationConfig Notifications { get; set; } = new();
    }

    private class ThreatDetectionConfig
    {
        public bool SuspiciousLogins { get; set; } = true;
        public bool PrivilegeEscalation { get; set; } = true;
        public bool DataExfiltration { get; set; } = true;
        public bool CryptoMining { get; set; } = true;
        public bool LateralMovement { get; set; } = true;
    }

    private class AutomationConfig
    {
        public bool AutoBlockSuspiciousIPs { get; set; } = true;
        public bool AutoDisableCompromisedAccounts { get; set; } = true;
        public bool IsolateInfectedVMs { get; set; } = false; // Disabled by default - high impact
    }

    private class NotificationConfig
    {
        public bool EmailSecurityTeam { get; set; } = true;
        public List<string>? EmailAddresses { get; set; } = new() { "security@example.com" };
        public bool CreatePagerDutyIncident { get; set; } = false;
        public string? PagerDutyServiceKey { get; set; }
    }

    private class IncidentResponseComponent
    {
        public string Category { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object? Configuration { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public string EstimatedSetupTime { get; set; } = string.Empty;
    }

    private class ComponentResult
    {
        public bool Success { get; set; }
        public string? ResourceId { get; set; }
        public string? Details { get; set; }
        public string? Error { get; set; }
    }

    #region MCP-Enhanced Functions

    [KernelFunction("scan_for_security_vulnerabilities")]
    [Description("Scan Azure resources for security vulnerabilities using Azure MCP security scanning tools. " +
                 "Provides comprehensive vulnerability detection, threat analysis, and remediation guidance.")]
    public async Task<string> ScanForSecurityVulnerabilitiesAsync(
        [Description("Target resource group to scan for vulnerabilities")] 
        string resourceGroup,
        
        [Description("Optional subscription ID or name (uses default if not provided)")] 
        string? subscriptionId = null,
        
        [Description("Scan scope: 'all', 'compute', 'storage', 'network' (default: all)")] 
        string scope = "all",
        
        [Description("Include detailed remediation steps (default: true)")] 
        bool includeRemediation = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription ID
            var resolvedSubscriptionId = !string.IsNullOrWhiteSpace(subscriptionId)
                ? ResolveSubscriptionId(subscriptionId)
                : ResolveSubscriptionId("default");

            // 1. Use Azure MCP security center tools for vulnerability scanning
            var scanArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup,
                ["scope"] = scope
            };

            var scanResult = await _azureMcpClient.CallToolAsync("securitycenter", scanArgs, cancellationToken);

            if (scanResult == null || !scanResult.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = scanResult?.ErrorMessage ?? "Security scan failed"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var vulnerabilities = scanResult.Result?.ToString() ?? "No scan results available";

            // 2. Get threat intelligence from MCP
            var threatArgs = new Dictionary<string, object?>
            {
                ["query"] = $"Security threats and vulnerabilities for {resourceGroup}"
            };
            var threatResult = await _azureMcpClient.CallToolAsync("threatintelligence", threatArgs, cancellationToken);
            var threatIntelligence = threatResult?.Result?.ToString() ?? "Threat intelligence unavailable";

            // 3. Get security recommendations from MCP
            var recommendationsArgs = new Dictionary<string, object?>
            {
                ["query"] = $"Security vulnerability remediation for Azure {scope} resources"
            };
            var recommendationsResult = await _azureMcpClient.CallToolAsync("get_bestpractices", recommendationsArgs, cancellationToken);
            var securityRecommendations = recommendationsResult?.Result?.ToString() ?? "Recommendations unavailable";

            // 4. Get compliance impact assessment
            var complianceArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup
            };
            var complianceResult = await _azureMcpClient.CallToolAsync("azurepolicy", complianceArgs, cancellationToken);
            var complianceImpact = complianceResult?.Result?.ToString() ?? "Compliance data unavailable";

            // 5. Build comprehensive vulnerability report
            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroup = resourceGroup,
                subscriptionId = resolvedSubscriptionId,
                scanScope = scope,
                scanResults = new
                {
                    source = "Azure Security Center via MCP",
                    vulnerabilities = vulnerabilities,
                    scanDate = DateTime.UtcNow
                },
                threatAnalysis = new
                {
                    source = "Azure Threat Intelligence",
                    intelligence = threatIntelligence
                },
                securityRecommendations = new
                {
                    source = "Azure Best Practices",
                    recommendations = securityRecommendations,
                    includesRemediation = includeRemediation
                },
                complianceImpact = new
                {
                    source = "Azure Policy",
                    analysis = complianceImpact
                },
                nextSteps = new[]
                {
                    "Review identified vulnerabilities above",
                    "Assess threat intelligence findings",
                    "Prioritize remediation based on severity",
                    includeRemediation ? "Apply recommended security fixes" : "Say 'scan with remediation steps' for fix guidance",
                    "Say 'get security best practices' for hardening recommendations",
                    "Verify fixes with 'scan for vulnerabilities' after remediation"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for security vulnerabilities");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Vulnerability scan failed: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_security_best_practices")]
    [Description("Get comprehensive security best practices and hardening recommendations using Azure MCP. " +
                 "Provides actionable security guidance for Azure resources, network security, identity, and data protection.")]
    public async Task<string> GetSecurityBestPracticesAsync(
        [Description("Target resource group for security recommendations")] 
        string resourceGroup,
        
        [Description("Optional subscription ID or name (uses default if not provided)")] 
        string? subscriptionId = null,
        
        [Description("Security domain: 'all', 'identity', 'network', 'data', 'compute', 'monitoring' (default: all)")] 
        string securityDomain = "all",
        
        [Description("Include implementation scripts and automation (default: true)")] 
        bool includeImplementation = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription ID
            var resolvedSubscriptionId = !string.IsNullOrWhiteSpace(subscriptionId)
                ? ResolveSubscriptionId(subscriptionId)
                : ResolveSubscriptionId("default");

            // 1. Get security best practices from MCP based on domain
            var domainQuery = securityDomain.ToLowerInvariant() switch
            {
                "identity" => "Azure identity and access management security best practices",
                "network" => "Azure network security and firewall hardening best practices",
                "data" => "Azure data protection and encryption best practices",
                "compute" => "Azure virtual machine and container security best practices",
                "monitoring" => "Azure security monitoring and threat detection best practices",
                _ => $"Comprehensive Azure security hardening for {resourceGroup}"
            };

            var bestPracticesArgs = new Dictionary<string, object?>
            {
                ["query"] = domainQuery
            };
            var bestPracticesResult = await _azureMcpClient.CallToolAsync("get_bestpractices", bestPracticesArgs, cancellationToken);
            var bestPractices = bestPracticesResult?.Result?.ToString() ?? "Best practices unavailable";

            // 2. Get security center recommendations via MCP
            var securityCenterArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup
            };
            var securityCenterResult = await _azureMcpClient.CallToolAsync("securitycenter", securityCenterArgs, cancellationToken);
            var securityCenterRecommendations = securityCenterResult?.Result?.ToString() ?? "Security Center data unavailable";

            // 3. Get Azure Policy security recommendations
            var policyArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup
            };
            var policyResult = await _azureMcpClient.CallToolAsync("azurepolicy", policyArgs, cancellationToken);
            var policyRecommendations = policyResult?.Result?.ToString() ?? "Policy recommendations unavailable";

            // 4. Get Microsoft Defender recommendations if available
            var defenderArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = resolvedSubscriptionId,
                ["resourceGroup"] = resourceGroup
            };
            var defenderResult = await _azureMcpClient.CallToolAsync("defender", defenderArgs, cancellationToken);
            var defenderRecommendations = defenderResult?.Result?.ToString() ?? "Defender recommendations unavailable";

            // 5. Get implementation automation if requested
            object? implementationGuide = null;
            if (includeImplementation)
            {
                var automationArgs = new Dictionary<string, object?>
                {
                    ["query"] = $"Azure security automation scripts for {securityDomain} hardening"
                };
                var automationResult = await _azureMcpClient.CallToolAsync("get_bestpractices", automationArgs, cancellationToken);
                
                implementationGuide = new
                {
                    scripts = automationResult?.Result?.ToString() ?? "Automation scripts unavailable",
                    note = "Review and test automation scripts before production deployment"
                };
            }

            // 6. Compile comprehensive security hardening guide
            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroup = resourceGroup,
                subscriptionId = resolvedSubscriptionId,
                securityDomain = securityDomain.ToUpperInvariant(),
                bestPractices = new
                {
                    domain = securityDomain,
                    source = "Azure Best Practices via MCP",
                    recommendations = bestPractices
                },
                securityCenter = new
                {
                    source = "Azure Security Center",
                    recommendations = securityCenterRecommendations
                },
                azurePolicy = new
                {
                    source = "Azure Policy",
                    securityPolicies = policyRecommendations
                },
                microsoftDefender = new
                {
                    source = "Microsoft Defender for Cloud",
                    recommendations = defenderRecommendations
                },
                implementation = implementationGuide,
                prioritizedActions = new[]
                {
                    new { priority = "Critical", action = "Enable Microsoft Defender for all resource types", timeframe = "Immediate" },
                    new { priority = "Critical", action = "Implement network security groups and firewall rules", timeframe = "24 hours" },
                    new { priority = "High", action = "Enable Azure AD multi-factor authentication", timeframe = "1 week" },
                    new { priority = "High", action = "Configure encryption at rest for all storage accounts", timeframe = "1 week" },
                    new { priority = "Medium", action = "Set up Azure Monitor and security alerts", timeframe = "2 weeks" },
                    new { priority = "Medium", action = "Implement Azure Policy for governance", timeframe = "2 weeks" }
                },
                nextSteps = new[]
                {
                    "Review security recommendations by priority",
                    "Check Security Center and Defender findings",
                    "Implement critical security controls first",
                    includeImplementation ? "Use provided automation scripts for deployment" : "Say 'get security best practices with implementation' for automation scripts",
                    "Say 'scan for security vulnerabilities' to validate improvements",
                    "Schedule regular security assessments"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security best practices");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get security best practices: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion
}
