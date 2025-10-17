using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.Azure.Cost;

/// <summary>
/// Auto-shutdown automation service for non-production resources.
/// Creates schedules, Logic Apps, and runbooks for automated cost optimization.
/// </summary>
public class AutoShutdownAutomationService
{
    private readonly ILogger<AutoShutdownAutomationService> _logger;

    public AutoShutdownAutomationService(ILogger<AutoShutdownAutomationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyze resources and create auto-shutdown recommendations.
    /// </summary>
    public async Task<List<CostOptimizationRecommendation>> GenerateAutoShutdownRecommendationsAsync(
        string subscriptionId,
        List<ResourceCostBreakdown> resources,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating auto-shutdown recommendations for subscription {SubscriptionId}", subscriptionId);

        var recommendations = new List<CostOptimizationRecommendation>();

        // Group resources by type and analyze shutdown potential
        var vms = resources.Where(r => r.ResourceType.Contains("virtualMachines", StringComparison.OrdinalIgnoreCase)).ToList();
        var databases = resources.Where(r => r.ResourceType.Contains("sql", StringComparison.OrdinalIgnoreCase) || 
                                             r.ResourceType.Contains("database", StringComparison.OrdinalIgnoreCase)).ToList();
        var aksCluster = resources.Where(r => r.ResourceType.Contains("managedClusters", StringComparison.OrdinalIgnoreCase)).ToList();
        var appServices = resources.Where(r => r.ResourceType.Contains("sites", StringComparison.OrdinalIgnoreCase)).ToList();

        // Analyze VMs for auto-shutdown
        recommendations.AddRange(await AnalyzeVMsForAutoShutdownAsync(vms, cancellationToken));

        // Analyze databases for scaling down/pause
        recommendations.AddRange(await AnalyzeDatabasesForAutoShutdownAsync(databases, cancellationToken));

        // Analyze AKS for node pool scaling
        recommendations.AddRange(await AnalyzeAKSForAutoShutdownAsync(aksCluster, cancellationToken));

        // Analyze App Services for auto-scale down
        recommendations.AddRange(await AnalyzeAppServicesForAutoShutdownAsync(appServices, cancellationToken));

        _logger.LogInformation("Generated {Count} auto-shutdown recommendations with potential savings ${Savings:F2}/month",
            recommendations.Count,
            recommendations.Sum(r => r.EstimatedMonthlySavings));

        return recommendations;
    }

    /// <summary>
    /// Create auto-shutdown automation for a resource.
    /// </summary>
    public async Task<AutoShutdownConfiguration> CreateAutoShutdownAutomationAsync(
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string resourceType,
        ShutdownSchedule schedule,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating auto-shutdown automation for {ResourceType} '{ResourceName}'", resourceType, resourceName);

        var config = new AutoShutdownConfiguration
        {
            SubscriptionId = subscriptionId,
            ResourceGroupName = resourceGroupName,
            ResourceName = resourceName,
            ResourceType = resourceType,
            Schedule = schedule,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = dryRun ? "Dry-Run" : "Creating"
        };

        // Determine automation method based on resource type
        if (resourceType.Contains("virtualMachines", StringComparison.OrdinalIgnoreCase))
        {
            config.AutomationMethod = "Azure Automation Runbook + Schedule";
            config.Components = await CreateVMAutoShutdownAsync(config, dryRun, cancellationToken);
        }
        else if (resourceType.Contains("sql", StringComparison.OrdinalIgnoreCase))
        {
            config.AutomationMethod = "Logic App + SQL Pause/Resume";
            config.Components = await CreateSQLAutoShutdownAsync(config, dryRun, cancellationToken);
        }
        else if (resourceType.Contains("managedClusters", StringComparison.OrdinalIgnoreCase))
        {
            config.AutomationMethod = "AKS Node Pool Auto-Scaling + Schedules";
            config.Components = await CreateAKSAutoShutdownAsync(config, dryRun, cancellationToken);
        }
        else if (resourceType.Contains("sites", StringComparison.OrdinalIgnoreCase))
        {
            config.AutomationMethod = "App Service Auto-Scale Rules";
            config.Components = await CreateAppServiceAutoShutdownAsync(config, dryRun, cancellationToken);
        }
        else
        {
            config.AutomationMethod = "Generic Logic App";
            config.Components = await CreateGenericAutoShutdownAsync(config, dryRun, cancellationToken);
        }

        config.Status = dryRun ? "Dry-Run Complete" : "Active";
        config.EstimatedMonthlySavings = CalculateEstimatedSavings(resourceType, schedule);

        var report = GenerateAutomationReport(config);
        _logger.LogInformation("Auto-shutdown automation created:\n{Report}", report);

        return config;
    }

    #region Resource-Specific Analysis

    private Task<List<CostOptimizationRecommendation>> AnalyzeVMsForAutoShutdownAsync(
        List<ResourceCostBreakdown> vms,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<CostOptimizationRecommendation>();

        foreach (var vm in vms)
        {
            // Check if VM is in dev/test environment (heuristic: resource group contains dev, test, qa, staging)
            var isNonProduction = vm.ResourceGroup.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
                                 vm.ResourceGroup.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                 vm.ResourceGroup.Contains("qa", StringComparison.OrdinalIgnoreCase) ||
                                 vm.ResourceGroup.Contains("staging", StringComparison.OrdinalIgnoreCase) ||
                                 vm.ResourceName.Contains("dev", StringComparison.OrdinalIgnoreCase);

            if (!isNonProduction) continue;

            var schedule = DetermineOptimalSchedule(vm);
            var savings = CalculateVMShutdownSavings(vm, schedule);

            recommendations.Add(new CostOptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ResourceId = $"/subscriptions/{vm.SubscriptionId}/resourceGroups/{vm.ResourceGroup}/providers/Microsoft.Compute/virtualMachines/{vm.ResourceName}",
                ResourceName = vm.ResourceName,
                ResourceType = vm.ResourceType,
                ResourceGroup = vm.ResourceGroup,
                Category = OptimizationCategory.AutoShutdown,
                Priority = OptimizationPriority.Medium,
                Title = $"Auto-shutdown for VM '{vm.ResourceName}'",
                Description = $"Implement automated shutdown schedule for non-production VM",
                Impact = $"Reduce runtime from 24/7 to {schedule.ActiveHoursPerWeek} hours/week",
                EstimatedMonthlySavings = savings,
                ImplementationComplexity = OptimizationComplexity.Simple,
                Actions = new List<OptimizationAction>
                {
                    new OptimizationAction
                    {
                        ActionType = "Configure Auto-Shutdown",
                        Description = $"Set shutdown at {schedule.ShutdownTime} {schedule.ShutdownTimeZone}",
                        Automated = true,
                        EstimatedDuration = "15 minutes",
                        Resources = new List<string> { vm.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Configure Auto-Start",
                        Description = $"Set startup at {schedule.StartupTime} {schedule.StartupTimeZone}",
                        Automated = true,
                        EstimatedDuration = "10 minutes",
                        Resources = new List<string> { vm.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Create Monitoring Alert",
                        Description = "Alert if VM running outside scheduled hours",
                        Automated = true,
                        EstimatedDuration = "5 minutes",
                        Resources = new List<string> { vm.ResourceName }
                    }
                },
                ScheduleDetails = schedule
            });
        }

        return Task.FromResult(recommendations);
    }

    private Task<List<CostOptimizationRecommendation>> AnalyzeDatabasesForAutoShutdownAsync(
        List<ResourceCostBreakdown> databases,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<CostOptimizationRecommendation>();

        foreach (var db in databases)
        {
            var isNonProduction = db.ResourceGroup.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
                                 db.ResourceGroup.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                 db.ResourceGroup.Contains("qa", StringComparison.OrdinalIgnoreCase);

            if (!isNonProduction) continue;

            var schedule = DetermineOptimalSchedule(db);
            var savings = CalculateDatabasePauseSavings(db, schedule);

            recommendations.Add(new CostOptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ResourceId = $"/subscriptions/{db.SubscriptionId}/resourceGroups/{db.ResourceGroup}/providers/Microsoft.Sql/servers/{db.ResourceName}",
                ResourceName = db.ResourceName,
                ResourceType = db.ResourceType,
                ResourceGroup = db.ResourceGroup,
                Category = OptimizationCategory.AutoShutdown,
                Priority = OptimizationPriority.High, // Databases can be expensive
                Title = $"Auto-pause for database '{db.ResourceName}'",
                Description = "Implement automated pause/resume schedule for non-production database",
                Impact = $"Pause database during off-hours ({168 - schedule.ActiveHoursPerWeek} hours/week)",
                EstimatedMonthlySavings = savings,
                ImplementationComplexity = OptimizationComplexity.Moderate,
                Actions = new List<OptimizationAction>
                {
                    new OptimizationAction
                    {
                        ActionType = "Create Logic App",
                        Description = "Deploy Logic App for database pause/resume automation",
                        Automated = true,
                        EstimatedDuration = "20 minutes",
                        Resources = new List<string> { db.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Configure Pause Schedule",
                        Description = $"Pause at {schedule.ShutdownTime} on {string.Join(", ", schedule.ShutdownDays)}",
                        Automated = true,
                        EstimatedDuration = "10 minutes",
                        Resources = new List<string> { db.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Configure Resume Schedule",
                        Description = $"Resume at {schedule.StartupTime} on {string.Join(", ", schedule.StartupDays)}",
                        Automated = true,
                        EstimatedDuration = "10 minutes",
                        Resources = new List<string> { db.ResourceName }
                    }
                },
                ScheduleDetails = schedule
            });
        }

        return Task.FromResult(recommendations);
    }

    private Task<List<CostOptimizationRecommendation>> AnalyzeAKSForAutoShutdownAsync(
        List<ResourceCostBreakdown> aksClusters,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<CostOptimizationRecommendation>();

        foreach (var aks in aksClusters)
        {
            var isNonProduction = aks.ResourceGroup.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
                                 aks.ResourceGroup.Contains("test", StringComparison.OrdinalIgnoreCase);

            if (!isNonProduction) continue;

            var schedule = DetermineOptimalSchedule(aks);
            var savings = CalculateAKSScalingSavings(aks, schedule);

            recommendations.Add(new CostOptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ResourceId = $"/subscriptions/{aks.SubscriptionId}/resourceGroups/{aks.ResourceGroup}/providers/Microsoft.ContainerService/managedClusters/{aks.ResourceName}",
                ResourceName = aks.ResourceName,
                ResourceType = aks.ResourceType,
                ResourceGroup = aks.ResourceGroup,
                Category = OptimizationCategory.AutoShutdown,
                Priority = OptimizationPriority.High,
                Title = $"Auto-scaling for AKS cluster '{aks.ResourceName}'",
                Description = "Scale down node pools during off-hours",
                Impact = $"Scale to minimum nodes during {168 - schedule.ActiveHoursPerWeek} hours/week",
                EstimatedMonthlySavings = savings,
                ImplementationComplexity = OptimizationComplexity.Moderate,
                Actions = new List<OptimizationAction>
                {
                    new OptimizationAction
                    {
                        ActionType = "Configure Auto-Scaler",
                        Description = "Set min nodes=1, max nodes=current during business hours",
                        Automated = true,
                        EstimatedDuration = "15 minutes",
                        Resources = new List<string> { aks.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Create Scaling Schedule",
                        Description = $"Scale down at {schedule.ShutdownTime}, scale up at {schedule.StartupTime}",
                        Automated = true,
                        EstimatedDuration = "20 minutes",
                        Resources = new List<string> { aks.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Configure Weekend Scaling",
                        Description = "Scale to 1 node on weekends",
                        Automated = true,
                        EstimatedDuration = "10 minutes",
                        Resources = new List<string> { aks.ResourceName }
                    }
                },
                ScheduleDetails = schedule
            });
        }

        return Task.FromResult(recommendations);
    }

    private Task<List<CostOptimizationRecommendation>> AnalyzeAppServicesForAutoShutdownAsync(
        List<ResourceCostBreakdown> appServices,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<CostOptimizationRecommendation>();

        foreach (var app in appServices)
        {
            var isNonProduction = app.ResourceGroup.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
                                 app.ResourceGroup.Contains("test", StringComparison.OrdinalIgnoreCase);

            if (!isNonProduction) continue;

            var schedule = DetermineOptimalSchedule(app);
            var savings = CalculateAppServiceScalingSavings(app, schedule);

            recommendations.Add(new CostOptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ResourceId = $"/subscriptions/{app.SubscriptionId}/resourceGroups/{app.ResourceGroup}/providers/Microsoft.Web/sites/{app.ResourceName}",
                ResourceName = app.ResourceName,
                ResourceType = app.ResourceType,
                ResourceGroup = app.ResourceGroup,
                Category = OptimizationCategory.AutoShutdown,
                Priority = OptimizationPriority.Medium,
                Title = $"Auto-scaling for App Service '{app.ResourceName}'",
                Description = "Scale down to F1/B1 tier during off-hours",
                Impact = $"Use lower tier during {168 - schedule.ActiveHoursPerWeek} hours/week",
                EstimatedMonthlySavings = savings,
                ImplementationComplexity = OptimizationComplexity.Simple,
                Actions = new List<OptimizationAction>
                {
                    new OptimizationAction
                    {
                        ActionType = "Configure Auto-Scale",
                        Description = "Create auto-scale rules for off-hours",
                        Automated = true,
                        EstimatedDuration = "15 minutes",
                        Resources = new List<string> { app.ResourceName }
                    },
                    new OptimizationAction
                    {
                        ActionType = "Create Scale Schedule",
                        Description = $"Scale down at {schedule.ShutdownTime}, scale up at {schedule.StartupTime}",
                        Automated = true,
                        EstimatedDuration = "10 minutes",
                        Resources = new List<string> { app.ResourceName }
                    }
                },
                ScheduleDetails = schedule
            });
        }

        return Task.FromResult(recommendations);
    }

    #endregion

    #region Automation Creation

    private Task<List<AutomationComponent>> CreateVMAutoShutdownAsync(
        AutoShutdownConfiguration config,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var components = new List<AutomationComponent>();

        // Component 1: Azure Automation Account
        components.Add(new AutomationComponent
        {
            Name = $"automation-{config.ResourceName}",
            Type = "Microsoft.Automation/automationAccounts",
            Purpose = "Host PowerShell runbooks for VM shutdown/startup",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = JsonSerializer.Serialize(new
            {
                sku = "Basic",
                location = "eastus2"
            }, new JsonSerializerOptions { WriteIndented = true })
        });

        // Component 2: Shutdown Runbook
        components.Add(new AutomationComponent
        {
            Name = $"shutdown-{config.ResourceName}",
            Type = "Automation Runbook",
            Purpose = "PowerShell script to stop VM",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateVMShutdownRunbook(config)
        });

        // Component 3: Startup Runbook
        components.Add(new AutomationComponent
        {
            Name = $"startup-{config.ResourceName}",
            Type = "Automation Runbook",
            Purpose = "PowerShell script to start VM",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateVMStartupRunbook(config)
        });

        // Component 4: Shutdown Schedule
        components.Add(new AutomationComponent
        {
            Name = $"schedule-shutdown-{config.ResourceName}",
            Type = "Automation Schedule",
            Purpose = $"Trigger shutdown at {config.Schedule.ShutdownTime}",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = JsonSerializer.Serialize(new
            {
                frequency = "Day",
                interval = 1,
                time = config.Schedule.ShutdownTime,
                timeZone = config.Schedule.ShutdownTimeZone,
                daysOfWeek = config.Schedule.ShutdownDays
            }, new JsonSerializerOptions { WriteIndented = true })
        });

        // Component 5: Startup Schedule
        components.Add(new AutomationComponent
        {
            Name = $"schedule-startup-{config.ResourceName}",
            Type = "Automation Schedule",
            Purpose = $"Trigger startup at {config.Schedule.StartupTime}",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = JsonSerializer.Serialize(new
            {
                frequency = "Day",
                interval = 1,
                time = config.Schedule.StartupTime,
                timeZone = config.Schedule.StartupTimeZone,
                daysOfWeek = config.Schedule.StartupDays
            }, new JsonSerializerOptions { WriteIndented = true })
        });

        return Task.FromResult(components);
    }

    private Task<List<AutomationComponent>> CreateSQLAutoShutdownAsync(
        AutoShutdownConfiguration config,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var components = new List<AutomationComponent>();

        // Component 1: Logic App for Database Pause
        components.Add(new AutomationComponent
        {
            Name = $"logicapp-pause-{config.ResourceName}",
            Type = "Microsoft.Logic/workflows",
            Purpose = "Logic App to pause SQL database",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateSQLPauseLogicApp(config)
        });

        // Component 2: Logic App for Database Resume
        components.Add(new AutomationComponent
        {
            Name = $"logicapp-resume-{config.ResourceName}",
            Type = "Microsoft.Logic/workflows",
            Purpose = "Logic App to resume SQL database",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateSQLResumeLogicApp(config)
        });

        // Component 3: Managed Identity for Logic Apps
        components.Add(new AutomationComponent
        {
            Name = $"identity-sqlautomation-{config.ResourceName}",
            Type = "Managed Identity",
            Purpose = "Allow Logic Apps to manage SQL database",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = "Assign SQL DB Contributor role on database"
        });

        return Task.FromResult(components);
    }

    private Task<List<AutomationComponent>> CreateAKSAutoShutdownAsync(
        AutoShutdownConfiguration config,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var components = new List<AutomationComponent>();

        // Component 1: Cluster Auto-Scaler Configuration
        components.Add(new AutomationComponent
        {
            Name = $"autoscaler-{config.ResourceName}",
            Type = "AKS Cluster Auto-Scaler",
            Purpose = "Configure auto-scaling for node pools",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateAKSAutoScalerConfig(config)
        });

        // Component 2: Keda Scaler (for workload-based scaling)
        components.Add(new AutomationComponent
        {
            Name = $"keda-scaler-{config.ResourceName}",
            Type = "KEDA ScaledObject",
            Purpose = "Scale pods based on schedule",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateKedaScheduleScaler(config)
        });

        return Task.FromResult(components);
    }

    private Task<List<AutomationComponent>> CreateAppServiceAutoShutdownAsync(
        AutoShutdownConfiguration config,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var components = new List<AutomationComponent>();

        // Component 1: Auto-Scale Rule
        components.Add(new AutomationComponent
        {
            Name = $"autoscale-{config.ResourceName}",
            Type = "Microsoft.Insights/autoscaleSettings",
            Purpose = "Auto-scale App Service Plan",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = GenerateAppServiceAutoScaleRules(config)
        });

        return Task.FromResult(components);
    }

    private Task<List<AutomationComponent>> CreateGenericAutoShutdownAsync(
        AutoShutdownConfiguration config,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var components = new List<AutomationComponent>();

        components.Add(new AutomationComponent
        {
            Name = $"generic-automation-{config.ResourceName}",
            Type = "Logic App",
            Purpose = "Generic resource start/stop automation",
            Status = dryRun ? "Dry-Run" : "Would Create",
            Configuration = "Azure Resource Manager API calls for start/stop operations"
        });

        return Task.FromResult(components);
    }

    #endregion

    #region Code Generation

    private string GenerateVMShutdownRunbook(AutoShutdownConfiguration config)
    {
        return $@"
# PowerShell Runbook: Shutdown VM
# Resource: {config.ResourceName}
# Schedule: {config.Schedule.ShutdownTime} {config.Schedule.ShutdownTimeZone}

Param(
    [string]$ResourceGroupName = ""{config.ResourceGroupName}"",
    [string]$VMName = ""{config.ResourceName}""
)

try {{
    # Authenticate using managed identity
    Connect-AzAccount -Identity

    # Get VM status
    $vm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $VMName -Status
    $vmStatus = $vm.Statuses | Where-Object {{ $_.Code -like 'PowerState/*' }}

    if ($vmStatus.Code -eq 'PowerState/running') {{
        Write-Output ""Stopping VM: $VMName""
        Stop-AzVM -ResourceGroupName $ResourceGroupName -Name $VMName -Force
        Write-Output ""VM stopped successfully""
    }} else {{
        Write-Output ""VM is already stopped""
    }}
}} catch {{
    Write-Error ""Failed to stop VM: $_""
    throw
}}
";
    }

    private string GenerateVMStartupRunbook(AutoShutdownConfiguration config)
    {
        return $@"
# PowerShell Runbook: Start VM
# Resource: {config.ResourceName}
# Schedule: {config.Schedule.StartupTime} {config.Schedule.StartupTimeZone}

Param(
    [string]$ResourceGroupName = ""{config.ResourceGroupName}"",
    [string]$VMName = ""{config.ResourceName}""
)

try {{
    # Authenticate using managed identity
    Connect-AzAccount -Identity

    # Get VM status
    $vm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $VMName -Status
    $vmStatus = $vm.Statuses | Where-Object {{ $_.Code -like 'PowerState/*' }}

    if ($vmStatus.Code -ne 'PowerState/running') {{
        Write-Output ""Starting VM: $VMName""
        Start-AzVM -ResourceGroupName $ResourceGroupName -Name $VMName
        Write-Output ""VM started successfully""
    }} else {{
        Write-Output ""VM is already running""
    }}
}} catch {{
    Write-Error ""Failed to start VM: $_""
    throw
}}
";
    }

    private string GenerateSQLPauseLogicApp(AutoShutdownConfiguration config)
    {
        return JsonSerializer.Serialize(new
        {
            definition = new
            {
                triggers = new
                {
                    Recurrence = new
                    {
                        type = "Recurrence",
                        recurrence = new
                        {
                            frequency = "Day",
                            interval = 1,
                            schedule = new
                            {
                                hours = new[] { config.Schedule.ShutdownTime.Split(':')[0] },
                                minutes = new[] { config.Schedule.ShutdownTime.Split(':')[1] }
                            }
                        }
                    }
                },
                actions = new
                {
                    Pause_Database = new
                    {
                        type = "Http",
                        inputs = new
                        {
                            method = "POST",
                            uri = $"https://management.azure.com/subscriptions/{config.SubscriptionId}/resourceGroups/{config.ResourceGroupName}/providers/Microsoft.Sql/servers/{{serverName}}/databases/{config.ResourceName}/pause?api-version=2021-02-01-preview",
                            authentication = new
                            {
                                type = "ManagedServiceIdentity"
                            }
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GenerateSQLResumeLogicApp(AutoShutdownConfiguration config)
    {
        return JsonSerializer.Serialize(new
        {
            definition = new
            {
                triggers = new
                {
                    Recurrence = new
                    {
                        type = "Recurrence",
                        recurrence = new
                        {
                            frequency = "Day",
                            interval = 1,
                            schedule = new
                            {
                                hours = new[] { config.Schedule.StartupTime.Split(':')[0] },
                                minutes = new[] { config.Schedule.StartupTime.Split(':')[1] }
                            }
                        }
                    }
                },
                actions = new
                {
                    Resume_Database = new
                    {
                        type = "Http",
                        inputs = new
                        {
                            method = "POST",
                            uri = $"https://management.azure.com/subscriptions/{config.SubscriptionId}/resourceGroups/{config.ResourceGroupName}/providers/Microsoft.Sql/servers/{{serverName}}/databases/{config.ResourceName}/resume?api-version=2021-02-01-preview",
                            authentication = new
                            {
                                type = "ManagedServiceIdentity"
                            }
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GenerateAKSAutoScalerConfig(AutoShutdownConfiguration config)
    {
        return JsonSerializer.Serialize(new
        {
            properties = new
            {
                autoScalerProfile = new
                {
                    scaleDownDelayAfterAdd = "15m",
                    scaleDownUnneededTime = "10m",
                    scaleDownUtilizationThreshold = "0.5",
                    minCount = 1,
                    maxCount = 10
                },
                agentPoolProfiles = new[]
                {
                    new
                    {
                        name = "nodepool1",
                        minCount = 1,
                        maxCount = 10,
                        enableAutoScaling = true
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GenerateKedaScheduleScaler(AutoShutdownConfiguration config)
    {
        return $@"
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: schedule-scaler-{config.ResourceName}
spec:
  scaleTargetRef:
    name: {config.ResourceName}
  minReplicaCount: 0
  maxReplicaCount: 10
  triggers:
  - type: cron
    metadata:
      timezone: {config.Schedule.ShutdownTimeZone}
      start: 0 {config.Schedule.StartupTime.Split(':')[1]} {config.Schedule.StartupTime.Split(':')[0]} * * 1-5  # Mon-Fri startup
      end: 0 {config.Schedule.ShutdownTime.Split(':')[1]} {config.Schedule.ShutdownTime.Split(':')[0]} * * 1-5    # Mon-Fri shutdown
      desiredReplicas: '3'
";
    }

    private string GenerateAppServiceAutoScaleRules(AutoShutdownConfiguration config)
    {
        return JsonSerializer.Serialize(new
        {
            properties = new
            {
                enabled = true,
                profiles = new object[]
                {
                    new
                    {
                        name = "Business Hours",
                        capacity = new { minimum = 2, maximum = 5, @default = 2 },
                        rules = new[]
                        {
                            new
                            {
                                metricTrigger = new
                                {
                                    metricName = "CpuPercentage",
                                    threshold = 70,
                                    @operator = "GreaterThan",
                                    timeAggregation = "Average"
                                },
                                scaleAction = new
                                {
                                    direction = "Increase",
                                    type = "ChangeCount",
                                    value = 1
                                }
                            }
                        },
                        recurrence = new
                        {
                            frequency = "Week",
                            schedule = new
                            {
                                timeZone = config.Schedule.StartupTimeZone,
                                days = config.Schedule.StartupDays,
                                hours = new[] { int.Parse(config.Schedule.StartupTime.Split(':')[0]) },
                                minutes = new[] { int.Parse(config.Schedule.StartupTime.Split(':')[1]) }
                            }
                        }
                    },
                    new
                    {
                        name = "Off Hours",
                        capacity = new { minimum = 1, maximum = 1, @default = 1 },
                        recurrence = new
                        {
                            frequency = "Week",
                            schedule = new
                            {
                                timeZone = config.Schedule.ShutdownTimeZone,
                                days = config.Schedule.ShutdownDays,
                                hours = new[] { int.Parse(config.Schedule.ShutdownTime.Split(':')[0]) },
                                minutes = new[] { int.Parse(config.Schedule.ShutdownTime.Split(':')[1]) }
                            }
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion

    #region Helper Methods

    private ShutdownSchedule DetermineOptimalSchedule(ResourceCostBreakdown resource)
    {
        // Default schedule: weekdays 8 AM - 6 PM (business hours)
        return new ShutdownSchedule
        {
            StartupTime = "08:00",
            ShutdownTime = "18:00",
            StartupTimeZone = "Eastern Standard Time",
            ShutdownTimeZone = "Eastern Standard Time",
            StartupDays = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
            ShutdownDays = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
            ActiveHoursPerWeek = 50, // 10 hours/day * 5 days
            EnableWeekendShutdown = true
        };
    }

    private decimal CalculateVMShutdownSavings(ResourceCostBreakdown vm, ShutdownSchedule schedule)
    {
        // VM cost = monthly cost
        // Savings = (168 - active hours) / 168 * monthly cost
        var shutdownHours = 168 - schedule.ActiveHoursPerWeek;
        var savingsPercentage = shutdownHours / 168m;
        return vm.MonthlyCost * savingsPercentage;
    }

    private decimal CalculateDatabasePauseSavings(ResourceCostBreakdown db, ShutdownSchedule schedule)
    {
        // Databases typically save ~90% when paused (pay only for storage)
        var shutdownHours = 168 - schedule.ActiveHoursPerWeek;
        var savingsPercentage = (shutdownHours / 168m) * 0.9m; // 90% savings when paused
        return db.MonthlyCost * savingsPercentage;
    }

    private decimal CalculateAKSScalingSavings(ResourceCostBreakdown aks, ShutdownSchedule schedule)
    {
        // AKS: assume scaling from 3 nodes to 1 node (66% savings)
        var shutdownHours = 168 - schedule.ActiveHoursPerWeek;
        var savingsPercentage = (shutdownHours / 168m) * 0.66m;
        return aks.MonthlyCost * savingsPercentage;
    }

    private decimal CalculateAppServiceScalingSavings(ResourceCostBreakdown app, ShutdownSchedule schedule)
    {
        // App Service: assume scaling from S2 to B1 (70% savings)
        var shutdownHours = 168 - schedule.ActiveHoursPerWeek;
        var savingsPercentage = (shutdownHours / 168m) * 0.70m;
        return app.MonthlyCost * savingsPercentage;
    }

    private decimal CalculateEstimatedSavings(string resourceType, ShutdownSchedule schedule)
    {
        // Generic savings calculation
        var shutdownHours = 168 - schedule.ActiveHoursPerWeek;
        var savingsPercentage = shutdownHours / 168m;

        if (resourceType.Contains("sql", StringComparison.OrdinalIgnoreCase))
            savingsPercentage *= 0.9m;
        else if (resourceType.Contains("managedClusters", StringComparison.OrdinalIgnoreCase))
            savingsPercentage *= 0.66m;

        return savingsPercentage * 100; // Return as percentage
    }

    private string GenerateAutomationReport(AutoShutdownConfiguration config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Auto-Shutdown Configuration: {config.ResourceName}");
        sb.AppendLine($"Resource Type: {config.ResourceType}");
        sb.AppendLine($"Automation Method: {config.AutomationMethod}");
        sb.AppendLine($"Status: {config.Status}");
        sb.AppendLine($"");
        sb.AppendLine($"Schedule:");
        sb.AppendLine($"  Startup: {config.Schedule.StartupTime} {config.Schedule.StartupTimeZone}");
        sb.AppendLine($"  Shutdown: {config.Schedule.ShutdownTime} {config.Schedule.ShutdownTimeZone}");
        sb.AppendLine($"  Active Days: {string.Join(", ", config.Schedule.StartupDays)}");
        sb.AppendLine($"  Active Hours/Week: {config.Schedule.ActiveHoursPerWeek}");
        sb.AppendLine($"");
        sb.AppendLine($"Components Created: {config.Components.Count}");
        foreach (var component in config.Components)
        {
            sb.AppendLine($"  - {component.Name} ({component.Type}): {component.Status}");
        }
        sb.AppendLine($"");
        sb.AppendLine($"Estimated Monthly Savings: {config.EstimatedMonthlySavings:P0}");

        return sb.ToString();
    }

    #endregion
}

#region Model Classes

public class AutoShutdownConfiguration
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public ShutdownSchedule Schedule { get; set; } = new();
    public string AutomationMethod { get; set; } = string.Empty;
    public List<AutomationComponent> Components { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public decimal EstimatedMonthlySavings { get; set; }
}

public class AutomationComponent
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Configuration { get; set; } = string.Empty;
}

public class ShutdownSchedule
{
    public string StartupTime { get; set; } = "08:00";
    public string ShutdownTime { get; set; } = "18:00";
    public string StartupTimeZone { get; set; } = "Eastern Standard Time";
    public string ShutdownTimeZone { get; set; } = "Eastern Standard Time";
    public List<string> StartupDays { get; set; } = new();
    public List<string> ShutdownDays { get; set; } = new();
    public int ActiveHoursPerWeek { get; set; } = 50;
    public bool EnableWeekendShutdown { get; set; } = true;
}

#endregion
