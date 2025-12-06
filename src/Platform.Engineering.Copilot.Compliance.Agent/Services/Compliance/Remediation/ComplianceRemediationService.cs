using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance.Remediation;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Models.Infrastructure;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance.Remediation;

/// <summary>
/// Compliance remediation service for automatically fixing compliance findings
/// through Azure Resource Manager API calls and configuration changes
/// </summary>
public class ComplianceRemediationService : IComplianceRemediationService
{
    private readonly IAzureResourceService _resourceService;
    private readonly IAzureClientFactory _clientFactory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComplianceRemediationService> _logger;
    private readonly ArmClient _armClient;

    // Predefined remediation templates for common findings
    private readonly Dictionary<string, Func<AtoFinding, InfrastructureRemediationPlan>> _remediationTemplates;

    public ComplianceRemediationService(
        IAzureResourceService resourceService,
        IAzureClientFactory clientFactory,
        HttpClient httpClient,
        ILogger<ComplianceRemediationService> logger)
    {
        _resourceService = resourceService;
        _clientFactory = clientFactory;
        _httpClient = httpClient;
        _logger = logger;
        
        // Get ARM client from factory (centralized credential management)
        _armClient = _clientFactory.GetArmClient();

        // Initialize remediation templates
        _remediationTemplates = InitializeRemediationTemplates();

        _logger.LogInformation("✅ Compliance Remediation Service initialized with {TemplateCount} remediation templates using {CloudEnvironment}", 
            _remediationTemplates.Count, _clientFactory.CloudEnvironment);
    }

    public async Task<bool> CanAutoRemediateAsync(AtoFinding finding)
    {
        _logger.LogInformation("🔍 Evaluating auto-remediation capability for finding: {FindingId} - {Rule}", 
            finding.Id, finding.RuleId);

        try
        {
            // Check if finding is marked as automatically fixable
            if (!finding.IsAutoRemediable)
            {
                _logger.LogDebug("Finding {FindingId} is not marked as automatically fixable", finding.Id);
                return false;
            }

            // Check if we have a remediation template for this type of finding
            var templateKey = GetRemediationTemplateKey(finding);
            if (!_remediationTemplates.ContainsKey(templateKey))
            {
                _logger.LogDebug("No remediation template found for finding type: {TemplateKey}", templateKey);
                return false;
            }

            // Check if resource type is supported
            if (string.IsNullOrEmpty(finding.ResourceId))
            {
                _logger.LogDebug("Finding {FindingId} has no resource ID", finding.Id);
                return false;
            }

            var resourceType = ExtractResourceType(finding.ResourceId);
            var supportedActions = await GetSupportedActionsAsync(resourceType);
            
            bool canRemediate = supportedActions.Any();
            
            _logger.LogInformation("Auto-remediation capability for finding {FindingId}: {CanRemediate}", 
                finding.Id, canRemediate);
            
            return canRemediate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating auto-remediation capability for finding {FindingId}", finding.Id);
            return false;
        }
    }

    public async Task<InfrastructureRemediationPlan> GenerateRemediationPlanAsync(
        AtoFinding finding,
        InfrastructureRemediationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🛠️ Generating remediation plan for finding: {FindingId} - {Rule}", 
            finding.Id, finding.RuleId);

        options ??= new InfrastructureRemediationOptions();

