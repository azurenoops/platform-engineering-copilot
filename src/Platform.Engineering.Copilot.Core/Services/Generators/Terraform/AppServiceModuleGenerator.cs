using System.Text;
using System.Linq;
using System.Collections.Generic;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform modules for Azure App Service
/// Supports Web Apps with Application Insights, Key Vault integration, and managed identity
/// </summary>
public class AppServiceModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Generate all App Service Terraform files
        files["appservice/app_service_plan.tf"] = GenerateAppServicePlan(request);
        files["appservice/web_app.tf"] = GenerateWebApp(request);
        files["appservice/application_insights.tf"] = GenerateApplicationInsights(request);
        files["appservice/key_vault.tf"] = GenerateKeyVault(request);
        files["appservice/variables.tf"] = GenerateVariables();
        files["appservice/outputs.tf"] = GenerateOutputs();
        
        // Optional components
        if (deployment.AutoScaling == true)
        {
            files["appservice/auto_scaling.tf"] = GenerateAutoScaling(request);
        }
        
        // Network integration (VNet integration + Private Endpoint)
        if (infrastructure.IncludeNetworking == true)
        {
            files["appservice/network.tf"] = GenerateNetworking(request);
        }
        
        return files;
    }
    
    private string GenerateAppServicePlan(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName?.ToLowerInvariant() ?? "app";
        
        sb.AppendLine("# App Service Plan Configuration");
        sb.AppendLine("# Defines the compute resources for your web app");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_service_plan\" \"main\" {");
        sb.AppendLine($"  name                = \"plan-${{var.service_name}}\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine();
        sb.AppendLine("  # SKU and capacity");
        sb.AppendLine("  sku_name = var.app_service_sku");
        sb.AppendLine();
        sb.AppendLine("  # OS type");
        sb.AppendLine($"  os_type = \"{GetOSType(request.Application)}\"");
        sb.AppendLine();
        sb.AppendLine("  # Zone redundancy for high availability");
        sb.AppendLine("  zone_balancing_enabled = var.enable_zone_redundancy");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateWebApp(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName?.ToLowerInvariant() ?? "app";
        var app = request.Application ?? new ApplicationSpec();
        var runtime = GetRuntimeStack(app.Language);
        
        sb.AppendLine("# Web App Configuration");
        sb.AppendLine("# Azure App Service web application with managed identity and monitoring");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_linux_web_app\" \"main\" {");
        sb.AppendLine($"  name                = \"app-${{var.service_name}}-${{var.environment}}\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  service_plan_id     = azurerm_service_plan.main.id");
        sb.AppendLine();
        sb.AppendLine("  # Managed Identity");
        sb.AppendLine("  identity {");
        sb.AppendLine("    type = \"SystemAssigned\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Site configuration");
        sb.AppendLine("  site_config {");
        sb.AppendLine($"    # Runtime stack: {runtime}");
        sb.AppendLine("    application_stack {");
        
        // Add runtime-specific configuration
        switch (app.Language)
        {
            case ProgrammingLanguage.NodeJS:
                sb.AppendLine("      node_version = \"18-lts\"");
                break;
            case ProgrammingLanguage.Python:
                sb.AppendLine("      python_version = \"3.11\"");
                break;
            case ProgrammingLanguage.DotNet:
                sb.AppendLine("      dotnet_version = \"8.0\"");
                break;
            case ProgrammingLanguage.Java:
                sb.AppendLine("      java_version = \"17\"");
                sb.AppendLine("      java_server = \"TOMCAT\"");
                sb.AppendLine("      java_server_version = \"10.1\"");
                break;
            case ProgrammingLanguage.PHP:
                sb.AppendLine("      php_version = \"8.2\"");
                break;
            case ProgrammingLanguage.Ruby:
                sb.AppendLine("      ruby_version = \"3.2\"");
                break;
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    # Always On - keeps the app loaded");
        sb.AppendLine("    always_on = var.always_on");
        sb.AppendLine();
        sb.AppendLine("    # HTTP settings");
        sb.AppendLine("    http2_enabled = true");
        sb.AppendLine("    minimum_tls_version = \"1.2\"");
        sb.AppendLine("    ftps_state = \"FtpsOnly\"");
        sb.AppendLine();
        
        if (app.IncludeHealthCheck == true)
        {
            sb.AppendLine("    # Health check");
            sb.AppendLine("    health_check_path = \"/health\"");
            sb.AppendLine("    health_check_eviction_time_in_min = 5");
            sb.AppendLine();
        }
        
        sb.AppendLine("    # CORS configuration");
        sb.AppendLine("    cors {");
        sb.AppendLine("      allowed_origins = var.cors_allowed_origins");
        sb.AppendLine("      support_credentials = false");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Application settings (environment variables)");
        sb.AppendLine("  app_settings = {");
        sb.AppendLine("    \"APPINSIGHTS_INSTRUMENTATIONKEY\"        = azurerm_application_insights.main.instrumentation_key");
        sb.AppendLine("    \"APPLICATIONINSIGHTS_CONNECTION_STRING\" = azurerm_application_insights.main.connection_string");
        sb.AppendLine("    \"ApplicationInsightsAgent_EXTENSION_VERSION\" = \"~3\"");
        sb.AppendLine("    \"KEY_VAULT_URI\"                         = azurerm_key_vault.main.vault_uri");
        
        // Add custom environment variables
        if (app.EnvironmentVariables?.Any() == true)
        {
            foreach (var env in app.EnvironmentVariables)
            {
                sb.AppendLine($"    \"{env.Key}\" = \"{env.Value}\"");
            }
        }
        
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Connection strings");
        sb.AppendLine("  connection_string {");
        sb.AppendLine("    name  = \"Database\"");
        sb.AppendLine("    type  = \"SQLAzure\"");
        sb.AppendLine("    value = var.database_connection_string");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Logging configuration");
        sb.AppendLine("  logs {");
        sb.AppendLine("    application_logs {");
        sb.AppendLine("      file_system_level = \"Information\"");
        sb.AppendLine("    }");
        sb.AppendLine("    http_logs {");
        sb.AppendLine("      file_system {");
        sb.AppendLine("        retention_in_days = 7");
        sb.AppendLine("        retention_in_mb   = 35");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Deployment slot for staging");
        sb.AppendLine("resource \"azurerm_linux_web_app_slot\" \"staging\" {");
        sb.AppendLine("  name           = \"staging\"");
        sb.AppendLine("  app_service_id = azurerm_linux_web_app.main.id");
        sb.AppendLine();
        sb.AppendLine("  site_config {");
        sb.AppendLine("    application_stack {");
        
        switch (app.Language)
        {
            case ProgrammingLanguage.NodeJS:
                sb.AppendLine("      node_version = \"18-lts\"");
                break;
            case ProgrammingLanguage.Python:
                sb.AppendLine("      python_version = \"3.11\"");
                break;
            case ProgrammingLanguage.DotNet:
                sb.AppendLine("      dotnet_version = \"8.0\"");
                break;
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("    always_on = false");
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
    
    private string GenerateKeyVault(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Key Vault Configuration");
        sb.AppendLine("# Securely stores secrets, keys, and certificates");
        sb.AppendLine();
        sb.AppendLine("data \"azurerm_client_config\" \"current\" {}");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_key_vault\" \"main\" {");
        sb.AppendLine($"  name                       = \"kv-${{var.service_name}}-${{var.environment}}\"");
        sb.AppendLine("  location                   = var.location");
        sb.AppendLine("  resource_group_name        = var.resource_group_name");
        sb.AppendLine("  tenant_id                  = data.azurerm_client_config.current.tenant_id");
        sb.AppendLine("  sku_name                   = \"standard\"");
        sb.AppendLine("  soft_delete_retention_days = 7");
        sb.AppendLine("  purge_protection_enabled   = true");
        sb.AppendLine();
        sb.AppendLine("  # Enable RBAC authorization");
        sb.AppendLine("  enable_rbac_authorization = true");
        sb.AppendLine();
        sb.AppendLine("  network_acls {");
        sb.AppendLine("    bypass         = \"AzureServices\"");
        sb.AppendLine("    default_action = \"Deny\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Grant web app access to Key Vault");
        sb.AppendLine("resource \"azurerm_role_assignment\" \"web_app_kv_secrets_user\" {");
        sb.AppendLine("  scope                = azurerm_key_vault.main.id");
        sb.AppendLine("  role_definition_name = \"Key Vault Secrets User\"");
        sb.AppendLine("  principal_id         = azurerm_linux_web_app.main.identity[0].principal_id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateAutoScaling(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        sb.AppendLine("# Auto Scaling Configuration");
        sb.AppendLine("# Automatically scales App Service Plan based on metrics");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_monitor_autoscale_setting\" \"main\" {");
        sb.AppendLine($"  name                = \"autoscale-${{var.service_name}}\"");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  target_resource_id  = azurerm_service_plan.main.id");
        sb.AppendLine();
        sb.AppendLine("  profile {");
        sb.AppendLine("    name = \"default\"");
        sb.AppendLine();
        sb.AppendLine("    capacity {");
        sb.AppendLine($"      default = {deployment.Replicas}");
        sb.AppendLine($"      minimum = {deployment.MinReplicas}");
        sb.AppendLine($"      maximum = {deployment.MaxReplicas}");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    # Scale out rule - CPU");
        sb.AppendLine("    rule {");
        sb.AppendLine("      metric_trigger {");
        sb.AppendLine("        metric_name        = \"CpuPercentage\"");
        sb.AppendLine("        metric_resource_id = azurerm_service_plan.main.id");
        sb.AppendLine("        time_grain         = \"PT1M\"");
        sb.AppendLine("        statistic          = \"Average\"");
        sb.AppendLine("        time_window        = \"PT5M\"");
        sb.AppendLine("        time_aggregation   = \"Average\"");
        sb.AppendLine("        operator           = \"GreaterThan\"");
        sb.AppendLine($"        threshold          = {deployment.TargetCpuPercent}");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      scale_action {");
        sb.AppendLine("        direction = \"Increase\"");
        sb.AppendLine("        type      = \"ChangeCount\"");
        sb.AppendLine("        value     = \"1\"");
        sb.AppendLine("        cooldown  = \"PT5M\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    # Scale in rule - CPU");
        sb.AppendLine("    rule {");
        sb.AppendLine("      metric_trigger {");
        sb.AppendLine("        metric_name        = \"CpuPercentage\"");
        sb.AppendLine("        metric_resource_id = azurerm_service_plan.main.id");
        sb.AppendLine("        time_grain         = \"PT1M\"");
        sb.AppendLine("        statistic          = \"Average\"");
        sb.AppendLine("        time_window        = \"PT5M\"");
        sb.AppendLine("        time_aggregation   = \"Average\"");
        sb.AppendLine("        operator           = \"LessThan\"");
        sb.AppendLine($"        threshold          = {deployment.TargetCpuPercent / 2}");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      scale_action {");
        sb.AppendLine("        direction = \"Decrease\"");
        sb.AppendLine("        type      = \"ChangeCount\"");
        sb.AppendLine("        value     = \"1\"");
        sb.AppendLine("        cooldown  = \"PT5M\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    # Scale out rule - Memory");
        sb.AppendLine("    rule {");
        sb.AppendLine("      metric_trigger {");
        sb.AppendLine("        metric_name        = \"MemoryPercentage\"");
        sb.AppendLine("        metric_resource_id = azurerm_service_plan.main.id");
        sb.AppendLine("        time_grain         = \"PT1M\"");
        sb.AppendLine("        statistic          = \"Average\"");
        sb.AppendLine("        time_window        = \"PT5M\"");
        sb.AppendLine("        time_aggregation   = \"Average\"");
        sb.AppendLine("        operator           = \"GreaterThan\"");
        sb.AppendLine($"        threshold          = {deployment.TargetMemoryPercent}");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      scale_action {");
        sb.AppendLine("        direction = \"Increase\"");
        sb.AppendLine("        type      = \"ChangeCount\"");
        sb.AppendLine("        value     = \"1\"");
        sb.AppendLine("        cooldown  = \"PT5M\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVariables()
    {
        return @"# Variables for App Service Module

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

variable ""app_service_sku"" {
  description = ""App Service Plan SKU (B1, S1, P1v2, P1v3, etc.)""
  type        = string
  default     = ""P1v3""
}

variable ""always_on"" {
  description = ""Keep the app loaded (required for production)""
  type        = bool
  default     = true
}

variable ""enable_zone_redundancy"" {
  description = ""Enable zone redundancy for high availability""
  type        = bool
  default     = false
}

variable ""cors_allowed_origins"" {
  description = ""List of allowed CORS origins""
  type        = list(string)
  default     = [""*""]
}

variable ""database_connection_string"" {
  description = ""Database connection string (sensitive)""
  type        = string
  default     = """"
  sensitive   = true
}

# === ZERO TRUST SECURITY PARAMETERS (Mirror Bicep App Service) ===

variable ""enable_vnet_integration"" {
  description = ""Enable VNet integration for outbound traffic""
  type        = bool
  default     = true
}

variable ""enable_private_endpoint"" {
  description = ""Enable private endpoint for inbound traffic""
  type        = bool
  default     = true
}

variable ""enable_managed_identity"" {
  description = ""Enable system-assigned managed identity""
  type        = bool
  default     = true
}

variable ""enable_https_only"" {
  description = ""Require HTTPS for all connections""
  type        = bool
  default     = true
}

variable ""minimum_tls_version"" {
  description = ""Minimum TLS version (1.2 or 1.3)""
  type        = string
  default     = ""1.3""
}

variable ""enable_ftp"" {
  description = ""Enable FTP/FTPS access (not recommended)""
  type        = bool
  default     = false
}

variable ""enable_ip_restrictions"" {
  description = ""Enable IP-based access restrictions""
  type        = bool
  default     = true
}

variable ""allowed_ip_addresses"" {
  description = ""List of allowed IP addresses/CIDR blocks""
  type        = list(string)
  default     = []
}

variable ""enable_client_certificates"" {
  description = ""Require client certificates for authentication""
  type        = bool
  default     = false
}

variable ""enable_always_encrypted"" {
  description = ""Enable Always Encrypted for connection strings""
  type        = bool
  default     = true
}

variable ""enable_keyvault_references"" {
  description = ""Use Key Vault references for secrets""
  type        = bool
  default     = true
}

variable ""key_vault_id"" {
  description = ""ID of Key Vault for secret references""
  type        = string
  default     = """"
}

variable ""enable_app_service_auth"" {
  description = ""Enable Easy Auth / App Service Authentication""
  type        = bool
  default     = false
}

variable ""enable_defender_for_cloud"" {
  description = ""Enable Microsoft Defender for App Service""
  type        = bool
  default     = true
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
        return @"# Outputs for App Service Module

output ""web_app_id"" {
  description = ""ID of the Web App""
  value       = azurerm_linux_web_app.main.id
}

output ""web_app_name"" {
  description = ""Name of the Web App""
  value       = azurerm_linux_web_app.main.name
}

output ""web_app_default_hostname"" {
  description = ""Default hostname of the Web App""
  value       = azurerm_linux_web_app.main.default_hostname
}

output ""web_app_identity_principal_id"" {
  description = ""Principal ID of the Web App managed identity""
  value       = azurerm_linux_web_app.main.identity[0].principal_id
}

output ""staging_slot_default_hostname"" {
  description = ""Default hostname of the staging slot""
  value       = azurerm_linux_web_app_slot.staging.default_hostname
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

output ""key_vault_id"" {
  description = ""ID of the Key Vault""
  value       = azurerm_key_vault.main.id
}

output ""key_vault_uri"" {
  description = ""URI of the Key Vault""
  value       = azurerm_key_vault.main.vault_uri
}

output ""app_service_plan_id"" {
  description = ""ID of the App Service Plan""
  value       = azurerm_service_plan.main.id
}
";
    }
    
    private string GenerateNetworking(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var networkConfig = infrastructure.NetworkConfig ?? CreateDefaultNetworkConfig();
        
        // Check if using existing network or creating new
        if (networkConfig.Mode == NetworkMode.UseExisting)
        {
            return GenerateExistingNetworkReferences(networkConfig);
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("# Networking Configuration for App Service");
        sb.AppendLine();
        
        // Virtual Network
        sb.AppendLine($@"# Virtual Network
resource ""azurerm_virtual_network"" ""main"" {{
  name                = ""{networkConfig.VNetName}""
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = [""{networkConfig.VNetAddressSpace}""]");
        
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
        
        // Generate Subnets
        var appServiceSubnet = networkConfig.Subnets.FirstOrDefault(s => s.Name.Contains("appservice") || s.Delegation == "Microsoft.Web/serverFarms");
        if (appServiceSubnet == null)
        {
            appServiceSubnet = new SubnetConfiguration
            {
                Name = "appservice-subnet",
                AddressPrefix = "10.0.1.0/24",
                Delegation = "Microsoft.Web/serverFarms"
            };
        }
        
        sb.AppendLine($@"# Subnet for App Service VNet Integration
resource ""azurerm_subnet"" ""app_service"" {{
  name                 = ""{appServiceSubnet.Name}""
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [""{appServiceSubnet.AddressPrefix}""]");
        
        // Service Endpoints
        if (appServiceSubnet.EnableServiceEndpoints && appServiceSubnet.ServiceEndpoints.Any())
        {
            sb.AppendLine($@"
  service_endpoints = [{string.Join(", ", appServiceSubnet.ServiceEndpoints.Select(se => $"\"{se}\""))}]");
        }
        
        // Delegation for App Service
        sb.AppendLine($@"
  # Delegation for App Service VNet Integration
  delegation {{
    name = ""appservice-delegation""
    
    service_delegation {{
      name    = ""{appServiceSubnet.Delegation ?? "Microsoft.Web/serverFarms"}""
      actions = [
        ""Microsoft.Network/virtualNetworks/subnets/action""
      ]
    }}
  }}
}}");
        sb.AppendLine();
        
        // Private Endpoint Subnet (if enabled)
        if (networkConfig.EnablePrivateEndpoint)
        {
            var privateEndpointSubnet = networkConfig.Subnets.FirstOrDefault(s => s.Name == networkConfig.PrivateEndpointSubnetName);
            if (privateEndpointSubnet == null)
            {
                privateEndpointSubnet = new SubnetConfiguration
                {
                    Name = networkConfig.PrivateEndpointSubnetName,
                    AddressPrefix = "10.0.2.0/24"
                };
            }
            
            sb.AppendLine($@"# Subnet for Private Endpoints
resource ""azurerm_subnet"" ""private_endpoints"" {{
  name                 = ""{privateEndpointSubnet.Name}""
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [""{privateEndpointSubnet.AddressPrefix}""]");
            
            // Service Endpoints for Private Endpoint subnet
            if (privateEndpointSubnet.EnableServiceEndpoints && privateEndpointSubnet.ServiceEndpoints.Any())
            {
                sb.AppendLine($@"
  service_endpoints = [{string.Join(", ", privateEndpointSubnet.ServiceEndpoints.Select(se => $"\"{se}\""))}]");
            }
            
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        // Network Security Group (if enabled)
        if (networkConfig.EnableNetworkSecurityGroup)
        {
            if (networkConfig.NsgMode == "existing" && !string.IsNullOrEmpty(networkConfig.ExistingNsgResourceId))
            {
                // Use existing NSG
                sb.AppendLine($@"# Reference existing Network Security Group
data ""azurerm_network_security_group"" ""app_service"" {{
  name                = element(split(""/"", ""{networkConfig.ExistingNsgResourceId}""), length(split(""/"", ""{networkConfig.ExistingNsgResourceId}"")) - 1)
  resource_group_name = element(split(""/"", ""{networkConfig.ExistingNsgResourceId}""), length(split(""/"", ""{networkConfig.ExistingNsgResourceId}"")) - 5)
}}");
            }
            else
            {
                // Create new NSG
                var nsgName = string.IsNullOrEmpty(networkConfig.NsgName) ? "nsg-${{var.service_name}}" : networkConfig.NsgName;
                sb.AppendLine($@"# Network Security Group for App Service Subnet
resource ""azurerm_network_security_group"" ""app_service"" {{
  name                = ""{nsgName}""
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
                // Default rule: Allow outbound internet access (required for App Service)
                sb.AppendLine($@"
  # Allow outbound internet access (required for App Service)
  security_rule {{
    name                       = ""AllowInternetOutbound""
    priority                   = 100
    direction                  = ""Outbound""
    access                     = ""Allow""
    protocol                   = ""*""
    source_port_range          = ""*""
    destination_port_range     = ""*""
    source_address_prefix      = ""*""
    destination_address_prefix = ""Internet""
  }}");
            }
            
            sb.AppendLine(@"
  tags = var.tags
}");
            }  // Close else block for NSG mode
            sb.AppendLine();
            
            sb.AppendLine($@"# Associate NSG with App Service Subnet
resource ""azurerm_subnet_network_security_group_association"" ""app_service"" {{
  subnet_id                 = azurerm_subnet.app_service.id
  network_security_group_id = {(networkConfig.NsgMode == "existing" ? "data.azurerm_network_security_group.app_service.id" : "azurerm_network_security_group.app_service.id")}
}}");
            sb.AppendLine();
        }
        
        // Private DNS Zone and Private Endpoint (if enabled)
        if (networkConfig.EnablePrivateEndpoint)
        {
            var privateDnsZone = !string.IsNullOrEmpty(networkConfig.PrivateDnsZoneName) 
                ? networkConfig.PrivateDnsZoneName 
                : "privatelink.azurewebsites.net";
            
            if (networkConfig.EnablePrivateDns)
            {
                sb.AppendLine($@"# Private DNS Zone for Private Endpoint
resource ""azurerm_private_dns_zone"" ""app_service"" {{
  name                = ""{privateDnsZone}""
  resource_group_name = var.resource_group_name
  
  tags = var.tags
}}");
                sb.AppendLine();
                
                sb.AppendLine($@"# Link Private DNS Zone to VNet
resource ""azurerm_private_dns_zone_virtual_network_link"" ""app_service"" {{
  name                  = ""pdns-link-${{var.service_name}}""
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.app_service.name
  virtual_network_id    = azurerm_virtual_network.main.id
  
  tags = var.tags
}}");
                sb.AppendLine();
            }
            
            sb.AppendLine($@"# Private Endpoint for App Service
resource ""azurerm_private_endpoint"" ""app_service"" {{
  name                = ""pe-${{var.service_name}}""
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.private_endpoints.id
  
  private_service_connection {{
    name                           = ""psc-${{var.service_name}}""
    private_connection_resource_id = azurerm_linux_web_app.main.id
    subresource_names              = [""sites""]
    is_manual_connection           = false
  }}");
            
            if (networkConfig.EnablePrivateDns)
            {
                sb.AppendLine($@"
  private_dns_zone_group {{
    name                 = ""pdns-group-${{var.service_name}}""
    private_dns_zone_ids = [azurerm_private_dns_zone.app_service.id]
  }}");
            }
            
            sb.AppendLine(@"
  tags = var.tags
}");
            sb.AppendLine();
        }
        
        // VNet Integration for Web App
        sb.AppendLine($@"# VNet Integration for Web App (enables outbound traffic through VNet)
resource ""azurerm_app_service_virtual_network_swift_connection"" ""main"" {{
  app_service_id = azurerm_linux_web_app.main.id
  subnet_id      = azurerm_subnet.app_service.id
}}");
        sb.AppendLine();
        
        // Outputs
        sb.AppendLine($@"# Output network information
output ""vnet_id"" {{
  description = ""ID of the Virtual Network""
  value       = azurerm_virtual_network.main.id
}}

output ""vnet_name"" {{
  description = ""Name of the Virtual Network""
  value       = azurerm_virtual_network.main.name
}}

output ""app_service_subnet_id"" {{
  description = ""ID of the App Service subnet""
  value       = azurerm_subnet.app_service.id
}}");
        
        if (networkConfig.EnablePrivateEndpoint)
        {
            sb.AppendLine($@"
output ""private_endpoint_ip"" {{
  description = ""Private IP address of the App Service Private Endpoint""
  value       = azurerm_private_endpoint.app_service.private_service_connection[0].private_ip_address
}}

output ""private_endpoint_subnet_id"" {{
  description = ""ID of the Private Endpoint subnet""
  value       = azurerm_subnet.private_endpoints.id
}}");
        }
        
        if (networkConfig.EnablePrivateDns)
        {
            sb.AppendLine($@"
output ""private_dns_zone_id"" {{
  description = ""ID of the Private DNS Zone""
  value       = azurerm_private_dns_zone.app_service.id
}}");
        }
        
        return sb.ToString();
    }
    
    private string GenerateExistingNetworkReferences(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Reference Existing Network Resources");
        sb.AppendLine();
        
        // Data source for existing VNet
        sb.AppendLine($@"# Reference existing Virtual Network
data ""azurerm_virtual_network"" ""main"" {{
  name                = ""{networkConfig.ExistingVNetName}""
  resource_group_name = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
        sb.AppendLine();
        
        // Data sources for existing subnets
        var appSubnet = networkConfig.ExistingSubnets.FirstOrDefault(s => 
            s.Purpose == SubnetPurpose.Application || s.Name.Contains("appservice"));
        var peSubnet = networkConfig.ExistingSubnets.FirstOrDefault(s => 
            s.Purpose == SubnetPurpose.PrivateEndpoints || s.Name.Contains("private"));
        
        if (appSubnet != null)
        {
            sb.AppendLine($@"# Reference existing App Service subnet
data ""azurerm_subnet"" ""app_service"" {{
  name                 = ""{appSubnet.Name}""
  virtual_network_name = data.azurerm_virtual_network.main.name
  resource_group_name  = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
            sb.AppendLine();
        }
        
        if (peSubnet != null && networkConfig.EnablePrivateEndpoint)
        {
            sb.AppendLine($@"# Reference existing Private Endpoint subnet
data ""azurerm_subnet"" ""private_endpoint"" {{
  name                 = ""{peSubnet.Name}""
  virtual_network_name = data.azurerm_virtual_network.main.name
  resource_group_name  = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
            sb.AppendLine();
        }
        
        // Optionally generate NSG if needed (attached to existing subnets)
        if (networkConfig.EnableNetworkSecurityGroup && networkConfig.NsgRules.Any())
        {
            sb.AppendLine($@"# Network Security Group for existing subnets
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
            
            // Associate NSG with existing app service subnet
            if (appSubnet != null)
            {
                sb.AppendLine($@"
resource ""azurerm_subnet_network_security_group_association"" ""app_service"" {{
  subnet_id                 = data.azurerm_subnet.app_service.id
  network_security_group_id = azurerm_network_security_group.main.id
}}");
                sb.AppendLine();
            }
        }
        
        // Private Endpoint if enabled
        if (networkConfig.EnablePrivateEndpoint && peSubnet != null)
        {
            sb.AppendLine($@"# Private Endpoint for App Service
resource ""azurerm_private_endpoint"" ""app_service"" {{
  name                = ""pe-${{var.service_name}}""
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = data.azurerm_subnet.private_endpoint.id
  
  private_service_connection {{
    name                           = ""psc-${{var.service_name}}""
    private_connection_resource_id = azurerm_linux_web_app.main.id
    subresource_names              = [""sites""]
    is_manual_connection           = false
  }}
  
  tags = var.tags
}}");
            sb.AppendLine();
            
            if (networkConfig.EnablePrivateDns)
            {
                var dnsZoneName = string.IsNullOrEmpty(networkConfig.PrivateDnsZoneName) 
                    ? "privatelink.azurewebsites.net" 
                    : networkConfig.PrivateDnsZoneName;
                
                sb.AppendLine($@"
resource ""azurerm_private_dns_zone"" ""appservice"" {{
  name                = ""{dnsZoneName}""
  resource_group_name = var.resource_group_name
  tags                = var.tags
}}

resource ""azurerm_private_dns_zone_virtual_network_link"" ""appservice"" {{
  name                  = ""link-${{var.service_name}}""
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.appservice.name
  virtual_network_id    = data.azurerm_virtual_network.main.id
  tags                  = var.tags
}}

resource ""azurerm_private_dns_a_record"" ""appservice"" {{
  name                = ""${{var.service_name}}""
  zone_name           = azurerm_private_dns_zone.appservice.name
  resource_group_name = var.resource_group_name
  ttl                 = 300
  records             = [azurerm_private_endpoint.app_service.private_service_connection[0].private_ip_address]
  tags                = var.tags
}}");
                sb.AppendLine();
            }
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
                    Name = "appservice-subnet",
                    AddressPrefix = "10.0.1.0/24",
                    Delegation = "Microsoft.Web/serverFarms",
                    Purpose = SubnetPurpose.Application
                },
                new SubnetConfiguration
                {
                    Name = "privateendpoints-subnet",
                    AddressPrefix = "10.0.2.0/24",
                    Purpose = SubnetPurpose.PrivateEndpoints
                }
            },
            EnableNetworkSecurityGroup = true,
            EnablePrivateEndpoint = true,
            EnableServiceEndpoints = false,
            EnableDDoSProtection = false,
            EnablePrivateDns = true,
            PrivateDnsZoneName = "privatelink.azurewebsites.net",
            PrivateEndpointSubnetName = "privateendpoints-subnet"
        };
    }
    
    private string GetOSType(ApplicationSpec? app)
    {
        if (app == null) return "Linux";
        
        return app.Language switch
        {
            ProgrammingLanguage.DotNet => "Windows",
            _ => "Linux"
        };
    }
    
    private string GetRuntimeStack(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.NodeJS => "Node.js 18 LTS",
            ProgrammingLanguage.Python => "Python 3.11",
            ProgrammingLanguage.DotNet => ".NET 8",
            ProgrammingLanguage.Java => "Java 17",
            ProgrammingLanguage.PHP => "PHP 8.2",
            ProgrammingLanguage.Ruby => "Ruby 3.2",
            _ => "Node.js 18 LTS"
        };
    }
}
