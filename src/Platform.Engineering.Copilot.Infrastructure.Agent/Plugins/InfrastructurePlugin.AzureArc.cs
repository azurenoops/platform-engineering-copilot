using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Partial class containing Azure Arc infrastructure functions:
/// - generate_arc_onboarding_script: Generate PowerShell/Bash scripts to onboard servers to Azure Arc
/// - deploy_arc_extensions: Deploy extensions (monitoring, security agents) to Arc machines at scale
/// - get_arc_onboarding_status: Check onboarding status of servers
/// </summary>
public partial class InfrastructurePlugin
{
    // ========== AZURE ARC INFRASTRUCTURE FUNCTIONS ==========

    [KernelFunction("generate_arc_onboarding_script")]
    [Description("Generate a PowerShell or Bash script to onboard on-premises or multi-cloud servers to Azure Arc. " +
                 "Creates ready-to-run scripts with your Azure tenant and subscription details. " +
                 "Supports Windows (PowerShell) and Linux (Bash) servers with optional proxy and tag configuration. " +
                 "Example: 'Generate Arc onboarding script for Windows servers', 'Create Linux Arc enrollment script with proxy settings'")]
    public async Task<string> GenerateArcOnboardingScriptAsync(
        [Description("Operating system: 'Windows' for PowerShell script, 'Linux' for Bash script")] string operatingSystem,
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group where Arc machines will be registered")] string? resourceGroup = null,
        [Description("Azure region for the Arc machine resource (default: eastus)")] string location = "eastus",
        [Description("Optional proxy URL for servers behind a proxy (format: http://proxy:port)")] string? proxyUrl = null,
        [Description("Optional tags as key=value pairs separated by commas (e.g., 'Environment=Production,Team=Platform')")] string? tags = null,
        [Description("Service principal client ID for automated onboarding (optional - uses interactive auth if not provided)")] string? servicePrincipalId = null,
        [Description("Service principal secret for automated onboarding (optional)")] string? servicePrincipalSecret = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            string subscriptionId;
            try
            {
                subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            }
            catch (ArgumentException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message,
                    hint = "Use 'set subscription to <name>' first or provide a valid subscription ID"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            _logger.LogInformation("Generating Azure Arc onboarding script for {OS} in subscription {SubscriptionId}",
                operatingSystem, subscriptionId);

            // Get tenant ID from Azure context
            var tenantId = await GetTenantIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(tenantId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Could not determine Azure tenant ID. Please ensure you are logged in to Azure.",
                    suggestion = "Run 'az login' or verify Azure credentials"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Default resource group if not specified
            var targetResourceGroup = resourceGroup ?? "rg-arc-machines";

            // Parse tags
            var tagDictionary = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(tags))
            {
                foreach (var tag in tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = tag.Trim().Split('=', 2);
                    if (parts.Length == 2)
                    {
                        tagDictionary[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            // Add default tags
            tagDictionary.TryAdd("OnboardedBy", "PlatformEngineeringCopilot");
            tagDictionary.TryAdd("OnboardedDate", DateTime.UtcNow.ToString("yyyy-MM-dd"));

            string script;
            string scriptFileName;
            string instructions;

            if (operatingSystem.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                script = GenerateWindowsOnboardingScript(
                    subscriptionId, tenantId, targetResourceGroup, location,
                    proxyUrl, tagDictionary, servicePrincipalId, servicePrincipalSecret);
                scriptFileName = "Install-ArcAgent.ps1";
                instructions = GetWindowsOnboardingInstructions(servicePrincipalId != null);
            }
            else if (operatingSystem.Equals("Linux", StringComparison.OrdinalIgnoreCase))
            {
                script = GenerateLinuxOnboardingScript(
                    subscriptionId, tenantId, targetResourceGroup, location,
                    proxyUrl, tagDictionary, servicePrincipalId, servicePrincipalSecret);
                scriptFileName = "install_arc_agent.sh";
                instructions = GetLinuxOnboardingInstructions(servicePrincipalId != null);
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Unsupported operating system: {operatingSystem}",
                    supportedValues = new[] { "Windows", "Linux" }
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Store the script for retrieval
            var scriptId = $"arc-onboarding-{operatingSystem.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await StoreGeneratedScriptAsync(scriptId, scriptFileName, script);

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = $"🖥️ AZURE ARC ONBOARDING SCRIPT ({operatingSystem.ToUpper()})",
                    icon = "🔗",
                    subscriptionId = subscriptionId,
                    tenantId = tenantId,
                    resourceGroup = targetResourceGroup,
                    location = location,
                    generatedAt = DateTimeOffset.UtcNow.ToString("u")
                },
                script = new
                {
                    id = scriptId,
                    fileName = scriptFileName,
                    content = script,
                    lineCount = script.Split('\n').Length
                },
                configuration = new
                {
                    operatingSystem = operatingSystem,
                    proxyConfigured = !string.IsNullOrEmpty(proxyUrl),
                    proxyUrl = proxyUrl,
                    authMethod = servicePrincipalId != null ? "Service Principal" : "Interactive (Device Code)",
                    tags = tagDictionary
                },
                instructions = instructions,
                prerequisites = operatingSystem.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                    ? new[]
                    {
                        "Windows Server 2012 R2 or later",
                        "PowerShell 5.1 or later",
                        "Outbound connectivity to Azure (port 443)",
                        "Administrator privileges"
                    }
                    : new[]
                    {
                        "Linux with systemd (RHEL 7+, Ubuntu 16.04+, SLES 15+, etc.)",
                        "Bash shell",
                        "Outbound connectivity to Azure (port 443)",
                        "Root or sudo privileges"
                    },
                networkRequirements = new[]
                {
                    "*.guestconfiguration.azure.com",
                    "*.his.arc.azure.com",
                    "*.servicebus.windows.net",
                    "management.azure.com",
                    "login.microsoftonline.com",
                    "*.blob.core.windows.net"
                },
                nextSteps = new[]
                {
                    $"Say 'get generated file {scriptId}' to retrieve the full script",
                    "Copy the script to your target server",
                    operatingSystem.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                        ? "Run in an elevated PowerShell session"
                        : "Run with sudo/root privileges",
                    "Say 'check Arc onboarding status' to verify registration",
                    "Say 'deploy Arc extensions' to install monitoring and security agents"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Arc onboarding script");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to generate Arc onboarding script: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("deploy_arc_extensions")]
    [Description("Deploy extensions to Azure Arc-connected machines at scale. " +
                 "Supports monitoring (Azure Monitor Agent, Log Analytics), security (Microsoft Defender), and custom extensions. " +
                 "Can target individual machines, resource groups, or all Arc machines in a subscription. " +
                 "Example: 'Deploy Azure Monitor extension to all Arc machines', 'Install Defender on Arc servers in production resource group'")]
    public async Task<string> DeployArcExtensionsAsync(
        [Description("Extension type: 'AzureMonitorAgent', 'LogAnalytics', 'Defender', 'DependencyAgent', 'GuestConfiguration', 'CustomScript', or 'All' for core extensions")] string extensionType,
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group containing Arc machines (optional - deploys to all if not specified)")] string? resourceGroup = null,
        [Description("Specific Arc machine name to deploy to (optional - deploys to all machines in scope if not specified)")] string? machineName = null,
        [Description("Log Analytics Workspace ID (required for LogAnalytics and AzureMonitorAgent extensions)")] string? workspaceId = null,
        [Description("Log Analytics Workspace Key (required for LogAnalytics extension)")] string? workspaceKey = null,
        [Description("Custom script URL for CustomScript extension")] string? scriptUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            string subscriptionId;
            try
            {
                subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            }
            catch (ArgumentException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            _logger.LogInformation("Deploying Arc extension {ExtensionType} in subscription {SubscriptionId}",
                extensionType, subscriptionId);

            // Get Arc machines in scope
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var arcMachines = allResources.Where(r =>
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true).ToList();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                arcMachines = arcMachines.Where(r =>
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (!string.IsNullOrWhiteSpace(machineName))
            {
                arcMachines = arcMachines.Where(r =>
                    r.Name?.Equals(machineName, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (arcMachines.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No Azure Arc machines found in the specified scope",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all",
                    machineName = machineName ?? "all",
                    suggestion = "Use 'list Arc machines' to see available machines or 'generate Arc onboarding script' to onboard servers"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Determine which extensions to deploy
            var extensionsToDeploy = GetExtensionsToDeploy(extensionType, workspaceId, workspaceKey, scriptUrl);

            if (extensionsToDeploy.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Unknown extension type: {extensionType}",
                    supportedExtensions = new[]
                    {
                        "AzureMonitorAgent - Azure Monitor Agent for metrics and logs",
                        "LogAnalytics - Legacy Log Analytics Agent (MMA)",
                        "Defender - Microsoft Defender for Endpoint",
                        "DependencyAgent - Dependency Agent for Service Map",
                        "GuestConfiguration - Azure Policy Guest Configuration",
                        "CustomScript - Custom Script Extension",
                        "All - Deploy core monitoring extensions (AzureMonitorAgent, DependencyAgent, GuestConfiguration)"
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Validate prerequisites
            if ((extensionType.Equals("LogAnalytics", StringComparison.OrdinalIgnoreCase) ||
                 extensionType.Equals("AzureMonitorAgent", StringComparison.OrdinalIgnoreCase)) &&
                string.IsNullOrWhiteSpace(workspaceId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Log Analytics Workspace ID is required for monitoring extensions",
                    hint = "Provide workspaceId parameter or create a workspace first"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Generate deployment templates/commands
            var deploymentPlans = new List<object>();
            var windowsMachines = new List<string>();
            var linuxMachines = new List<string>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    var osType = GetPropertyValue<string>(details?.Properties, "osType", "Unknown");

                    if (osType.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                        windowsMachines.Add(machine.Name!);
                    else if (osType.Equals("Linux", StringComparison.OrdinalIgnoreCase))
                        linuxMachines.Add(machine.Name!);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get OS type for machine {MachineName}", machine.Name);
                }
            }

            foreach (var ext in extensionsToDeploy)
            {
                if (windowsMachines.Count > 0 && ext.SupportsWindows)
                {
                    deploymentPlans.Add(new
                    {
                        extensionName = ext.Name,
                        publisher = ext.Publisher,
                        type = ext.WindowsType,
                        targetOS = "Windows",
                        targetMachines = windowsMachines,
                        machineCount = windowsMachines.Count,
                        settings = ext.Settings,
                        status = "Ready to deploy"
                    });
                }

                if (linuxMachines.Count > 0 && ext.SupportsLinux)
                {
                    deploymentPlans.Add(new
                    {
                        extensionName = ext.Name,
                        publisher = ext.Publisher,
                        type = ext.LinuxType,
                        targetOS = "Linux",
                        targetMachines = linuxMachines,
                        machineCount = linuxMachines.Count,
                        settings = ext.Settings,
                        status = "Ready to deploy"
                    });
                }
            }

            // Generate ARM template for deployment
            var armTemplate = GenerateArcExtensionArmTemplate(extensionsToDeploy, workspaceId, workspaceKey);
            var templateId = $"arc-extensions-{extensionType.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await StoreGeneratedScriptAsync(templateId, "deploy-arc-extensions.json", armTemplate);

            // Generate Azure CLI commands as alternative
            var cliCommands = GenerateArcExtensionCliCommands(
                extensionsToDeploy, subscriptionId, resourceGroup, machineName, workspaceId, workspaceKey);

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🔌 AZURE ARC EXTENSION DEPLOYMENT PLAN",
                    icon = "📦",
                    subscriptionId = subscriptionId,
                    extensionType = extensionType,
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                summary = new
                {
                    totalMachines = arcMachines.Count,
                    windowsMachines = windowsMachines.Count,
                    linuxMachines = linuxMachines.Count,
                    extensionsCount = extensionsToDeploy.Count,
                    deploymentsPlanned = deploymentPlans.Count
                },
                deploymentPlans = deploymentPlans,
                armTemplate = new
                {
                    id = templateId,
                    fileName = "deploy-arc-extensions.json",
                    description = "ARM template for deploying extensions via Azure portal or CLI"
                },
                cliCommands = new
                {
                    description = "Azure CLI commands to deploy extensions",
                    commands = cliCommands
                },
                estimatedDuration = $"{arcMachines.Count * 2}-{arcMachines.Count * 5} minutes",
                recommendations = new[]
                {
                    windowsMachines.Count > 0 && linuxMachines.Count > 0
                        ? "Mixed OS environment detected - extensions will be deployed with OS-appropriate versions"
                        : null,
                    extensionsToDeploy.Any(e => e.Name == "AzureMonitorAgent")
                        ? "Azure Monitor Agent will collect metrics and logs to your workspace"
                        : null,
                    extensionsToDeploy.Any(e => e.Name == "GuestConfiguration")
                        ? "Guest Configuration enables Azure Policy compliance for Arc machines"
                        : null,
                    extensionsToDeploy.Any(e => e.Name == "Defender")
                        ? "Microsoft Defender will provide advanced threat protection"
                        : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    $"Say 'get generated file {templateId}' to retrieve the ARM template",
                    "Deploy using Azure CLI: az deployment group create --template-file deploy-arc-extensions.json",
                    "Or deploy via Azure Portal > Deploy a custom template",
                    "Say 'get Arc extensions' to verify deployment status",
                    "Say 'scan Arc machine compliance' to check configuration compliance"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying Arc extensions");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to deploy Arc extensions: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_arc_onboarding_status")]
    [Description("Check the onboarding and connection status of Azure Arc machines. " +
                 "Shows which servers are connected, disconnected, or have issues. " +
                 "Useful for verifying onboarding success and troubleshooting connectivity problems. " +
                 "Example: 'Check Arc onboarding status', 'Show me which servers failed to connect to Arc'")]
    public async Task<string> GetArcOnboardingStatusAsync(
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group to filter by (optional)")] string? resourceGroup = null,
        [Description("Filter by status: 'Connected', 'Disconnected', 'Error', or 'All' (default)")] string statusFilter = "All",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            string subscriptionId;
            try
            {
                subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            }
            catch (ArgumentException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            _logger.LogInformation("Getting Arc onboarding status in subscription {SubscriptionId}", subscriptionId);

            // Get all Arc machines
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var arcMachines = allResources.Where(r =>
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true).ToList();

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                arcMachines = arcMachines.Where(r =>
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (arcMachines.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "No Azure Arc machines found",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all",
                    suggestion = "Use 'generate Arc onboarding script' to create scripts for onboarding servers"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Collect detailed status
            var machineStatuses = new List<object>();
            var statusSummary = new Dictionary<string, int>
            {
                ["Connected"] = 0,
                ["Disconnected"] = 0,
                ["Error"] = 0,
                ["Unknown"] = 0
            };
            var osSummary = new Dictionary<string, int>();
            var recentlyOnboarded = new List<object>();
            var problemMachines = new List<object>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    if (details?.Properties != null)
                    {
                        var status = GetPropertyValue<string>(details.Properties, "status", "Unknown");
                        var osType = GetPropertyValue<string>(details.Properties, "osType", "Unknown");
                        var osName = GetPropertyValue<string>(details.Properties, "osName", "");
                        var lastStatusChange = GetPropertyValue<string>(details.Properties, "lastStatusChange", "");
                        var agentVersion = GetPropertyValue<string>(details.Properties, "agentVersion", "Unknown");
                        var machineFqdn = GetPropertyValue<string>(details.Properties, "machineFqdn", "");
                        var provisioningState = GetPropertyValue<string>(details.Properties, "provisioningState", "Unknown");

                        // Update summaries
                        statusSummary[status] = statusSummary.GetValueOrDefault(status) + 1;
                        osSummary[osType] = osSummary.GetValueOrDefault(osType) + 1;

                        // Apply status filter
                        if (!statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                            !status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var machineInfo = new
                        {
                            name = machine.Name,
                            resourceGroup = machine.ResourceGroup,
                            location = machine.Location,
                            status = status,
                            statusIcon = status switch
                            {
                                "Connected" => "🟢",
                                "Disconnected" => "🔴",
                                "Error" => "❌",
                                _ => "❓"
                            },
                            osType = osType,
                            osName = osName,
                            machineFqdn = machineFqdn,
                            agentVersion = agentVersion,
                            provisioningState = provisioningState,
                            lastStatusChange = lastStatusChange
                        };

                        machineStatuses.Add(machineInfo);

                        // Track recently onboarded (within last 24 hours)
                        if (!string.IsNullOrEmpty(lastStatusChange) &&
                            DateTime.TryParse(lastStatusChange, out var statusChangeTime) &&
                            statusChangeTime > DateTime.UtcNow.AddHours(-24) &&
                            status == "Connected")
                        {
                            recentlyOnboarded.Add(new
                            {
                                name = machine.Name,
                                onboardedAt = statusChangeTime.ToString("u"),
                                hoursAgo = (int)(DateTime.UtcNow - statusChangeTime).TotalHours
                            });
                        }

                        // Track problem machines
                        if (status != "Connected")
                        {
                            problemMachines.Add(new
                            {
                                name = machine.Name,
                                status = status,
                                lastSeen = lastStatusChange,
                                troubleshootingSteps = GetTroubleshootingSteps(status, osType)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get details for Arc machine {MachineName}", machine.Name);
                    machineStatuses.Add(new
                    {
                        name = machine.Name,
                        status = "Error",
                        statusIcon = "❌",
                        error = $"Could not retrieve details: {ex.Message}"
                    });
                }
            }

            // Calculate health score
            var totalMachines = arcMachines.Count;
            var connectedMachines = statusSummary.GetValueOrDefault("Connected");
            var healthScore = totalMachines > 0 ? (connectedMachines * 100.0 / totalMachines) : 0;

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "📊 AZURE ARC ONBOARDING STATUS",
                    icon = healthScore >= 90 ? "✅" : healthScore >= 70 ? "⚠️" : "🔴",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    statusFilter = statusFilter,
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                summary = new
                {
                    totalMachines = totalMachines,
                    healthScore = $"{healthScore:F1}%",
                    healthStatus = healthScore >= 90 ? "Healthy" : healthScore >= 70 ? "Needs Attention" : "Critical",
                    connected = connectedMachines,
                    disconnected = statusSummary.GetValueOrDefault("Disconnected"),
                    error = statusSummary.GetValueOrDefault("Error"),
                    unknown = statusSummary.GetValueOrDefault("Unknown")
                },
                operatingSystems = osSummary,
                recentlyOnboarded = recentlyOnboarded.Count > 0
                    ? new { count = recentlyOnboarded.Count, machines = recentlyOnboarded }
                    : null,
                problemMachines = problemMachines.Count > 0
                    ? new { count = problemMachines.Count, machines = problemMachines }
                    : null,
                machines = machineStatuses,
                recommendations = new[]
                {
                    healthScore >= 90 ? "✅ Excellent connectivity - all machines are healthy" : null,
                    healthScore < 90 && healthScore >= 70 ? "⚠️ Some machines need attention - review disconnected servers" : null,
                    healthScore < 70 ? "🔴 Critical connectivity issues - investigate disconnected machines immediately" : null,
                    statusSummary.GetValueOrDefault("Disconnected") > 0
                        ? $"🔌 {statusSummary["Disconnected"]} machine(s) disconnected - check network connectivity and agent status"
                        : null,
                    recentlyOnboarded.Count > 0
                        ? $"🆕 {recentlyOnboarded.Count} machine(s) onboarded in the last 24 hours"
                        : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    problemMachines.Count > 0 ? "Review troubleshooting steps for disconnected machines" : null,
                    "Say 'generate Arc onboarding script' to onboard additional servers",
                    "Say 'deploy Arc extensions' to install monitoring and security agents",
                    "Say 'scan Arc machine compliance' to check security compliance",
                    "Say 'list Arc machines' for detailed inventory"
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Arc onboarding status");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get Arc onboarding status: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    // ========== AZURE ARC HELPER METHODS ==========

    /// <summary>
    /// Get Azure tenant ID from environment
    /// </summary>
    private Task<string?> GetTenantIdAsync(CancellationToken cancellationToken)
    {
        // Try to get from environment
        var envTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        if (!string.IsNullOrEmpty(envTenantId))
        {
            return Task.FromResult<string?>(envTenantId);
        }

        // Return placeholder - user needs to set AZURE_TENANT_ID environment variable
        // or the script will prompt them to fill in the tenant ID
        _logger.LogWarning("AZURE_TENANT_ID environment variable not set. Please set it or update the generated script.");
        return Task.FromResult<string?>("<YOUR-TENANT-ID>");
    }

    /// <summary>
    /// Generate Windows PowerShell onboarding script
    /// </summary>
    private string GenerateWindowsOnboardingScript(
        string subscriptionId, string tenantId, string resourceGroup, string location,
        string? proxyUrl, Dictionary<string, string> tags,
        string? servicePrincipalId, string? servicePrincipalSecret)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Azure Arc Onboarding Script for Windows");
        sb.AppendLine("# Generated by Platform Engineering Copilot");
        sb.AppendLine($"# Generated at: {DateTime.UtcNow:u}");
        sb.AppendLine();
        sb.AppendLine("# Prerequisites:");
        sb.AppendLine("#   - Windows Server 2012 R2 or later");
        sb.AppendLine("#   - PowerShell 5.1 or later");
        sb.AppendLine("#   - Run as Administrator");
        sb.AppendLine("#   - Outbound connectivity to Azure (port 443)");
        sb.AppendLine();
        sb.AppendLine("param(");
        sb.AppendLine("    [switch]$Force,");
        sb.AppendLine("    [switch]$Verbose");
        sb.AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine();
        sb.AppendLine("# Configuration");
        sb.AppendLine($"$subscriptionId = '{subscriptionId}'");
        sb.AppendLine($"$tenantId = '{tenantId}'");
        sb.AppendLine($"$resourceGroup = '{resourceGroup}'");
        sb.AppendLine($"$location = '{location}'");
        sb.AppendLine();

        // Tags
        sb.AppendLine("$tags = @{");
        foreach (var tag in tags)
        {
            sb.AppendLine($"    '{tag.Key}' = '{tag.Value}'");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Proxy configuration
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            sb.AppendLine("# Proxy Configuration");
            sb.AppendLine($"$proxyUrl = '{proxyUrl}'");
            sb.AppendLine("[System.Net.WebRequest]::DefaultWebProxy = New-Object System.Net.WebProxy($proxyUrl)");
            sb.AppendLine("$env:HTTPS_PROXY = $proxyUrl");
            sb.AppendLine("$env:HTTP_PROXY = $proxyUrl");
            sb.AppendLine();
        }

        sb.AppendLine("Write-Host '========================================' -ForegroundColor Cyan");
        sb.AppendLine("Write-Host '  Azure Arc Onboarding for Windows' -ForegroundColor Cyan");
        sb.AppendLine("Write-Host '========================================' -ForegroundColor Cyan");
        sb.AppendLine();
        sb.AppendLine("# Check if running as Administrator");
        sb.AppendLine("$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())");
        sb.AppendLine("if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {");
        sb.AppendLine("    Write-Error 'This script must be run as Administrator'");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Check if already connected");
        sb.AppendLine("$existingAgent = Get-Service -Name 'himds' -ErrorAction SilentlyContinue");
        sb.AppendLine("if ($existingAgent -and -not $Force) {");
        sb.AppendLine("    Write-Host 'Azure Arc agent is already installed. Use -Force to reinstall.' -ForegroundColor Yellow");
        sb.AppendLine("    azcmagent show");
        sb.AppendLine("    exit 0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Download the installation package");
        sb.AppendLine("Write-Host 'Downloading Azure Connected Machine Agent...' -ForegroundColor Green");
        sb.AppendLine("$downloadUrl = 'https://aka.ms/AzureConnectedMachineAgent'");
        sb.AppendLine("$installerPath = \"$env:TEMP\\AzureConnectedMachineAgent.msi\"");
        sb.AppendLine("Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing");
        sb.AppendLine();
        sb.AppendLine("# Install the agent");
        sb.AppendLine("Write-Host 'Installing Azure Connected Machine Agent...' -ForegroundColor Green");
        sb.AppendLine("$installArgs = @('/i', $installerPath, '/qn', '/norestart')");
        sb.AppendLine("$process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $installArgs -Wait -PassThru");
        sb.AppendLine("if ($process.ExitCode -ne 0) {");
        sb.AppendLine("    Write-Error \"Installation failed with exit code: $($process.ExitCode)\"");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Connect to Azure Arc");
        sb.AppendLine("Write-Host 'Connecting to Azure Arc...' -ForegroundColor Green");
        sb.AppendLine();

        // Build tags string for azcmagent
        var tagStrings = tags.Select(t => $"'{t.Key}={t.Value}'");
        var tagsArg = string.Join(",", tagStrings);

        if (!string.IsNullOrEmpty(servicePrincipalId) && !string.IsNullOrEmpty(servicePrincipalSecret))
        {
            sb.AppendLine("# Service Principal Authentication (automated)");
            sb.AppendLine($"$servicePrincipalId = '{servicePrincipalId}'");
            sb.AppendLine("$servicePrincipalSecret = Read-Host -Prompt 'Enter Service Principal Secret' -AsSecureString");
            sb.AppendLine("$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($servicePrincipalSecret)");
            sb.AppendLine("$plainSecret = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)");
            sb.AppendLine();
            sb.AppendLine($"& \"$env:ProgramW6432\\AzureConnectedMachineAgent\\azcmagent.exe\" connect `");
            sb.AppendLine($"    --subscription-id $subscriptionId `");
            sb.AppendLine($"    --tenant-id $tenantId `");
            sb.AppendLine($"    --resource-group $resourceGroup `");
            sb.AppendLine($"    --location $location `");
            sb.AppendLine($"    --service-principal-id $servicePrincipalId `");
            sb.AppendLine($"    --service-principal-secret $plainSecret `");
            sb.AppendLine($"    --tags {tagsArg}");
        }
        else
        {
            sb.AppendLine("# Interactive Authentication (device code flow)");
            sb.AppendLine($"& \"$env:ProgramW6432\\AzureConnectedMachineAgent\\azcmagent.exe\" connect `");
            sb.AppendLine($"    --subscription-id $subscriptionId `");
            sb.AppendLine($"    --tenant-id $tenantId `");
            sb.AppendLine($"    --resource-group $resourceGroup `");
            sb.AppendLine($"    --location $location `");
            sb.AppendLine($"    --tags {tagsArg}");
        }
        sb.AppendLine();
        sb.AppendLine("if ($LASTEXITCODE -ne 0) {");
        sb.AppendLine("    Write-Error 'Failed to connect to Azure Arc'");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Verify connection");
        sb.AppendLine("Write-Host 'Verifying connection...' -ForegroundColor Green");
        sb.AppendLine("& \"$env:ProgramW6432\\AzureConnectedMachineAgent\\azcmagent.exe\" show");
        sb.AppendLine();
        sb.AppendLine("Write-Host '========================================' -ForegroundColor Green");
        sb.AppendLine("Write-Host '  Successfully connected to Azure Arc!' -ForegroundColor Green");
        sb.AppendLine("Write-Host '========================================' -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# Cleanup");
        sb.AppendLine("Remove-Item -Path $installerPath -Force -ErrorAction SilentlyContinue");

        return sb.ToString();
    }

    /// <summary>
    /// Generate Linux Bash onboarding script
    /// </summary>
    private string GenerateLinuxOnboardingScript(
        string subscriptionId, string tenantId, string resourceGroup, string location,
        string? proxyUrl, Dictionary<string, string> tags,
        string? servicePrincipalId, string? servicePrincipalSecret)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("# Azure Arc Onboarding Script for Linux");
        sb.AppendLine("# Generated by Platform Engineering Copilot");
        sb.AppendLine($"# Generated at: {DateTime.UtcNow:u}");
        sb.AppendLine();
        sb.AppendLine("# Prerequisites:");
        sb.AppendLine("#   - Linux with systemd (RHEL 7+, Ubuntu 16.04+, SLES 15+, etc.)");
        sb.AppendLine("#   - Run with sudo or as root");
        sb.AppendLine("#   - Outbound connectivity to Azure (port 443)");
        sb.AppendLine();
        sb.AppendLine("set -e");
        sb.AppendLine();
        sb.AppendLine("# Configuration");
        sb.AppendLine($"SUBSCRIPTION_ID=\"{subscriptionId}\"");
        sb.AppendLine($"TENANT_ID=\"{tenantId}\"");
        sb.AppendLine($"RESOURCE_GROUP=\"{resourceGroup}\"");
        sb.AppendLine($"LOCATION=\"{location}\"");
        sb.AppendLine();

        // Proxy configuration
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            sb.AppendLine("# Proxy Configuration");
            sb.AppendLine($"export HTTPS_PROXY=\"{proxyUrl}\"");
            sb.AppendLine($"export HTTP_PROXY=\"{proxyUrl}\"");
            sb.AppendLine();
        }

        sb.AppendLine("echo '========================================'");
        sb.AppendLine("echo '  Azure Arc Onboarding for Linux'");
        sb.AppendLine("echo '========================================'");
        sb.AppendLine();
        sb.AppendLine("# Check if running as root");
        sb.AppendLine("if [ \"$EUID\" -ne 0 ]; then");
        sb.AppendLine("    echo 'Please run as root or with sudo'");
        sb.AppendLine("    exit 1");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("# Check if already connected");
        sb.AppendLine("if command -v azcmagent &> /dev/null && azcmagent show &> /dev/null; then");
        sb.AppendLine("    echo 'Azure Arc agent is already installed and connected.'");
        sb.AppendLine("    azcmagent show");
        sb.AppendLine("    echo 'Use --force to reinstall'");
        sb.AppendLine("    if [ \"$1\" != \"--force\" ]; then");
        sb.AppendLine("        exit 0");
        sb.AppendLine("    fi");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("# Download and install the agent");
        sb.AppendLine("echo 'Downloading and installing Azure Connected Machine Agent...'");
        sb.AppendLine("wget https://aka.ms/azcmagent -O ~/install_linux_azcmagent.sh 2>/dev/null || \\");
        sb.AppendLine("    curl -L https://aka.ms/azcmagent -o ~/install_linux_azcmagent.sh");
        sb.AppendLine("bash ~/install_linux_azcmagent.sh");
        sb.AppendLine();

        // Build tags string
        var tagStrings = tags.Select(t => $"'{t.Key}={t.Value}'");
        var tagsArg = string.Join(",", tagStrings);

        sb.AppendLine("# Connect to Azure Arc");
        sb.AppendLine("echo 'Connecting to Azure Arc...'");

        if (!string.IsNullOrEmpty(servicePrincipalId) && !string.IsNullOrEmpty(servicePrincipalSecret))
        {
            sb.AppendLine();
            sb.AppendLine("# Service Principal Authentication (automated)");
            sb.AppendLine($"SERVICE_PRINCIPAL_ID=\"{servicePrincipalId}\"");
            sb.AppendLine("echo -n 'Enter Service Principal Secret: '");
            sb.AppendLine("read -s SERVICE_PRINCIPAL_SECRET");
            sb.AppendLine("echo");
            sb.AppendLine();
            sb.AppendLine("azcmagent connect \\");
            sb.AppendLine("    --subscription-id \"$SUBSCRIPTION_ID\" \\");
            sb.AppendLine("    --tenant-id \"$TENANT_ID\" \\");
            sb.AppendLine("    --resource-group \"$RESOURCE_GROUP\" \\");
            sb.AppendLine("    --location \"$LOCATION\" \\");
            sb.AppendLine("    --service-principal-id \"$SERVICE_PRINCIPAL_ID\" \\");
            sb.AppendLine("    --service-principal-secret \"$SERVICE_PRINCIPAL_SECRET\" \\");
            sb.AppendLine($"    --tags {tagsArg}");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("# Interactive Authentication (device code flow)");
            sb.AppendLine("azcmagent connect \\");
            sb.AppendLine("    --subscription-id \"$SUBSCRIPTION_ID\" \\");
            sb.AppendLine("    --tenant-id \"$TENANT_ID\" \\");
            sb.AppendLine("    --resource-group \"$RESOURCE_GROUP\" \\");
            sb.AppendLine("    --location \"$LOCATION\" \\");
            sb.AppendLine($"    --tags {tagsArg}");
        }
        sb.AppendLine();
        sb.AppendLine("# Verify connection");
        sb.AppendLine("echo 'Verifying connection...'");
        sb.AppendLine("azcmagent show");
        sb.AppendLine();
        sb.AppendLine("echo '========================================'");
        sb.AppendLine("echo '  Successfully connected to Azure Arc!'");
        sb.AppendLine("echo '========================================'");
        sb.AppendLine();
        sb.AppendLine("# Cleanup");
        sb.AppendLine("rm -f ~/install_linux_azcmagent.sh");

        return sb.ToString();
    }

    /// <summary>
    /// Get Windows onboarding instructions
    /// </summary>
    private string GetWindowsOnboardingInstructions(bool useServicePrincipal)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Windows Server Onboarding Instructions:**");
        sb.AppendLine();
        sb.AppendLine("1. Copy the generated script to the target Windows server");
        sb.AppendLine("2. Open PowerShell as Administrator");
        sb.AppendLine("3. If needed, allow script execution: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`");
        sb.AppendLine("4. Run the script: `.\\Install-ArcAgent.ps1`");
        if (!useServicePrincipal)
        {
            sb.AppendLine("5. When prompted, authenticate using the device code flow:");
            sb.AppendLine("   - Open a browser and go to https://microsoft.com/devicelogin");
            sb.AppendLine("   - Enter the code displayed in the console");
            sb.AppendLine("   - Sign in with an account that has Contributor access");
        }
        sb.AppendLine("6. Verify successful connection with: `azcmagent show`");
        return sb.ToString();
    }

    /// <summary>
    /// Get Linux onboarding instructions
    /// </summary>
    private string GetLinuxOnboardingInstructions(bool useServicePrincipal)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Linux Server Onboarding Instructions:**");
        sb.AppendLine();
        sb.AppendLine("1. Copy the generated script to the target Linux server");
        sb.AppendLine("2. Make the script executable: `chmod +x install_arc_agent.sh`");
        sb.AppendLine("3. Run with sudo: `sudo ./install_arc_agent.sh`");
        if (!useServicePrincipal)
        {
            sb.AppendLine("4. When prompted, authenticate using the device code flow:");
            sb.AppendLine("   - Open a browser and go to https://microsoft.com/devicelogin");
            sb.AppendLine("   - Enter the code displayed in the console");
            sb.AppendLine("   - Sign in with an account that has Contributor access");
        }
        sb.AppendLine("5. Verify successful connection with: `azcmagent show`");
        return sb.ToString();
    }

    /// <summary>
    /// Store generated script for later retrieval
    /// </summary>
    private async Task StoreGeneratedScriptAsync(string scriptId, string fileName, string content)
    {
        try
        {
            // Store in template storage service with a wrapper object
            var scriptTemplate = new
            {
                id = scriptId,
                fileName = fileName,
                content = content,
                type = "arc-script",
                generatedAt = DateTime.UtcNow
            };
            await _templateStorageService.StoreTemplateAsync(scriptId, scriptTemplate);
            _lastGeneratedTemplateName = scriptId;
            _logger.LogInformation("Stored generated script: {ScriptId}", scriptId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not store script {ScriptId}, content will be returned inline", scriptId);
        }
    }

    /// <summary>
    /// Get extensions to deploy based on extension type
    /// </summary>
    private List<ArcExtensionInfo> GetExtensionsToDeploy(string extensionType, string? workspaceId, string? workspaceKey, string? scriptUrl)
    {
        var extensions = new List<ArcExtensionInfo>();

        switch (extensionType.ToLowerInvariant())
        {
            case "azuremonitoragent":
            case "ama":
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "AzureMonitorAgent",
                    Publisher = "Microsoft.Azure.Monitor",
                    WindowsType = "AzureMonitorWindowsAgent",
                    LinuxType = "AzureMonitorLinuxAgent",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                break;

            case "loganalytics":
            case "mma":
            case "omsagent":
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "LogAnalytics",
                    Publisher = "Microsoft.EnterpriseCloud.Monitoring",
                    WindowsType = "MicrosoftMonitoringAgent",
                    LinuxType = "OmsAgentForLinux",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>
                    {
                        ["workspaceId"] = workspaceId ?? "",
                        ["workspaceKey"] = workspaceKey ?? ""
                    }
                });
                break;

            case "defender":
            case "mde":
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "Defender",
                    Publisher = "Microsoft.Azure.AzureDefenderForServers",
                    WindowsType = "MDE.Windows",
                    LinuxType = "MDE.Linux",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                break;

            case "dependencyagent":
            case "dependency":
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "DependencyAgent",
                    Publisher = "Microsoft.Azure.Monitoring.DependencyAgent",
                    WindowsType = "DependencyAgentWindows",
                    LinuxType = "DependencyAgentLinux",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                break;

            case "guestconfiguration":
            case "gc":
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "GuestConfiguration",
                    Publisher = "Microsoft.GuestConfiguration",
                    WindowsType = "ConfigurationForWindows",
                    LinuxType = "ConfigurationForLinux",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                break;

            case "customscript":
            case "script":
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "CustomScript",
                    Publisher = "Microsoft.Azure.Extensions",
                    WindowsType = "CustomScriptExtension",
                    LinuxType = "CustomScript",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>
                    {
                        ["commandToExecute"] = scriptUrl ?? ""
                    }
                });
                break;

            case "all":
            case "core":
                // Deploy core monitoring extensions
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "AzureMonitorAgent",
                    Publisher = "Microsoft.Azure.Monitor",
                    WindowsType = "AzureMonitorWindowsAgent",
                    LinuxType = "AzureMonitorLinuxAgent",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "DependencyAgent",
                    Publisher = "Microsoft.Azure.Monitoring.DependencyAgent",
                    WindowsType = "DependencyAgentWindows",
                    LinuxType = "DependencyAgentLinux",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                extensions.Add(new ArcExtensionInfo
                {
                    Name = "GuestConfiguration",
                    Publisher = "Microsoft.GuestConfiguration",
                    WindowsType = "ConfigurationForWindows",
                    LinuxType = "ConfigurationForLinux",
                    SupportsWindows = true,
                    SupportsLinux = true,
                    Settings = new Dictionary<string, object>()
                });
                break;
        }

        return extensions;
    }

    /// <summary>
    /// Generate ARM template for Arc extension deployment
    /// </summary>
    private string GenerateArcExtensionArmTemplate(List<ArcExtensionInfo> extensions, string? workspaceId, string? workspaceKey)
    {
        var template = new
        {
            schema = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            contentVersion = "1.0.0.0",
            parameters = new
            {
                machineName = new { type = "string", metadata = new { description = "Name of the Arc machine" } },
                location = new { type = "string", defaultValue = "[resourceGroup().location]" }
            },
            variables = new { },
            resources = extensions.Select(ext => new
            {
                type = "Microsoft.HybridCompute/machines/extensions",
                apiVersion = "2023-10-03-preview",
                name = "[concat(parameters('machineName'), '/', '" + ext.Name + "')]",
                location = "[parameters('location')]",
                properties = new
                {
                    publisher = ext.Publisher,
                    type = ext.WindowsType, // Will need OS-specific handling in real deployment
                    typeHandlerVersion = "1.0",
                    autoUpgradeMinorVersion = true,
                    settings = ext.Settings as object ?? new Dictionary<string, object>()
                }
            }).ToArray()
        };

        return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Generate Azure CLI commands for Arc extension deployment
    /// </summary>
    private List<string> GenerateArcExtensionCliCommands(
        List<ArcExtensionInfo> extensions,
        string subscriptionId,
        string? resourceGroup,
        string? machineName,
        string? workspaceId,
        string? workspaceKey)
    {
        var commands = new List<string>();
        var machineFilter = string.IsNullOrEmpty(machineName) ? "<machine-name>" : machineName;
        var rgFilter = string.IsNullOrEmpty(resourceGroup) ? "<resource-group>" : resourceGroup;

        commands.Add($"# Set subscription");
        commands.Add($"az account set --subscription {subscriptionId}");
        commands.Add("");

        foreach (var ext in extensions)
        {
            commands.Add($"# Deploy {ext.Name}");
            var settingsJson = ext.Settings.Count > 0
                ? $" --settings '{JsonSerializer.Serialize(ext.Settings)}'"
                : "";

            commands.Add($"az connectedmachine extension create \\");
            commands.Add($"  --machine-name {machineFilter} \\");
            commands.Add($"  --resource-group {rgFilter} \\");
            commands.Add($"  --name {ext.Name} \\");
            commands.Add($"  --publisher {ext.Publisher} \\");
            commands.Add($"  --type {ext.WindowsType}{settingsJson}");
            commands.Add("");
        }

        return commands;
    }

    /// <summary>
    /// Get troubleshooting steps for disconnected machines
    /// </summary>
    private string[] GetTroubleshootingSteps(string status, string osType)
    {
        var steps = new List<string>();

        if (status == "Disconnected")
        {
            steps.Add("Check network connectivity to Azure endpoints");
            steps.Add("Verify the Azure Arc agent service is running");

            if (osType.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add("Run: Get-Service himds | Start-Service");
                steps.Add("Check logs: Get-Content 'C:\\ProgramData\\AzureConnectedMachineAgent\\Log\\himds.log' -Tail 50");
            }
            else
            {
                steps.Add("Run: sudo systemctl start himdsd");
                steps.Add("Check logs: sudo journalctl -u himdsd -n 50");
            }

            steps.Add("Verify firewall allows outbound HTTPS (443)");
            steps.Add("Test connectivity: azcmagent check");
        }
        else if (status == "Error")
        {
            steps.Add("Check agent logs for error details");
            steps.Add("Verify Azure credentials and permissions");
            steps.Add("Consider reinstalling the agent");
        }

        return steps.ToArray();
    }

    /// <summary>
    /// Helper to get property value from dictionary with default
    /// </summary>
    private T GetPropertyValue<T>(Dictionary<string, object>? properties, string key, T defaultValue)
    {
        if (properties == null) return defaultValue;

        if (properties.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            if (value is JsonElement jsonElement)
            {
                try
                {
                    if (typeof(T) == typeof(string))
                        return (T)(object)(jsonElement.GetString() ?? defaultValue?.ToString() ?? "");
                    if (typeof(T) == typeof(int))
                        return (T)(object)jsonElement.GetInt32();
                    if (typeof(T) == typeof(bool))
                        return (T)(object)jsonElement.GetBoolean();
                }
                catch
                {
                    return defaultValue;
                }
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Arc extension information model
    /// </summary>
    private class ArcExtensionInfo
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string WindowsType { get; set; } = "";
        public string LinuxType { get; set; } = "";
        public bool SupportsWindows { get; set; }
        public bool SupportsLinux { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new();
    }
}