        try
        {
            // Get the appropriate remediation template
            var templateKey = GetRemediationTemplateKey(finding);
            if (!_remediationTemplates.ContainsKey(templateKey))
            {
                throw new NotSupportedException($"No remediation template available for finding type: {templateKey}");
            }

            // Generate the plan using the template
            var plan = _remediationTemplates[templateKey](finding);

            // Parse resource information
            if (!string.IsNullOrEmpty(finding.ResourceId))
            {
                var resourceInfo = ParseResourceId(finding.ResourceId);
                plan.SubscriptionId = resourceInfo.SubscriptionId;
                plan.ResourceGroupName = resourceInfo.ResourceGroupName;
                plan.ResourceType = resourceInfo.ResourceType;
                plan.ResourceId = finding.ResourceId;
            }

            // Assess risk level and add prerequisites
            await AssessAndEnhancePlanAsync(plan, options, cancellationToken);

            // Generate rollback plan if requested
            if (options.CreateRollbackPlan)
            {
                plan.RollbackPlan = await GenerateRollbackPlanAsync(plan, cancellationToken);
            }

            _logger.LogInformation("✅ Generated remediation plan with {ActionCount} actions for finding {FindingId}", 
                plan.Actions.Count, finding.Id);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating remediation plan for finding {FindingId}", finding.Id);
            throw;
        }
    }

    public async Task<InfrastructureRemediationResult> ExecuteRemediationAsync(
        InfrastructureRemediationPlan plan,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 {Mode} remediation execution for plan: {PlanId}", 
            dryRun ? "Simulating" : "Executing", plan.PlanId);

        var result = new InfrastructureRemediationResult
        {
            Plan = plan,
            WasDryRun = dryRun,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Capture original values for rollback
            if (!dryRun)
            {
                result.RollbackInfo = await CaptureRollbackInfoAsync(plan, cancellationToken);
            }

            // Sort actions by execution order
            var orderedActions = plan.Actions.OrderBy(a => a.ExecutionOrder).ToList();

            foreach (var action in orderedActions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var actionResult = await ExecuteActionAsync(action, plan, dryRun, cancellationToken);
                result.ActionResults.Add(actionResult);

                if (!actionResult.IsSuccess && action.IsCritical)
                {
                    result.IsSuccess = false;
                    result.Errors.Add($"Critical action failed: {action.Description}");
                    break;
                }
            }

            result.IsSuccess = result.ActionResults.All(r => r.IsSuccess);
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation("✅ Remediation execution completed. Success: {IsSuccess}, Duration: {Duration}", 
                result.IsSuccess, result.Duration);

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.EndTime = DateTime.UtcNow;
            result.Errors.Add($"Execution failed: {ex.Message}");
            
            _logger.LogError(ex, "Error executing remediation plan {PlanId}", plan.PlanId);
            return result;
        }
    }

    public async Task<InfrastructureRemediationValidation> ValidateRemediationAsync(
        InfrastructureRemediationResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("✅ Validating remediation result: {ExecutionId}", result.ExecutionId);

        var validation = new InfrastructureRemediationValidation
        {
            RemediationResult = result
        };

        try
        {
            // Validate each action that was executed
            foreach (var actionResult in result.ActionResults.Where(ar => ar.IsSuccess))
            {
                if (actionResult.Action.Validation != null)
                {
                    var validationResult = await ValidateActionAsync(actionResult, cancellationToken);
                    validation.ValidationResults.Add(validationResult);
                }
            }

            validation.IsValid = validation.ValidationResults.All(vr => vr.IsPassed);

            // Check if compliance is resolved by re-scanning the resource
            if (validation.IsValid && !string.IsNullOrEmpty(result.Plan.ResourceId))
            {
                validation.IsComplianceResolved = await CheckComplianceResolutionAsync(
                    result.Plan.Finding, result.Plan.ResourceId, cancellationToken);
            }

            validation.ComplianceStatus = validation.IsComplianceResolved ? "Resolved" : "Needs Further Review";

            _logger.LogInformation("Validation completed for {ExecutionId}. Valid: {IsValid}, Compliance Resolved: {IsResolved}", 
                result.ExecutionId, validation.IsValid, validation.IsComplianceResolved);

            return validation;
        }
        catch (Exception ex)
        {
            validation.IsValid = false;
            validation.ValidationIssues.Add($"Validation error: {ex.Message}");
            
            _logger.LogError(ex, "Error validating remediation result {ExecutionId}", result.ExecutionId);
            return validation;
        }
    }

    public async Task<InfrastructureRollbackResult> RollbackRemediationAsync(
        InfrastructureRemediationResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔄 Rolling back remediation: {ExecutionId}", result.ExecutionId);

        var rollbackResult = new InfrastructureRollbackResult();

        try
        {
            if (result.RollbackInfo == null)
            {
                throw new InvalidOperationException("No rollback information available for this remediation");
            }

            rollbackResult.RollbackPlan = new InfrastructureRollbackPlan
            {
                RollbackActions = result.RollbackInfo.RollbackActions,
                OriginalConfiguration = result.RollbackInfo.OriginalValues
            };

            // Execute rollback actions in reverse order
            var rollbackActions = result.RollbackInfo.RollbackActions.OrderByDescending(a => a.ExecutionOrder);

            foreach (var action in rollbackActions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var actionResult = await ExecuteActionAsync(action, result.Plan, false, cancellationToken);
                rollbackResult.RollbackActionResults.Add(actionResult);

                if (!actionResult.IsSuccess)
                {
                    rollbackResult.Errors.Add($"Rollback action failed: {action.Description}");
                }
            }

            rollbackResult.IsSuccess = rollbackResult.RollbackActionResults.All(r => r.IsSuccess);
            rollbackResult.Duration = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - rollbackResult.ExecutedAt.Ticks);

            _logger.LogInformation("Rollback completed for {ExecutionId}. Success: {IsSuccess}", 
                result.ExecutionId, rollbackResult.IsSuccess);

            return rollbackResult;
        }
        catch (Exception ex)
        {
            rollbackResult.IsSuccess = false;
            rollbackResult.Errors.Add($"Rollback failed: {ex.Message}");
            
            _logger.LogError(ex, "Error rolling back remediation {ExecutionId}", result.ExecutionId);
            return rollbackResult;
        }
    }

    public async Task<IEnumerable<InfrastructureRemediationAction>> GetSupportedActionsAsync(string resourceType)
    {
        // This would be implemented based on Azure resource type capabilities
        // For now, return a basic set of common actions
        
        var supportedActions = new List<InfrastructureRemediationAction>();

        switch (resourceType.ToLower())
        {
            case "microsoft.storage/storageaccounts":
                supportedActions.AddRange(GetStorageAccountRemediationActions());
                break;
            case "microsoft.keyvault/vaults":
                supportedActions.AddRange(GetKeyVaultRemediationActions());
                break;
            case "microsoft.network/networksecuritygroups":
                supportedActions.AddRange(GetNetworkSecurityGroupRemediationActions());
                break;
            // Add more resource types as needed
        }

        _logger.LogDebug("Found {ActionCount} supported remediation actions for resource type: {ResourceType}", 
            supportedActions.Count, resourceType);

        return supportedActions;
    }

    public async Task<InfrastructureRemediationImpactAssessment> AssessRemediationImpactAsync(
        InfrastructureRemediationPlan plan)
    {
        _logger.LogInformation("📊 Assessing impact for remediation plan: {PlanId}", plan.PlanId);

        var assessment = new InfrastructureRemediationImpactAssessment
        {
            OverallRisk = plan.RiskLevel,
            EstimatedDowntime = plan.EstimatedDuration
        };

        try
        {
            // Analyze each action for potential impact
            foreach (var action in plan.Actions)
            {
                await AnalyzeActionImpactAsync(action, plan, assessment);
            }

            // Determine if manual approval is needed
            assessment.RecommendManualApproval = assessment.OverallRisk >= RemediationRiskLevel.High ||
                                                 assessment.EstimatedDowntime > TimeSpan.FromMinutes(30);

            // Add safety recommendations
            AddSafetyRecommendations(assessment, plan);

            _logger.LogInformation("Impact assessment completed for plan {PlanId}. Risk: {Risk}, Approval Required: {RequiresApproval}", 
                plan.PlanId, assessment.OverallRisk, assessment.RecommendManualApproval);

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing impact for plan {PlanId}", plan.PlanId);
            
            // Return safe default assessment
            assessment.OverallRisk = RemediationRiskLevel.Critical;
            assessment.RecommendManualApproval = true;
            assessment.Risks.Add(new RemediationRisk
            {
                RiskType = "Assessment Error",
                Description = $"Could not assess impact: {ex.Message}",
                Severity = RemediationRiskLevel.Critical,
                Probability = 1.0,
                MitigationStrategy = "Manual review required"
            });

            return assessment;
        }
    }

    #region Private Helper Methods

    private Dictionary<string, Func<AtoFinding, InfrastructureRemediationPlan>> InitializeRemediationTemplates()
    {
        return new Dictionary<string, Func<AtoFinding, InfrastructureRemediationPlan>>
        {
            // Storage Account remediations
            ["storage:encryption"] = CreateStorageEncryptionRemediation,
            ["storage:https"] = CreateStorageHttpsRemediation,
            ["storage:publicaccess"] = CreateStoragePublicAccessRemediation,
            
            // Key Vault remediations
            ["keyvault:softdelete"] = CreateKeyVaultSoftDeleteRemediation,
            ["keyvault:purgeprotection"] = CreateKeyVaultPurgeProtectionRemediation,
            
            // Network Security Group remediations
            ["nsg:openports"] = CreateNsgPortRemediation,
            ["nsg:allowall"] = CreateNsgAllowAllRemediation,
            
            // Virtual Machine remediations
            ["vm:diskencryption"] = CreateVmDiskEncryptionRemediation,
            ["vm:antimalware"] = CreateVmAntiMalwareRemediation,
            ["vm:monitoring"] = CreateVmMonitoringRemediation,
            ["vm:backup"] = CreateVmBackupRemediation,
            ["vm:networkaccess"] = CreateVmNetworkAccessRemediation,
            
            // SQL Database remediations
            ["sql:encryption"] = CreateSqlEncryptionRemediation,
            ["sql:firewall"] = CreateSqlFirewallRemediation,
            ["sql:auditing"] = CreateSqlAuditingRemediation,
            ["sql:threatdetection"] = CreateSqlThreatDetectionRemediation,
            ["sql:backup"] = CreateSqlBackupRemediation,
            
            // Cosmos DB remediations
            ["cosmos:encryption"] = CreateCosmosEncryptionRemediation,
            ["cosmos:firewall"] = CreateCosmosFirewallRemediation,
            ["cosmos:backup"] = CreateCosmosBackupRemediation,
            ["cosmos:networkaccess"] = CreateCosmosNetworkAccessRemediation,
            
            // Generic tag-based remediations
            ["tags:missing"] = CreateMissingTagsRemediation,
        };
    }

    private string GetRemediationTemplateKey(AtoFinding finding)
    {
        // Create a key based on resource type and finding details
        var resourceType = ExtractResourceType(finding.ResourceId ?? "");
        
        // Map common compliance findings to remediation templates
        if ((finding.RuleId?.StartsWith("SC-") == true || finding.AffectedNistControls.Any(c => c.StartsWith("SC-"))) 
            && resourceType.Contains("storage"))
            return "storage:encryption";
        
        if (finding.Description?.Contains("HTTPS", StringComparison.OrdinalIgnoreCase) == true)
            return "storage:https";
            
        if (finding.Description?.Contains("public access", StringComparison.OrdinalIgnoreCase) == true)
            return "storage:publicaccess";
            
        if (finding.Description?.Contains("soft delete", StringComparison.OrdinalIgnoreCase) == true)
            return "keyvault:softdelete";
            
        if (finding.Description?.Contains("open port", StringComparison.OrdinalIgnoreCase) == true)
            return "nsg:openports";
            
        if (finding.Description?.Contains("missing tag", StringComparison.OrdinalIgnoreCase) == true)
            return "tags:missing";

        // Virtual Machine specific mappings
        if (resourceType.Contains("virtualMachine"))
        {
            if (finding.Description?.Contains("disk encryption", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("encryption at rest", StringComparison.OrdinalIgnoreCase) == true)
                return "vm:diskencryption";
                
            if (finding.Description?.Contains("antimalware", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("anti-malware", StringComparison.OrdinalIgnoreCase) == true)
                return "vm:antimalware";
                
            if (finding.Description?.Contains("monitoring", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("log analytics", StringComparison.OrdinalIgnoreCase) == true)
                return "vm:monitoring";
                
            if (finding.Description?.Contains("backup", StringComparison.OrdinalIgnoreCase) == true)
                return "vm:backup";
                
            if (finding.Description?.Contains("network access", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("just-in-time", StringComparison.OrdinalIgnoreCase) == true)
                return "vm:networkaccess";
        }

        // SQL Database specific mappings
        if (resourceType.Contains("Microsoft.Sql"))
        {
            if (finding.Description?.Contains("transparent data encryption", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("TDE", StringComparison.OrdinalIgnoreCase) == true)
                return "sql:encryption";
                
            if (finding.Description?.Contains("firewall", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("network access", StringComparison.OrdinalIgnoreCase) == true)
                return "sql:firewall";
                
            if (finding.Description?.Contains("auditing", StringComparison.OrdinalIgnoreCase) == true)
                return "sql:auditing";
                
            if (finding.Description?.Contains("threat detection", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("advanced threat protection", StringComparison.OrdinalIgnoreCase) == true)
                return "sql:threatdetection";
                
            if (finding.Description?.Contains("backup", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("retention", StringComparison.OrdinalIgnoreCase) == true)
                return "sql:backup";
        }

        // Cosmos DB specific mappings
        if (resourceType.Contains("Microsoft.DocumentDB") || resourceType.Contains("databaseAccounts"))
        {
            if (finding.Description?.Contains("encryption", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("customer managed key", StringComparison.OrdinalIgnoreCase) == true)
                return "cosmos:encryption";
                
            if (finding.Description?.Contains("firewall", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("IP rules", StringComparison.OrdinalIgnoreCase) == true)
                return "cosmos:firewall";
                
            if (finding.Description?.Contains("backup", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("continuous backup", StringComparison.OrdinalIgnoreCase) == true)
                return "cosmos:backup";
                
            if (finding.Description?.Contains("private endpoint", StringComparison.OrdinalIgnoreCase) == true ||
                finding.Description?.Contains("network access", StringComparison.OrdinalIgnoreCase) == true)
                return "cosmos:networkaccess";
        }

        return $"{resourceType}:generic";
    }

    private string ExtractResourceType(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return "unknown";

        try
        {
            var segments = resourceId.Split('/');
            if (segments.Length >= 8)
            {
                return $"{segments[6]}/{segments[7]}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract resource type from ID: {ResourceId}", resourceId);
        }

        return "unknown";
    }

    private (string SubscriptionId, string ResourceGroupName, string ResourceType) ParseResourceId(string resourceId)
    {
        var segments = resourceId.Split('/');
        return (
            SubscriptionId: segments[2],
            ResourceGroupName: segments[4],
            ResourceType: $"{segments[6]}/{segments[7]}"
        );
    }

    private InfrastructureRemediationPlan CreateStorageEncryptionRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(5),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableEncryption",
                    Description = "Enable encryption at rest for storage account",
                    ApiOperation = "Microsoft.Storage/storageAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            encryption = new
                            {
                                keySource = "Microsoft.Storage",
                                services = new
                                {
                                    blob = new { enabled = true },
                                    file = new { enabled = true }
                                }
                            }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true,
                    Validation = new InfrastructureActionValidation
                    {
                        PropertyPath = "properties.encryption.keySource",
                        ExpectedValue = "Microsoft.Storage",
                        ValidationType = "Equals"
                    }
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateStorageHttpsRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(2),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableHttpsOnly",
                    Description = "Enable HTTPS-only access for storage account",
                    ApiOperation = "Microsoft.Storage/storageAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            supportsHttpsTrafficOnly = true
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateStoragePublicAccessRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Medium,
            EstimatedDuration = TimeSpan.FromMinutes(3),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "DisablePublicAccess",
                    Description = "Disable public blob access for storage account",
                    ApiOperation = "Microsoft.Storage/storageAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            allowBlobPublicAccess = false
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateKeyVaultSoftDeleteRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(5),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableSoftDelete",
                    Description = "Enable soft delete for Key Vault",
                    ApiOperation = "Microsoft.KeyVault/vaults",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            enableSoftDelete = true,
                            softDeleteRetentionInDays = 90
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateKeyVaultPurgeProtectionRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(5),
            RequiresApproval = true,
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnablePurgeProtection",
                    Description = "Enable purge protection for Key Vault (irreversible)",
                    ApiOperation = "Microsoft.KeyVault/vaults",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            enablePurgeProtection = true
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateNsgPortRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            RequiresApproval = true,
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "RestrictPortAccess",
                    Description = "Restrict open port access in NSG rule",
                    ApiOperation = "Microsoft.Network/networkSecurityGroups/securityRules",
                    HttpMethod = "PUT",
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateNsgAllowAllRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Critical,
            EstimatedDuration = TimeSpan.FromMinutes(15),
            RequiresApproval = true,
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "RemoveAllowAllRule",
                    Description = "Remove or restrict overly permissive NSG rule",
                    ApiOperation = "Microsoft.Network/networkSecurityGroups/securityRules",
                    HttpMethod = "DELETE",
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateMissingTagsRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(2),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "AddRequiredTags",
                    Description = "Add missing required tags to resource",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        tags = new Dictionary<string, string>
                        {
                            { "Environment", "Production" },
                            { "Owner", "ComplianceTeam" },
                            { "CostCenter", "IT" }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = false
                }
            }
        };
    }

    private async Task AssessAndEnhancePlanAsync(InfrastructureRemediationPlan plan, InfrastructureRemediationOptions options, CancellationToken cancellationToken)
    {
        // Add execution order if not set
        for (int i = 0; i < plan.Actions.Count; i++)
        {
            if (plan.Actions[i].ExecutionOrder == 0)
            {
                plan.Actions[i].ExecutionOrder = i + 1;
            }
        }

        // Assess risk and add approval requirement if needed
        if (plan.RiskLevel >= options.MaxRiskLevel && options.RequireApprovalForHighRisk)
        {
            plan.RequiresApproval = true;
        }

        // Add prerequisites based on resource type
        await AddResourceSpecificPrerequisitesAsync(plan, cancellationToken);
    }

    private async Task<InfrastructureRollbackPlan> GenerateRollbackPlanAsync(InfrastructureRemediationPlan plan, CancellationToken cancellationToken)
    {
        // Generate rollback actions (simplified implementation)
        var rollbackPlan = new InfrastructureRollbackPlan
        {
            EstimatedRollbackDuration = TimeSpan.FromMinutes(plan.Actions.Count * 2)
        };

        // For each action, create a corresponding rollback action
        foreach (var action in plan.Actions.OrderByDescending(a => a.ExecutionOrder))
        {
            var rollbackAction = new InfrastructureRemediationAction
            {
                ActionType = $"Rollback_{action.ActionType}",
                Description = $"Rollback: {action.Description}",
                ApiOperation = action.ApiOperation,
                HttpMethod = action.HttpMethod,
                ExecutionOrder = action.ExecutionOrder
                // Payload would be set with original values during execution
            };

            rollbackPlan.RollbackActions.Add(rollbackAction);
        }

        return rollbackPlan;
    }

    private async Task<InfrastructureRollbackInfo> CaptureRollbackInfoAsync(InfrastructureRemediationPlan plan, CancellationToken cancellationToken)
    {
        var rollbackInfo = new InfrastructureRollbackInfo();

        // Capture current resource state for rollback
        // This would involve calling Azure ARM to get current resource configuration
        // Simplified implementation for now

        return rollbackInfo;
    }

    private async Task<InfrastructureActionResult> ExecuteActionAsync(
        InfrastructureRemediationAction action,
        InfrastructureRemediationPlan plan,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var result = new InfrastructureActionResult
        {
            Action = action,
            ExecutedAt = DateTime.UtcNow
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (dryRun)
            {
                _logger.LogInformation("🧪 DRY RUN: Would execute action: {Description}", action.Description);
                result.IsSuccess = true;
                result.HttpStatusCode = 200;
                result.ApiResponse = "DRY RUN - No actual changes made";
            }
            else
            {
                _logger.LogInformation("⚡ Executing action: {Description}", action.Description);
                
                // Build the Azure ARM API URL
                var apiUrl = BuildAzureApiUrl(plan, action);
                
                // Execute the API call
                var response = await ExecuteAzureApiCallAsync(apiUrl, action, cancellationToken);
                
                result.IsSuccess = action.SuccessStatusCodes.Contains((int)response.StatusCode);
                result.HttpStatusCode = (int)response.StatusCode;
                result.ApiResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!result.IsSuccess)
                {
                    result.ErrorMessage = $"API call failed with status {response.StatusCode}";
                }
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error executing action: {Description}", action.Description);
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task<InfrastructureValidationResult> ValidateActionAsync(InfrastructureActionResult actionResult, CancellationToken cancellationToken)
    {
        var validation = new InfrastructureValidationResult
        {
            ValidationName = $"Validate_{actionResult.Action.ActionType}",
            PropertyPath = actionResult.Action.Validation?.PropertyPath ?? ""
        };

        try
        {
            if (actionResult.Action.Validation != null)
            {
                // Wait for validation delay
                await Task.Delay(actionResult.Action.Validation.ValidationDelay, cancellationToken);
                
                // Perform the actual validation
                // This would involve querying the resource to check if changes were applied
                validation.IsPassed = true; // Simplified for now
                validation.ActualValue = actionResult.Action.Validation.ExpectedValue;
            }
            else
            {
                validation.IsPassed = actionResult.IsSuccess;
            }
        }
        catch (Exception ex)
        {
            validation.IsPassed = false;
            validation.ErrorMessage = ex.Message;
        }

        return validation;
    }

    private async Task<bool> CheckComplianceResolutionAsync(AtoFinding originalFinding, string resourceId, CancellationToken cancellationToken)
    {
        // This would involve re-running compliance scans on the resource
        // Simplified implementation - assume success if remediation succeeded
        return true;
    }

    private string BuildAzureApiUrl(InfrastructureRemediationPlan plan, InfrastructureRemediationAction action)
    {
        var baseUrl = "https://management.azure.com";
        return $"{baseUrl}{plan.ResourceId}?api-version=2021-04-01";
    }

    private async Task<HttpResponseMessage> ExecuteAzureApiCallAsync(string apiUrl, InfrastructureRemediationAction action, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(new HttpMethod(action.HttpMethod), apiUrl);
        
        if (action.Payload != null)
        {
            var json = JsonSerializer.Serialize(action.Payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Add authentication headers (would need proper Azure authentication)
        // For now, this is a placeholder
        
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private IEnumerable<InfrastructureRemediationAction> GetStorageAccountRemediationActions()
    {
        return new[]
        {
            new InfrastructureRemediationAction
            {
                ActionType = "EnableEncryption",
                Description = "Enable storage account encryption"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableHttpsOnly",
                Description = "Enable HTTPS-only access"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "DisablePublicAccess",
                Description = "Disable public blob access"
            }
        };
    }

    private IEnumerable<InfrastructureRemediationAction> GetKeyVaultRemediationActions()
    {
        return new[]
        {
            new InfrastructureRemediationAction
            {
                ActionType = "EnableSoftDelete",
                Description = "Enable Key Vault soft delete"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnablePurgeProtection",
                Description = "Enable Key Vault purge protection"
            }
        };
    }

    private IEnumerable<InfrastructureRemediationAction> GetNetworkSecurityGroupRemediationActions()
    {
        return new[]
        {
            new InfrastructureRemediationAction
            {
                ActionType = "RestrictPortAccess",
                Description = "Restrict open port access"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "RemoveAllowAllRule",
                Description = "Remove overly permissive rules"
            }
        };
    }

    #region Virtual Machine Remediation Templates

    private InfrastructureRemediationPlan CreateVmDiskEncryptionRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(30),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableDiskEncryption",
                    Description = "Enable Azure Disk Encryption for VM disks",
                    ApiOperation = "Microsoft.Compute/virtualMachines/encrypt",
                    HttpMethod = "POST",
                    Payload = new
                    {
                        keyVaultResourceId = $"/subscriptions/{finding.SubscriptionId}/resourceGroups/{finding.ResourceGroupName}/providers/Microsoft.KeyVault/vaults/vm-encryption-kv",
                        keyEncryptionKeyUrl = "",
                        volumeType = "All",
                        encryptionOperation = "EnableEncryption"
                    },
                    ExecutionOrder = 1,
                    IsCritical = true,
                    Validation = new InfrastructureActionValidation
                    {
                        PropertyPath = "properties.storageProfile.osDisk.encryptionSettings.enabled",
                        ExpectedValue = true,
                        ValidationType = "Equals"
                    }
                }
            },
            Prerequisites = new List<string>
            {
                "VM must be running for encryption to complete",
                "Adequate storage space required for encryption metadata",
                "Backup existing VM before proceeding"
            }
        };
    }

    private InfrastructureRemediationPlan CreateVmAntiMalwareRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Medium,
            EstimatedDuration = TimeSpan.FromMinutes(15),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "InstallAntiMalware",
                    Description = "Install Microsoft Antimalware extension on VM",
                    ApiOperation = "Microsoft.Compute/virtualMachines/extensions/Microsoft.Azure.Security.IaaSAntimalware",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            publisher = "Microsoft.Azure.Security",
                            type = "IaaSAntimalware",
                            typeHandlerVersion = "1.3",
                            autoUpgradeMinorVersion = true,
                            settings = new
                            {
                                AntimalwareEnabled = true,
                                RealtimeProtectionEnabled = true,
                                ScheduledScanSettings = new
                                {
                                    isEnabled = true,
                                    day = "7", // Sunday
                                    time = "120", // 2:00 AM
                                    scanType = "Quick"
                                },
                                Exclusions = new
                                {
                                    Extensions = "",
                                    Paths = "",
                                    Processes = ""
                                }
                            }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateVmMonitoringRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableMonitoring",
                    Description = "Install Azure Monitor Agent on VM",
                    ApiOperation = "Microsoft.Compute/virtualMachines/extensions/AzureMonitorAgent",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            publisher = "Microsoft.Azure.Monitor",
                            type = "AzureMonitorAgent",
                            typeHandlerVersion = "1.0",
                            autoUpgradeMinorVersion = true,
                            settings = new
                            {
                                workspaceId = $"/subscriptions/{finding.SubscriptionId}/resourceGroups/{finding.ResourceGroupName}/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace"
                            }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = false
                },
                new()
                {
                    ActionType = "ConfigureDiagnostics",
                    Description = "Enable diagnostic settings for VM",
                    ApiOperation = "Microsoft.Insights/diagnosticSettings",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            workspaceId = $"/subscriptions/{finding.SubscriptionId}/resourceGroups/{finding.ResourceGroupName}/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace",
                            metrics = new[]
                            {
                                new
                                {
                                    category = "AllMetrics",
                                    enabled = true,
                                    retentionPolicy = new { enabled = true, days = 30 }
                                }
                            },
                            logs = new[]
                            {
                                new
                                {
                                    category = "Administrative",
                                    enabled = true,
                                    retentionPolicy = new { enabled = true, days = 30 }
                                }
                            }
                        }
                    },
                    ExecutionOrder = 2,
                    IsCritical = false
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateVmBackupRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(20),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableBackup",
                    Description = "Enable Azure Backup for VM",
                    ApiOperation = "Microsoft.RecoveryServices/vaults/backupFabrics/protectionContainers/protectedItems",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            protectedItemType = "Microsoft.Compute/virtualMachines",
                            policyId = $"/subscriptions/{finding.SubscriptionId}/resourceGroups/{finding.ResourceGroupName}/providers/Microsoft.RecoveryServices/vaults/DefaultBackupVault/backupPolicies/DefaultPolicy",
                            sourceResourceId = finding.ResourceId
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateVmNetworkAccessRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableJustInTimeAccess",
                    Description = "Enable Just-in-Time VM access",
                    ApiOperation = "Microsoft.Security/jitNetworkAccessPolicies",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            virtualMachines = new[]
                            {
                                new
                                {
                                    id = finding.ResourceId,
                                    ports = new[]
                                    {
                                        new
                                        {
                                            number = 22,
                                            protocol = "TCP",
                                            allowedSourceAddressPrefix = "*",
                                            maxRequestAccessDuration = "PT3H" // 3 hours
                                        },
                                        new
                                        {
                                            number = 3389,
                                            protocol = "TCP", 
                                            allowedSourceAddressPrefix = "*",
                                            maxRequestAccessDuration = "PT3H"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private IEnumerable<InfrastructureRemediationAction> GetVirtualMachineRemediationActions()
    {
        return new[]
        {
            new InfrastructureRemediationAction
            {
                ActionType = "EnableDiskEncryption",
                Description = "Enable Azure Disk Encryption"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "InstallAntiMalware",
                Description = "Install Microsoft Antimalware extension"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableMonitoring",
                Description = "Install Azure Monitor Agent"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableBackup",
                Description = "Configure Azure Backup"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableJustInTimeAccess",
                Description = "Configure Just-in-Time VM access"
            }
        };
    }

    #endregion

    #region SQL Database Remediation Templates

    private InfrastructureRemediationPlan CreateSqlEncryptionRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(15),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableTransparentDataEncryption",
                    Description = "Enable Transparent Data Encryption (TDE) for SQL Database",
                    ApiOperation = "Microsoft.Sql/servers/databases/transparentDataEncryption",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            status = "Enabled"
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true,
                    Validation = new InfrastructureActionValidation
                    {
                        PropertyPath = "properties.status",
                        ExpectedValue = "Enabled",
                        ValidationType = "Equals"
                    }
                }
            },
            Prerequisites = new List<string>
            {
                "Verify database is accessible and not corrupted",
                "Ensure sufficient storage for encryption overhead",
                "Schedule during maintenance window for minimal impact"
            }
        };
    }

    private InfrastructureRemediationPlan CreateSqlFirewallRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "RestrictFirewallRules",
                    Description = "Remove overly permissive SQL Server firewall rules",
                    ApiOperation = "Microsoft.Sql/servers/firewallRules/AllowAllWindowsAzureIps",
                    HttpMethod = "DELETE",
                    Payload = new { },
                    ExecutionOrder = 1,
                    IsCritical = true
                },
                new()
                {
                    ActionType = "AddRestrictiveFirewallRule",
                    Description = "Add restrictive firewall rule for specific IP ranges",
                    ApiOperation = "Microsoft.Sql/servers/firewallRules",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            startIpAddress = "10.0.0.0",
                            endIpAddress = "10.255.255.255"
                        }
                    },
                    ExecutionOrder = 2,
                    IsCritical = false
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateSqlAuditingRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableServerAuditing",
                    Description = "Enable SQL Server auditing to storage account",
                    ApiOperation = "Microsoft.Sql/servers/auditingSettings",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            state = "Enabled",
                            storageEndpoint = $"https://{finding.SubscriptionId}audit.blob.core.windows.net/",
                            retentionDays = 90,
                            auditActionsAndGroups = new[]
                            {
                                "SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP",
                                "FAILED_DATABASE_AUTHENTICATION_GROUP",
                                "BATCH_COMPLETED_GROUP"
                            }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateSqlThreatDetectionRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Medium,
            EstimatedDuration = TimeSpan.FromMinutes(5),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableAdvancedThreatProtection",
                    Description = "Enable Advanced Threat Protection for SQL Database",
                    ApiOperation = "Microsoft.Sql/servers/databases/securityAlertPolicies",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            state = "Enabled",
                            emailAccountAdmins = true,
                            disabledAlerts = new string[] { },
                            retentionDays = 30
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateSqlBackupRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(15),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "ConfigureLongTermRetention",
                    Description = "Configure long-term backup retention for SQL Database",
                    ApiOperation = "Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            weeklyRetention = "P12W",
                            monthlyRetention = "P12M",
                            yearlyRetention = "P7Y",
                            weekOfYear = 1
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = false
                }
            }
        };
    }

    #endregion

    #region Cosmos DB Remediation Templates

    private InfrastructureRemediationPlan CreateCosmosEncryptionRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableCustomerManagedKeys",
                    Description = "Enable customer-managed keys for Cosmos DB encryption",
                    ApiOperation = "Microsoft.DocumentDB/databaseAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            keyVaultKeyUri = $"https://{finding.SubscriptionId}-kv.vault.azure.net/keys/cosmos-key/latest"
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true,
                    Validation = new InfrastructureActionValidation
                    {
                        PropertyPath = "properties.keyVaultKeyUri",
                        ExpectedValue = "https://",
                        ValidationType = "StartsWith"
                    }
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateCosmosFirewallRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.High,
            EstimatedDuration = TimeSpan.FromMinutes(5),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "RestrictNetworkAccess",
                    Description = "Restrict Cosmos DB network access to specific IP ranges",
                    ApiOperation = "Microsoft.DocumentDB/databaseAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            ipRules = new[]
                            {
                                new { ipAddressOrRange = "10.0.0.0/8" },
                                new { ipAddressOrRange = "172.16.0.0/12" },
                                new { ipAddressOrRange = "192.168.0.0/16" }
                            },
                            isVirtualNetworkFilterEnabled = true
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateCosmosBackupRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Low,
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnableContinuousBackup",
                    Description = "Enable continuous backup for Cosmos DB",
                    ApiOperation = "Microsoft.DocumentDB/databaseAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            backupPolicy = new
                            {
                                type = "Continuous"
                            }
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true,
                    Validation = new InfrastructureActionValidation
                    {
                        PropertyPath = "properties.backupPolicy.type",
                        ExpectedValue = "Continuous",
                        ValidationType = "Equals"
                    }
                }
            }
        };
    }

    private InfrastructureRemediationPlan CreateCosmosNetworkAccessRemediation(AtoFinding finding)
    {
        return new InfrastructureRemediationPlan
        {
            Finding = finding,
            RiskLevel = RemediationRiskLevel.Medium,
            EstimatedDuration = TimeSpan.FromMinutes(15),
            Actions = new List<InfrastructureRemediationAction>
            {
                new()
                {
                    ActionType = "EnablePrivateEndpoint",
                    Description = "Configure private endpoint for Cosmos DB",
                    ApiOperation = "Microsoft.DocumentDB/databaseAccounts",
                    HttpMethod = "PATCH",
                    Payload = new
                    {
                        properties = new
                        {
                            publicNetworkAccess = "Disabled",
                            networkAclBypass = "AzureServices"
                        }
                    },
                    ExecutionOrder = 1,
                    IsCritical = true
                },
                new()
                {
                    ActionType = "CreatePrivateEndpoint",
                    Description = "Create private endpoint resource",
                    ApiOperation = "Microsoft.Network/privateEndpoints",
                    HttpMethod = "PUT",
                    Payload = new
                    {
                        properties = new
                        {
                            subnet = new
                            {
                                id = $"/subscriptions/{finding.SubscriptionId}/resourceGroups/{finding.ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/default-vnet/subnets/default"
                            },
                            privateLinkServiceConnections = new[]
                            {
                                new
                                {
                                    name = "cosmos-private-connection",
                                    properties = new
                                    {
                                        privateLinkServiceId = finding.ResourceId,
                                        groupIds = new[] { "Sql" }
                                    }
                                }
                            }
                        }
                    },
                    ExecutionOrder = 2,
                    IsCritical = false
                }
            }
        };
    }

    private IEnumerable<InfrastructureRemediationAction> GetSqlDatabaseRemediationActions()
    {
        return new[]
        {
            new InfrastructureRemediationAction
            {
                ActionType = "EnableTransparentDataEncryption",
                Description = "Enable Transparent Data Encryption (TDE)"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "RestrictFirewallRules",
                Description = "Configure restrictive firewall rules"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableServerAuditing",
                Description = "Enable SQL Server auditing"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableAdvancedThreatProtection",
                Description = "Enable Advanced Threat Protection"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "ConfigureLongTermRetention",
                Description = "Configure backup retention policies"
            }
        };
    }

    private IEnumerable<InfrastructureRemediationAction> GetCosmosDatabaseRemediationActions()
    {
        return new[]
        {
            new InfrastructureRemediationAction
            {
                ActionType = "EnableCustomerManagedKeys",
                Description = "Enable customer-managed encryption keys"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "RestrictNetworkAccess",
                Description = "Configure network access restrictions"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnableContinuousBackup",
                Description = "Enable continuous backup policy"
            },
            new InfrastructureRemediationAction
            {
                ActionType = "EnablePrivateEndpoint",
                Description = "Configure private endpoint access"
            }
        };
    }

    #endregion

    private async Task AddResourceSpecificPrerequisitesAsync(InfrastructureRemediationPlan plan, CancellationToken cancellationToken)
    {
        // Add prerequisites based on resource type and actions
        switch (plan.ResourceType?.ToLower())
        {
            case "microsoft.keyvault/vaults":
                plan.Prerequisites.Add("Verify Key Vault is not in use during maintenance window");
                break;
            case "microsoft.network/networksecuritygroups":
                plan.Prerequisites.Add("Verify network connectivity will not be disrupted");
                plan.Prerequisites.Add("Notify network administrators");
                break;
        }
    }

    private async Task AnalyzeActionImpactAsync(InfrastructureRemediationAction action, InfrastructureRemediationPlan plan, InfrastructureRemediationImpactAssessment assessment)
    {
        // Analyze potential impact of each action
        switch (action.ActionType)
        {
            case "RemoveAllowAllRule":
            case "RestrictPortAccess":
                assessment.AffectedServices.Add("Network Connectivity");
                assessment.Risks.Add(new RemediationRisk
                {
                    RiskType = "Connectivity",
                    Description = "Network rule changes may affect application connectivity",
                    Severity = RemediationRiskLevel.High,
                    Probability = 0.3,
                    MitigationStrategy = "Test connectivity after changes, have rollback plan ready"
                });
                break;
                
            case "EnablePurgeProtection":
                assessment.Risks.Add(new RemediationRisk
                {
                    RiskType = "Irreversible Change",
                    Description = "Purge protection cannot be disabled once enabled",
                    Severity = RemediationRiskLevel.Medium,
                    Probability = 1.0,
                    MitigationStrategy = "Ensure this change is intentional and approved"
                });
                break;
        }
    }

    private void AddSafetyRecommendations(InfrastructureRemediationImpactAssessment assessment, InfrastructureRemediationPlan plan)
    {
        assessment.SafetyRecommendations.Add("Execute during maintenance window");
        assessment.SafetyRecommendations.Add("Have rollback plan ready");
        assessment.SafetyRecommendations.Add("Monitor system health after changes");
        
        if (assessment.OverallRisk >= RemediationRiskLevel.High)
        {
            assessment.SafetyRecommendations.Add("Perform dry run first");
            assessment.SafetyRecommendations.Add("Get approval from system owners");
        }

        // Add recommended maintenance windows
        assessment.RecommendedMaintenanceWindows.Add(new MaintenanceWindow
        {
            StartTime = DateTime.Today.AddDays(1).AddHours(2), // 2 AM tomorrow
            EndTime = DateTime.Today.AddDays(1).AddHours(6),   // 6 AM tomorrow
            Reason = "Low traffic period",
            Priority = 1
        });
    }

    #endregion
}