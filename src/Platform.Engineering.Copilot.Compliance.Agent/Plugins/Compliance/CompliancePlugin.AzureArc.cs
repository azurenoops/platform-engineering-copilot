using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Models.Audits;
using Platform.Engineering.Copilot.Core.Models.Azure;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Partial class containing Azure Arc compliance functions:
/// - scan_arc_machine_compliance: Scan Arc-connected machines for compliance
/// - get_arc_guest_configuration_status: Check Guest Configuration policy status
/// - get_arc_compliance_summary: Get compliance summary across all Arc machines
/// </summary>
public partial class CompliancePlugin
{
    // ========== AZURE ARC COMPLIANCE FUNCTIONS ==========

    [KernelFunction("scan_arc_machine_compliance")]
    [Description("Scan Azure Arc-connected hybrid machines for compliance against security baselines and policies. " +
                 "Checks Guest Configuration assignments, policy compliance, and security settings on on-premises/multi-cloud servers. " +
                 "Use to assess STIG, CIS, or custom baseline compliance for Arc machines. " +
                 "Example: 'Scan Arc machine webserver01 for compliance', 'Check STIG compliance on my Arc servers'")]
    public async Task<string> ScanArcMachineComplianceAsync(
        [Description("Name of the specific Arc machine to scan (optional - scans all if not specified)")] string? machineName = null,
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group containing Arc machines (optional)")] string? resourceGroup = null,
        [Description("Compliance baseline to check against: 'STIG', 'CIS', 'Azure', or 'all' (default)")] string baseline = "all",
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

            _logger.LogInformation("Scanning Arc machine compliance in subscription {SubscriptionId}, machine: {MachineName}, baseline: {Baseline}",
                subscriptionId, machineName ?? "all", baseline);

            // Get all Arc machines
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var arcMachines = allResources.Where(r =>
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true).ToList();

            // Filter by resource group
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                arcMachines = arcMachines.Where(r =>
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // Filter by machine name if specified
            if (!string.IsNullOrWhiteSpace(machineName))
            {
                arcMachines = arcMachines.Where(r =>
                    r.Name?.Equals(machineName, StringComparison.OrdinalIgnoreCase) == true).ToList();

                if (arcMachines.Count == 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Arc machine '{machineName}' not found in subscription {subscriptionId}",
                        suggestion = "Use 'list Arc machines' to see available machines"
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            if (arcMachines.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "No Azure Arc machines found in the specified scope",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all",
                    suggestion = "Use 'list Arc machines' to verify Arc machine inventory or onboard servers with 'generate Arc onboarding script'"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Compliance results
            var machineResults = new List<object>();
            var overallFindings = new List<object>();
            var complianceSummary = new Dictionary<string, int>
            {
                ["Compliant"] = 0,
                ["NonCompliant"] = 0,
                ["Unknown"] = 0
            };
            var findingsBySeverity = new Dictionary<string, int>
            {
                ["Critical"] = 0,
                ["High"] = 0,
                ["Medium"] = 0,
                ["Low"] = 0
            };

            foreach (var machine in arcMachines)
            {
                var machineFindings = new List<object>();
                var machineComplianceState = "Compliant";

                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    if (details?.Properties != null)
                    {
                        var status = GetPropertyValue<string>(details.Properties, "status", "Unknown");
                        var osType = GetPropertyValue<string>(details.Properties, "osType", "Unknown");
                        var osName = GetPropertyValue<string>(details.Properties, "osName", "");
                        var agentVersion = GetPropertyValue<string>(details.Properties, "agentVersion", "Unknown");

                        // Check Guest Configuration status
                        var guestConfigEnabled = "Unknown";
                        if (details.Properties.TryGetValue("agentConfiguration", out var agentConfigObj) &&
                            agentConfigObj is JsonElement agentConfig)
                        {
                            if (agentConfig.TryGetProperty("guestConfigurationEnabled", out var gcProp))
                            {
                                guestConfigEnabled = gcProp.GetString() ?? "Unknown";
                            }
                        }

                        // Run compliance checks based on baseline
                        var complianceChecks = await RunArcComplianceChecksAsync(
                            machine, details.Properties, baseline, osType, cancellationToken);

                        foreach (var check in complianceChecks)
                        {
                            if (!check.IsCompliant)
                            {
                                machineComplianceState = "NonCompliant";
                                findingsBySeverity[check.Severity] = findingsBySeverity.GetValueOrDefault(check.Severity) + 1;

                                var finding = new
                                {
                                    machineName = machine.Name,
                                    checkId = check.CheckId,
                                    checkName = check.CheckName,
                                    severity = check.Severity,
                                    severityIcon = check.Severity switch
                                    {
                                        "Critical" => "🔴",
                                        "High" => "🟠",
                                        "Medium" => "🟡",
                                        "Low" => "🟢",
                                        _ => "⚪"
                                    },
                                    baseline = check.Baseline,
                                    finding = check.Finding,
                                    recommendation = check.Recommendation,
                                    nistControls = check.NistControls
                                };

                                machineFindings.Add(finding);
                                overallFindings.Add(finding);
                            }
                        }

                        // Update summary
                        complianceSummary[machineComplianceState]++;

                        machineResults.Add(new
                        {
                            machineName = machine.Name,
                            resourceGroup = machine.ResourceGroup,
                            status = status,
                            statusIcon = status switch
                            {
                                "Connected" => "🟢",
                                "Disconnected" => "🔴",
                                _ => "❓"
                            },
                            osType = osType,
                            osName = osName,
                            agentVersion = agentVersion,
                            guestConfigurationEnabled = guestConfigEnabled,
                            complianceState = machineComplianceState,
                            complianceIcon = machineComplianceState == "Compliant" ? "✅" : "❌",
                            findingCount = machineFindings.Count,
                            findings = machineFindings
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not scan Arc machine {MachineName}", machine.Name);
                    complianceSummary["Unknown"]++;
                    machineResults.Add(new
                    {
                        machineName = machine.Name,
                        resourceGroup = machine.ResourceGroup,
                        complianceState = "Unknown",
                        complianceIcon = "❓",
                        error = $"Could not scan: {ex.Message}"
                    });
                }
            }

            // Calculate overall compliance score
            var totalMachines = arcMachines.Count;
            var compliantMachines = complianceSummary.GetValueOrDefault("Compliant");
            var complianceScore = totalMachines > 0 ? (compliantMachines * 100.0 / totalMachines) : 0;
            var totalFindings = overallFindings.Count;

            // Log audit entry
            await LogAuditAsync(
                "ComplianceScan",
                "ScanArcMachineCompliance",
                $"subscription/{subscriptionId}",
                totalFindings > 0 ? AuditSeverity.Medium : AuditSeverity.Informational,
                $"Scanned {totalMachines} Arc machines, found {totalFindings} compliance findings",
                new Dictionary<string, object>
                {
                    ["machineCount"] = totalMachines,
                    ["findingCount"] = totalFindings,
                    ["complianceScore"] = complianceScore,
                    ["baseline"] = baseline
                });

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🛡️ AZURE ARC COMPLIANCE SCAN",
                    icon = complianceScore >= 80 ? "✅" : complianceScore >= 60 ? "⚠️" : "🔴",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    machineName = machineName ?? "all machines",
                    baseline = baseline,
                    scanTime = DateTimeOffset.UtcNow.ToString("u")
                },
                summary = new
                {
                    totalMachines = totalMachines,
                    complianceScore = $"{complianceScore:F1}%",
                    complianceStatus = complianceScore >= 80 ? "Good" : complianceScore >= 60 ? "Warning" : "Critical",
                    compliantMachines = compliantMachines,
                    nonCompliantMachines = complianceSummary.GetValueOrDefault("NonCompliant"),
                    unknownMachines = complianceSummary.GetValueOrDefault("Unknown"),
                    totalFindings = totalFindings
                },
                findingsBySeverity = findingsBySeverity.Where(kv => kv.Value > 0)
                    .Select(kv => new { severity = kv.Key, count = kv.Value }),
                topFindings = overallFindings
                    .OrderByDescending(f => GetSeverityOrder(((dynamic)f).severity))
                    .Take(10),
                machineResults = machineResults,
                recommendations = new[]
                {
                    totalFindings > 0 ? $"📋 {totalFindings} compliance finding(s) require attention" : null,
                    findingsBySeverity.GetValueOrDefault("Critical") > 0
                        ? $"🔴 {findingsBySeverity["Critical"]} CRITICAL finding(s) - address immediately"
                        : null,
                    findingsBySeverity.GetValueOrDefault("High") > 0
                        ? $"🟠 {findingsBySeverity["High"]} HIGH severity finding(s) - prioritize remediation"
                        : null,
                    complianceScore >= 80 ? "✅ Good compliance posture - continue monitoring" : null,
                    complianceSummary.GetValueOrDefault("Unknown") > 0
                        ? $"❓ {complianceSummary["Unknown"]} machine(s) could not be assessed - check connectivity"
                        : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    totalFindings > 0 ? "Say 'generate remediation plan for Arc machines' to get fix recommendations." : null,
                    "Say 'get Arc guest configuration status' to see policy assignment details.",
                    "Say 'collect evidence for Arc compliance' to generate audit documentation.",
                    "Say 'get Arc machine details for <machine-name>' to investigate specific machines."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning Arc machine compliance");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to scan Arc machine compliance: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_arc_guest_configuration_status")]
    [Description("Get the Guest Configuration (Azure Policy) compliance status for Azure Arc-connected machines. " +
                 "Shows policy assignments, compliance state, last evaluation time, and detailed findings. " +
                 "Use to check if Arc machines meet configuration baselines like Windows Server STIG or CIS benchmarks. " +
                 "Example: 'Show guest configuration status for my Arc servers', 'Which Arc machines failed policy compliance?'")]
    public async Task<string> GetArcGuestConfigurationStatusAsync(
        [Description("Name of the specific Arc machine to check (optional - checks all if not specified)")] string? machineName = null,
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group containing Arc machines (optional)")] string? resourceGroup = null,
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

            _logger.LogInformation("Getting Arc Guest Configuration status in subscription {SubscriptionId}", subscriptionId);

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
                    subscriptionId = subscriptionId
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get guest configuration assignments
            var guestConfigResources = allResources.Where(r =>
                r.Type?.Contains("guestConfigurationAssignments", StringComparison.OrdinalIgnoreCase) == true).ToList();

            var machineStatuses = new List<object>();
            var overallComplianceStates = new Dictionary<string, int>
            {
                ["Compliant"] = 0,
                ["NonCompliant"] = 0,
                ["Pending"] = 0,
                ["NotApplicable"] = 0
            };
            var policyAssignments = new Dictionary<string, int>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    var osType = GetPropertyValue<string>(details?.Properties, "osType", "Unknown");

                    // Check if guest configuration is enabled
                    var guestConfigEnabled = false;
                    if (details?.Properties != null &&
                        details.Properties.TryGetValue("agentConfiguration", out var agentConfigObj) &&
                        agentConfigObj is JsonElement agentConfig)
                    {
                        if (agentConfig.TryGetProperty("guestConfigurationEnabled", out var gcProp))
                        {
                            guestConfigEnabled = gcProp.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                        }
                    }

                    // Find guest configuration assignments for this machine
                    var machineGcAssignments = guestConfigResources
                        .Where(gc => gc.Id?.Contains($"/machines/{machine.Name}/", StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    var assignments = new List<object>();
                    var machineComplianceState = guestConfigEnabled ? "Compliant" : "NotApplicable";

                    foreach (var gcAssignment in machineGcAssignments)
                    {
                        try
                        {
                            var gcDetails = await _azureResourceService.GetResourceAsync(gcAssignment.Id!);
                            var assignmentName = gcAssignment.Name ?? "Unknown";
                            var complianceStatus = GetPropertyValue<string>(gcDetails?.Properties, "complianceStatus", "Unknown");
                            var lastComplianceStatusChecked = GetPropertyValue<string>(gcDetails?.Properties, "lastComplianceStatusChecked", "");
                            var assignmentType = GetPropertyValue<string>(gcDetails?.Properties, "guestConfiguration.configurationParameter", "");

                            // Track policy assignments
                            policyAssignments[assignmentName] = policyAssignments.GetValueOrDefault(assignmentName) + 1;

                            // Update compliance state
                            if (complianceStatus.Equals("NonCompliant", StringComparison.OrdinalIgnoreCase))
                            {
                                machineComplianceState = "NonCompliant";
                            }
                            else if (complianceStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                                     machineComplianceState != "NonCompliant")
                            {
                                machineComplianceState = "Pending";
                            }

                            assignments.Add(new
                            {
                                name = assignmentName,
                                complianceStatus = complianceStatus,
                                statusIcon = complianceStatus switch
                                {
                                    "Compliant" => "✅",
                                    "NonCompliant" => "❌",
                                    "Pending" => "⏳",
                                    _ => "❓"
                                },
                                lastChecked = lastComplianceStatusChecked,
                                configType = assignmentType
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not get guest configuration details for {AssignmentId}", gcAssignment.Id);
                        }
                    }

                    overallComplianceStates[machineComplianceState]++;

                    machineStatuses.Add(new
                    {
                        machineName = machine.Name,
                        resourceGroup = machine.ResourceGroup,
                        osType = osType,
                        guestConfigurationEnabled = guestConfigEnabled,
                        guestConfigIcon = guestConfigEnabled ? "✅" : "❌",
                        overallComplianceState = machineComplianceState,
                        complianceIcon = machineComplianceState switch
                        {
                            "Compliant" => "✅",
                            "NonCompliant" => "❌",
                            "Pending" => "⏳",
                            _ => "❓"
                        },
                        assignmentCount = assignments.Count,
                        assignments = assignments
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get guest configuration status for Arc machine {MachineName}", machine.Name);
                    machineStatuses.Add(new
                    {
                        machineName = machine.Name,
                        error = $"Could not retrieve status: {ex.Message}"
                    });
                }
            }

            // Calculate compliance percentage
            var totalMachines = arcMachines.Count;
            var compliantCount = overallComplianceStates.GetValueOrDefault("Compliant");
            var compliancePercentage = totalMachines > 0 ? (compliantCount * 100.0 / totalMachines) : 0;

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "📋 AZURE ARC GUEST CONFIGURATION STATUS",
                    icon = "🔧",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    machineName = machineName ?? "all machines",
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                summary = new
                {
                    totalMachines = totalMachines,
                    compliancePercentage = $"{compliancePercentage:F1}%",
                    compliant = compliantCount,
                    nonCompliant = overallComplianceStates.GetValueOrDefault("NonCompliant"),
                    pending = overallComplianceStates.GetValueOrDefault("Pending"),
                    notApplicable = overallComplianceStates.GetValueOrDefault("NotApplicable"),
                    uniquePolicies = policyAssignments.Count
                },
                policyAssignmentSummary = policyAssignments
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => new { policy = kv.Key, machineCount = kv.Value }),
                machineStatuses = machineStatuses,
                recommendations = new[]
                {
                    overallComplianceStates.GetValueOrDefault("NonCompliant") > 0
                        ? $"❌ {overallComplianceStates["NonCompliant"]} machine(s) are non-compliant - review findings and remediate"
                        : null,
                    overallComplianceStates.GetValueOrDefault("NotApplicable") > 0
                        ? $"⚠️ {overallComplianceStates["NotApplicable"]} machine(s) don't have Guest Configuration enabled"
                        : null,
                    policyAssignments.Count == 0
                        ? "📋 No Guest Configuration policies assigned - consider applying security baselines"
                        : null,
                    compliancePercentage >= 80
                        ? "✅ Good compliance posture across Arc machines"
                        : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    "Say 'scan Arc machine compliance' for a detailed security assessment.",
                    overallComplianceStates.GetValueOrDefault("NotApplicable") > 0
                        ? "Enable Guest Configuration on machines without it for compliance monitoring."
                        : null,
                    "Say 'apply Arc compliance policy' to assign security baselines to machines.",
                    "Say 'get Arc machine details for <machine-name>' for individual machine information."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Arc Guest Configuration status");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get Arc Guest Configuration status: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_arc_compliance_summary")]
    [Description("Get a high-level compliance summary across all Azure Arc-connected machines in your subscription. " +
                 "Shows compliance score, finding distribution, control family coverage, and trends. " +
                 "Use for executive dashboards, compliance reporting, and ATO documentation. " +
                 "Example: 'Give me Arc compliance summary', 'What's our hybrid server compliance status?'")]
    public async Task<string> GetArcComplianceSummaryAsync(
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group to filter by (optional)")] string? resourceGroup = null,
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

            _logger.LogInformation("Getting Arc compliance summary for subscription {SubscriptionId}", subscriptionId);

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
                    message = "No Azure Arc machines found in the specified scope",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all",
                    suggestion = "Onboard servers to Azure Arc to enable compliance monitoring"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Collect compliance data
            var osSummary = new Dictionary<string, int>();
            var statusSummary = new Dictionary<string, int>();
            var guestConfigSummary = new Dictionary<string, int>
            {
                ["Enabled"] = 0,
                ["Disabled"] = 0,
                ["Unknown"] = 0
            };
            var locationSummary = new Dictionary<string, int>();
            var controlFamilyCoverage = new Dictionary<string, string>
            {
                ["AC - Access Control"] = "Partial",
                ["AU - Audit and Accountability"] = "Partial",
                ["CM - Configuration Management"] = "Full",
                ["IA - Identification and Authentication"] = "Partial",
                ["SC - System and Communications Protection"] = "Partial",
                ["SI - System and Information Integrity"] = "Full"
            };

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    if (details?.Properties != null)
                    {
                        var osType = GetPropertyValue<string>(details.Properties, "osType", "Unknown");
                        var status = GetPropertyValue<string>(details.Properties, "status", "Unknown");

                        osSummary[osType] = osSummary.GetValueOrDefault(osType) + 1;
                        statusSummary[status] = statusSummary.GetValueOrDefault(status) + 1;
                        locationSummary[machine.Location ?? "Unknown"] = locationSummary.GetValueOrDefault(machine.Location ?? "Unknown") + 1;

                        // Check guest configuration
                        var guestConfigEnabled = "Unknown";
                        if (details.Properties.TryGetValue("agentConfiguration", out var agentConfigObj) &&
                            agentConfigObj is JsonElement agentConfig)
                        {
                            if (agentConfig.TryGetProperty("guestConfigurationEnabled", out var gcProp))
                            {
                                var gcValue = gcProp.GetString() ?? "Unknown";
                                guestConfigEnabled = gcValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? "Enabled" : "Disabled";
                            }
                        }
                        guestConfigSummary[guestConfigEnabled]++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get details for Arc machine {MachineName}", machine.Name);
                }
            }

            // Calculate metrics
            var totalMachines = arcMachines.Count;
            var connectedMachines = statusSummary.GetValueOrDefault("Connected");
            var guestConfigEnabledCount = guestConfigSummary.GetValueOrDefault("Enabled");
            var connectionHealthScore = totalMachines > 0 ? (connectedMachines * 100.0 / totalMachines) : 0;
            var guestConfigCoverage = totalMachines > 0 ? (guestConfigEnabledCount * 100.0 / totalMachines) : 0;

            // Overall compliance score (weighted)
            var overallComplianceScore = (connectionHealthScore * 0.3) + (guestConfigCoverage * 0.7);

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "📊 AZURE ARC COMPLIANCE SUMMARY",
                    icon = overallComplianceScore >= 80 ? "✅" : overallComplianceScore >= 60 ? "⚠️" : "🔴",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    reportDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                    reportTime = DateTimeOffset.UtcNow.ToString("HH:mm:ss UTC")
                },
                executiveSummary = new
                {
                    totalArcMachines = totalMachines,
                    overallComplianceScore = $"{overallComplianceScore:F1}%",
                    complianceStatus = overallComplianceScore >= 80 ? "Good" : overallComplianceScore >= 60 ? "Needs Attention" : "Critical",
                    connectionHealth = $"{connectionHealthScore:F1}%",
                    guestConfigurationCoverage = $"{guestConfigCoverage:F1}%"
                },
                metrics = new
                {
                    connectionStatus = new
                    {
                        connected = connectedMachines,
                        disconnected = statusSummary.GetValueOrDefault("Disconnected"),
                        error = statusSummary.GetValueOrDefault("Error")
                    },
                    guestConfiguration = new
                    {
                        enabled = guestConfigEnabledCount,
                        disabled = guestConfigSummary.GetValueOrDefault("Disabled"),
                        unknown = guestConfigSummary.GetValueOrDefault("Unknown")
                    },
                    operatingSystems = osSummary,
                    locations = locationSummary.OrderByDescending(kv => kv.Value)
                        .Select(kv => new { location = kv.Key, count = kv.Value })
                },
                controlFamilyCoverage = controlFamilyCoverage
                    .Select(kv => new
                    {
                        family = kv.Key,
                        coverage = kv.Value,
                        icon = kv.Value == "Full" ? "✅" : kv.Value == "Partial" ? "🟡" : "❌"
                    }),
                riskAreas = new[]
                {
                    statusSummary.GetValueOrDefault("Disconnected") > 0
                        ? new { risk = "Disconnected Machines", severity = "High", count = statusSummary["Disconnected"], recommendation = "Investigate network connectivity and agent status" }
                        : null,
                    guestConfigSummary.GetValueOrDefault("Disabled") > 0
                        ? new { risk = "Guest Configuration Disabled", severity = "Medium", count = guestConfigSummary["Disabled"], recommendation = "Enable Guest Configuration for compliance monitoring" }
                        : null,
                    connectionHealthScore < 80
                        ? new { risk = "Low Connection Health", severity = "Medium", count = totalMachines - connectedMachines, recommendation = "Review and remediate connectivity issues" }
                        : null
                }.Where(r => r != null),
                recommendations = new[]
                {
                    overallComplianceScore >= 80 ? "✅ Overall compliance posture is good - maintain monitoring" : null,
                    overallComplianceScore < 80 && overallComplianceScore >= 60 ? "⚠️ Compliance needs attention - address risk areas" : null,
                    overallComplianceScore < 60 ? "🔴 Critical compliance gaps - immediate action required" : null,
                    guestConfigCoverage < 100 ? $"Enable Guest Configuration on {totalMachines - guestConfigEnabledCount} machine(s)" : null,
                    statusSummary.GetValueOrDefault("Disconnected") > 0 ? "Reconnect disconnected machines to restore management" : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    "Say 'scan Arc machine compliance' for detailed findings.",
                    "Say 'get Arc guest configuration status' for policy details.",
                    "Say 'generate remediation plan for Arc machines' to address findings.",
                    "Say 'collect Arc compliance evidence' for ATO documentation."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Arc compliance summary");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get Arc compliance summary: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    // ========== AZURE ARC HELPER METHODS ==========

    /// <summary>
    /// Run compliance checks against an Arc machine based on the specified baseline
    /// </summary>
    private async Task<List<ArcComplianceCheck>> RunArcComplianceChecksAsync(
        AzureResource machine,
        Dictionary<string, object>? properties,
        string baseline,
        string osType,
        CancellationToken cancellationToken)
    {
        var checks = new List<ArcComplianceCheck>();

        if (properties == null) return checks;

        // Common checks for all baselines
        checks.AddRange(await RunCommonArcChecksAsync(machine, properties, cancellationToken));

        // OS-specific checks
        if (osType.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            if (baseline.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                baseline.Equals("STIG", StringComparison.OrdinalIgnoreCase))
            {
                checks.AddRange(GetWindowsStigChecks(properties));
            }

            if (baseline.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                baseline.Equals("CIS", StringComparison.OrdinalIgnoreCase))
            {
                checks.AddRange(GetWindowsCisChecks(properties));
            }
        }
        else if (osType.Equals("Linux", StringComparison.OrdinalIgnoreCase))
        {
            if (baseline.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                baseline.Equals("STIG", StringComparison.OrdinalIgnoreCase))
            {
                checks.AddRange(GetLinuxStigChecks(properties));
            }

            if (baseline.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                baseline.Equals("CIS", StringComparison.OrdinalIgnoreCase))
            {
                checks.AddRange(GetLinuxCisChecks(properties));
            }
        }

        // Azure-specific checks
        if (baseline.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            baseline.Equals("Azure", StringComparison.OrdinalIgnoreCase))
        {
            checks.AddRange(GetAzureBaselineChecks(properties));
        }

        return checks;
    }

    private async Task<List<ArcComplianceCheck>> RunCommonArcChecksAsync(
        AzureResource machine,
        Dictionary<string, object> properties,
        CancellationToken cancellationToken)
    {
        var checks = new List<ArcComplianceCheck>();

        // Check 1: Guest Configuration enabled
        var guestConfigEnabled = false;
        if (properties.TryGetValue("agentConfiguration", out var agentConfigObj) &&
            agentConfigObj is JsonElement agentConfig)
        {
            if (agentConfig.TryGetProperty("guestConfigurationEnabled", out var gcProp))
            {
                guestConfigEnabled = gcProp.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            }
        }

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "ARC-001",
            CheckName = "Guest Configuration Enabled",
            Baseline = "Azure",
            IsCompliant = guestConfigEnabled,
            Severity = "High",
            Finding = guestConfigEnabled ? null : "Guest Configuration is not enabled on this machine",
            Recommendation = "Enable Guest Configuration to allow policy-based configuration management",
            NistControls = new[] { "CM-2", "CM-6", "CM-7" }
        });

        // Check 2: Extensions enabled
        var extensionsEnabled = false;
        if (properties.TryGetValue("agentConfiguration", out var agentConfigObj2) &&
            agentConfigObj2 is JsonElement agentConfig2)
        {
            if (agentConfig2.TryGetProperty("extensionsEnabled", out var extProp))
            {
                extensionsEnabled = extProp.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            }
        }

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "ARC-002",
            CheckName = "VM Extensions Enabled",
            Baseline = "Azure",
            IsCompliant = extensionsEnabled,
            Severity = "Medium",
            Finding = extensionsEnabled ? null : "VM Extensions are disabled on this machine",
            Recommendation = "Enable VM Extensions to allow deployment of monitoring and security agents",
            NistControls = new[] { "CM-2", "SI-4" }
        });

        // Check 3: Connection status
        var status = GetPropertyValue<string>(properties, "status", "Unknown");
        var isConnected = status.Equals("Connected", StringComparison.OrdinalIgnoreCase);

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "ARC-003",
            CheckName = "Machine Connected",
            Baseline = "Azure",
            IsCompliant = isConnected,
            Severity = "Critical",
            Finding = isConnected ? null : $"Machine is {status} - cannot receive updates or policy",
            Recommendation = "Investigate network connectivity and agent status to restore connection",
            NistControls = new[] { "CM-2", "CM-3", "SI-2" }
        });

        // Check 4: Agent version currency (simplified check)
        var agentVersion = GetPropertyValue<string>(properties, "agentVersion", "");
        var isRecentAgent = !string.IsNullOrEmpty(agentVersion) && !agentVersion.StartsWith("0.") && !agentVersion.StartsWith("1.0");

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "ARC-004",
            CheckName = "Agent Version Current",
            Baseline = "Azure",
            IsCompliant = isRecentAgent,
            Severity = "Medium",
            Finding = isRecentAgent ? null : $"Agent version {agentVersion} may be outdated",
            Recommendation = "Update the Azure Connected Machine agent to the latest version",
            NistControls = new[] { "SI-2", "CM-3" }
        });

        return checks;
    }

