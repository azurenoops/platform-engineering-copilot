using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Terraform generator for Private Endpoint cross-cutting concern
/// Generates reusable Private Endpoint configuration for any Azure resource
/// </summary>
public class TerraformPrivateEndpointGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.PrivateEndpoint;
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public CloudProvider Provider => CloudProvider.Azure;

    /// <summary>
    /// Mapping of Azure resource types to their Private Link group IDs
    /// </summary>
    private static readonly Dictionary<string, string> ResourceTypeToGroupId = new()
    {
        { "Microsoft.KeyVault/vaults", "vault" },
        { "Microsoft.Storage/storageAccounts", "blob" },
        { "Microsoft.ContainerRegistry/registries", "registry" },
        { "Microsoft.Sql/servers", "sqlServer" },
        { "Microsoft.DocumentDB/databaseAccounts", "Sql" },
        { "Microsoft.ServiceBus/namespaces", "namespace" },
        { "Microsoft.EventHub/namespaces", "namespace" },
        { "Microsoft.Web/sites", "sites" },
        { "Microsoft.CognitiveServices/accounts", "account" },
        { "Microsoft.Search/searchServices", "searchService" }
    };

    /// <summary>
    /// Mapping of Azure resource types to Private DNS zone names
    /// </summary>
    private static readonly Dictionary<string, string> ResourceTypeToDnsZone = new()
    {
        { "Microsoft.KeyVault/vaults", "privatelink.vaultcore.azure.net" },
        { "Microsoft.Storage/storageAccounts", "privatelink.blob.core.windows.net" },
        { "Microsoft.ContainerRegistry/registries", "privatelink.azurecr.io" },
        { "Microsoft.Sql/servers", "privatelink.database.windows.net" },
        { "Microsoft.DocumentDB/databaseAccounts", "privatelink.documents.azure.com" },
        { "Microsoft.ServiceBus/namespaces", "privatelink.servicebus.windows.net" },
        { "Microsoft.EventHub/namespaces", "privatelink.servicebus.windows.net" },
        { "Microsoft.Web/sites", "privatelink.azurewebsites.net" },
        { "Microsoft.CognitiveServices/accounts", "privatelink.cognitiveservices.azure.com" },
        { "Microsoft.Search/searchServices", "privatelink.search.windows.net" }
    };

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var resourceType = request.ResourceType ?? "Microsoft.KeyVault/vaults";
        var groupId = GetGroupIdForResourceType(resourceType);
        var dnsZoneName = GetDnsZoneForResourceType(resourceType);

        files["private-endpoint.tf"] = GeneratePrivateEndpointTerraform(request, groupId, dnsZoneName);
        files["variables.tf"] = GenerateVariablesTerraform(request);
        files["outputs.tf"] = GenerateOutputsTerraform(request);

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        // Private endpoints are supported for most Azure PaaS resources
        return ResourceTypeToGroupId.ContainsKey(resourceType);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var resourceName = request.ResourceReference ?? "resource";
        var config = request.PrivateEndpoint ?? new PrivateEndpointConfig();

        sb.AppendLine($"# Private Endpoint for {resourceName}");
        sb.AppendLine($"module \"private_endpoint_{resourceName}\" {{");
        sb.AppendLine($"  source = \"./modules/private-endpoint\"");
        sb.AppendLine();
        sb.AppendLine($"  resource_id        = module.{dependsOn}.resource_id");
        sb.AppendLine($"  resource_name      = module.{dependsOn}.resource_name");
        sb.AppendLine($"  resource_type      = \"{request.ResourceType}\"");
        sb.AppendLine($"  location           = var.location");
        sb.AppendLine($"  resource_group_name = var.resource_group_name");
        sb.AppendLine($"  subnet_id          = \"{config.SubnetId}\"");
        if (config.CreateDnsZone)
        {
            sb.AppendLine($"  create_dns_zone    = true");
            sb.AppendLine($"  virtual_network_id = var.virtual_network_id");
        }
        sb.AppendLine($"  tags               = var.tags");
        sb.AppendLine();
        sb.AppendLine($"  depends_on = [module.{dependsOn}]");
        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GeneratePrivateEndpointTerraform(CrossCuttingRequest request, string groupId, string dnsZoneName)
    {
        var sb = new StringBuilder();
        var config = request.PrivateEndpoint ?? new PrivateEndpointConfig();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# Private Endpoint Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-7 (Boundary Protection)");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("terraform {");
        sb.AppendLine("  required_providers {");
        sb.AppendLine("    azurerm = {");
        sb.AppendLine("      source  = \"hashicorp/azurerm\"");
        sb.AppendLine("      version = \"~> 3.0\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Private Endpoint resource
        sb.AppendLine("resource \"azurerm_private_endpoint\" \"main\" {");
        sb.AppendLine("  name                = \"${var.resource_name}-pe\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  subnet_id           = var.subnet_id");
        sb.AppendLine();
        sb.AppendLine("  private_service_connection {");
        sb.AppendLine("    name                           = \"${var.resource_name}-psc\"");
        sb.AppendLine("    private_connection_resource_id = var.resource_id");
        sb.AppendLine("    is_manual_connection           = false");
        sb.AppendLine($"    subresource_names             = [\"{groupId}\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    \"security-control\" = \"SC-7\"");
        sb.AppendLine("    \"managed-by\"       = \"terraform\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();

        // Private DNS Zone (conditional)
        sb.AppendLine("# Private DNS Zone (optional)");
        sb.AppendLine("resource \"azurerm_private_dns_zone\" \"main\" {");
        sb.AppendLine("  count               = var.create_dns_zone ? 1 : 0");
        sb.AppendLine($"  name                = \"{dnsZoneName}\"");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  tags                = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // DNS Zone VNet Link
        sb.AppendLine("resource \"azurerm_private_dns_zone_virtual_network_link\" \"main\" {");
        sb.AppendLine("  count                 = var.create_dns_zone ? 1 : 0");
        sb.AppendLine("  name                  = \"${var.resource_name}-vnet-link\"");
        sb.AppendLine("  resource_group_name   = var.resource_group_name");
        sb.AppendLine("  private_dns_zone_name = azurerm_private_dns_zone.main[0].name");
        sb.AppendLine("  virtual_network_id    = var.virtual_network_id");
        sb.AppendLine("  registration_enabled  = false");
        sb.AppendLine("}");
        sb.AppendLine();

        // DNS Zone Group
        sb.AppendLine("resource \"azurerm_private_dns_a_record\" \"main\" {");
        sb.AppendLine("  count               = var.create_dns_zone ? 1 : 0");
        sb.AppendLine("  name                = var.resource_name");
        sb.AppendLine("  zone_name           = azurerm_private_dns_zone.main[0].name");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  ttl                 = 300");
        sb.AppendLine("  records             = [azurerm_private_endpoint.main.private_service_connection[0].private_ip_address]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateVariablesTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# Private Endpoint Variables");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_id\" {");
        sb.AppendLine("  description = \"The resource ID of the target resource\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_name\" {");
        sb.AppendLine("  description = \"The name of the target resource\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_type\" {");
        sb.AppendLine("  description = \"The Azure resource type (e.g., Microsoft.KeyVault/vaults)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"location\" {");
        sb.AppendLine("  description = \"Azure region for the private endpoint\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_group_name\" {");
        sb.AppendLine("  description = \"Resource group name\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"subnet_id\" {");
        sb.AppendLine("  description = \"Subnet ID for the private endpoint\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"create_dns_zone\" {");
        sb.AppendLine("  description = \"Whether to create a private DNS zone\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"virtual_network_id\" {");
        sb.AppendLine("  description = \"VNet ID for DNS zone link (required if create_dns_zone is true)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Resource tags\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateOutputsTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# Private Endpoint Outputs");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("output \"private_endpoint_id\" {");
        sb.AppendLine("  description = \"The ID of the private endpoint\"");
        sb.AppendLine("  value       = azurerm_private_endpoint.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"private_ip_address\" {");
        sb.AppendLine("  description = \"The private IP address of the private endpoint\"");
        sb.AppendLine("  value       = azurerm_private_endpoint.main.private_service_connection[0].private_ip_address");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"dns_zone_id\" {");
        sb.AppendLine("  description = \"The ID of the private DNS zone (if created)\"");
        sb.AppendLine("  value       = var.create_dns_zone ? azurerm_private_dns_zone.main[0].id : null");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetGroupIdForResourceType(string resourceType)
    {
        return ResourceTypeToGroupId.TryGetValue(resourceType, out var groupId) 
            ? groupId 
            : "vault"; // Default
    }

    private string GetDnsZoneForResourceType(string resourceType)
    {
        return ResourceTypeToDnsZone.TryGetValue(resourceType, out var dnsZone) 
            ? dnsZone 
            : "privatelink.vaultcore.azure.net"; // Default
    }
}
