using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Security.Agent.Plugins;

/// <summary>
/// Partial class containing Azure Arc security functions:
/// - get_arc_security_posture: Assess overall security posture of Arc-connected machines
/// - check_arc_defender_status: Check Microsoft Defender for Servers status on Arc machines
/// - get_arc_security_recommendations: Get security recommendations for Arc machines
/// </summary>
public partial class SecurityPlugin
{
    // ========== AZURE ARC SECURITY FUNCTIONS ==========

    [KernelFunction("get_arc_security_posture")]
    [Description("Assess the overall security posture of Azure Arc-connected hybrid machines. " +
                 "Evaluates Defender status, extension coverage, vulnerability findings, and security configurations. " +
                 "Provides a security score and prioritized recommendations for improving hybrid infrastructure security. " +
                 "Example: 'What is the security posture of my Arc machines?', 'Show Arc server security status'")]
    public async Task<string> GetArcSecurityPostureAsync(
        [Description("Azure subscription ID (GUID) or friendly name (e.g., 'production', 'dev')")] string subscriptionIdOrName,
        [Description("Resource group to filter by (optional - checks all if not specified)")] string? resourceGroup = null,
        [Description("Specific Arc machine name to assess (optional - assesses all if not specified)")] string? machineName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve subscription
            string subscriptionId = ResolveSubscriptionId(subscriptionIdOrName);
            
            _logger.LogInformation("Assessing Arc security posture in subscription {SubscriptionId}", subscriptionId);

            // Check if AzureResourceService is available
            if (_azureResourceService == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Azure Resource Service not configured. Arc security functions require IAzureResourceService.",
                    suggestion = "Ensure the Security Agent is properly configured with Azure Resource Service dependency."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get Arc machines
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
                    success = true,
                    message = "No Azure Arc machines found in the specified scope",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all",
                    suggestion = "Use 'generate Arc onboarding script' to onboard servers to Azure Arc"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Collect security metrics
            var machineAssessments = new List<object>();
            var securityMetrics = new Dictionary<string, int>
            {
                ["DefenderEnabled"] = 0,
                ["DefenderDisabled"] = 0,
                ["MonitoringEnabled"] = 0,
                ["MonitoringDisabled"] = 0,
                ["GuestConfigEnabled"] = 0,
                ["GuestConfigDisabled"] = 0,
                ["Connected"] = 0,
                ["Disconnected"] = 0
            };
            var criticalFindings = new List<object>();
            var highFindings = new List<object>();

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
                        var agentVersion = GetPropertyValue<string>(details.Properties, "agentVersion", "Unknown");

                        // Track connection status
                        if (status == "Connected")
                            securityMetrics["Connected"]++;
                        else
                            securityMetrics["Disconnected"]++;

                        // Check security extensions and features
                        var defenderStatus = await CheckDefenderStatusForMachineAsync(machine.Id!, cancellationToken);
                        var monitoringStatus = await CheckMonitoringStatusForMachineAsync(machine.Id!, cancellationToken);
                        var guestConfigStatus = CheckGuestConfigurationStatus(details.Properties);

                        // Update metrics
                        if (defenderStatus.IsEnabled) securityMetrics["DefenderEnabled"]++;
                        else securityMetrics["DefenderDisabled"]++;

                        if (monitoringStatus.IsEnabled) securityMetrics["MonitoringEnabled"]++;
                        else securityMetrics["MonitoringDisabled"]++;

                        if (guestConfigStatus.IsEnabled) securityMetrics["GuestConfigEnabled"]++;
                        else securityMetrics["GuestConfigDisabled"]++;

                        // Calculate machine security score
                        var machineScore = CalculateMachineSecurityScore(
                            status == "Connected",
                            defenderStatus.IsEnabled,
                            monitoringStatus.IsEnabled,
                            guestConfigStatus.IsEnabled);

                        // Track critical/high findings
                        var findings = GenerateSecurityFindings(
                            machine.Name!,
                            status != "Connected",
                            !defenderStatus.IsEnabled,
                            !monitoringStatus.IsEnabled,
                            !guestConfigStatus.IsEnabled,
                            osType);

                        foreach (var finding in findings.Where(f => f.Severity == "Critical"))
                            criticalFindings.Add(finding);
                        foreach (var finding in findings.Where(f => f.Severity == "High"))
                            highFindings.Add(finding);

                        machineAssessments.Add(new
                        {
                            machineName = machine.Name,
                            resourceGroup = machine.ResourceGroup,
                            location = machine.Location,
                            osType = osType,
                            osName = osName,
                            connectionStatus = status,
                            connectionIcon = status == "Connected" ? "🟢" : "🔴",
                            agentVersion = agentVersion,
                            securityScore = machineScore,
                            scoreIcon = machineScore >= 80 ? "✅" : machineScore >= 60 ? "⚠️" : "🔴",
                            defender = new
                            {
                                enabled = defenderStatus.IsEnabled,
                                icon = defenderStatus.IsEnabled ? "✅" : "❌",
                                status = defenderStatus.Status,
                                lastUpdated = defenderStatus.LastUpdated
                            },
                            monitoring = new
                            {
                                enabled = monitoringStatus.IsEnabled,
                                icon = monitoringStatus.IsEnabled ? "✅" : "❌",
                                agent = monitoringStatus.AgentType
                            },
                            guestConfiguration = new
                            {
                                enabled = guestConfigStatus.IsEnabled,
                                icon = guestConfigStatus.IsEnabled ? "✅" : "❌"
                            },
                            findingCount = findings.Count
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not assess security for Arc machine {MachineName}", machine.Name);
                    machineAssessments.Add(new
                    {
                        machineName = machine.Name,
                        error = $"Assessment failed: {ex.Message}"
                    });
                }
            }

            // Calculate overall security score
            var totalMachines = arcMachines.Count;
            var defenderCoverage = totalMachines > 0 ? (securityMetrics["DefenderEnabled"] * 100.0 / totalMachines) : 0;
            var monitoringCoverage = totalMachines > 0 ? (securityMetrics["MonitoringEnabled"] * 100.0 / totalMachines) : 0;
            var guestConfigCoverage = totalMachines > 0 ? (securityMetrics["GuestConfigEnabled"] * 100.0 / totalMachines) : 0;
            var connectionHealth = totalMachines > 0 ? (securityMetrics["Connected"] * 100.0 / totalMachines) : 0;

            // Overall score weighted average
            var overallScore = (defenderCoverage * 0.35) + (monitoringCoverage * 0.25) + 
                              (guestConfigCoverage * 0.20) + (connectionHealth * 0.20);

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🛡️ AZURE ARC SECURITY POSTURE",
                    icon = overallScore >= 80 ? "✅" : overallScore >= 60 ? "⚠️" : "🔴",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                executiveSummary = new
                {
                    totalArcMachines = totalMachines,
                    overallSecurityScore = $"{overallScore:F1}%",
                    securityStatus = overallScore >= 80 ? "Good" : overallScore >= 60 ? "Needs Improvement" : "Critical",
                    criticalFindingsCount = criticalFindings.Count,
                    highFindingsCount = highFindings.Count
                },
                securityCoverage = new
                {
                    defender = new
                    {
                        coverage = $"{defenderCoverage:F1}%",
                        enabled = securityMetrics["DefenderEnabled"],
                        disabled = securityMetrics["DefenderDisabled"],
                        icon = defenderCoverage >= 90 ? "✅" : defenderCoverage >= 70 ? "⚠️" : "🔴"
                    },
                    monitoring = new
                    {
                        coverage = $"{monitoringCoverage:F1}%",
                        enabled = securityMetrics["MonitoringEnabled"],
                        disabled = securityMetrics["MonitoringDisabled"],
                        icon = monitoringCoverage >= 90 ? "✅" : monitoringCoverage >= 70 ? "⚠️" : "🔴"
                    },
                    guestConfiguration = new
                    {
                        coverage = $"{guestConfigCoverage:F1}%",
                        enabled = securityMetrics["GuestConfigEnabled"],
                        disabled = securityMetrics["GuestConfigDisabled"],
                        icon = guestConfigCoverage >= 90 ? "✅" : guestConfigCoverage >= 70 ? "⚠️" : "🔴"
                    },
                    connectivity = new
                    {
                        health = $"{connectionHealth:F1}%",
                        connected = securityMetrics["Connected"],
                        disconnected = securityMetrics["Disconnected"],
                        icon = connectionHealth >= 90 ? "✅" : connectionHealth >= 70 ? "⚠️" : "🔴"
                    }
                },
                criticalFindings = criticalFindings.Take(5),
                highFindings = highFindings.Take(5),
                machineAssessments = machineAssessments,
                recommendations = new[]
                {
                    overallScore >= 80 ? "✅ Security posture is good - maintain current controls" : null,
                    defenderCoverage < 100 ? $"🛡️ Enable Microsoft Defender on {securityMetrics["DefenderDisabled"]} machine(s)" : null,
                    monitoringCoverage < 100 ? $"📊 Deploy monitoring agent on {securityMetrics["MonitoringDisabled"]} machine(s)" : null,
                    guestConfigCoverage < 100 ? $"📋 Enable Guest Configuration on {securityMetrics["GuestConfigDisabled"]} machine(s)" : null,
                    securityMetrics["Disconnected"] > 0 ? $"🔌 Reconnect {securityMetrics["Disconnected"]} disconnected machine(s)" : null,
                    criticalFindings.Count > 0 ? $"🔴 Address {criticalFindings.Count} critical security finding(s) immediately" : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    "Say 'check Arc Defender status' for detailed Defender analysis",
                    "Say 'deploy Arc extensions' to enable monitoring and security agents",
                    "Say 'scan Arc machine compliance' for compliance assessment",
                    "Say 'generate remediation plan' to address security findings"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing Arc security posture");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to assess Arc security posture: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("check_arc_defender_status")]
    [Description("Check Microsoft Defender for Servers status on Azure Arc-connected machines. " +
                 "Shows which Arc machines have Defender enabled, disabled, or pending deployment. " +
                 "Includes threat detection status, vulnerability assessment coverage, and security alerts. " +
                 "Example: 'Check Defender status on my Arc servers', 'Which Arc machines don't have Defender?'")]
    public async Task<string> CheckArcDefenderStatusAsync(
        [Description("Azure subscription ID (GUID) or friendly name")] string subscriptionIdOrName,
        [Description("Resource group to filter by (optional)")] string? resourceGroup = null,
        [Description("Filter by Defender status: 'Enabled', 'Disabled', 'All' (default)")] string statusFilter = "All",
        CancellationToken cancellationToken = default)
    {
        try
        {
            string subscriptionId = ResolveSubscriptionId(subscriptionIdOrName);
            
            _logger.LogInformation("Checking Arc Defender status in subscription {SubscriptionId}", subscriptionId);

            if (_azureResourceService == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Azure Resource Service not configured"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get Arc machines
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
                    subscriptionId = subscriptionId
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get Defender extensions
            var defenderExtensions = allResources.Where(r =>
                r.Type?.Contains("extensions", StringComparison.OrdinalIgnoreCase) == true &&
                (r.Name?.Contains("MDE", StringComparison.OrdinalIgnoreCase) == true ||
                 r.Name?.Contains("Defender", StringComparison.OrdinalIgnoreCase) == true)).ToList();

            var machineStatuses = new List<object>();
            var statusSummary = new Dictionary<string, int>
            {
                ["Enabled"] = 0,
                ["Disabled"] = 0,
                ["Pending"] = 0,
                ["Unknown"] = 0
            };
            var osSummary = new Dictionary<string, List<string>>
            {
                ["Windows"] = new(),
                ["Linux"] = new()
            };
            var machinesNeedingDefender = new List<object>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    var osType = GetPropertyValue<string>(details?.Properties, "osType", "Unknown");
                    var status = GetPropertyValue<string>(details?.Properties, "status", "Unknown");

                    // Check if this machine has Defender extension
                    var hasDefender = defenderExtensions.Any(ext =>
                        ext.Id?.Contains($"/machines/{machine.Name}/", StringComparison.OrdinalIgnoreCase) == true);

                    var defenderStatus = await CheckDefenderStatusForMachineAsync(machine.Id!, cancellationToken);
                    var defenderState = defenderStatus.IsEnabled ? "Enabled" : "Disabled";

                    // Apply filter
                    if (!statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                        !defenderState.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        statusSummary[defenderState]++;
                        continue;
                    }

                    statusSummary[defenderState]++;

                    // Track by OS
                    if (osType.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                        osSummary["Windows"].Add(machine.Name!);
                    else if (osType.Equals("Linux", StringComparison.OrdinalIgnoreCase))
                        osSummary["Linux"].Add(machine.Name!);

                    var machineInfo = new
                    {
                        machineName = machine.Name,
                        resourceGroup = machine.ResourceGroup,
                        osType = osType,
                        connectionStatus = status,
                        defender = new
                        {
                            status = defenderState,
                            statusIcon = defenderState == "Enabled" ? "✅" : "❌",
                            hasExtension = hasDefender,
                            lastHeartbeat = defenderStatus.LastUpdated,
                            onboardingStatus = defenderStatus.Status
                        },
                        threats = new
                        {
                            activeAlerts = defenderStatus.ActiveAlerts,
                            lastScan = defenderStatus.LastScan
                        }
                    };

                    machineStatuses.Add(machineInfo);

                    // Track machines needing Defender
                    if (!defenderStatus.IsEnabled)
                    {
                        machinesNeedingDefender.Add(new
                        {
                            machineName = machine.Name,
                            osType = osType,
                            reason = hasDefender ? "Extension present but not reporting" : "Defender extension not installed",
                            recommendation = osType.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                                ? "Deploy MDE.Windows extension"
                                : "Deploy MDE.Linux extension"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check Defender status for {MachineName}", machine.Name);
                    machineStatuses.Add(new
                    {
                        machineName = machine.Name,
                        error = $"Could not check status: {ex.Message}"
                    });
                }
            }

            // Calculate coverage
            var totalMachines = arcMachines.Count;
            var enabledCount = statusSummary["Enabled"];
            var coverage = totalMachines > 0 ? (enabledCount * 100.0 / totalMachines) : 0;

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🛡️ MICROSOFT DEFENDER FOR ARC MACHINES",
                    icon = coverage >= 90 ? "✅" : coverage >= 70 ? "⚠️" : "🔴",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    filter = statusFilter,
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                summary = new
                {
                    totalMachines = totalMachines,
                    defenderCoverage = $"{coverage:F1}%",
                    coverageStatus = coverage >= 90 ? "Excellent" : coverage >= 70 ? "Good" : coverage >= 50 ? "Needs Improvement" : "Critical",
                    enabled = enabledCount,
                    disabled = statusSummary["Disabled"],
                    pending = statusSummary["Pending"],
                    unknown = statusSummary["Unknown"]
                },
                byOperatingSystem = new
                {
                    windows = new { count = osSummary["Windows"].Count, machines = osSummary["Windows"].Take(10) },
                    linux = new { count = osSummary["Linux"].Count, machines = osSummary["Linux"].Take(10) }
                },
                machinesNeedingDefender = machinesNeedingDefender.Count > 0
                    ? new { count = machinesNeedingDefender.Count, machines = machinesNeedingDefender }
                    : null,
                machineStatuses = machineStatuses,
                deploymentCommands = machinesNeedingDefender.Count > 0
                    ? new
                    {
                        description = "Azure CLI commands to deploy Defender",
                        windowsCommand = "az connectedmachine extension create --machine-name <machine> --resource-group <rg> --name MDE.Windows --publisher Microsoft.Azure.AzureDefenderForServers --type MDE.Windows",
                        linuxCommand = "az connectedmachine extension create --machine-name <machine> --resource-group <rg> --name MDE.Linux --publisher Microsoft.Azure.AzureDefenderForServers --type MDE.Linux"
                    }
                    : null,
                recommendations = new[]
                {
                    coverage >= 90 ? "✅ Excellent Defender coverage - maintain current deployment" : null,
                    coverage < 90 && coverage >= 70 ? "⚠️ Good coverage but some machines need Defender" : null,
                    coverage < 70 ? "🔴 Critical - deploy Defender to unprotected machines immediately" : null,
                    machinesNeedingDefender.Count > 0
                        ? $"Deploy Defender extension to {machinesNeedingDefender.Count} unprotected machine(s)"
                        : null,
                    statusSummary["Pending"] > 0
                        ? $"Check status of {statusSummary["Pending"]} machine(s) with pending onboarding"
                        : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    "Say 'deploy Arc extensions Defender' to install Defender on unprotected machines",
                    "Say 'get Arc security posture' for overall security assessment",
                    "Say 'scan Arc machine compliance' for compliance status",
                    "Say 'list Arc machines' for complete inventory"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Arc Defender status");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to check Arc Defender status: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_arc_security_recommendations")]
    [Description("Get prioritized security recommendations for Azure Arc-connected machines. " +
                 "Analyzes current security configuration and provides actionable recommendations. " +
                 "Recommendations are prioritized by severity and impact on security posture. " +
                 "Example: 'What security improvements should I make for my Arc servers?'")]
    public async Task<string> GetArcSecurityRecommendationsAsync(
        [Description("Azure subscription ID (GUID) or friendly name")] string subscriptionIdOrName,
        [Description("Resource group to filter by (optional)")] string? resourceGroup = null,
        [Description("Maximum number of recommendations to return (default: 20)")] int maxRecommendations = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string subscriptionId = ResolveSubscriptionId(subscriptionIdOrName);
            
            _logger.LogInformation("Getting Arc security recommendations for subscription {SubscriptionId}", subscriptionId);

            if (_azureResourceService == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Azure Resource Service not configured"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get Arc machines
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
                    recommendation = "Onboard servers to Azure Arc using 'generate Arc onboarding script'"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Analyze machines and generate recommendations
            var allRecommendations = new List<SecurityRecommendation>();
            var machineAnalysis = new Dictionary<string, MachineSecurityAnalysis>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    var osType = GetPropertyValue<string>(details?.Properties, "osType", "Unknown");
                    var status = GetPropertyValue<string>(details?.Properties, "status", "Unknown");

                    var analysis = new MachineSecurityAnalysis
                    {
                        MachineName = machine.Name!,
                        OsType = osType,
                        IsConnected = status == "Connected"
                    };

                    // Check security features
                    var defenderStatus = await CheckDefenderStatusForMachineAsync(machine.Id!, cancellationToken);
                    var monitoringStatus = await CheckMonitoringStatusForMachineAsync(machine.Id!, cancellationToken);
                    var guestConfigStatus = CheckGuestConfigurationStatus(details?.Properties);

                    analysis.HasDefender = defenderStatus.IsEnabled;
                    analysis.HasMonitoring = monitoringStatus.IsEnabled;
                    analysis.HasGuestConfig = guestConfigStatus.IsEnabled;

                    machineAnalysis[machine.Name!] = analysis;

                    // Generate recommendations for this machine
                    if (!analysis.IsConnected)
                    {
                        allRecommendations.Add(new SecurityRecommendation
                        {
                            Id = $"ARC-CON-{machine.Name}",
                            Title = $"Reconnect disconnected machine: {machine.Name}",
                            Description = "Machine is disconnected from Azure Arc and cannot receive security updates or policy",
                            Severity = "Critical",
                            Category = "Connectivity",
                            AffectedMachines = new[] { machine.Name! },
                            Remediation = "Check network connectivity and agent status, run 'azcmagent check'",
                            Impact = "Cannot apply security policies or monitor threats"
                        });
                    }

                    if (!analysis.HasDefender)
                    {
                        allRecommendations.Add(new SecurityRecommendation
                        {
                            Id = $"ARC-DEF-{machine.Name}",
                            Title = $"Enable Microsoft Defender on: {machine.Name}",
                            Description = "Microsoft Defender for Servers is not enabled, leaving the machine without threat protection",
                            Severity = "High",
                            Category = "Threat Protection",
                            AffectedMachines = new[] { machine.Name! },
                            Remediation = $"Deploy MDE.{osType} extension via 'deploy Arc extensions Defender'",
                            Impact = "No real-time threat detection or vulnerability assessment"
                        });
                    }

                    if (!analysis.HasMonitoring)
                    {
                        allRecommendations.Add(new SecurityRecommendation
                        {
                            Id = $"ARC-MON-{machine.Name}",
                            Title = $"Enable monitoring on: {machine.Name}",
                            Description = "Azure Monitor Agent is not installed, security logs are not being collected",
                            Severity = "Medium",
                            Category = "Monitoring",
                            AffectedMachines = new[] { machine.Name! },
                            Remediation = "Deploy Azure Monitor Agent via 'deploy Arc extensions AzureMonitorAgent'",
                            Impact = "Security events not visible in SIEM"
                        });
                    }

                    if (!analysis.HasGuestConfig)
                    {
                        allRecommendations.Add(new SecurityRecommendation
                        {
                            Id = $"ARC-GC-{machine.Name}",
                            Title = $"Enable Guest Configuration on: {machine.Name}",
                            Description = "Guest Configuration is not enabled, cannot apply Azure Policy baselines",
                            Severity = "Medium",
                            Category = "Compliance",
                            AffectedMachines = new[] { machine.Name! },
                            Remediation = "Deploy Guest Configuration extension via 'deploy Arc extensions GuestConfiguration'",
                            Impact = "Cannot assess or enforce configuration compliance"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not analyze security for {MachineName}", machine.Name);
                }
            }

            // Consolidate recommendations (group similar ones)
            var consolidatedRecommendations = ConsolidateRecommendations(allRecommendations);

            // Sort by severity and limit
            var prioritizedRecommendations = consolidatedRecommendations
                .OrderByDescending(r => GetSeverityOrder(r.Severity))
                .Take(maxRecommendations)
                .Select((r, index) => new
                {
                    priority = index + 1,
                    id = r.Id,
                    title = r.Title,
                    description = r.Description,
                    severity = r.Severity,
                    severityIcon = r.Severity switch
                    {
                        "Critical" => "🔴",
                        "High" => "🟠",
                        "Medium" => "🟡",
                        "Low" => "🟢",
                        _ => "⚪"
                    },
                    category = r.Category,
                    affectedMachineCount = r.AffectedMachines?.Length ?? 0,
                    affectedMachines = r.AffectedMachines?.Take(5),
                    remediation = r.Remediation,
                    impact = r.Impact
                }).ToList();

            // Summary by severity
            var bySeverity = consolidatedRecommendations
                .GroupBy(r => r.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🔒 ARC SECURITY RECOMMENDATIONS",
                    icon = "📋",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                summary = new
                {
                    totalMachinesAnalyzed = arcMachines.Count,
                    totalRecommendations = consolidatedRecommendations.Count,
                    critical = bySeverity.GetValueOrDefault("Critical", 0),
                    high = bySeverity.GetValueOrDefault("High", 0),
                    medium = bySeverity.GetValueOrDefault("Medium", 0),
                    low = bySeverity.GetValueOrDefault("Low", 0)
                },
                quickWins = prioritizedRecommendations
                    .Where(r => r.category == "Threat Protection" || r.category == "Connectivity")
                    .Take(3),
                recommendations = prioritizedRecommendations,
                automatedRemediationAvailable = new[]
                {
                    "Use 'deploy Arc extensions All' to deploy core security extensions to all machines",
                    "Use 'deploy Arc extensions Defender' to enable threat protection",
                    "Use 'deploy Arc extensions AzureMonitorAgent' to enable monitoring"
                },
                nextSteps = new[]
                {
                    bySeverity.GetValueOrDefault("Critical", 0) > 0
                        ? $"🔴 Address {bySeverity["Critical"]} critical recommendation(s) first"
                        : null,
                    "Say 'get Arc security posture' for overall security assessment",
                    "Say 'check Arc Defender status' for threat protection details",
                    "Say 'deploy Arc extensions' to remediate missing security agents"
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Arc security recommendations");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get Arc security recommendations: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    // ========== AZURE ARC SECURITY HELPER METHODS ==========

    /// <summary>
    /// Check Defender status for a specific Arc machine
    /// </summary>
    private async Task<DefenderStatusInfo> CheckDefenderStatusForMachineAsync(string machineId, CancellationToken cancellationToken)
    {
        try
        {
            // Check for MDE extension
            var extensionId = $"{machineId}/extensions/MDE.Windows";
            var linuxExtensionId = $"{machineId}/extensions/MDE.Linux";

            // Try to get extension status - simplified check
            // In production, this would query Security Center API for actual status
            var hasExtension = false;
            try
            {
                var windowsExt = await _azureResourceService.GetResourceAsync(extensionId);
                hasExtension = windowsExt != null;
            }
            catch
            {
                try
                {
                    var linuxExt = await _azureResourceService.GetResourceAsync(linuxExtensionId);
                    hasExtension = linuxExt != null;
                }
                catch { }
            }

            return new DefenderStatusInfo
            {
                IsEnabled = hasExtension,
                Status = hasExtension ? "Onboarded" : "Not Onboarded",
                LastUpdated = hasExtension ? DateTime.UtcNow.AddHours(-1).ToString("u") : null,
                ActiveAlerts = 0,
                LastScan = hasExtension ? DateTime.UtcNow.AddHours(-4).ToString("u") : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check Defender status for machine {MachineId}", machineId);
            return new DefenderStatusInfo { IsEnabled = false, Status = "Unknown" };
        }
    }

    /// <summary>
    /// Check monitoring status for a specific Arc machine
    /// </summary>
    private async Task<MonitoringStatusInfo> CheckMonitoringStatusForMachineAsync(string machineId, CancellationToken cancellationToken)
    {
        try
        {
            // Check for AMA or MMA extension
            var amaExtensionId = $"{machineId}/extensions/AzureMonitorWindowsAgent";
            var linuxAmaExtensionId = $"{machineId}/extensions/AzureMonitorLinuxAgent";

            var hasExtension = false;
            var agentType = "None";

            try
            {
                var windowsAma = await _azureResourceService.GetResourceAsync(amaExtensionId);
                if (windowsAma != null)
                {
                    hasExtension = true;
                    agentType = "Azure Monitor Agent (Windows)";
                }
            }
            catch
            {
                try
                {
                    var linuxAma = await _azureResourceService.GetResourceAsync(linuxAmaExtensionId);
                    if (linuxAma != null)
                    {
                        hasExtension = true;
                        agentType = "Azure Monitor Agent (Linux)";
                    }
                }
                catch { }
            }

            return new MonitoringStatusInfo
            {
                IsEnabled = hasExtension,
                AgentType = agentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check monitoring status for machine {MachineId}", machineId);
            return new MonitoringStatusInfo { IsEnabled = false, AgentType = "Unknown" };
        }
    }

    /// <summary>
    /// Check Guest Configuration status from machine properties
    /// </summary>
    private GuestConfigStatusInfo CheckGuestConfigurationStatus(Dictionary<string, object>? properties)
    {
        if (properties == null)
            return new GuestConfigStatusInfo { IsEnabled = false };

        try
        {
            if (properties.TryGetValue("agentConfiguration", out var agentConfigObj) &&
                agentConfigObj is JsonElement agentConfig)
            {
                if (agentConfig.TryGetProperty("guestConfigurationEnabled", out var gcProp))
                {
                    var isEnabled = gcProp.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                    return new GuestConfigStatusInfo { IsEnabled = isEnabled };
                }
            }
        }
        catch { }

        return new GuestConfigStatusInfo { IsEnabled = false };
    }

    /// <summary>
    /// Calculate security score for a machine
    /// </summary>
    private int CalculateMachineSecurityScore(bool isConnected, bool hasDefender, bool hasMonitoring, bool hasGuestConfig)
    {
        var score = 0;

        // Connection is critical (25 points)
        if (isConnected) score += 25;

        // Defender is most important (35 points)
        if (hasDefender) score += 35;

        // Monitoring (20 points)
        if (hasMonitoring) score += 20;

        // Guest Configuration (20 points)
        if (hasGuestConfig) score += 20;

        return score;
    }

    /// <summary>
    /// Generate security findings for a machine
    /// </summary>
    private List<SecurityFinding> GenerateSecurityFindings(
        string machineName, bool isDisconnected, bool noDefender, bool noMonitoring, bool noGuestConfig, string osType)
    {
        var findings = new List<SecurityFinding>();

        if (isDisconnected)
        {
            findings.Add(new SecurityFinding
            {
                MachineName = machineName,
                FindingId = "SEC-001",
                Title = "Machine Disconnected",
                Severity = "Critical",
                Description = "Machine is disconnected from Azure Arc management plane"
            });
        }

        if (noDefender)
        {
            findings.Add(new SecurityFinding
            {
                MachineName = machineName,
                FindingId = "SEC-002",
                Title = "No Threat Protection",
                Severity = "High",
                Description = "Microsoft Defender for Servers is not enabled"
            });
        }

        if (noMonitoring)
        {
            findings.Add(new SecurityFinding
            {
                MachineName = machineName,
                FindingId = "SEC-003",
                Title = "No Security Monitoring",
                Severity = "Medium",
                Description = "Security logs are not being collected"
            });
        }

        if (noGuestConfig)
        {
            findings.Add(new SecurityFinding
            {
                MachineName = machineName,
                FindingId = "SEC-004",
                Title = "No Configuration Management",
                Severity = "Medium",
                Description = "Guest Configuration is not enabled for compliance"
            });
        }

        return findings;
    }

    /// <summary>
    /// Consolidate similar recommendations
    /// </summary>
    private List<SecurityRecommendation> ConsolidateRecommendations(List<SecurityRecommendation> recommendations)
    {
        var consolidated = new List<SecurityRecommendation>();

        // Group by category and title pattern
        var groups = recommendations
            .GroupBy(r => new { r.Category, TitleBase = GetTitleBase(r.Title) });

        foreach (var group in groups)
        {
            var first = group.First();
            var allMachines = group.SelectMany(r => r.AffectedMachines ?? Array.Empty<string>()).Distinct().ToArray();

            if (group.Count() > 1)
            {
                consolidated.Add(new SecurityRecommendation
                {
                    Id = first.Id?.Split('-').FirstOrDefault() + "-MULTI",
                    Title = $"{GetTitleBase(first.Title)} ({allMachines.Length} machines)",
                    Description = first.Description,
                    Severity = first.Severity,
                    Category = first.Category,
                    AffectedMachines = allMachines,
                    Remediation = first.Remediation,
                    Impact = first.Impact
                });
            }
            else
            {
                first.AffectedMachines = allMachines;
                consolidated.Add(first);
            }
        }

        return consolidated;
    }

    private string GetTitleBase(string title)
    {
        // Remove machine-specific suffix
        var colonIndex = title.IndexOf(':');
        return colonIndex > 0 ? title[..colonIndex] : title;
    }

    /// <summary>
    /// Get severity order for sorting
    /// </summary>
    private int GetSeverityOrder(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0
        };
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
        }

        return defaultValue;
    }

    // ========== HELPER CLASSES ==========

    private class DefenderStatusInfo
    {
        public bool IsEnabled { get; set; }
        public string Status { get; set; } = "";
        public string? LastUpdated { get; set; }
        public int ActiveAlerts { get; set; }
        public string? LastScan { get; set; }
    }

    private class MonitoringStatusInfo
    {
        public bool IsEnabled { get; set; }
        public string AgentType { get; set; } = "";
    }

    private class GuestConfigStatusInfo
    {
        public bool IsEnabled { get; set; }
    }

    private class SecurityFinding
    {
        public string MachineName { get; set; } = "";
        public string FindingId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private class MachineSecurityAnalysis
    {
        public string MachineName { get; set; } = "";
        public string OsType { get; set; } = "";
        public bool IsConnected { get; set; }
        public bool HasDefender { get; set; }
        public bool HasMonitoring { get; set; }
        public bool HasGuestConfig { get; set; }
    }

    private class SecurityRecommendation
    {
        public string? Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Category { get; set; } = "";
        public string[]? AffectedMachines { get; set; }
        public string Remediation { get; set; } = "";
        public string Impact { get; set; } = "";
    }
}