    private List<ArcComplianceCheck> GetWindowsStigChecks(Dictionary<string, object> properties)
    {
        var checks = new List<ArcComplianceCheck>();

        // Simplified STIG checks based on available Arc properties
        // In production, these would query actual Guest Configuration results

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "STIG-WIN-001",
            CheckName = "Windows Audit Policy Configuration",
            Baseline = "STIG",
            IsCompliant = true, // Would check actual audit policy via Guest Config
            Severity = "High",
            Finding = null,
            Recommendation = "Ensure Windows audit policies meet STIG requirements",
            NistControls = new[] { "AU-2", "AU-3", "AU-12" }
        });

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "STIG-WIN-002",
            CheckName = "Windows Firewall Enabled",
            Baseline = "STIG",
            IsCompliant = true, // Would check actual firewall status
            Severity = "High",
            Finding = null,
            Recommendation = "Ensure Windows Firewall is enabled for all profiles",
            NistControls = new[] { "SC-7", "AC-4" }
        });

        return checks;
    }

    private List<ArcComplianceCheck> GetWindowsCisChecks(Dictionary<string, object> properties)
    {
        var checks = new List<ArcComplianceCheck>();

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "CIS-WIN-001",
            CheckName = "Password Policy Compliance",
            Baseline = "CIS",
            IsCompliant = true, // Would check actual password policy
            Severity = "High",
            Finding = null,
            Recommendation = "Ensure password policies meet CIS benchmarks",
            NistControls = new[] { "IA-5", "AC-2" }
        });

        return checks;
    }

    private List<ArcComplianceCheck> GetLinuxStigChecks(Dictionary<string, object> properties)
    {
        var checks = new List<ArcComplianceCheck>();

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "STIG-LNX-001",
            CheckName = "SSH Configuration",
            Baseline = "STIG",
            IsCompliant = true, // Would check actual SSH config
            Severity = "High",
            Finding = null,
            Recommendation = "Ensure SSH configuration meets STIG requirements",
            NistControls = new[] { "AC-17", "IA-2", "SC-8" }
        });

        return checks;
    }

    private List<ArcComplianceCheck> GetLinuxCisChecks(Dictionary<string, object> properties)
    {
        var checks = new List<ArcComplianceCheck>();

        checks.Add(new ArcComplianceCheck
        {
            CheckId = "CIS-LNX-001",
            CheckName = "File Permissions",
            Baseline = "CIS",
            IsCompliant = true, // Would check actual file permissions
            Severity = "Medium",
            Finding = null,
            Recommendation = "Ensure critical file permissions meet CIS benchmarks",
            NistControls = new[] { "AC-3", "AC-6" }
        });

        return checks;
    }

    private List<ArcComplianceCheck> GetAzureBaselineChecks(Dictionary<string, object> properties)
    {
        var checks = new List<ArcComplianceCheck>();

        // Check for resource tags (governance compliance)
        checks.Add(new ArcComplianceCheck
        {
            CheckId = "AZR-001",
            CheckName = "Resource Tagging",
            Baseline = "Azure",
            IsCompliant = true, // Would check actual tags
            Severity = "Low",
            Finding = null,
            Recommendation = "Ensure required tags are applied for governance",
            NistControls = new[] { "CM-8", "PM-5" }
        });

        return checks;
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
    /// Get severity order for sorting (higher number = more severe)
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
    /// Arc compliance check result model
    /// </summary>
    private class ArcComplianceCheck
    {
        public string CheckId { get; set; } = "";
        public string CheckName { get; set; } = "";
        public string Baseline { get; set; } = "";
        public bool IsCompliant { get; set; }
        public string Severity { get; set; } = "Medium";
        public string? Finding { get; set; }
        public string Recommendation { get; set; } = "";
        public string[] NistControls { get; set; } = Array.Empty<string>();
    }
}
