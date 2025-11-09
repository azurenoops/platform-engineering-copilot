using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Extensions;
using Azure.Core;
using Azure.ResourceManager.Resources;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Compliance.Agent.Extensions;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Scanner for Contingency Planning (CP) family controls using real Azure APIs
/// </summary>
public class ContingencyPlanningScanner : IComplianceScanner
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public ContingencyPlanningScanner(ILogger logger, IAzureResourceService azureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
    }

    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken = default)
    {
        return await ScanControlAsync(subscriptionId, null, control, cancellationToken);
    }

    /// <summary>
    /// Resource group-scoped scanning
    /// </summary>
    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control,
        CancellationToken cancellationToken = default)
    {
        var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
        _logger.LogDebug("Scanning control {ControlId} for {Scope} in subscription {SubscriptionId}", 
            control.Id, scope, subscriptionId);

        var findings = new List<AtoFinding>();

        // CRITICAL: Control IDs from NIST catalog are lowercase (cp-6, cp-7, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        switch (controlId)
        {
            case "CP-6":
            case "CP-7":
                findings.AddRange(await ScanAlternateSitesAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "CP-9":
            case "CP-10":
                findings.AddRange(await ScanBackupCapabilitiesAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericContingencyAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }


    private async Task<List<AtoFinding>> ScanAlternateSitesAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning disaster recovery and alternate sites (CP-6, CP-7, CP-10) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            // Get Recovery Services vaults for Site Recovery
            var recoverySvcVaults = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.RecoveryServices/vaults", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            // Get critical resources that should have disaster recovery
            var virtualMachines = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            var sqlDatabases = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Sql/servers/databases", StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            var storageAccounts = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Track resources without disaster recovery
            var vmsWithoutSiteRecovery = new List<string>();
            var sqlWithoutGeoReplication = new List<string>();
            var storageWithoutGeoRedundancy = new List<string>();

            // CHECK 1: Query Storage Accounts for geo-redundancy
            _logger.LogInformation("Checking geo-redundancy for {StorageCount} Storage Accounts", storageAccounts.Count);
            
            foreach (var storage in storageAccounts)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)storage).Data.Id! ?? "");
                    var storageResource = armClient?.GetGenericResource(resourceId);
                    var storageData = await storageResource.GetAsync(cancellationToken);
                    
                    // Check SKU for geo-redundancy
                    var properties = storageData.Value.Data.Properties.ToObjectFromJson<JsonElement>();
                    bool isGeoRedundant = false;
                    
                    if (properties.TryGetProperty("sku", out var skuElement) &&
                        skuElement.TryGetProperty("name", out var skuName))
                    {
                        var skuValue = skuName.GetString() ?? "";
                        // GRS, RAGRS, GZRS, RAGZRS are geo-redundant
                        isGeoRedundant = skuValue.Contains("GRS", StringComparison.OrdinalIgnoreCase) ||
                                        skuValue.Contains("GZRS", StringComparison.OrdinalIgnoreCase);
                    }
                    
                    if (!isGeoRedundant)
                    {
                        storageWithoutGeoRedundancy.Add($"{((GenericResource)storage).Data.Name} ({((GenericResource)storage).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query geo-redundancy for Storage Account {StorageId}", ((GenericResource)storage).Data.Id!);
                    storageWithoutGeoRedundancy.Add($"{((GenericResource)storage).Data.Name} (status unknown)");
                }
            }

            // CHECK 2: Query SQL Databases for geo-replication
            _logger.LogInformation("Checking geo-replication for {SqlCount} SQL Databases", sqlDatabases.Count);
            
            foreach (var db in sqlDatabases)
            {
                try
                {
                    // Skip system databases (master, model, msdb, tempdb)
                    if (((GenericResource)db).Data.Name?.EndsWith("/master") == true || 
                        ((GenericResource)db).Data.Name?.EndsWith("/model") == true ||
                        ((GenericResource)db).Data.Name?.EndsWith("/msdb") == true || 
                        ((GenericResource)db).Data.Name?.EndsWith("/tempdb") == true)
                    {
                        continue;
                    }
                    
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)db).Data.Id! ?? "");
                    var dbResource = armClient?.GetGenericResource(resourceId);
                    var dbData = await dbResource.GetAsync(cancellationToken);
                    
                    // Query replication links to check for active geo-replication
                    var properties = JsonDocument.Parse(dbData.Value.Data.Properties.ToStream());
                    
                    // In production, query replication links: GET /databases/{database}/replicationLinks
                    // For now, check if this is a secondary or if properties indicate geo-replication
                    bool hasGeoReplication = false;
                    
                    if (properties.RootElement.TryGetProperty("failoverGroupId", out var failoverGroup))
                    {
                        // Part of a failover group (automatic geo-replication)
                        hasGeoReplication = !string.IsNullOrEmpty(failoverGroup.GetString());
                    }
                    
                    if (!hasGeoReplication)
                    {
                        var dbName = ((GenericResource)db).Data.Name?.Split('/').LastOrDefault() ?? ((GenericResource)db).Data.Name ?? "Unknown";
                        sqlWithoutGeoReplication.Add($"{dbName} ({((GenericResource)db).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query geo-replication for SQL Database {DbId}", ((GenericResource)db).Data.Id!);
                    var dbName = ((GenericResource)db).Data.Name?.Split('/').LastOrDefault() ?? ((GenericResource)db).Data.Name ?? "Unknown";
                    sqlWithoutGeoReplication.Add($"{dbName} (status unknown)");
                }
            }

            // CHECK 3: Query VMs for Site Recovery protection
            _logger.LogInformation("Checking Site Recovery protection for {VmCount} VMs", virtualMachines.Count);
            
            foreach (var vm in virtualMachines)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)vm).Data.Id! ?? "");
                    var vmResource = armClient?.GetGenericResource(resourceId);
                    var vmData = await vmResource.GetAsync(cancellationToken);
                    
                    // Check for Azure Site Recovery extension (Microsoft.SiteRecovery)
                    // In production, query Recovery Services vault for protected items
                    // For now, we'll flag VMs without known ASR configuration
                    
                    // VMs should be replicated to alternate region for CP-6/CP-7 (Alternate Sites)
                    vmsWithoutSiteRecovery.Add($"{((GenericResource)vm).Data.Name} ({((GenericResource)vm).Data.Location})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query Site Recovery for VM {VmId}", ((GenericResource)vm).Data.Id!);
                    vmsWithoutSiteRecovery.Add($"{((GenericResource)vm).Data.Name} (status unknown)");
                }
            }

            // Generate findings based on disaster recovery status
            var totalCriticalResources = virtualMachines.Count + sqlDatabases.Count + storageAccounts.Count;
            var totalWithoutDR = vmsWithoutSiteRecovery.Count + sqlWithoutGeoReplication.Count + storageWithoutGeoRedundancy.Count;

            if (totalWithoutDR > 0)
            {
                var severity = totalWithoutDR > (totalCriticalResources * 0.5) ? AtoFindingSeverity.Critical : 
                               totalWithoutDR > (totalCriticalResources * 0.2) ? AtoFindingSeverity.High : AtoFindingSeverity.Medium;

                var description = $"Found {totalWithoutDR} of {totalCriticalResources} critical resources without disaster recovery or alternate site configuration. " +
                                 $"CP-6/CP-7 require alternate processing/storage sites for mission-critical systems. " +
                                 $"CP-10 requires system recovery and reconstitution capabilities.";

                if (storageWithoutGeoRedundancy.Any())
                {
                    description += $"\n\n**Storage Accounts without geo-redundancy ({storageWithoutGeoRedundancy.Count})**: {string.Join(", ", storageWithoutGeoRedundancy.Take(10))}";
                    if (storageWithoutGeoRedundancy.Count > 10) description += $" and {storageWithoutGeoRedundancy.Count - 10} more";
                }

                if (sqlWithoutGeoReplication.Any())
                {
                    description += $"\n\n**SQL Databases without geo-replication ({sqlWithoutGeoReplication.Count})**: {string.Join(", ", sqlWithoutGeoReplication.Take(10))}";
                    if (sqlWithoutGeoReplication.Count > 10) description += $" and {sqlWithoutGeoReplication.Count - 10} more";
                }

                if (vmsWithoutSiteRecovery.Any())
                {
                    description += $"\n\n**VMs without Site Recovery ({vmsWithoutSiteRecovery.Count})**: {string.Join(", ", vmsWithoutSiteRecovery.Take(10))}";
                    if (vmsWithoutSiteRecovery.Count > 10) description += $" and {vmsWithoutSiteRecovery.Count - 10} more";
                }

                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Multiple",
                    ResourceName = "Disaster Recovery Configuration",
                    FindingType = AtoFindingType.ContingencyPlanning,
                    Severity = severity,
                    Title = $"Insufficient Disaster Recovery: {totalWithoutDR}/{totalCriticalResources} Resources Not Protected",
                    Description = description,
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per CP-6/CP-7/CP-10 (Alternate Sites & System Recovery):

1. **Enable Storage Account Geo-Redundancy**:
   - Azure Portal → Storage Account → Configuration → Replication
   - Select GRS (Geo-Redundant Storage) or GZRS (Geo-Zone-Redundant Storage)
   - GRS: Synchronously replicates to secondary paired region
   - RAGRS: Read access to secondary region (recommended)
   - GZRS: Zone-redundancy + geo-redundancy (highest availability)
   - Data durability: 99.99999999999999% (16 nines)
   - Failover time: ~1 hour for customer-initiated failover

2. **Configure SQL Database Geo-Replication**:
   - Azure Portal → SQL Database → Geo-Replication
   - Add secondary database in paired region
   - Active Geo-Replication: Up to 4 readable secondaries
   - Failover Groups: Automatic failover with connection string redirects
   - RPO: ~5 seconds (near-zero data loss)
   - RTO: ~30 seconds (automatic failover)
   - Test failover quarterly

3. **Enable Azure Site Recovery for VMs**:
   - Azure Portal → Recovery Services vault → Site Recovery
   - Replicate VMs to secondary Azure region
   - Configure replication policy:
     - RPO threshold: 15 minutes minimum (alert if exceeded)
     - Recovery points: 24 hours minimum
     - App-consistent snapshots: 4 hours
     - Retention: 72 hours minimum
   - Test failover quarterly (CP-10 requirement)
   - Maintain runbooks for failover procedures

4. **Establish Azure Paired Regions**:
   - Primary region → Secondary paired region mapping:
     - East US → West US
     - UK South → UK West
     - West Europe → North Europe
     - See: https://docs.microsoft.com/azure/best-practices-availability-paired-regions
   - Benefits: Isolated failure domains, priority recovery, sequential updates
   - Use Azure Traffic Manager or Front Door for automatic failover

5. **Implement Multi-Region Architecture**:
   - Deploy critical workloads in at least 2 Azure regions
   - Use Azure Load Balancer or Application Gateway for traffic distribution
   - Configure health probes to detect regional failures
   - Maintain data synchronization between regions (ASR, geo-replication, Cosmos DB)
   - Document failover sequence and decision criteria

6. **Configure DNS Failover**:
   - Azure Traffic Manager: Geographic or priority routing
   - Health checks: Monitor endpoint availability every 10-30 seconds
   - Automatic failover: TTL 0-300 seconds
   - Fallback to secondary region on primary failure
   - Test DNS failover monthly

7. **Test Disaster Recovery Plan**:
   - **Monthly**: Test individual service failover (SQL, Storage)
   - **Quarterly**: Test full VM failover with Azure Site Recovery
   - **Annually**: Full disaster recovery exercise (CP-10 requirement)
   - Document test results: RTO actual vs target, RPO verification
   - Update contingency plan with lessons learned
   - Maintain test evidence for auditors

8. **Implement Cross-Region Backup**:
   - Azure Backup: Geo-redundant Recovery Services vault
   - Backup data replicated to paired region automatically
   - Cross-Region Restore (CRR): Restore from secondary region anytime
   - No additional cost for geo-redundant backup storage
   - Test cross-region restore quarterly

9. **Monitor Replication Health**:
   - Azure Monitor: Create alerts for replication lag
   - Alert on: RPO threshold breached, replication errors, sync failures
   - Dashboard: Replication health across all protected resources
   - Weekly review: Ensure all resources within RPO targets
   - Escalation: Page on-call if replication down >4 hours

10. **Document Contingency Plan (SSP)**:
    - Alternate site locations (Azure regions)
    - RTO targets: 4 hours for critical systems, 24 hours for non-critical
    - RPO targets: 1 hour for critical data, 24 hours for non-critical
    - Failover decision criteria and approval process
    - Contact information for DR team members
    - Step-by-step failover procedures (runbooks)
    - Quarterly testing schedule and results
    - Integration with incident response plan (IR family)

DISASTER RECOVERY REQUIREMENTS (FedRAMP/DoD):
- RTO (Recovery Time Objective): ≤4 hours for High Impact systems
- RPO (Recovery Point Objective): ≤1 hour for High Impact systems  
- Alternate Site: Different Azure region (geographic separation)
- Testing Frequency: Quarterly for full DR, annually for tabletop
- Contingency Plan: Must be in System Security Plan (SSP)
- Evidence: Test reports, failover logs, time measurements

AZURE REGION PAIRS (US Gov Cloud - IL5):
- US Gov Virginia → US Gov Texas
- US DoD East → US DoD Central

REFERENCES:
- NIST 800-53 CP-6: Alternate Storage Site
- NIST 800-53 CP-7: Alternate Processing Site
- NIST 800-53 CP-10: System Recovery and Reconstitution
- FedRAMP Contingency Planning: Quarterly testing required
- DoD Cloud Computing SRG: Multi-region deployment (IL4+)
- Azure Business Continuity: https://docs.microsoft.com/azure/availability-zones/",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { "CP-6", "CP-7", "CP-10" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else if (totalCriticalResources > 0)
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Multiple",
                    ResourceName = "Disaster Recovery Configuration",
                    FindingType = AtoFindingType.ContingencyPlanning,
                    Severity = AtoFindingSeverity.Informational,
                    Title = $"Disaster Recovery Configured: {totalCriticalResources} Resources Protected",
                    Description = $"All {totalCriticalResources} critical resources have disaster recovery configured. " +
                                 $"This includes {virtualMachines.Count} VMs, {sqlDatabases.Count} databases, and {storageAccounts.Count} storage accounts. " +
                                 $"CP-6/CP-7 alternate site requirements and CP-10 recovery capabilities are met.",
                    Recommendation = @"MAINTAIN DISASTER RECOVERY POSTURE per CP-6/CP-7/CP-10:

1. **Quarterly DR Testing**: Execute full failover test to alternate region
2. **Monitor Replication Health**: Ensure RPO <1 hour, RTO <4 hours  
3. **Review Contingency Plan**: Update quarterly with any architecture changes
4. **Test Failback Procedures**: Verify return to primary region after failover
5. **Audit DR Configuration**: Monthly review of geo-replication status
6. **Maintain Runbooks**: Keep failover procedures current and accessible
7. **Train DR Team**: Annual training on failover procedures
8. **Document Test Results**: Maintain evidence of quarterly tests for auditors

Continue quarterly testing and annual full DR exercises to ensure compliance is maintained.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { "CP-6", "CP-7", "CP-10" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning alternate sites/disaster recovery for subscription {SubscriptionId}", subscriptionId);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Disaster Recovery Scan",
                FindingType = AtoFindingType.ContingencyPlanning,
                Severity = AtoFindingSeverity.High,
                Title = "Disaster Recovery Scan Error - Manual Review Required",
                Description = $"Could not complete automated disaster recovery scan: {ex.Message}. Manual review required to verify CP-6/CP-7/CP-10 compliance.",
                Recommendation = "Manually verify all critical resources have disaster recovery configured with alternate sites per CP-6/CP-7 requirements. Test quarterly per CP-10.",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { "CP-6", "CP-7", "CP-10" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                DetectedAt = DateTime.UtcNow
            });
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanBackupCapabilitiesAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning backup capabilities (CP-9, CP-10) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            // Get critical resources that should have backups
            var virtualMachines = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var sqlServers = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var storageAccounts = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            // Track resources without backup
            var vmsWithoutBackup = new List<string>();
            var sqlServersWithoutBackup = new List<string>();
            var storageWithoutBackup = new List<string>();

            // CHECK 1: Query each VM for backup protection status
            _logger.LogInformation("Checking backup status for {VmCount} VMs", virtualMachines.Count);
            
            foreach (var vm in virtualMachines)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)vm).Data.Id! ?? "");
                    var vmResource = armClient?.GetGenericResource(resourceId);
                    var vmData = await vmResource.GetAsync(cancellationToken);
                    
                    // Query backup protection status
                    // Azure Backup protection is registered via Recovery Services vault
                    // Check if VM has backup extension installed (Microsoft.Azure.RecoveryServices)
                    var properties = JsonDocument.Parse(vmData.Value.Data.Properties.ToStream());
                    
                    bool hasBackupExtension = false;
                    if (properties.RootElement.TryGetProperty("storageProfile", out var storageProfile))
                    {
                        // VM extensions are checked separately - we'll mark VMs without backup
                        // In production, query the Recovery Services vault's protected items
                        hasBackupExtension = false; // Default to not protected unless verified
                    }
                    
                    if (!hasBackupExtension)
                    {
                        vmsWithoutBackup.Add($"{((GenericResource)vm).Data.Name} ({((GenericResource)vm).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query backup status for VM {VmId}", ((GenericResource)vm).Data.Id!);
                    vmsWithoutBackup.Add($"{((GenericResource)vm).Data.Name} (status unknown)");
                }
            }

            // CHECK 2: Query SQL Servers for backup configuration
            _logger.LogInformation("Checking backup status for {SqlCount} SQL Servers", sqlServers.Count);
            
            foreach (var sqlServer in sqlServers)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)sqlServer).Data.Id! ?? "");
                    var sqlResource = armClient?.GetGenericResource(resourceId);
                    var sqlData = await sqlResource.GetAsync(cancellationToken);
                    
                    // SQL databases have built-in automatic backups, but check retention
                    // Query databases under this server
                    var serverResourceId = ((GenericResource)sqlServer).Data.Id!?.ToString()?.TrimEnd('/') ?? "";
                    var databasesUri = $"{serverResourceId}/databases?api-version=2021-11-01";
                    
                    // Simplified check - in production, verify backup retention policy
                    // Default retention is 7 days, FedRAMP/DoD requires 90 days minimum
                    sqlServersWithoutBackup.Add($"{((GenericResource)sqlServer).Data.Name} (retention policy needs manual verification)");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query backup status for SQL Server {SqlId}", ((GenericResource)sqlServer).Data.Id!);
                    sqlServersWithoutBackup.Add($"{((GenericResource)sqlServer).Data.Name} (status unknown)");
                }
            }

            // CHECK 3: Query Storage Accounts for geo-redundancy and soft delete
            _logger.LogInformation("Checking backup/redundancy for {StorageCount} Storage Accounts", storageAccounts.Count);
            
            foreach (var storage in storageAccounts)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)storage).Data.Id! ?? "");
                    var storageResource = armClient?.GetGenericResource(resourceId);
                    var storageData = await storageResource.GetAsync(cancellationToken);
                    
                    var properties = JsonDocument.Parse(storageData.Value.Data.Properties.ToStream());
                    
                    // Check SKU for geo-redundancy (GRS, RAGRS, GZRS, RAGZRS)
                    var sku = storageData.Value.Data.Properties.ToObjectFromJson<JsonElement>();
                    bool isGeoRedundant = false;
                    bool hasSoftDelete = false;
                    
                    if (sku.TryGetProperty("sku", out var skuElement) &&
                        skuElement.TryGetProperty("name", out var skuName))
                    {
                        var skuValue = skuName.GetString() ?? "";
                        isGeoRedundant = skuValue.Contains("GRS", StringComparison.OrdinalIgnoreCase) ||
                                        skuValue.Contains("GZRS", StringComparison.OrdinalIgnoreCase);
                    }
                    
                    // Check blob soft delete
                    if (properties.RootElement.TryGetProperty("deleteRetentionPolicy", out var retentionPolicy) &&
                        retentionPolicy.TryGetProperty("enabled", out var enabled))
                    {
                        hasSoftDelete = enabled.GetBoolean();
                    }
                    
                    if (!isGeoRedundant || !hasSoftDelete)
                    {
                        var issues = new List<string>();
                        if (!isGeoRedundant) issues.Add("not geo-redundant");
                        if (!hasSoftDelete) issues.Add("soft delete disabled");
                        storageWithoutBackup.Add($"{((GenericResource)storage).Data.Name} ({string.Join(", ", issues)})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query redundancy for Storage Account {StorageId}", ((GenericResource)storage).Data.Id!);
                    storageWithoutBackup.Add($"{((GenericResource)storage).Data.Name} (status unknown)");
                }
            }

            // Generate findings based on backup status
            var totalCriticalResources = virtualMachines.Count + sqlServers.Count + storageAccounts.Count;
            var totalUnprotected = vmsWithoutBackup.Count + sqlServersWithoutBackup.Count + storageWithoutBackup.Count;

            if (totalUnprotected > 0)
            {
                var severity = totalUnprotected > (totalCriticalResources * 0.5) ? AtoFindingSeverity.Critical : 
                               totalUnprotected > (totalCriticalResources * 0.2) ? AtoFindingSeverity.High : AtoFindingSeverity.Medium;

                var description = $"Found {totalUnprotected} of {totalCriticalResources} critical resources without proper backup/redundancy configuration. " +
                                 $"CP-9 requires system backups to be conducted and maintained. ";

                if (vmsWithoutBackup.Any())
                {
                    description += $"\n\n**VMs without Azure Backup ({vmsWithoutBackup.Count})**: {string.Join(", ", vmsWithoutBackup.Take(10))}";
                    if (vmsWithoutBackup.Count > 10) description += $" and {vmsWithoutBackup.Count - 10} more";
                }

                if (sqlServersWithoutBackup.Any())
                {
                    description += $"\n\n**SQL Servers needing retention verification ({sqlServersWithoutBackup.Count})**: {string.Join(", ", sqlServersWithoutBackup.Take(5))}";
                    if (sqlServersWithoutBackup.Count > 5) description += $" and {sqlServersWithoutBackup.Count - 5} more";
                }

                if (storageWithoutBackup.Any())
                {
                    description += $"\n\n**Storage Accounts without geo-redundancy/soft delete ({storageWithoutBackup.Count})**: {string.Join(", ", storageWithoutBackup.Take(5))}";
                    if (storageWithoutBackup.Count > 5) description += $" and {storageWithoutBackup.Count - 5} more";
                }

                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Multiple",
                    ResourceName = "Backup Configuration",
                    FindingType = AtoFindingType.ContingencyPlanning,
                    Severity = severity,
                    Title = $"Insufficient Backup Protection: {totalUnprotected}/{totalCriticalResources} Resources Not Protected",
                    Description = description,
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per CP-9 (System Backup):

1. **Enable Azure Backup for All VMs**:
   - Azure Portal → Recovery Services vault → Backup
   - Select VMs to protect
   - Configure backup policy:
     - Retention: Minimum 90 days (FedRAMP/DoD requirement)
     - Frequency: Daily (minimum)
     - Instant restore snapshot: 2-5 days
     - Long-term retention: Weekly (12 weeks), Monthly (12 months), Yearly (7 years)
   - Enable encryption at rest for backup data (automatic with RSV)

2. **Verify SQL Database Backup Retention**:
   - Azure Portal → SQL Database → Automated backups
   - Configure long-term retention (LTR):
     - Weekly backups: 12 weeks minimum
     - Monthly backups: 12 months minimum
     - Yearly backups: 7 years (DoD requirement)
   - Point-in-time restore (PITR): 7-35 days retention
   - Geo-redundant backups: Enabled (default for GRS)

3. **Enable Storage Account Geo-Redundancy**:
   - Azure Portal → Storage Account → Configuration → Replication
   - Change SKU to GRS (Geo-Redundant Storage) or GZRS (Geo-Zone-Redundant)
   - GRS: Data replicated to secondary region (async)
   - RAGRS: Read access to secondary region
   - GZRS: Zone-redundancy + geo-redundancy (highest durability)

4. **Enable Blob Soft Delete and Versioning**:
   - Azure Portal → Storage Account → Data protection
   - Enable soft delete for blobs: 90 days retention
   - Enable soft delete for containers: 90 days retention
   - Enable blob versioning for critical data
   - Enable point-in-time restore for containers

5. **Implement Azure Site Recovery (ASR)**:
   - For critical VMs, enable continuous replication to secondary region
   - ASR provides: RPO ~5 minutes, RTO ~2 hours
   - Test failover quarterly (CP-10 requirement)
   - Document recovery procedures in contingency plan

6. **Configure Backup Alerts**:
   - Azure Monitor → Alerts → Backup Alerts
   - Alert on backup failures (immediate notification)
   - Alert on backup job duration anomalies
   - Alert on missed backups
   - Send to security team distribution list

7. **Test Backup Restore Procedures**:
   - Quarterly: Test VM restore to alternate location
   - Monthly: Test SQL database restore (sample databases)
   - Annually: Full disaster recovery exercise
   - Document restore times (RTO metrics)
   - Update contingency plan with lessons learned

8. **Implement Immutable Backups (Ransomware Protection)**:
   - Azure Backup: Enable Soft Delete (14-day minimum)
   - Azure Backup: Enable Multi-User Authorization (MUA) for critical vaults
   - SQL LTR: Backups are immutable by design
   - Storage: Enable immutability policies on backup containers

9. **Encrypt Backups**:
   - Azure Backup: Automatic encryption with platform-managed keys
   - For customer-managed keys: Use Azure Key Vault with BYOK
   - SQL TDE: Encrypted databases produce encrypted backups
   - Storage: Server-side encryption (SSE) enabled by default

10. **Document Backup Strategy in SSP**:
    - Backup frequency and retention for each resource type
    - Recovery Time Objective (RTO): Target 4 hours for critical systems
    - Recovery Point Objective (RPO): Target 1 hour for critical systems
    - Backup testing schedule and results
    - Roles and responsibilities for backup operations
    - Integration with incident response procedures

BACKUP RETENTION REQUIREMENTS (FedRAMP/DoD):
- Incremental: Daily, 90 days minimum
- Full: Weekly, 12 weeks minimum  
- Monthly: 12 months minimum
- Yearly: 7 years minimum (audit requirement)
- Off-site/geo-redundant: Required for all critical systems

REFERENCES:
- NIST 800-53 CP-9: System Backup
- NIST 800-53 CP-10: System Recovery and Reconstitution
- FedRAMP Backup Requirements: 90-day minimum retention
- DoD Cloud Computing SRG: Geo-redundant backups required (IL4+)
- Azure Backup Best Practices: https://docs.microsoft.com/azure/backup/",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CP-9", "CP-10" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else if (totalCriticalResources > 0)
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Multiple",
                    ResourceName = "Backup Configuration",
                    FindingType = AtoFindingType.ContingencyPlanning,
                    Severity = AtoFindingSeverity.Informational,
                    Title = $"Backup Capabilities Configured: {totalCriticalResources} Resources Protected",
                    Description = $"All {totalCriticalResources} critical resources have backup or redundancy configured. " +
                                 $"This includes {virtualMachines.Count} VMs, {sqlServers.Count} SQL servers, and {storageAccounts.Count} storage accounts. " +
                                 $"CP-9 backup requirements are met.",
                    Recommendation = @"MAINTAIN CURRENT BACKUP POSTURE per CP-9:

1. **Quarterly Backup Testing**: Test restore procedures for each resource type
2. **Monitor Backup Success Rates**: Target >99% successful backups
3. **Review Retention Policies**: Ensure 90-day minimum retention maintained
4. **Annual Disaster Recovery Exercise**: Test full system recovery (CP-10)
5. **Update Contingency Plan**: Document any changes to backup procedures
6. **Verify Geo-Redundancy**: Confirm secondary region availability
7. **Audit Backup Logs**: Review backup audit logs monthly
8. **Maintain Backup Documentation**: Keep SSP backup section current

Continue quarterly reviews to ensure compliance is maintained.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CP-9" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning backup capabilities for subscription {SubscriptionId}", subscriptionId);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Backup Scan",
                FindingType = AtoFindingType.ContingencyPlanning,
                Severity = AtoFindingSeverity.High,
                Title = "Backup Scan Error - Manual Review Required",
                Description = $"Could not complete automated backup scan: {ex.Message}. Manual review required to verify CP-9 compliance.",
                Recommendation = "Manually verify all critical resources have backup configured with 90-day minimum retention per CP-9 requirements.",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "CP-9" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                DetectedAt = DateTime.UtcNow
            });
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanGenericContingencyAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Contingency Planning Review",
                FindingType = AtoFindingType.ContingencyPlanning,
                Severity = AtoFindingSeverity.Informational,
                Title = $"Control {control.Id} - Manual Review Required",
                Description = control.Title ?? "Contingency planning control requires manual review",
                Recommendation = "Review contingency planning documentation and ensure procedures are in place",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "CP" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning generic contingency planning for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}
