using System.Text;
using System.Linq;
using System.Collections.Generic;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform modules for Azure Container Instances
/// Supports containerized applications with managed identity, networking, and monitoring
/// </summary>
public class ContainerInstancesModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Generate all Container Instances Terraform files
        files["container-instances/container_group.tf"] = GenerateContainerGroup(request);
        files["container-instances/container_registry.tf"] = GenerateContainerRegistry(request);
        files["container-instances/application_insights.tf"] = GenerateApplicationInsights(request);
        files["container-instances/variables.tf"] = GenerateVariables();
        files["container-instances/outputs.tf"] = GenerateOutputs();
        
        // Optional components
        if (infrastructure.IncludeNetworking == true)
        {
            files["container-instances/network.tf"] = GenerateNetwork(request);
        }
        
        return files;
    }
    
    private string GenerateContainerGroup(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName?.ToLowerInvariant() ?? "app";
        var app = request.Application ?? new ApplicationSpec();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        sb.AppendLine("# Container Group Configuration");
        sb.AppendLine("# Azure Container Instances - serverless container deployment");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_container_group\" \"main\" {");
        sb.AppendLine($"  name                = \"aci-${{var.service_name}}-${{var.environment}}\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  os_type             = \"Linux\"");
        sb.AppendLine();
        sb.AppendLine("  # Container definition");
        sb.AppendLine("  container {");
        sb.AppendLine($"    name   = \"{serviceName}\"");
        sb.AppendLine("    image  = var.container_image");
        sb.AppendLine($"    cpu    = \"{deployment.Resources?.CpuRequest ?? "1.0"}\"");
        sb.AppendLine($"    memory = \"{deployment.Resources?.MemoryRequest ?? "1.5"}\"");
        sb.AppendLine();
        sb.AppendLine("    # Ports");
        sb.AppendLine("    ports {");
        sb.AppendLine($"      port     = {(app.Port > 0 ? app.Port : 8080)}");
        sb.AppendLine("      protocol = \"TCP\"");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Environment variables
        if (app.EnvironmentVariables?.Any() == true)
        {
            sb.AppendLine("    # Environment variables");
            foreach (var env in app.EnvironmentVariables)
            {
                sb.AppendLine("    environment_variables = {");
                sb.AppendLine($"      \"{env.Key}\" = \"{env.Value}\"");
                sb.AppendLine("    }");
            }
            sb.AppendLine();
        }
        
        // Add Application Insights
        sb.AppendLine("    environment_variables = {");
        sb.AppendLine("      \"APPINSIGHTS_INSTRUMENTATIONKEY\"        = azurerm_application_insights.main.instrumentation_key");
        sb.AppendLine("      \"APPLICATIONINSIGHTS_CONNECTION_STRING\" = azurerm_application_insights.main.connection_string");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Health probes
        if (app.IncludeHealthCheck == true)
        {
            sb.AppendLine("    # Liveness probe");
            sb.AppendLine("    liveness_probe {");
            sb.AppendLine("      http_get {");
            sb.AppendLine("        path   = \"/health\"");
            sb.AppendLine($"        port   = {(app.Port > 0 ? app.Port : 8080)}");
            sb.AppendLine("        scheme = \"Http\"");
            sb.AppendLine("      }");
            sb.AppendLine("      initial_delay_seconds = 30");
            sb.AppendLine("      period_seconds        = 10");
            sb.AppendLine("      failure_threshold     = 3");
            sb.AppendLine("      timeout_seconds       = 2");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        if (app.IncludeReadinessProbe == true)
        {
            sb.AppendLine("    # Readiness probe");
            sb.AppendLine("    readiness_probe {");
            sb.AppendLine("      http_get {");
            sb.AppendLine("        path   = \"/ready\"");
            sb.AppendLine($"        port   = {(app.Port > 0 ? app.Port : 8080)}");
            sb.AppendLine("        scheme = \"Http\"");
            sb.AppendLine("      }");
            sb.AppendLine("      initial_delay_seconds = 10");
            sb.AppendLine("      period_seconds        = 5");
            sb.AppendLine("      failure_threshold     = 3");
            sb.AppendLine("      timeout_seconds       = 2");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Container registry credentials");
        sb.AppendLine("  image_registry_credential {");
        sb.AppendLine("    server   = azurerm_container_registry.main.login_server");
        sb.AppendLine("    username = azurerm_container_registry.main.admin_username");
        sb.AppendLine("    password = azurerm_container_registry.main.admin_password");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Managed identity");
        sb.AppendLine("  identity {");
        sb.AppendLine("    type = \"SystemAssigned\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Restart policy");
        sb.AppendLine("  restart_policy = \"Always\"");
        sb.AppendLine();
        sb.AppendLine("  # DNS name label for public IP");
        sb.AppendLine("  dns_name_label = var.dns_name_label");
        sb.AppendLine();
        sb.AppendLine("  # Diagnostics");
        sb.AppendLine("  diagnostics {");
        sb.AppendLine("    log_analytics {");
        sb.AppendLine("      workspace_id  = azurerm_log_analytics_workspace.main.workspace_id");
        sb.AppendLine("      workspace_key = azurerm_log_analytics_workspace.main.primary_shared_key");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateContainerRegistry(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Azure Container Registry Configuration");
        sb.AppendLine("# Private registry for container images");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_container_registry\" \"main\" {");
        sb.AppendLine($"  name                = \"acr${{replace(var.service_name, \"-\", \"\")}}${{var.environment}}\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  sku                 = var.container_registry_sku");
        sb.AppendLine("  admin_enabled       = true");
        sb.AppendLine();
        sb.AppendLine("  # Georeplications for multi-region");
        sb.AppendLine("  dynamic \"georeplications\" {");
        sb.AppendLine("    for_each = var.enable_geo_replication ? var.replication_locations : []");
        sb.AppendLine("    content {");
        sb.AppendLine("      location = georeplications.value");
        sb.AppendLine("      tags     = var.tags");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Network rules");
        sb.AppendLine("  network_rule_set {");
        sb.AppendLine("    default_action = \"Deny\"");
        sb.AppendLine("    ip_rule {");
        sb.AppendLine("      action   = \"Allow\"");
        sb.AppendLine("      ip_range = \"0.0.0.0/0\"  # Update with specific IP ranges in production");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Enable vulnerability scanning (Premium SKU)");
        sb.AppendLine("  quarantine_policy_enabled = var.container_registry_sku == \"Premium\" ? true : false");
        sb.AppendLine("  retention_policy {");
        sb.AppendLine("    days    = 30");
        sb.AppendLine("    enabled = var.container_registry_sku == \"Premium\" ? true : false");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  trust_policy {");
        sb.AppendLine("    enabled = var.container_registry_sku == \"Premium\" ? true : false");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateApplicationInsights(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Application Insights Configuration");
        sb.AppendLine("# Provides application performance monitoring and diagnostics");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_log_analytics_workspace\" \"main\" {");
        sb.AppendLine($"  name                = \"log-${{var.service_name}}\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  sku                 = \"PerGB2018\"");
        sb.AppendLine("  retention_in_days   = 30");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_application_insights\" \"main\" {");
        sb.AppendLine($"  name                = \"appi-${{var.service_name}}\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  workspace_id        = azurerm_log_analytics_workspace.main.id");
        sb.AppendLine("  application_type    = \"web\"");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateNetwork(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var networkConfig = infrastructure.NetworkConfig ?? CreateDefaultNetworkConfig();
        
        // Check if using existing network or creating new
        if (networkConfig.Mode == NetworkMode.UseExisting)
        {
            return GenerateExistingNetworkReferences(networkConfig);
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("# Network Configuration for Container Instances");
        sb.AppendLine();
        
        // Virtual Network
        sb.AppendLine($@"# Virtual Network
resource ""azurerm_virtual_network"" ""main"" {{
  name                = ""{networkConfig.VNetName}""
  address_space       = [""{networkConfig.VNetAddressSpace}""]
  location            = var.location
  resource_group_name = var.resource_group_name");
        
        // DDoS Protection
        if (networkConfig.EnableDDoSProtection && !string.IsNullOrEmpty(networkConfig.DDoSProtectionPlanId))
        {
            sb.AppendLine($@"
  ddos_protection_plan {{
    id     = ""{networkConfig.DDoSProtectionPlanId}""
    enable = true
  }}");
        }
        
        sb.AppendLine(@"
  tags = var.tags
}");
        sb.AppendLine();
        
        // Container Subnet
        var containerSubnet = networkConfig.Subnets.FirstOrDefault(s => 
            s.Name.Contains("container") || s.Delegation == "Microsoft.ContainerInstance/containerGroups");
        if (containerSubnet == null)
        {
            containerSubnet = new SubnetConfiguration
            {
                Name = "container-subnet",
                AddressPrefix = "10.0.1.0/24",
                Delegation = "Microsoft.ContainerInstance/containerGroups"
            };
        }
        
        sb.AppendLine($@"# Subnet for Container Instances
resource ""azurerm_subnet"" ""container_subnet"" {{
  name                 = ""{containerSubnet.Name}""
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [""{containerSubnet.AddressPrefix}""]");
        
        // Service Endpoints
        if (containerSubnet.EnableServiceEndpoints && containerSubnet.ServiceEndpoints.Any())
        {
            sb.AppendLine($@"
  service_endpoints = [{string.Join(", ", containerSubnet.ServiceEndpoints.Select(se => $"\"{se}\""))}]");
        }
        
        // Delegation for Container Instances
        sb.AppendLine($@"
  delegation {{
    name = ""container-delegation""
    service_delegation {{
      name    = ""{containerSubnet.Delegation ?? "Microsoft.ContainerInstance/containerGroups"}""
      actions = [""Microsoft.Network/virtualNetworks/subnets/action""]
    }}
  }}
}}");
        sb.AppendLine();
        
        // Network Security Group (if enabled)
        if (networkConfig.EnableNetworkSecurityGroup)
        {
            sb.AppendLine($@"# Network Security Group for Container Subnet
resource ""azurerm_network_security_group"" ""container"" {{
  name                = ""nsg-${{var.service_name}}-container""
  location            = var.location
  resource_group_name = var.resource_group_name");
            
            // Custom NSG Rules
            if (networkConfig.NsgRules.Any())
            {
                sb.AppendLine();
                foreach (var rule in networkConfig.NsgRules)
                {
                    sb.AppendLine($@"  security_rule {{
    name                       = ""{rule.Name}""
    priority                   = {rule.Priority}
    direction                  = ""{rule.Direction}""
    access                     = ""{rule.Access}""
    protocol                   = ""{rule.Protocol}""
    source_port_range          = ""{rule.SourcePortRange}""
    destination_port_range     = ""{rule.DestinationPortRange}""
    source_address_prefix      = ""{rule.SourceAddressPrefix}""
    destination_address_prefix = ""{rule.DestinationAddressPrefix}""
    description                = ""{rule.Description}""
  }}");
                }
            }
            else
            {
                // Default rules for container access
                sb.AppendLine($@"
  # Allow HTTP inbound
  security_rule {{
    name                       = ""AllowHTTP""
    priority                   = 100
    direction                  = ""Inbound""
    access                     = ""Allow""
    protocol                   = ""Tcp""
    source_port_range          = ""*""
    destination_port_range     = ""80""
    source_address_prefix      = ""*""
    destination_address_prefix = ""*""
  }}
  
  # Allow HTTPS inbound
  security_rule {{
    name                       = ""AllowHTTPS""
    priority                   = 110
    direction                  = ""Inbound""
    access                     = ""Allow""
    protocol                   = ""Tcp""
    source_port_range          = ""*""
    destination_port_range     = ""443""
    source_address_prefix      = ""*""
    destination_address_prefix = ""*""
  }}");
            }
            
            sb.AppendLine(@"
  tags = var.tags
}");
            sb.AppendLine();
            
            sb.AppendLine($@"# Associate NSG with Container Subnet
resource ""azurerm_subnet_network_security_group_association"" ""container"" {{
  subnet_id                 = azurerm_subnet.container_subnet.id
  network_security_group_id = azurerm_network_security_group.container.id
}}");
            sb.AppendLine();
        }
        
        // Network Profile for Container Group
        sb.AppendLine($@"# Network profile for container group
resource ""azurerm_network_profile"" ""main"" {{
  name                = ""netprofile-${{var.service_name}}""
  location            = var.location
  resource_group_name = var.resource_group_name

  container_network_interface {{
    name = ""aci-nic""

    ip_configuration {{
      name      = ""aci-ipconfig""
      subnet_id = azurerm_subnet.container_subnet.id
    }}
  }}

  tags = var.tags
}}");
        sb.AppendLine();
        
        // Outputs
        sb.AppendLine($@"# Network Outputs
output ""vnet_id"" {{
  description = ""ID of the Virtual Network""
  value       = azurerm_virtual_network.main.id
}}

output ""vnet_name"" {{
  description = ""Name of the Virtual Network""
  value       = azurerm_virtual_network.main.name
}}

output ""container_subnet_id"" {{
  description = ""ID of the container subnet""
  value       = azurerm_subnet.container_subnet.id
}}

output ""network_profile_id"" {{
  description = ""ID of the network profile""
  value       = azurerm_network_profile.main.id
}}");
        
        return sb.ToString();
    }
    
    private string GenerateExistingNetworkReferences(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Reference Existing Network Resources for Container Instances");
        sb.AppendLine();
        
        // Data source for existing VNet
        sb.AppendLine($@"# Reference existing Virtual Network
data ""azurerm_virtual_network"" ""main"" {{
  name                = ""{networkConfig.ExistingVNetName}""
  resource_group_name = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
        sb.AppendLine();
        
        // Find the container subnet from existing subnets
        var containerSubnet = networkConfig.ExistingSubnets.FirstOrDefault(s => 
            s.Purpose == SubnetPurpose.Application || s.Name.Contains("container"));
        
        if (containerSubnet != null)
        {
            sb.AppendLine($@"# Reference existing Container subnet
data ""azurerm_subnet"" ""container_subnet"" {{
  name                 = ""{containerSubnet.Name}""
  virtual_network_name = data.azurerm_virtual_network.main.name
  resource_group_name  = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("# WARNING: No container subnet found in existing network configuration");
            sb.AppendLine("# Container Instances require a dedicated subnet with delegation");
            sb.AppendLine();
        }
        
        // Network Profile (required for Container Instances with VNet)
        if (containerSubnet != null)
        {
            sb.AppendLine($@"# Network Profile for Container Instances
resource ""azurerm_network_profile"" ""main"" {{
  name                = ""np-${{var.service_name}}""
  location            = var.location
  resource_group_name = var.resource_group_name
  
  container_network_interface {{
    name = ""nic-${{var.service_name}}""
    
    ip_configuration {{
      name      = ""ipconfig1""
      subnet_id = data.azurerm_subnet.container_subnet.id
    }}
  }}
  
  tags = var.tags
}}");
            sb.AppendLine();
        }
        
        // Optionally generate NSG if needed
        if (networkConfig.EnableNetworkSecurityGroup && networkConfig.NsgRules.Any())
        {
            sb.AppendLine($@"# Network Security Group
resource ""azurerm_network_security_group"" ""main"" {{
  name                = ""nsg-${{var.service_name}}""
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}}");
            sb.AppendLine();
            
            foreach (var rule in networkConfig.NsgRules)
            {
                sb.AppendLine($@"
resource ""azurerm_network_security_rule"" ""{rule.Name.ToLower().Replace(" ", "_")}"" {{
  name                        = ""{rule.Name}""
  priority                    = {rule.Priority}
  direction                   = ""{rule.Direction}""
  access                      = ""{rule.Access}""
  protocol                    = ""{rule.Protocol}""
  source_port_range           = ""{rule.SourcePortRange}""
  destination_port_range      = ""{rule.DestinationPortRange}""
  source_address_prefix       = ""{rule.SourceAddressPrefix}""
  destination_address_prefix  = ""{rule.DestinationAddressPrefix}""
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.main.name
  description                 = ""{rule.Description}""
}}");
            }
            sb.AppendLine();
            
            // Associate NSG with existing container subnet
            if (containerSubnet != null)
            {
                sb.AppendLine($@"
resource ""azurerm_subnet_network_security_group_association"" ""container"" {{
  subnet_id                 = data.azurerm_subnet.container_subnet.id
  network_security_group_id = azurerm_network_security_group.main.id
}}");
                sb.AppendLine();
            }
        }
        
        // Outputs
        sb.AppendLine($@"# Outputs
output ""vnet_id"" {{
  description = ""ID of the existing Virtual Network""
  value       = data.azurerm_virtual_network.main.id
}}");
        
        if (containerSubnet != null)
        {
            sb.AppendLine($@"
output ""container_subnet_id"" {{
  description = ""ID of the existing container subnet""
  value       = data.azurerm_subnet.container_subnet.id
}}

output ""network_profile_id"" {{
  description = ""ID of the network profile""
  value       = azurerm_network_profile.main.id
}}");
        }
        
        return sb.ToString();
    }
    
    private NetworkingConfiguration CreateDefaultNetworkConfig()
    {
        return new NetworkingConfiguration
        {
            Mode = NetworkMode.CreateNew,
            VNetName = "vnet-${var.service_name}",
            VNetAddressSpace = "10.0.0.0/16",
            Subnets = new List<SubnetConfiguration>
            {
                new SubnetConfiguration
                {
                    Name = "container-subnet",
                    AddressPrefix = "10.0.1.0/24",
                    Delegation = "Microsoft.ContainerInstance/containerGroups",
                    Purpose = SubnetPurpose.Application
                }
            },
            EnableNetworkSecurityGroup = true,
            EnablePrivateEndpoint = false,
            EnableServiceEndpoints = false,
            EnableDDoSProtection = false,
            EnablePrivateDns = false
        };
    }
    
    private string GenerateVariables()
    {
        return @"# Variables for Container Instances Module

variable ""service_name"" {
  description = ""Name of the service""
  type        = string
}

variable ""environment"" {
  description = ""Environment (dev, staging, prod)""
  type        = string
}

variable ""location"" {
  description = ""Azure region""
  type        = string
}

variable ""resource_group_name"" {
  description = ""Resource group name""
  type        = string
}

variable ""container_image"" {
  description = ""Full container image name (registry/image:tag)""
  type        = string
}

variable ""container_registry_sku"" {
  description = ""Container Registry SKU (Basic, Standard, Premium)""
  type        = string
  default     = ""Standard""
}

variable ""dns_name_label"" {
  description = ""DNS name label for the container group""
  type        = string
  default     = null
}

variable ""enable_geo_replication"" {
  description = ""Enable geo-replication for container registry""
  type        = bool
  default     = false
}

variable ""replication_locations"" {
  description = ""List of locations for geo-replication""
  type        = list(string)
  default     = []
}

# === ZERO TRUST SECURITY PARAMETERS (Mirror Bicep Container Apps) ===

variable ""enable_vnet_integration"" {
  description = ""Deploy container group in VNet""
  type        = bool
  default     = true
}

variable ""subnet_id"" {
  description = ""Subnet ID for VNet integration""
  type        = string
  default     = """"
}

variable ""enable_managed_identity"" {
  description = ""Enable system-assigned managed identity""
  type        = bool
  default     = true
}

variable ""enable_private_endpoint"" {
  description = ""Enable private endpoint for ACR access""
  type        = bool
  default     = true
}

variable ""enable_image_scanning"" {
  description = ""Enable vulnerability scanning for container images""
  type        = bool
  default     = true
}

variable ""enable_content_trust"" {
  description = ""Enable content trust for image signing""
  type        = bool
  default     = true
}

variable ""enable_defender"" {
  description = ""Enable Microsoft Defender for Containers""
  type        = bool
  default     = true
}

variable ""enable_zone_redundancy"" {
  description = ""Enable zone redundancy for ACR Premium""
  type        = bool
  default     = false
}

variable ""enable_public_network_access"" {
  description = ""Allow public network access to ACR""
  type        = bool
  default     = false
}

variable ""allowed_ip_ranges"" {
  description = ""List of allowed IP ranges for ACR access""
  type        = list(string)
  default     = []
}

variable ""enable_encryption"" {
  description = ""Enable encryption at rest with customer-managed key""
  type        = bool
  default     = false
}

variable ""encryption_key_vault_key_id"" {
  description = ""Key Vault key ID for encryption""
  type        = string
  default     = """"
}

variable ""enable_log_analytics"" {
  description = ""Enable Log Analytics integration""
  type        = bool
  default     = true
}

variable ""enable_azure_monitor"" {
  description = ""Enable Azure Monitor integration""
  type        = bool
  default     = true
}

variable ""restart_policy"" {
  description = ""Container restart policy: Always, OnFailure, Never""
  type        = string
  default     = ""OnFailure""
}

variable ""tags"" {
  description = ""Tags to apply to all resources""
  type        = map(string)
  default     = {}
}
";
    }
    
    private string GenerateOutputs()
    {
        return @"# Outputs for Container Instances Module

output ""container_group_id"" {
  description = ""ID of the Container Group""
  value       = azurerm_container_group.main.id
}

output ""container_group_ip_address"" {
  description = ""IP address of the Container Group""
  value       = azurerm_container_group.main.ip_address
}

output ""container_group_fqdn"" {
  description = ""FQDN of the Container Group""
  value       = azurerm_container_group.main.fqdn
}

output ""container_group_identity_principal_id"" {
  description = ""Principal ID of the Container Group managed identity""
  value       = azurerm_container_group.main.identity[0].principal_id
}

output ""container_registry_id"" {
  description = ""ID of the Container Registry""
  value       = azurerm_container_registry.main.id
}

output ""container_registry_login_server"" {
  description = ""Login server for the Container Registry""
  value       = azurerm_container_registry.main.login_server
}

output ""container_registry_admin_username"" {
  description = ""Admin username for the Container Registry""
  value       = azurerm_container_registry.main.admin_username
  sensitive   = true
}

output ""application_insights_instrumentation_key"" {
  description = ""Application Insights instrumentation key""
  value       = azurerm_application_insights.main.instrumentation_key
  sensitive   = true
}

output ""application_insights_connection_string"" {
  description = ""Application Insights connection string""
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output ""log_analytics_workspace_id"" {
  description = ""ID of the Log Analytics Workspace""
  value       = azurerm_log_analytics_workspace.main.id
}
";
    }
}
