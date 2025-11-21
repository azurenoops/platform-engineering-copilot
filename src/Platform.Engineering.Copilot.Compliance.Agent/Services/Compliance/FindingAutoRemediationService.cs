using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Service to determine if findings are auto-remediable and categorize remediation capabilities
/// </summary>
public class FindingAutoRemediationService
{
    private static ILogger? _logger;
    
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }
    /// <summary>
    /// Determines if a finding can be automatically remediated based on finding type and resource
    /// </summary>
    public static bool IsAutoRemediable(AtoFinding finding)
    {
        if (finding == null || string.IsNullOrEmpty(finding.Title))
            return false;

        var title = finding.Title.ToLowerInvariant();
        var resourceType = finding.ResourceType?.ToLowerInvariant() ?? "";
        var findingType = finding.FindingType;
        var controlIds = finding.AffectedNistControls;

        // ===== GROUPED FINDINGS =====
        // Handle grouped findings (ResourceType = "Multiple") that aggregate multiple resource issues
        if (resourceType == "multiple")
        {
            // TLS/HTTPS grouped findings (from SystemCommunicationScanner SC-8)
            if (title.Contains("tls") || title.Contains("https"))
            {
                // These are technical configuration changes that can be automated:
                // - Storage: Enable "Require Secure Transfer"
                // - App Services: Set "HTTPS Only" and "Minimum TLS Version 1.2"
                // - SQL: Set "Minimum TLS Version 1.2"
                // - Redis: Disable non-TLS port 6379
                return true;
            }
            
            // Diagnostic settings grouped findings
            if (title.Contains("diagnostic") && (title.Contains("disabled") || title.Contains("not enabled")))
            {
                return true; // Can auto-configure diagnostics
            }
            
            // Encryption grouped findings
            if (title.Contains("encryption") && (title.Contains("disabled") || title.Contains("not enabled")))
            {
                return true; // Can enable encryption
            }
            
            // Logging/Monitoring grouped findings (AU family - Audit and Accountability)
            if ((title.Contains("logging") || title.Contains("log analytics") || title.Contains("monitoring")) && 
                (title.Contains("disabled") || title.Contains("not enabled") || title.Contains("not configured")))
            {
                return true; // Can enable logging/monitoring
            }
            
            // Backup/Recovery grouped findings (CP family - Contingency Planning)
            if ((title.Contains("backup") || title.Contains("recovery") || title.Contains("disaster")) && 
                (title.Contains("disabled") || title.Contains("not enabled") || title.Contains("not configured")))
            {
                return true; // Can enable backup/recovery
            }
            
            // Authentication/MFA grouped findings (IA family - Identification and Authentication)
            // Note: MFA enforcement can be automated but needs careful consideration
            if (title.Contains("multi-factor") || title.Contains("mfa"))
            {
                return true; // Can enable MFA policies
            }
            
            // Network security grouped findings (SC-7 - Boundary Protection)
            if (title.Contains("network security") || title.Contains("firewall") || title.Contains("nsg"))
            {
                return true; // Can configure network security
            }
            
            // Configuration management findings (CM family)
            if (title.Contains("configuration baseline") || title.Contains("security baseline"))
            {
                return true; // Can apply security baselines
            }
            
            // EXPAND: Be more optimistic about grouped findings
            // Most grouped technical/configuration findings can be auto-remediated
            // Only exclude those that require business decisions (access control, data classification)
            if (title.Contains("access") && title.Contains("assignment"))
            {
                return false; // Access assignments require business logic
            }
            
            if (title.Contains("data classification") || title.Contains("sensitivity label"))
            {
                return false; // Data classification requires business decisions
            }
            
            // Default for grouped findings: assume auto-remediable unless proven otherwise
            // This is optimistic but encourages automation
            return true;
        }

        // ===== INDIVIDUAL FINDINGS (by FindingType) =====
        // Use FindingType as primary discriminator for auto-remediation
        switch (findingType)
        {
            case AtoFindingType.Encryption:
                // Encryption findings are generally auto-remediable
                return true;

            case AtoFindingType.NetworkSecurity:
                // Network security findings (NSG rules, firewalls) can often be auto-remediated
                // But be cautious about breaking connectivity
                return true;

            case AtoFindingType.Configuration:
                // Configuration findings may be auto-remediable depending on what needs configuring
                // Check resource type and control
                if (controlIds.Contains("SC-28") || controlIds.Contains("SC-13"))
                    return true; // Encryption configuration
                if (controlIds.Contains("AU-2") || controlIds.Contains("AU-3") || controlIds.Contains("AU-12"))
                    return true; // Audit logging configuration
                if (controlIds.Contains("CP-9") || controlIds.Contains("CP-10"))
                    return true; // Backup and recovery configuration
                if (controlIds.Contains("IA-5"))
                    return true; // Authenticator management
                if (controlIds.Contains("SC-7") || controlIds.Contains("SC-8"))
                    return true; // Network boundary protection
                if (resourceType.Contains("keyvault"))
                    return true; // Key Vault configuration (soft delete, purge protection)
                if (resourceType.Contains("storage"))
                    return true; // Storage account configuration
                if (resourceType.Contains("sql") || resourceType.Contains("database"))
                    return true; // Database configuration
                // Default: most configuration findings can be automated
                return true;

            case AtoFindingType.AccessControl:
                // Access Control findings generally require human decision-making
                // Exception: some basic security hardening like requiring managed identities
                if (title.Contains("managed identity"))
                    return false; // Assigning identities requires business logic
                return false; // Most access control = manual

            case AtoFindingType.Security:
                // Generic security findings - check if they're diagnostic/monitoring related
                if (title.Contains("diagnostic") || title.Contains("monitoring") || title.Contains("log analytics"))
                    return true; // Can auto-configure diagnostics
                if (title.Contains("authentication monitoring"))
                    return true; // Can enable monitoring agents
                return false;

            case AtoFindingType.Compliance:
                // Generic compliance findings - check the control and resource
                if (controlIds.Contains("SC-7") || controlIds.Contains("SC-8"))
                    return true; // Boundary protection, transmission protection
                if (controlIds.Contains("AU-2") || controlIds.Contains("AU-12"))
                    return true; // Audit configuration
                return false;

            default:
                // Fall back to title/resource type checking for other types
                break;
        }
        
        // Fallback: check title patterns (for backwards compatibility)
        
        // Storage Account findings
        if (resourceType.Contains("microsoft.storage/storageaccounts"))
        {
            if (title.Contains("encryption") && title.Contains("disabled"))
                return true; // Can enable encryption
            if (title.Contains("https") && title.Contains("required"))
                return true; // Can require HTTPS
            if (title.Contains("tls") && (title.Contains("version") || title.Contains("1.0") || title.Contains("1.1")))
                return true; // Can update minimum TLS version
            if (title.Contains("public") && title.Contains("access"))
                return true; // Can disable public blob access
        }
        
        // Virtual Machine findings
        if (resourceType.Contains("microsoft.compute/virtualmachines"))
        {
            if (title.Contains("disk") && title.Contains("encryption"))
                return true; // Can enable Azure Disk Encryption
            if (title.Contains("diagnostic") && (title.Contains("disabled") || title.Contains("not enabled")))
                return true; // Can enable diagnostics
        }
        
        // Network Security Group findings
        if (resourceType.Contains("microsoft.network/networksecuritygroups"))
        {
            if (title.Contains("port") && (title.Contains("open") || title.Contains("unrestricted")))
                return true; // Can restrict NSG rules
            if (title.Contains("rdp") || title.Contains("3389"))
                return true; // Can block RDP
            if (title.Contains("ssh") || title.Contains("22"))
                return true; // Can restrict SSH
            if (title.Contains("management port") || title.Contains("administrative"))
                return true; // Can restrict management ports
        }
        
        // Key Vault findings
        if (resourceType.Contains("microsoft.keyvault/vaults"))
        {
            if (title.Contains("soft delete") && title.Contains("disabled"))
                return true; // Can enable soft delete
            if (title.Contains("purge protection") && (title.Contains("disabled") || title.Contains("not enabled")))
                return true; // Can enable purge protection
            if (title.Contains("diagnostic") && title.Contains("disabled"))
                return true; // Can enable diagnostics
            if (title.Contains("rbac") && title.Contains("not enabled"))
                return true; // Can enable RBAC
        }
        
        // SQL Server/Database findings
        if (resourceType.Contains("microsoft.sql/servers") || resourceType.Contains("microsoft.sql/databases"))
        {
            if (title.Contains("tde") && (title.Contains("disabled") || title.Contains("not enabled")))
                return true; // Can enable Transparent Data Encryption
            if (title.Contains("auditing") && title.Contains("disabled"))
                return true; // Can enable SQL auditing
            if (title.Contains("threat detection") && title.Contains("disabled"))
                return true; // Can enable Advanced Threat Protection
            if (title.Contains("firewall") && title.Contains("0.0.0.0"))
                return true; // Can restrict firewall rules
        }
        
        // Cosmos DB findings
        if (resourceType.Contains("microsoft.documentdb/databaseaccounts"))
        {
            if (title.Contains("diagnostic") && title.Contains("disabled"))
                return true; // Can enable diagnostics
        }
        
        // App Service findings
        if (resourceType.Contains("microsoft.web/sites"))
        {
            if (title.Contains("https") && title.Contains("only"))
                return true; // Can require HTTPS only
            if (title.Contains("tls") && title.Contains("version"))
                return true; // Can set minimum TLS version
            if (title.Contains("diagnostic") && title.Contains("disabled"))
                return true; // Can enable diagnostics
        }
        
        // Diagnostic Settings (applies to multiple resource types)
        if (title.Contains("diagnostic") && (title.Contains("disabled") || title.Contains("not enabled") || title.Contains("not configured")))
        {
            // Most resources can have diagnostics auto-configured
            if (resourceType.Contains("microsoft.storage") ||
                resourceType.Contains("microsoft.compute") ||
                resourceType.Contains("microsoft.keyvault") ||
                resourceType.Contains("microsoft.sql") ||
                resourceType.Contains("microsoft.network") ||
                resourceType.Contains("microsoft.web"))
            {
                return true;
            }
        }
        
        // Tagging findings (can auto-apply tags)
        if (title.Contains("tag") && (title.Contains("missing") || title.Contains("required")))
        {
            return true; // Can apply tags
        }
        
        // Backup findings
        if (title.Contains("backup") && (title.Contains("not configured") || title.Contains("disabled")))
        {
            if (resourceType.Contains("microsoft.compute/virtualmachines") ||
                resourceType.Contains("microsoft.sql"))
            {
                return true; // Can enable Azure Backup
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets the remediation complexity for a finding
    /// </summary>
    public static AtoRemediationComplexity GetRemediationComplexity(AtoFinding finding)
    {
        if (!IsAutoRemediable(finding))
            return AtoRemediationComplexity.Complex; // Manual = High complexity
            
        var title = finding.Title.ToLowerInvariant();
        
        // Low complexity - simple configuration changes
        if (title.Contains("tag") ||
            title.Contains("https") ||
            title.Contains("tls version") ||
            title.Contains("diagnostic") ||
            title.Contains("soft delete") ||
            title.Contains("purge protection"))
        {
            return AtoRemediationComplexity.Simple;
        }
        
        // Medium complexity - requires more configuration
        if (title.Contains("encryption") ||
            title.Contains("firewall") ||
            title.Contains("nsg") ||
            title.Contains("backup"))
        {
            return AtoRemediationComplexity.Moderate;
        }
        
        return AtoRemediationComplexity.Moderate;
    }
    
    /// <summary>
    /// Gets the estimated remediation duration for a finding
    /// </summary>
    public static TimeSpan GetEstimatedDuration(AtoFinding finding)
    {
        var complexity = GetRemediationComplexity(finding);
        
        return complexity switch
        {
            AtoRemediationComplexity.Simple => TimeSpan.FromMinutes(5),
            AtoRemediationComplexity.Moderate => TimeSpan.FromMinutes(15),
            AtoRemediationComplexity.Complex => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(30)
        };
    }
    
    /// <summary>
    /// Gets suggested remediation actions for a finding
    /// </summary>
    public static List<AtoRemediationAction> GetRemediationActions(AtoFinding finding)
    {
        var actions = new List<AtoRemediationAction>();
        
        if (!IsAutoRemediable(finding))
        {
            _logger?.LogDebug("Finding {FindingId} is NOT auto-remediable, returning manual action", finding.Id);
            actions.Add(new AtoRemediationAction
            {
                Name = "Manual Review Required",
                Description = finding.Recommendation,
                ActionType = AtoRemediationActionType.ManualAction,
                Complexity = AtoRemediationComplexity.Complex,
                EstimatedDuration = TimeSpan.FromHours(1)
            });
            return actions;
        }
        
        var title = finding.Title.ToLowerInvariant();
        var resourceType = finding.ResourceType?.ToLowerInvariant() ?? "";
        
        _logger?.LogInformation("Getting remediation actions for auto-remediable finding: Title='{Title}', ResourceType='{ResourceType}'", 
            finding.Title, finding.ResourceType);
        
        // Storage encryption
        if (title.Contains("encryption") && resourceType.Contains("storage"))
        {
            var action = new AtoRemediationAction
            {
                Name = "Enable Storage Encryption",
                Description = "Enable encryption at rest for the storage account using Azure Storage Service Encryption",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Moderate,
                EstimatedDuration = TimeSpan.FromMinutes(10),
                RequiresApproval = false,
                ToolCommand = "ENABLE_ENCRYPTION",  // Action type for AtoRemediationEngine
                ScriptPath = "EnableStorageEncryption.ps1"
            };
            actions.Add(action);
        }
        
        // VM disk encryption
        if (title.Contains("disk") && title.Contains("encryption") && resourceType.Contains("virtualmachine"))
        {
            actions.Add(new AtoRemediationAction
            {
                Name = "Enable Azure Disk Encryption",
                Description = "Enable Azure Disk Encryption for VM disks using Key Vault",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Moderate,
                EstimatedDuration = TimeSpan.FromMinutes(20),
                RequiresApproval = true // VM restart may be needed
            });
        }
        
        // NSG rules
        if (title.Contains("port") && resourceType.Contains("networksecuritygroup"))
        {
            var action = new AtoRemediationAction
            {
                Name = "Update NSG Rules",
                Description = "Restrict open ports and remove overly permissive rules",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Moderate,
                EstimatedDuration = TimeSpan.FromMinutes(10),
                RequiresApproval = true, // May impact connectivity
                ToolCommand = "CONFIGURE_NSG",  // Action type for AtoRemediationEngine
                ScriptPath = "ConfigureNsgRules.ps1"
            };
            actions.Add(action);
        }
        
        // Key Vault soft delete
        if (title.Contains("soft delete") && resourceType.Contains("keyvault"))
        {
            actions.Add(new AtoRemediationAction
            {
                Name = "Enable Soft Delete",
                Description = "Enable soft delete protection for Key Vault",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresApproval = false
            });
        }
        
        // Key Vault purge protection
        if (title.Contains("purge protection") && resourceType.Contains("keyvault"))
        {
            actions.Add(new AtoRemediationAction
            {
                Name = "Enable Purge Protection",
                Description = "Enable purge protection for Key Vault (cannot be disabled once enabled)",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresApproval = true // Irreversible
            });
        }
        
        // SQL TDE
        if (title.Contains("tde") && resourceType.Contains("sql"))
        {
            actions.Add(new AtoRemediationAction
            {
                Name = "Enable Transparent Data Encryption",
                Description = "Enable TDE to encrypt SQL database at rest",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Moderate,
                EstimatedDuration = TimeSpan.FromMinutes(15),
                RequiresApproval = false
            });
        }
        
        // Diagnostic settings
        if (title.Contains("diagnostic"))
        {
            var action = new AtoRemediationAction
            {
                Name = "Enable Diagnostic Settings",
                Description = "Configure diagnostic settings to send logs to Log Analytics workspace",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresApproval = false,
                ToolCommand = "ENABLE_DIAGNOSTIC_SETTINGS",  // Action type for AtoRemediationEngine
                ScriptPath = "EnableDiagnosticSettings.ps1"
            };
            actions.Add(action);
        }
        
        // TLS version
        if (title.Contains("tls"))
        {
            var action = new AtoRemediationAction
            {
                Name = "Update Minimum TLS Version",
                Description = "Set minimum TLS version to 1.2 or higher",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresApproval = false,
                ToolCommand = "UPDATE_TLS_VERSION",  // Action type for AtoRemediationEngine
                ScriptPath = "UpdateTlsVersion.ps1"
            };
            // Set parameters for TLS version update
            action.Parameters["minimumTlsVersion"] = "1.2";
            actions.Add(action);
        }
        
        // HTTPS only
        if (title.Contains("https"))
        {
            var action = new AtoRemediationAction
            {
                Name = "Require HTTPS Only",
                Description = "Configure resource to accept only HTTPS traffic",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresApproval = false,
                ToolCommand = "ENABLE_HTTPS",  // Action type for AtoRemediationEngine
                ScriptPath = "EnableHttpsOnly.ps1"
            };
            actions.Add(action);
        }
        
        // Tags
        if (title.Contains("tag"))
        {
            actions.Add(new AtoRemediationAction
            {
                Name = "Apply Required Tags",
                Description = "Apply required compliance tags to the resource",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(2),
                RequiresApproval = false
            });
        }
        
        // Alert rules / Audit review
        if ((title.Contains("alert") || title.Contains("audit review")) && 
            (resourceType.Contains("scheduledqueryrules") || resourceType.Contains("insights")))
        {
            var action = new AtoRemediationAction
            {
                Name = "Configure Audit Alert Rules",
                Description = "Set up scheduled query rules for automated audit log review and alerting",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Moderate,
                EstimatedDuration = TimeSpan.FromMinutes(15),
                RequiresApproval = false,
                ToolCommand = "CONFIGURE_ALERT_RULES",
                ScriptPath = "ConfigureAuditAlertRules.ps1"
            };
            actions.Add(action);
        }
        
        // Log Analytics workspace retention
        if (title.Contains("retention") && resourceType.Contains("operationalinsights"))
        {
            var action = new AtoRemediationAction
            {
                Name = "Configure Log Retention",
                Description = "Set Log Analytics workspace retention period to meet compliance requirements (90+ days)",
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = AtoRemediationComplexity.Simple,
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresApproval = false,
                ToolCommand = "CONFIGURE_LOG_RETENTION",
                ScriptPath = "ConfigureLogRetention.ps1"
            };
            action.Parameters["retentionDays"] = "90";
            actions.Add(action);
        }
        
        // If no specific action matched, provide generic auto-remediation action
        if (actions.Count == 0)
        {
            _logger?.LogWarning("No specific remediation action matched for auto-remediable finding. Title: {Title}, ResourceType: {ResourceType}", 
                finding.Title, finding.ResourceType);
                
            actions.Add(new AtoRemediationAction
            {
                Name = "Auto-Configure Compliance Setting",
                Description = finding.Recommendation,
                ActionType = AtoRemediationActionType.ConfigurationChange,
                Complexity = GetRemediationComplexity(finding),
                EstimatedDuration = GetEstimatedDuration(finding),
                RequiresApproval = false
            });
        }
        else
        {
            _logger?.LogInformation("Created {Count} remediation action(s) for finding {FindingId}", actions.Count, finding.Id);
        }
        
        return actions;
    }
}
