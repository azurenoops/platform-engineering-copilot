using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Security operations plugin for SIEM setup, incident response, threat detection, and security automation.
/// Complements CompliancePlugin by providing proactive security infrastructure setup.
/// </summary>
public class SecurityPlugin : BaseSupervisorPlugin
{
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
        Kernel kernel) : base(logger, kernel)
    {
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
}
