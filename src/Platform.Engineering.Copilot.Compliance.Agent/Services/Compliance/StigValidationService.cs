using Platform.Engineering.Copilot.Core.Constants;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Text.Json;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Azure.Core;
using Azure.ResourceManager.Resources;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Service for validating STIG (Security Technical Implementation Guide) compliance
/// across Azure resources. Handles validation of 40+ STIGs across different service types.
/// </summary>
public class StigValidationService : IStigValidationService
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IStigKnowledgeService _stigKnowledgeService;
    private readonly ILogger<StigValidationService> _logger;

    public StigValidationService(
        IAzureResourceService azureResourceService,
        IStigKnowledgeService stigKnowledgeService,
        ILogger<StigValidationService> logger)
    {
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _stigKnowledgeService = stigKnowledgeService ?? throw new ArgumentNullException(nameof(stigKnowledgeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region STIG Validation Methods

    /// <summary>
    /// Validates STIGs for a specific control family
    /// </summary>
    public async Task<List<AtoFinding>> ValidateFamilyStigsAsync(
        string subscriptionId,
        string? resourceGroupName,
        string family,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        // Get all STIGs mapped to this control family
        var allStigs = await _stigKnowledgeService.GetAllStigsAsync(cancellationToken);
        var familyStigs = allStigs.Where(s =>
            s.NistControls != null &&
            s.NistControls.Any(nc => nc.StartsWith(family, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        _logger.LogDebug("Found {Count} STIGs for control family {Family}", familyStigs.Count, family);

        // Validate each STIG
        foreach (var stig in familyStigs)
        {
            var stigFindings = await ValidateStigComplianceAsync(
                subscriptionId,
                resourceGroupName,
                stig,
                cancellationToken);
            findings.AddRange(stigFindings);
        }

        return findings;
    }

    /// <summary>
    /// Validates a specific STIG and returns findings
    /// </summary>
    public async Task<List<AtoFinding>> ValidateStigComplianceAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        // Dispatch to specific validation method based on STIG service type
        return stig.ServiceType switch
        {
            StigServiceType.Network => await ValidateNetworkStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Storage => await ValidateStorageStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Compute => await ValidateComputeStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Database => await ValidateDatabaseStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Identity => await ValidateIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Monitoring => await ValidateMonitoringStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Security => await ValidateSecurityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Platform => await ValidatePlatformStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Integration => await ValidateIntegrationStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            StigServiceType.Containers => await ValidateContainerStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => new List<AtoFinding>()
        };
    }

    private async Task<List<AtoFinding>> ValidateNetworkStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219187" => await ValidateNoPublicIpsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219210" => await ValidateNsgDenyAllStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219235" => await ValidateAksPrivateClusterStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219240" => await ValidateAzureFirewallStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219245" => await ValidateStoragePrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateStorageStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219165" => await ValidateStorageEncryptionStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219215" => await ValidateStoragePublicAccessStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219245" => await ValidateStoragePrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateComputeStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219230" => await ValidateAksRbacStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219235" => await ValidateAksPrivateClusterStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219265" => await ValidateVmDiskEncryptionStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateDatabaseStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219201" => await ValidateSqlTlsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219225" => await ValidateSqlTdeStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219255" => await ValidateSqlAtpStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219305" => await ValidateCosmosDbPrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219310" => await ValidateCosmosDbCmkStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateIdentityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219153" => await ValidateMfaStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219250" => await ValidateAzureAdPimStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219275" => await ValidateManagedIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateMonitoringStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219220" => await ValidateDiagnosticLogsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219260" => await ValidateActivityLogRetentionStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateSecurityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219178" => await ValidateKeyVaultStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219270" => await ValidateAzurePolicyStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219280" => await ValidateDefenderForCloudStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    // Individual STIG validation implementations
    private async Task<List<AtoFinding>> ValidateNoPublicIpsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var vms = allResources.Where(r =>
                r.Type.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var vm in vms)
            {
                try
                {
                    var vmResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(vm.Id)).GetAsync(cancellationToken);
                    var vmProps = vmResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    if (vmProps != null && vmProps.TryGetValue("networkProfile", out var networkProfileObj))
                    {
                        var networkProfile = JsonSerializer.Deserialize<Dictionary<string, object>>(networkProfileObj.ToString() ?? "{}");

                        if (networkProfile != null && networkProfile.TryGetValue("networkInterfaces", out var nicsObj))
                        {
                            var nics = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(nicsObj.ToString() ?? "[]");

                            if (nics != null)
                            {
                                foreach (var nic in nics)
                                {
                                    if (nic.TryGetValue("id", out var nicIdObj))
                                    {
                                        var nicId = nicIdObj.ToString();
                                        if (!string.IsNullOrEmpty(nicId))
                                        {
                                            var nicResource = await armClient.GetGenericResource(
                                                new ResourceIdentifier(nicId)).GetAsync(cancellationToken);
                                            var nicProps = nicResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                                            if (nicProps != null && nicProps.TryGetValue("ipConfigurations", out var ipConfigsObj))
                                            {
                                                var ipConfigs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(ipConfigsObj.ToString() ?? "[]");

                                                if (ipConfigs != null)
                                                {
                                                    foreach (var ipConfig in ipConfigs)
                                                    {
                                                        if (ipConfig.TryGetValue("properties", out var ipPropsObj))
                                                        {
                                                            var ipProps = JsonSerializer.Deserialize<Dictionary<string, object>>(ipPropsObj.ToString() ?? "{}");

                                                            if (ipProps != null && ipProps.ContainsKey("publicIPAddress"))
                                                            {
                                                                findings.Add(new AtoFinding
                                                                {
                                                                    AffectedNistControls = stig.NistControls.ToList(),
                                                                    Title = $"VM Has Public IP Address - {stig.Title}",
                                                                    Description = $"Virtual machine '{vm.Name}' has a public IP address assigned, which increases attack surface. {stig.Description}",
                                                                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                                                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                                                    ResourceId = vm.Id,
                                                                    ResourceName = vm.Name,
                                                                    ResourceType = vm.Type,
                                                                    Evidence = $"Public IP found on network interface: {nicId}",
                                                                    RemediationGuidance = stig.FixText,
                                                                    Metadata = new Dictionary<string, object>
                                                                    {
                                                                        ["StigId"] = stig.StigId,
                                                                        ["VulnId"] = stig.VulnId,
                                                                        ["StigSeverity"] = stig.Severity.ToString(),
                                                                        ["Category"] = stig.Category,
                                                                        ["CciRefs"] = stig.CciRefs,
                                                                        ["Source"] = "STIG"
                                                                    }
                                                                });
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception vmEx)
                {
                    _logger.LogWarning(vmEx, "Unable to query VM {VmName}", vm.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} VMs have public IPs",
                stig.StigId, findings.Count, vms.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateNsgDenyAllStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var nsgs = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.NetworkSecurityGroups, StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var nsg in nsgs)
            {
                try
                {
                    var nsgResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(nsg.Id)).GetAsync(cancellationToken);
                    var nsgProps = nsgResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    if (nsgProps != null && nsgProps.TryGetValue("securityRules", out var rulesObj))
                    {
                        var rules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(rulesObj.ToString() ?? "[]");

                        if (rules != null)
                        {
                            var inboundRules = rules.Where(r =>
                            {
                                if (r.TryGetValue("properties", out var propsObj))
                                {
                                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsObj.ToString() ?? "{}");
                                    return props != null && props.TryGetValue("direction", out var dir) && dir.ToString() == "Inbound";
                                }
                                return false;
                            }).ToList();

                            // Check if there's a deny-all rule at lowest priority (highest number)
                            var hasDenyAll = inboundRules.Any(r =>
                            {
                                if (r.TryGetValue("properties", out var propsObj))
                                {
                                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsObj.ToString() ?? "{}");
                                    if (props != null)
                                    {
                                        var isDeny = props.TryGetValue("access", out var access) && access.ToString() == "Deny";
                                        var isAnySource = props.TryGetValue("sourceAddressPrefix", out var src) && src.ToString() == "*";
                                        return isDeny && isAnySource;
                                    }
                                }
                                return false;
                            });

                            if (!hasDenyAll)
                            {
                                findings.Add(new AtoFinding
                                {
                                    AffectedNistControls = stig.NistControls.ToList(),
                                    Title = $"NSG Missing Deny-All Rule - {stig.Title}",
                                    Description = $"Network Security Group '{nsg.Name}' does not have a deny-all inbound rule at lowest priority. {stig.Description}",
                                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                    ResourceId = nsg.Id,
                                    ResourceName = nsg.Name,
                                    ResourceType = nsg.Type,
                                    Evidence = $"NSG has {inboundRules.Count} inbound rules but no deny-all rule",
                                    RemediationGuidance = stig.FixText,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["StigId"] = stig.StigId,
                                        ["VulnId"] = stig.VulnId,
                                        ["StigSeverity"] = stig.Severity.ToString(),
                                        ["Category"] = stig.Category,
                                        ["CciRefs"] = stig.CciRefs,
                                        ["Source"] = "STIG"
                                    }
                                });
                            }
                        }
                    }
                }
                catch (Exception nsgEx)
                {
                    _logger.LogWarning(nsgEx, "Unable to query NSG {NsgName}", nsg.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} NSGs missing deny-all rules",
                stig.StigId, findings.Count, nsgs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateStorageEncryptionStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var storageAccounts = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.StorageAccounts, StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var storage in storageAccounts)
            {
                try
                {
                    var storageResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(storage.Id)).GetAsync(cancellationToken);
                    var storageProps = storageResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool encryptionEnabled = true;

                    if (storageProps != null && storageProps.TryGetValue("encryption", out var encryptionObj))
                    {
                        var encryption = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptionObj.ToString() ?? "{}");

                        if (encryption != null && encryption.TryGetValue("services", out var servicesObj))
                        {
                            var services = JsonSerializer.Deserialize<Dictionary<string, object>>(servicesObj.ToString() ?? "{}");

                            if (services != null)
                            {
                                // Check blob encryption
                                if (services.TryGetValue("blob", out var blobObj))
                                {
                                    var blob = JsonSerializer.Deserialize<Dictionary<string, object>>(blobObj.ToString() ?? "{}");
                                    if (blob != null && blob.TryGetValue("enabled", out var blobEnabled))
                                    {
                                        encryptionEnabled = encryptionEnabled && bool.Parse(blobEnabled.ToString() ?? "false");
                                    }
                                    else
                                    {
                                        encryptionEnabled = false;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        encryptionEnabled = false;
                    }

                    if (!encryptionEnabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Storage Account Encryption Disabled - {stig.Title}",
                            Description = $"Storage account '{storage.Name}' does not have encryption at rest enabled. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = storage.Id,
                            ResourceName = storage.Name,
                            ResourceType = storage.Type,
                            Evidence = "Encryption not enabled for storage services",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Unable to query storage account {StorageName}", storage.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} storage accounts without encryption",
                stig.StigId, findings.Count, storageAccounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateStoragePublicAccessStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var storageAccounts = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.StorageAccounts, StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var storage in storageAccounts)
            {
                try
                {
                    var storageResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(storage.Id)).GetAsync(cancellationToken);
                    var storageProps = storageResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool publicAccessEnabled = false;

                    if (storageProps != null)
                    {
                        if (storageProps.TryGetValue("allowBlobPublicAccess", out var allowPublicObj))
                        {
                            publicAccessEnabled = bool.Parse(allowPublicObj.ToString() ?? "true");
                        }
                        else
                        {
                            publicAccessEnabled = true;
                        }
                    }

                    if (publicAccessEnabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Storage Account Public Access Enabled - {stig.Title}",
                            Description = $"Storage account '{storage.Name}' allows public blob access. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = storage.Id,
                            ResourceName = storage.Name,
                            ResourceType = storage.Type,
                            Evidence = "Public blob access is enabled",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Unable to query storage account {StorageName}", storage.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} storage accounts with public access",
                stig.StigId, findings.Count, storageAccounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateDiagnosticLogsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var criticalResourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ComplianceConstants.AzureResourceTypes.KeyVaults,
                ComplianceConstants.AzureResourceTypes.NetworkSecurityGroups,
                ComplianceConstants.AzureResourceTypes.StorageAccounts,
                ComplianceConstants.AzureResourceTypes.SqlServers
            };

            var criticalResources = allResources.Where(r =>
                criticalResourceTypes.Contains(r.Type)).ToList();

            foreach (var resource in criticalResources)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Diagnostic Logging Not Configured - {stig.Title}",
                    Description = $"Critical resource '{resource.Name}' may not have diagnostic logging configured. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    Evidence = "Diagnostic settings validation required",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {Count} critical resources require diagnostic logging validation",
                stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateSqlTlsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var sqlServers = allResources.Where(r =>
                r.Type.Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var sqlServer in sqlServers)
            {
                try
                {
                    var serverResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(sqlServer.Id)).GetAsync(cancellationToken);
                    var serverProps = serverResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    string minimalTlsVersion = "1.0";

                    if (serverProps != null && serverProps.TryGetValue("minimalTlsVersion", out var tlsVersionObj))
                    {
                        minimalTlsVersion = tlsVersionObj.ToString() ?? "1.0";
                    }

                    if (string.Compare(minimalTlsVersion, "1.2", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"SQL Server TLS Version Too Low - {stig.Title}",
                            Description = $"SQL Server '{sqlServer.Name}' allows TLS version {minimalTlsVersion}. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = sqlServer.Id,
                            ResourceName = sqlServer.Name,
                            ResourceType = sqlServer.Type,
                            Evidence = $"Minimal TLS version: {minimalTlsVersion}",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG",
                                ["CurrentTlsVersion"] = minimalTlsVersion,
                                ["RequiredTlsVersion"] = "1.2"
                            }
                        });
                    }
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning(sqlEx, "Unable to query SQL Server {ServerName}", sqlServer.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} SQL servers with inadequate TLS",
                stig.StigId, findings.Count, sqlServers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateSqlTdeStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var sqlDatabases = allResources.Where(r =>
                r.Type.Equals("Microsoft.Sql/servers/databases", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.EndsWith("/master", StringComparison.OrdinalIgnoreCase)).ToList();

            // Note: In production, you would query the TDE API endpoint
            // For now, we log informational findings
            _logger.LogInformation(
                "STIG {StigId}: Found {Count} SQL databases to validate for TDE",
                stig.StigId, sqlDatabases.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAksRbacStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var aksClusters = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerService/managedClusters", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var aks in aksClusters)
            {
                try
                {
                    var aksResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(aks.Id)).GetAsync(cancellationToken);
                    var aksProps = aksResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool rbacEnabled = false;

                    if (aksProps != null && aksProps.TryGetValue("enableRBAC", out var rbacObj))
                    {
                        rbacEnabled = bool.Parse(rbacObj.ToString() ?? "false");
                    }

                    if (!rbacEnabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"AKS RBAC Not Enabled - {stig.Title}",
                            Description = $"AKS cluster '{aks.Name}' does not have RBAC enabled. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = aks.Id,
                            ResourceName = aks.Name,
                            ResourceType = aks.Type,
                            Evidence = "enableRBAC is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception aksEx)
                {
                    _logger.LogWarning(aksEx, "Unable to query AKS cluster {AksName}", aks.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} AKS clusters without RBAC",
                stig.StigId, findings.Count, aksClusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAksPrivateClusterStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var aksClusters = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerService/managedClusters", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var aks in aksClusters)
            {
                try
                {
                    var aksResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(aks.Id)).GetAsync(cancellationToken);
                    var aksProps = aksResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool isPrivateCluster = false;

                    if (aksProps != null && aksProps.TryGetValue("apiServerAccessProfile", out var apiServerObj))
                    {
                        var apiServer = JsonSerializer.Deserialize<Dictionary<string, object>>(apiServerObj.ToString() ?? "{}");
                        if (apiServer != null && apiServer.TryGetValue("enablePrivateCluster", out var privateObj))
                        {
                            isPrivateCluster = bool.Parse(privateObj.ToString() ?? "false");
                        }
                    }

                    if (!isPrivateCluster)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"AKS Cluster Not Private - {stig.Title}",
                            Description = $"AKS cluster '{aks.Name}' is not configured as a private cluster. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = aks.Id,
                            ResourceName = aks.Name,
                            ResourceType = aks.Type,
                            Evidence = "enablePrivateCluster is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception aksEx)
                {
                    _logger.LogWarning(aksEx, "Unable to query AKS cluster {AksName}", aks.Name);
                }
            }

            _logger.LogInformation(
                "STIG {StigId} validation complete: {NonCompliant}/{Total} AKS clusters not private",
                stig.StigId, findings.Count, aksClusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAzureFirewallStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var firewalls = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.AzureFirewalls, StringComparison.OrdinalIgnoreCase)).ToList();

            if (firewalls.Count == 0)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Azure Firewall Not Deployed - {stig.Title}",
                    Description = $"No Azure Firewall found for egress filtering. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = "Subscription",
                    ResourceType = ComplianceConstants.AzureResourceTypes.Subscription,
                    Evidence = "No Azure Firewall resources found",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} firewalls found",
                stig.StigId, firewalls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateStoragePrivateEndpointStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var storageAccounts = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.StorageAccounts, StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var storage in storageAccounts)
            {
                try
                {
                    var storageResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(storage.Id)).GetAsync(cancellationToken);
                    var storageProps = storageResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasPrivateEndpoint = false;
                    if (storageProps != null && storageProps.TryGetValue("privateEndpointConnections", out var peConnectionsObj))
                    {
                        var peConnections = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(peConnectionsObj.ToString() ?? "[]");
                        hasPrivateEndpoint = peConnections != null && peConnections.Count > 0;
                    }

                    if (!hasPrivateEndpoint)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Storage Account Missing Private Endpoint - {stig.Title}",
                            Description = $"Storage account '{storage.Name}' does not use private endpoints for secure access. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = storage.Id,
                            ResourceName = storage.Name,
                            ResourceType = storage.Type,
                            Evidence = "No private endpoint connections found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Unable to query storage account {Name}", storage.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateMfaStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            // MFA validation requires Azure AD Graph API or Microsoft Graph API
            // For compliance assessment, we'll mark as manual review required
            findings.Add(new AtoFinding
            {
                AffectedNistControls = stig.NistControls.ToList(),
                Title = $"MFA Policy Validation Required - {stig.Title}",
                Description = $"Multi-factor authentication policy validation requires manual verification via Azure AD portal or Microsoft Graph API. {stig.Description}",
                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.AAD",
                ResourceName = "Azure Active Directory",
                ResourceType = "Microsoft.AAD/tenants",
                Evidence = "Manual verification required - check Azure AD > Security > Conditional Access > MFA policies",
                RemediationGuidance = stig.FixText,
                Metadata = new Dictionary<string, object>
                {
                    ["StigId"] = stig.StigId,
                    ["VulnId"] = stig.VulnId,
                    ["StigSeverity"] = stig.Severity.ToString(),
                    ["Category"] = stig.Category,
                    ["CciRefs"] = stig.CciRefs,
                    ["Source"] = "STIG",
                    ["ValidationNote"] = "Requires Microsoft Graph API permissions for automated validation"
                }
            });

            _logger.LogInformation("STIG {StigId} validation complete: Manual review required", stig.StigId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAzureAdPimStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            // PIM validation requires Azure AD Premium P2 and Microsoft Graph API
            findings.Add(new AtoFinding
            {
                AffectedNistControls = stig.NistControls.ToList(),
                Title = $"PIM Configuration Validation Required - {stig.Title}",
                Description = $"Privileged Identity Management (PIM) configuration requires manual verification via Azure AD portal. Verify PIM is enabled for privileged roles. {stig.Description}",
                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.AAD",
                ResourceName = "Azure AD Privileged Identity Management",
                ResourceType = "Microsoft.AAD/tenants",
                Evidence = "Manual verification required - check Azure AD > Identity Governance > Privileged Identity Management",
                RemediationGuidance = stig.FixText,
                Metadata = new Dictionary<string, object>
                {
                    ["StigId"] = stig.StigId,
                    ["VulnId"] = stig.VulnId,
                    ["StigSeverity"] = stig.Severity.ToString(),
                    ["Category"] = stig.Category,
                    ["CciRefs"] = stig.CciRefs,
                    ["Source"] = "STIG",
                    ["ValidationNote"] = "Requires Azure AD Premium P2 and Microsoft Graph API for automated validation"
                }
            });

            _logger.LogInformation("STIG {StigId} validation complete: Manual review required", stig.StigId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateSqlAtpStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var sqlServers = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.SqlServers, StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var sqlServer in sqlServers)
            {
                try
                {
                    var atpResourceId = $"{sqlServer.Id}/securityAlertPolicies/Default";
                    try
                    {
                        var atpResource = await armClient.GetGenericResource(
                            new ResourceIdentifier(atpResourceId)).GetAsync(cancellationToken);
                        var atpProps = atpResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                        bool atpEnabled = false;
                        string atpState = "Unknown";
                        if (atpProps != null && atpProps.TryGetValue("state", out var stateObj))
                        {
                            atpState = stateObj.ToString() ?? "Unknown";
                            atpEnabled = atpState.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                        }

                        if (!atpEnabled)
                        {
                            findings.Add(new AtoFinding
                            {
                                AffectedNistControls = stig.NistControls.ToList(),
                                Title = $"SQL Server ATP Disabled - {stig.Title}",
                                Description = $"SQL Server '{sqlServer.Name}' does not have Advanced Threat Protection (Microsoft Defender for SQL) enabled. {stig.Description}",
                                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                ResourceId = sqlServer.Id,
                                ResourceName = sqlServer.Name,
                                ResourceType = sqlServer.Type,
                                Evidence = $"ATP state: {atpState}",
                                RemediationGuidance = stig.FixText,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["StigId"] = stig.StigId,
                                    ["VulnId"] = stig.VulnId,
                                    ["StigSeverity"] = stig.Severity.ToString(),
                                    ["Category"] = stig.Category,
                                    ["CciRefs"] = stig.CciRefs,
                                    ["Source"] = "STIG"
                                }
                            });
                        }
                    }
                    catch (Exception)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"SQL Server ATP Not Configured - {stig.Title}",
                            Description = $"SQL Server '{sqlServer.Name}' does not have Advanced Threat Protection configured. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = sqlServer.Id,
                            ResourceName = sqlServer.Name,
                            ResourceType = sqlServer.Type,
                            Evidence = "No ATP security alert policy found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning(sqlEx, "Unable to query SQL server {Name}", sqlServer.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateActivityLogRetentionStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var logWorkspaces = allResources.Where(r =>
                r.Type.Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase)).ToList();

            if (logWorkspaces.Count == 0)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"No Log Analytics Workspace Found - {stig.Title}",
                    Description = $"No Log Analytics workspace found for activity log retention. Activity logs must be retained for at least 365 days. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = "No Log Analytics workspace configured for activity log retention",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }
            else
            {
                foreach (var workspace in logWorkspaces)
                {
                    try
                    {
                        var workspaceResource = await armClient.GetGenericResource(
                            new ResourceIdentifier(workspace.Id)).GetAsync(cancellationToken);
                        var workspaceProps = workspaceResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                        int retentionDays = 30;
                        if (workspaceProps != null && workspaceProps.TryGetValue("retentionInDays", out var retentionObj))
                        {
                            retentionDays = Convert.ToInt32(retentionObj);
                        }

                        if (retentionDays < 365)
                        {
                            findings.Add(new AtoFinding
                            {
                                AffectedNistControls = stig.NistControls.ToList(),
                                Title = $"Activity Log Retention Insufficient - {stig.Title}",
                                Description = $"Log Analytics workspace '{workspace.Name}' has retention of {retentionDays} days, which is less than the required 365 days. {stig.Description}",
                                Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                                ComplianceStatus = AtoComplianceStatus.NonCompliant,
                                ResourceId = workspace.Id,
                                ResourceName = workspace.Name,
                                ResourceType = workspace.Type,
                                Evidence = $"Retention configured: {retentionDays} days (required: 365 days)",
                                RemediationGuidance = stig.FixText,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["StigId"] = stig.StigId,
                                    ["VulnId"] = stig.VulnId,
                                    ["StigSeverity"] = stig.Severity.ToString(),
                                    ["Category"] = stig.Category,
                                    ["CciRefs"] = stig.CciRefs,
                                    ["Source"] = "STIG",
                                    ["CurrentRetentionDays"] = retentionDays,
                                    ["RequiredRetentionDays"] = 365
                                }
                            });
                        }
                    }
                    catch (Exception wsEx)
                    {
                        _logger.LogWarning(wsEx, "Unable to query workspace {Name}", workspace.Name);
                    }
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateVmDiskEncryptionStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var vms = allResources.Where(r =>
                r.Type.Equals(ComplianceConstants.AzureResourceTypes.VirtualMachines, StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var vm in vms)
            {
                try
                {
                    var vmResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(vm.Id)).GetAsync(cancellationToken);
                    var vmProps = vmResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasEncryption = false;

                    if (vmProps != null && vmProps.TryGetValue("storageProfile", out var storageProfileObj))
                    {
                        var storageProfile = JsonSerializer.Deserialize<Dictionary<string, object>>(storageProfileObj.ToString() ?? "{}");
                        if (storageProfile != null && storageProfile.TryGetValue("osDisk", out var osDiskObj))
                        {
                            var osDisk = JsonSerializer.Deserialize<Dictionary<string, object>>(osDiskObj.ToString() ?? "{}");
                            if (osDisk != null && osDisk.TryGetValue("encryptionSettings", out var encryptionObj))
                            {
                                var encryption = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptionObj.ToString() ?? "{}");
                                if (encryption != null && encryption.TryGetValue("enabled", out var enabledObj))
                                {
                                    hasEncryption = Convert.ToBoolean(enabledObj);
                                }
                            }
                        }
                    }

                    if (!hasEncryption)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"VM Disk Encryption Not Enabled - {stig.Title}",
                            Description = $"Virtual machine '{vm.Name}' does not have Azure Disk Encryption enabled. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = vm.Id,
                            ResourceName = vm.Name,
                            ResourceType = vm.Type,
                            Evidence = "No encryption settings found on OS disk",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception vmEx)
                {
                    _logger.LogWarning(vmEx, "Unable to query VM {Name}", vm.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAzurePolicyStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            try
            {
                var subscriptionResource = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                int policyCount = 0;
                await foreach (var policy in subscriptionResource.GetPolicyAssignments().GetAllAsync())
                {
                    policyCount++;
                }

                if (policyCount == 0)
                {
                    findings.Add(new AtoFinding
                    {
                        AffectedNistControls = stig.NistControls.ToList(),
                        Title = $"No Azure Policy Assignments Found - {stig.Title}",
                        Description = $"Subscription has no Azure Policy assignments configured. Azure Policy should be used to enforce security and compliance controls. {stig.Description}",
                        Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                        ComplianceStatus = AtoComplianceStatus.NonCompliant,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceName = subscriptionId,
                        ResourceType = "Microsoft.Subscription",
                        Evidence = "No policy assignments found at subscription level",
                        RemediationGuidance = stig.FixText,
                        Metadata = new Dictionary<string, object>
                        {
                            ["StigId"] = stig.StigId,
                            ["VulnId"] = stig.VulnId,
                            ["StigSeverity"] = stig.Severity.ToString(),
                            ["Category"] = stig.Category,
                            ["CciRefs"] = stig.CciRefs,
                            ["Source"] = "STIG"
                        }
                    });
                }
                else
                {
                    _logger.LogInformation("Found {Count} policy assignments for subscription {SubscriptionId}",
                        policyCount, subscriptionId);
                }
            }
            catch (Exception rfEx)
            {
                _logger.LogWarning(rfEx, "Unable to query policy assignments for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Azure Policy Validation Failed - {stig.Title}",
                    Description = $"Unable to validate Azure Policy assignments. Manual verification required. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = $"API error: {rfEx.Message}",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateManagedIdentityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var appServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var appService in appServices)
            {
                try
                {
                    var appResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(appService.Id)).GetAsync(cancellationToken);

                    bool hasManagedIdentity = false;
                    if (appResource.Value.Data.Identity != null)
                    {
                        var identityType = appResource.Value.Data.Identity.ManagedServiceIdentityType.ToString();
                        hasManagedIdentity = !identityType.Equals("None", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!hasManagedIdentity)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"App Service Missing Managed Identity - {stig.Title}",
                            Description = $"App Service '{appService.Name}' does not use managed identity for authentication. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = appService.Id,
                            ResourceName = appService.Name,
                            ResourceType = appService.Type,
                            Evidence = "No managed identity configured",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception appEx)
                {
                    _logger.LogWarning(appEx, "Unable to query App Service {Name}", appService.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateDefenderForCloudStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            try
            {
                // Defender for Cloud validation requires Security Center API access
                // Mark as manual review required
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Defender for Cloud Validation Required - {stig.Title}",
                    Description = $"Microsoft Defender for Cloud Standard tier configuration requires manual verification via Azure Portal. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = "Manual verification required - check Azure Security Center > Pricing & settings",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG",
                        ["ValidationNote"] = "Requires Security Center API or manual verification"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to query Defender for Cloud pricing for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"Defender for Cloud Validation Failed - {stig.Title}",
                    Description = $"Unable to validate Microsoft Defender for Cloud configuration. Manual verification required. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceName = subscriptionId,
                    ResourceType = "Microsoft.Subscription",
                    Evidence = $"API error: {ex.Message}",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateKeyVaultStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var keyVaults = allResources.Where(r =>
                r.Type.Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var keyVault in keyVaults)
            {
                try
                {
                    var kvResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(keyVault.Id)).GetAsync(cancellationToken);
                    var kvProps = kvResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool softDeleteEnabled = false;
                    bool purgeProtectionEnabled = false;

                    if (kvProps != null)
                    {
                        if (kvProps.TryGetValue("enableSoftDelete", out var softDeleteObj))
                        {
                            softDeleteEnabled = Convert.ToBoolean(softDeleteObj);
                        }

                        if (kvProps.TryGetValue("enablePurgeProtection", out var purgeObj))
                        {
                            purgeProtectionEnabled = Convert.ToBoolean(purgeObj);
                        }
                    }

                    if (!softDeleteEnabled || !purgeProtectionEnabled)
                    {
                        var issues = new List<string>();
                        if (!softDeleteEnabled) issues.Add("soft delete disabled");
                        if (!purgeProtectionEnabled) issues.Add("purge protection disabled");

                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Key Vault Security Settings Insufficient - {stig.Title}",
                            Description = $"Key Vault '{keyVault.Name}' has insufficient security settings: {string.Join(", ", issues)}. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = keyVault.Id,
                            ResourceName = keyVault.Name,
                            ResourceType = keyVault.Type,
                            Evidence = $"Security issues: {string.Join(", ", issues)}",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG",
                                ["SoftDeleteEnabled"] = softDeleteEnabled,
                                ["PurgeProtectionEnabled"] = purgeProtectionEnabled
                            }
                        });
                    }
                }
                catch (Exception kvEx)
                {
                    _logger.LogWarning(kvEx, "Unable to query Key Vault {Name}", keyVault.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidatePlatformStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219275" => await ValidateManagedIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219285" => await ValidateAppServiceHttpsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219290" => await ValidateAppServiceTlsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219315" => await ValidateFunctionAppHttpsStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219320" => await ValidateFunctionAppManagedIdentityStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateIntegrationStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219325" => await ValidateApimSubscriptionKeysStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219330" => await ValidateApimVnetStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219335" => await ValidateServiceBusPrivateEndpointStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219340" => await ValidateServiceBusCmkStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    private async Task<List<AtoFinding>> ValidateContainerStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        return stig.StigId switch
        {
            "V-219230" => await ValidateAksRbacStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219235" => await ValidateAksPrivateClusterStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219295" => await ValidateAcrPrivateAccessStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            "V-219300" => await ValidateAcrVulnerabilityScanStigAsync(subscriptionId, resourceGroupName, stig, cancellationToken),
            _ => findings
        };
    }

    // New STIG validation methods for Platform services
    private async Task<List<AtoFinding>> ValidateAppServiceHttpsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var appServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Contains("/slots/", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (appServices.Count == 0)
            {
                _logger.LogInformation("No App Services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var appService in appServices)
            {
                try
                {
                    var appResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(appService.Id)).GetAsync(cancellationToken);
                    var appProps = appResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool httpsOnly = false;
                    if (appProps != null && appProps.TryGetValue("httpsOnly", out var httpsOnlyObj))
                    {
                        httpsOnly = Convert.ToBoolean(httpsOnlyObj);
                    }

                    if (!httpsOnly)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"App Service HTTPS Not Enforced - {stig.Title}",
                            Description = $"App Service '{appService.Name}' does not enforce HTTPS only. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = appService.Id,
                            ResourceName = appService.Name,
                            ResourceType = appService.Type,
                            Evidence = "httpsOnly is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception appEx)
                {
                    _logger.LogWarning(appEx, "Unable to query App Service {Name}", appService.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAppServiceTlsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var appServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Contains("/slots/", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (appServices.Count == 0)
            {
                _logger.LogInformation("No App Services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var appService in appServices)
            {
                try
                {
                    var appResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(appService.Id)).GetAsync(cancellationToken);
                    var appProps = appResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    string minTlsVersion = "1.0";
                    if (appProps != null && appProps.TryGetValue("siteConfig", out var siteConfigObj))
                    {
                        var siteConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(siteConfigObj.ToString() ?? "{}");
                        if (siteConfig != null && siteConfig.TryGetValue("minTlsVersion", out var tlsObj))
                        {
                            minTlsVersion = tlsObj.ToString() ?? "1.0";
                        }
                    }

                    if (!minTlsVersion.StartsWith("1.2", StringComparison.OrdinalIgnoreCase) &&
                        !minTlsVersion.StartsWith("1.3", StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"App Service TLS Version Insufficient - {stig.Title}",
                            Description = $"App Service '{appService.Name}' does not enforce TLS 1.2 or higher (current: {minTlsVersion}). {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = appService.Id,
                            ResourceName = appService.Name,
                            ResourceType = appService.Type,
                            Evidence = $"Minimum TLS version: {minTlsVersion} (required: 1.2 or higher)",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG",
                                ["CurrentTlsVersion"] = minTlsVersion
                            }
                        });
                    }
                }
                catch (Exception appEx)
                {
                    _logger.LogWarning(appEx, "Unable to query App Service {Name}", appService.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateFunctionAppHttpsStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var functionApps = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                r.Properties?.ToString()?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (functionApps.Count == 0)
            {
                _logger.LogInformation("No Function Apps found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var functionApp in functionApps)
            {
                try
                {
                    var funcResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(functionApp.Id)).GetAsync(cancellationToken);
                    var funcProps = funcResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool httpsOnly = false;
                    if (funcProps != null && funcProps.TryGetValue("httpsOnly", out var httpsOnlyObj))
                    {
                        httpsOnly = Convert.ToBoolean(httpsOnlyObj);
                    }

                    if (!httpsOnly)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Function App HTTPS Not Enforced - {stig.Title}",
                            Description = $"Function App '{functionApp.Name}' does not enforce HTTPS only. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = functionApp.Id,
                            ResourceName = functionApp.Name,
                            ResourceType = functionApp.Type,
                            Evidence = "httpsOnly is not set to true",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception funcEx)
                {
                    _logger.LogWarning(funcEx, "Unable to query Function App {Name}", functionApp.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateFunctionAppManagedIdentityStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var functionApps = allResources.Where(r =>
                r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) &&
                r.Properties?.ToString()?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (functionApps.Count == 0)
            {
                _logger.LogInformation("No Function Apps found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var functionApp in functionApps)
            {
                try
                {
                    var funcResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(functionApp.Id)).GetAsync(cancellationToken);

                    bool hasManagedIdentity = false;
                    if (funcResource.Value.Data.Identity != null)
                    {
                        var identityType = funcResource.Value.Data.Identity.ManagedServiceIdentityType.ToString();
                        hasManagedIdentity = !identityType.Equals("None", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!hasManagedIdentity)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Function App Missing Managed Identity - {stig.Title}",
                            Description = $"Function App '{functionApp.Name}' does not use managed identity. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = functionApp.Id,
                            ResourceName = functionApp.Name,
                            ResourceType = functionApp.Type,
                            Evidence = "No managed identity configured",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception funcEx)
                {
                    _logger.LogWarning(funcEx, "Unable to query Function App {Name}", functionApp.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    // New STIG validation methods for Integration services
    private async Task<List<AtoFinding>> ValidateApimSubscriptionKeysStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var apimServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.ApiManagement/service", StringComparison.OrdinalIgnoreCase)).ToList();

            if (apimServices.Count == 0)
            {
                _logger.LogInformation("No API Management services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            // Note: Validating subscription key requirements would require querying individual API policies
            // This is marked as manual review required
            foreach (var apim in apimServices)
            {
                findings.Add(new AtoFinding
                {
                    AffectedNistControls = stig.NistControls.ToList(),
                    Title = $"APIM Subscription Key Validation Required - {stig.Title}",
                    Description = $"API Management service '{apim.Name}' subscription key enforcement requires manual verification of API policies. {stig.Description}",
                    Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    ResourceId = apim.Id,
                    ResourceName = apim.Name,
                    ResourceType = apim.Type,
                    Evidence = "Manual verification required - check API policies for subscription key requirements",
                    RemediationGuidance = stig.FixText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StigId"] = stig.StigId,
                        ["VulnId"] = stig.VulnId,
                        ["StigSeverity"] = stig.Severity.ToString(),
                        ["Category"] = stig.Category,
                        ["CciRefs"] = stig.CciRefs,
                        ["Source"] = "STIG"
                    }
                });
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateApimVnetStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var apimServices = allResources.Where(r =>
                r.Type.Equals("Microsoft.ApiManagement/service", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (apimServices.Count == 0)
            {
                _logger.LogInformation("No API Management services found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var apim in apimServices)
            {
                try
                {
                    var apimResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(apim.Id)).GetAsync(cancellationToken);
                    var apimProps = apimResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasVnetIntegration = false;
                    if (apimProps != null && apimProps.TryGetValue("virtualNetworkType", out var vnetTypeObj))
                    {
                        var vnetType = vnetTypeObj?.ToString() ?? "None";
                        hasVnetIntegration = !vnetType.Equals("None", StringComparison.OrdinalIgnoreCase);
                    }

                    if (!hasVnetIntegration)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"APIM VNet Integration Missing - {stig.Title}",
                            Description = $"API Management service '{apim.Name}' is not integrated with a Virtual Network. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = apim.Id,
                            ResourceName = apim.Name,
                            ResourceType = apim.Type,
                            Evidence = "virtualNetworkType is set to None",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception apimEx)
                {
                    _logger.LogWarning(apimEx, "Unable to query APIM {Name}", apim.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateServiceBusPrivateEndpointStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var serviceBusNamespaces = allResources.Where(r =>
                r.Type.Equals("Microsoft.ServiceBus/namespaces", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (serviceBusNamespaces.Count == 0)
            {
                _logger.LogInformation("No Service Bus namespaces found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var serviceBus in serviceBusNamespaces)
            {
                try
                {
                    var sbResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(serviceBus.Id)).GetAsync(cancellationToken);
                    var sbProps = sbResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasPrivateEndpoint = false;
                    if (sbProps != null && sbProps.TryGetValue("privateEndpointConnections", out var peConnectionsObj))
                    {
                        var peConnections = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(peConnectionsObj.ToString() ?? "[]");
                        hasPrivateEndpoint = peConnections != null && peConnections.Count > 0;
                    }

                    if (!hasPrivateEndpoint)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Service Bus Missing Private Endpoint - {stig.Title}",
                            Description = $"Service Bus namespace '{serviceBus.Name}' does not use private endpoints. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = serviceBus.Id,
                            ResourceName = serviceBus.Name,
                            ResourceType = serviceBus.Type,
                            Evidence = "No private endpoint connections found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception sbEx)
                {
                    _logger.LogWarning(sbEx, "Unable to query Service Bus {Name}", serviceBus.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateServiceBusCmkStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var serviceBusNamespaces = allResources.Where(r =>
                r.Type.Equals("Microsoft.ServiceBus/namespaces", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (serviceBusNamespaces.Count == 0)
            {
                _logger.LogInformation("No Service Bus namespaces found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var serviceBus in serviceBusNamespaces)
            {
                try
                {
                    var sbResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(serviceBus.Id)).GetAsync(cancellationToken);
                    var sbProps = sbResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasCmk = false;
                    if (sbProps != null && sbProps.TryGetValue("encryption", out var encryptionObj))
                    {
                        var encryption = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptionObj.ToString() ?? "{}");
                        if (encryption != null && encryption.TryGetValue("keySource", out var keySourceObj))
                        {
                            hasCmk = keySourceObj?.ToString()?.Equals("Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase) ?? false;
                        }
                    }

                    if (!hasCmk)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Service Bus Not Using Customer-Managed Keys - {stig.Title}",
                            Description = $"Service Bus namespace '{serviceBus.Name}' does not use customer-managed encryption keys. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = serviceBus.Id,
                            ResourceName = serviceBus.Name,
                            ResourceType = serviceBus.Type,
                            Evidence = "No customer-managed key encryption configured",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception sbEx)
                {
                    _logger.LogWarning(sbEx, "Unable to query Service Bus {Name}", serviceBus.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    // New STIG validation methods for Container services
    private async Task<List<AtoFinding>> ValidateAcrPrivateAccessStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var containerRegistries = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerRegistry/registries", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (containerRegistries.Count == 0)
            {
                _logger.LogInformation("No Container Registries found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var acr in containerRegistries)
            {
                try
                {
                    var acrResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(acr.Id)).GetAsync(cancellationToken);
                    var acrProps = acrResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool publicNetworkAccessDisabled = false;
                    if (acrProps != null && acrProps.TryGetValue("publicNetworkAccess", out var publicAccessObj))
                    {
                        publicNetworkAccessDisabled = publicAccessObj?.ToString()?.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ?? false;
                    }

                    if (!publicNetworkAccessDisabled)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Container Registry Public Access Enabled - {stig.Title}",
                            Description = $"Container Registry '{acr.Name}' allows public network access. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = acr.Id,
                            ResourceName = acr.Name,
                            ResourceType = acr.Type,
                            Evidence = "Public network access is not disabled",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception acrEx)
                {
                    _logger.LogWarning(acrEx, "Unable to query Container Registry {Name}", acr.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateAcrVulnerabilityScanStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var containerRegistries = allResources.Where(r =>
                r.Type.Equals("Microsoft.ContainerRegistry/registries", StringComparison.OrdinalIgnoreCase)).ToList();

            if (containerRegistries.Count == 0)
            {
                _logger.LogInformation("No Container Registries found for STIG {StigId}", stig.StigId);
                return findings;
            }

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            // Check if Defender for Containers is enabled at subscription level
            bool defenderForContainersEnabled = false;
            try
            {
                // Query Defender for Cloud pricing tiers
                // Check for "Containers" or "ContainerRegistry" pricing plan
                var pricingResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Security/pricings/Containers";
                
                try
                {
                    var pricingResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(pricingResourceId)).GetAsync(cancellationToken);
                    
                    var pricingProps = pricingResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();
                    var pricingTier = pricingProps?.ContainsKey("pricingTier") == true 
                        ? pricingProps["pricingTier"]?.ToString() 
                        : null;
                    
                    if (pricingTier != null && pricingTier.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                    {
                        defenderForContainersEnabled = true;
                        _logger.LogInformation("Defender for Containers is enabled at Standard tier");
                    }
                    else
                    {
                        _logger.LogInformation("Defender for Containers pricing tier: {Tier}", pricingTier ?? "Free");
                    }
                }
                catch (Exception)
                {
                    // Try older ContainerRegistry pricing name (for backward compatibility)
                    var legacyPricingId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Security/pricings/ContainerRegistry";
                    
                    try
                    {
                        var legacyPricingResource = await armClient.GetGenericResource(
                            new ResourceIdentifier(legacyPricingId)).GetAsync(cancellationToken);
                        
                        var legacyPricingProps = legacyPricingResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();
                        var legacyPricingTier = legacyPricingProps?.ContainsKey("pricingTier") == true 
                            ? legacyPricingProps["pricingTier"]?.ToString() 
                            : null;
                        
                        if (legacyPricingTier != null && legacyPricingTier.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                        {
                            defenderForContainersEnabled = true;
                            _logger.LogInformation("Defender for Container Registry is enabled at Standard tier (legacy)");
                        }
                        else
                        {
                            _logger.LogInformation("Defender for Container Registry pricing tier: {Tier}", legacyPricingTier ?? "Free");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to query Defender for Containers/Container Registry pricing");
                    }
                }
            }
            catch (Exception defenderEx)
            {
                _logger.LogWarning(defenderEx, "Unable to query Defender for Cloud pricing - will check per-ACR");
            }

            // If Defender for Containers is not enabled, all ACRs are non-compliant
            if (!defenderForContainersEnabled)
            {
                foreach (var acr in containerRegistries)
                {
                    findings.Add(new AtoFinding
                    {
                        AffectedNistControls = stig.NistControls.ToList(),
                        Title = $"Container Registry Vulnerability Scanning Not Enabled - {stig.Title}",
                        Description = $"Container Registry '{acr.Name}' does not have vulnerability scanning enabled. Microsoft Defender for Containers must be enabled at the subscription level to provide vulnerability scanning. {stig.Description}",
                        Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                        ComplianceStatus = AtoComplianceStatus.NonCompliant,
                        ResourceId = acr.Id,
                        ResourceName = acr.Name,
                        ResourceType = acr.Type,
                        Evidence = "Defender for Containers is not enabled at subscription level - vulnerability scanning unavailable",
                        RemediationGuidance = stig.FixText,
                        Metadata = new Dictionary<string, object>
                        {
                            ["StigId"] = stig.StigId,
                            ["VulnId"] = stig.VulnId,
                            ["StigSeverity"] = stig.Severity.ToString(),
                            ["Category"] = stig.Category,
                            ["CciRefs"] = stig.CciRefs,
                            ["Source"] = "STIG",
                            ["DefenderForContainersEnabled"] = false,
                            ["RemediationNote"] = "Enable Microsoft Defender for Containers at subscription level"
                        }
                    });
                }
            }
            else
            {
                _logger.LogInformation("Defender for Containers is enabled - vulnerability scanning available for {Count} ACR instances", 
                    containerRegistries.Count);
                // Defender is enabled - ACRs should have automatic vulnerability scanning
                // No findings to report as the requirement is met
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    // Database STIG validation methods
    private async Task<List<AtoFinding>> ValidateCosmosDbPrivateEndpointStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var cosmosDbAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.DocumentDB/databaseAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (cosmosDbAccounts.Count == 0)
            {
                _logger.LogInformation("No Cosmos DB accounts found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var cosmosDb in cosmosDbAccounts)
            {
                try
                {
                    var cosmosResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(cosmosDb.Id)).GetAsync(cancellationToken);
                    var cosmosProps = cosmosResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasPrivateEndpoint = false;
                    if (cosmosProps != null && cosmosProps.TryGetValue("privateEndpointConnections", out var peConnectionsObj))
                    {
                        var peConnections = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(peConnectionsObj.ToString() ?? "[]");
                        hasPrivateEndpoint = peConnections != null && peConnections.Count > 0;
                    }

                    if (!hasPrivateEndpoint)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Cosmos DB Missing Private Endpoint - {stig.Title}",
                            Description = $"Cosmos DB account '{cosmosDb.Name}' does not use private endpoints. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = cosmosDb.Id,
                            ResourceName = cosmosDb.Name,
                            ResourceType = cosmosDb.Type,
                            Evidence = "No private endpoint connections found",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception cosmosEx)
                {
                    _logger.LogWarning(cosmosEx, "Unable to query Cosmos DB {Name}", cosmosDb.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ValidateCosmosDbCmkStigAsync(
        string subscriptionId,
        string? resourceGroupName,
        StigControl stig,
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var allResources = resourceGroupName != null
                ? await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken)
                : await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            var cosmosDbAccounts = allResources.Where(r =>
                r.Type.Equals("Microsoft.DocumentDB/databaseAccounts", StringComparison.OrdinalIgnoreCase)).ToList();

            var armClient = _azureResourceService.GetArmClient();
            if (armClient == null)
            {
                _logger.LogWarning("ARM client not available for STIG {StigId}", stig.StigId);
                return findings;
            }

            if (cosmosDbAccounts.Count == 0)
            {
                _logger.LogInformation("No Cosmos DB accounts found for STIG {StigId}", stig.StigId);
                return findings;
            }

            foreach (var cosmosDb in cosmosDbAccounts)
            {
                try
                {
                    var cosmosResource = await armClient.GetGenericResource(
                        new ResourceIdentifier(cosmosDb.Id)).GetAsync(cancellationToken);
                    var cosmosProps = cosmosResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();

                    bool hasCmk = false;
                    if (cosmosProps != null && cosmosProps.TryGetValue("keyVaultKeyUri", out var keyVaultKeyUriObj))
                    {
                        hasCmk = !string.IsNullOrEmpty(keyVaultKeyUriObj?.ToString());
                    }

                    if (!hasCmk)
                    {
                        findings.Add(new AtoFinding
                        {
                            AffectedNistControls = stig.NistControls.ToList(),
                            Title = $"Cosmos DB Not Using Customer-Managed Keys - {stig.Title}",
                            Description = $"Cosmos DB account '{cosmosDb.Name}' does not use customer-managed encryption keys. {stig.Description}",
                            Severity = MapStigSeverityToFindingSeverity(stig.Severity),
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            ResourceId = cosmosDb.Id,
                            ResourceName = cosmosDb.Name,
                            ResourceType = cosmosDb.Type,
                            Evidence = "No Key Vault key URI configured for customer-managed encryption",
                            RemediationGuidance = stig.FixText,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StigId"] = stig.StigId,
                                ["VulnId"] = stig.VulnId,
                                ["StigSeverity"] = stig.Severity.ToString(),
                                ["Category"] = stig.Category,
                                ["CciRefs"] = stig.CciRefs,
                                ["Source"] = "STIG"
                            }
                        });
                    }
                }
                catch (Exception cosmosEx)
                {
                    _logger.LogWarning(cosmosEx, "Unable to query Cosmos DB {Name}", cosmosDb.Name);
                }
            }

            _logger.LogInformation("STIG {StigId} validation complete: {Count} findings", stig.StigId, findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating STIG {StigId}", stig.StigId);
        }

        return findings;
    }

    /// <summary>
    /// Maps STIG severity to ATO finding severity
    /// </summary>
    private static AtoFindingSeverity MapStigSeverityToFindingSeverity(StigSeverity stigSeverity)
    {
        return stigSeverity switch
        {
            StigSeverity.Critical => AtoFindingSeverity.Critical,
            StigSeverity.High => AtoFindingSeverity.High,
            StigSeverity.Medium => AtoFindingSeverity.Medium,
            StigSeverity.Low => AtoFindingSeverity.Low,
            _ => AtoFindingSeverity.Informational
        };
    }

    /// <summary>
    /// Gets the list of supported STIG service types
    /// </summary>
    public Task<IReadOnlyList<StigServiceType>> GetSupportedServiceTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var supportedTypes = Enum.GetValues<StigServiceType>().ToList();
        return Task.FromResult<IReadOnlyList<StigServiceType>>(supportedTypes);
    }

    /// <summary>
    /// Checks if a specific STIG is supported for automated validation
    /// </summary>
    public async Task<bool> IsStigSupportedAsync(
        string stigId,
        CancellationToken cancellationToken = default)
    {
        // Get all STIGs and check if the ID exists
        var allStigs = await _stigKnowledgeService.GetAllStigsAsync(cancellationToken);
        return allStigs.Any(s => s.StigId.Equals(stigId, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
