using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform module for GCP GKE (Google Kubernetes Engine)
/// Follows the same modular pattern as EKS and AKS generators
/// </summary>
public class GKEModuleGenerator
{
    public Dictionary<string, string> GenerateGKEModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var serviceName = request.ServiceName ?? "gke-service";
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Generate all GKE module files
        files["infra/terraform/modules/gke/cluster.tf"] = GenerateClusterConfig(request);
        files["infra/terraform/modules/gke/node_pools.tf"] = GenerateNodePoolsConfig(request);
        files["infra/terraform/modules/gke/iam.tf"] = GenerateIAMConfig(request);
        files["infra/terraform/modules/gke/vpc.tf"] = GenerateVPCConfig(request);
        files["infra/terraform/modules/gke/firewall.tf"] = GenerateFirewallConfig(request);
        files["infra/terraform/modules/gke/service_account.tf"] = GenerateServiceAccountConfig(request);
        files["infra/terraform/modules/gke/autoscaling.tf"] = GenerateAutoScalingConfig(request);
        files["infra/terraform/modules/gke/logging.tf"] = GenerateLoggingConfig(request);
        files["infra/terraform/modules/gke/variables.tf"] = GenerateVariablesConfig(request);
        files["infra/terraform/modules/gke/outputs.tf"] = GenerateOutputsConfig(request);
        
        return files;
    }
    
    private string GenerateClusterConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "gke-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        var region = infrastructure.Region ?? "us-central1";
        
        sb.AppendLine("# GKE Cluster Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"google_container_cluster\" \"primary\" {");
        sb.AppendLine("  name     = var.cluster_name");
        sb.AppendLine("  location = var.region");
        sb.AppendLine();
        sb.AppendLine("  # We can't create a cluster with no node pool defined, but we want to only use");
        sb.AppendLine("  # separately managed node pools. So we create the smallest possible default");
        sb.AppendLine("  # node pool and immediately delete it.");
        sb.AppendLine("  remove_default_node_pool = true");
        sb.AppendLine("  initial_node_count       = 1");
        sb.AppendLine();
        sb.AppendLine("  # Network configuration");
        sb.AppendLine("  network    = google_compute_network.vpc.name");
        sb.AppendLine("  subnetwork = google_compute_subnetwork.subnet.name");
        sb.AppendLine();
        sb.AppendLine("  # IP allocation policy for VPC-native cluster");
        sb.AppendLine("  ip_allocation_policy {");
        sb.AppendLine("    cluster_secondary_range_name  = \"pods\"");
        sb.AppendLine("    services_secondary_range_name = \"services\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Private cluster configuration
        sb.AppendLine("  # Private cluster configuration");
        sb.AppendLine("  private_cluster_config {");
        sb.AppendLine("    enable_private_nodes    = true");
        sb.AppendLine("    enable_private_endpoint = false");
        sb.AppendLine("    master_ipv4_cidr_block  = \"172.16.0.0/28\"");
        sb.AppendLine();
        sb.AppendLine("    master_global_access_config {");
        sb.AppendLine("      enabled = true");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Master authorized networks
        sb.AppendLine("  # Master authorized networks");
        sb.AppendLine("  master_authorized_networks_config {");
        sb.AppendLine("    cidr_blocks {");
        sb.AppendLine("      cidr_block   = \"0.0.0.0/0\"");
        sb.AppendLine("      display_name = \"All\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        if (security.RBAC)
        {
            sb.AppendLine("  # Enable RBAC");
            sb.AppendLine("  # RBAC is enabled by default in GKE");
        }
        
        // Workload Identity
        sb.AppendLine("  # Workload Identity");
        sb.AppendLine("  workload_identity_config {");
        sb.AppendLine("    workload_pool = \"${var.project_id}.svc.id.goog\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Addons
        sb.AppendLine("  # Add-ons");
        sb.AppendLine("  addons_config {");
        sb.AppendLine("    http_load_balancing {");
        sb.AppendLine("      disabled = false");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    horizontal_pod_autoscaling {");
        sb.AppendLine("      disabled = false");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    network_policy_config {");
        sb.AppendLine("      disabled = false");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    gce_persistent_disk_csi_driver_config {");
        sb.AppendLine("      enabled = true");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Network policy
        if (security.NetworkPolicies)
        {
            sb.AppendLine("  # Network policy");
            sb.AppendLine("  network_policy {");
            sb.AppendLine("    enabled  = true");
            sb.AppendLine("    provider = \"PROVIDER_UNSPECIFIED\"");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
        
        // Maintenance policy
        sb.AppendLine("  # Maintenance policy");
        sb.AppendLine("  maintenance_policy {");
        sb.AppendLine("    daily_maintenance_window {");
        sb.AppendLine("      start_time = \"03:00\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Release channel
        sb.AppendLine("  # Release channel");
        sb.AppendLine("  release_channel {");
        sb.AppendLine("    channel = \"REGULAR\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Security features
        sb.AppendLine("  # Security features");
        sb.AppendLine("  binary_authorization {");
        sb.AppendLine("    evaluation_mode = \"PROJECT_SINGLETON_POLICY_ENFORCE\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Enable Shielded Nodes");
        sb.AppendLine("  enable_shielded_nodes = true");
        sb.AppendLine();
        
        // Resource labels
        sb.AppendLine("  resource_labels = var.labels");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateNodePoolsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        var resources = deployment.Resources ?? new ResourceRequirements();
        
        var minNodes = deployment.MinReplicas > 0 ? deployment.MinReplicas : 2;
        var maxNodes = deployment.MaxReplicas > 0 ? deployment.MaxReplicas : 10;
        var initialNodes = deployment.Replicas > 0 ? deployment.Replicas : 3;
        
        sb.AppendLine("# GKE Node Pool Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"google_container_node_pool\" \"primary\" {");
        sb.AppendLine("  name       = \"${var.cluster_name}-node-pool\"");
        sb.AppendLine("  location   = var.region");
        sb.AppendLine("  cluster    = google_container_cluster.primary.name");
        sb.AppendLine($"  node_count = {initialNodes}");
        sb.AppendLine();
        
        // Autoscaling
        if (deployment.AutoScaling)
        {
            sb.AppendLine("  autoscaling {");
            sb.AppendLine($"    min_node_count = {minNodes}");
            sb.AppendLine($"    max_node_count = {maxNodes}");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
        
        // Node configuration
        sb.AppendLine("  node_config {");
        
        // Determine machine type based on resource requirements
        var machineType = DetermineMachineType(resources);
        sb.AppendLine($"    machine_type = \"{machineType}\"");
        sb.AppendLine();
        
        sb.AppendLine("    # Service account");
        sb.AppendLine("    service_account = google_service_account.node.email");
        sb.AppendLine("    oauth_scopes = [");
        sb.AppendLine("      \"https://www.googleapis.com/auth/cloud-platform\"");
        sb.AppendLine("    ]");
        sb.AppendLine();
        
        sb.AppendLine("    # Workload Identity");
        sb.AppendLine("    workload_metadata_config {");
        sb.AppendLine("      mode = \"GKE_METADATA\"");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    # Disk configuration");
        sb.AppendLine("    disk_size_gb = 100");
        sb.AppendLine("    disk_type    = \"pd-standard\"");
        sb.AppendLine();
        
        sb.AppendLine("    # Image type");
        sb.AppendLine("    image_type = \"COS_CONTAINERD\"");
        sb.AppendLine();
        
        sb.AppendLine("    # Shielded instance config");
        sb.AppendLine("    shielded_instance_config {");
        sb.AppendLine("      enable_secure_boot          = true");
        sb.AppendLine("      enable_integrity_monitoring = true");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    # Node labels");
        sb.AppendLine("    labels = {");
        sb.AppendLine("      role        = \"worker\"");
        sb.AppendLine("      environment = var.environment");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    # Node taints (none for general workloads)");
        sb.AppendLine("    # taint = []");
        sb.AppendLine();
        
        sb.AppendLine("    # Metadata");
        sb.AppendLine("    metadata = {");
        sb.AppendLine("      disable-legacy-endpoints = \"true\"");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    tags = [var.cluster_name, \"gke-node\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Management
        sb.AppendLine("  management {");
        sb.AppendLine("    auto_repair  = true");
        sb.AppendLine("    auto_upgrade = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Upgrade settings
        sb.AppendLine("  upgrade_settings {");
        sb.AppendLine("    max_surge       = 1");
        sb.AppendLine("    max_unavailable = 0");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateIAMConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# IAM Configuration for GKE Workload Identity");
        sb.AppendLine();
        sb.AppendLine("# Service account for Kubernetes workloads");
        sb.AppendLine("resource \"google_service_account\" \"workload\" {");
        sb.AppendLine("  account_id   = \"${var.cluster_name}-workload\"");
        sb.AppendLine("  display_name = \"GKE Workload Identity Service Account\"");
        sb.AppendLine("  project      = var.project_id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Allow Kubernetes service accounts to impersonate the Google service account");
        sb.AppendLine("resource \"google_service_account_iam_binding\" \"workload_identity\" {");
        sb.AppendLine("  service_account_id = google_service_account.workload.name");
        sb.AppendLine("  role               = \"roles/iam.workloadIdentityUser\"");
        sb.AppendLine();
        sb.AppendLine("  members = [");
        sb.AppendLine("    \"serviceAccount:${var.project_id}.svc.id.goog[${var.namespace}/${var.service_account_name}]\"");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Grant necessary permissions to the workload service account");
        sb.AppendLine("resource \"google_project_iam_member\" \"workload_logging\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/logging.logWriter\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.workload.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"google_project_iam_member\" \"workload_monitoring\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/monitoring.metricWriter\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.workload.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"google_project_iam_member\" \"workload_trace\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/cloudtrace.agent\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.workload.email}\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVPCConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# VPC Configuration for GKE");
        sb.AppendLine();
        sb.AppendLine("resource \"google_compute_network\" \"vpc\" {");
        sb.AppendLine("  name                    = \"${var.cluster_name}-vpc\"");
        sb.AppendLine("  auto_create_subnetworks = false");
        sb.AppendLine("  project                 = var.project_id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Subnet
        sb.AppendLine("resource \"google_compute_subnetwork\" \"subnet\" {");
        sb.AppendLine("  name          = \"${var.cluster_name}-subnet\"");
        sb.AppendLine("  ip_cidr_range = var.subnet_cidr");
        sb.AppendLine("  region        = var.region");
        sb.AppendLine("  network       = google_compute_network.vpc.name");
        sb.AppendLine("  project       = var.project_id");
        sb.AppendLine();
        sb.AppendLine("  # Secondary IP ranges for pods and services");
        sb.AppendLine("  secondary_ip_range {");
        sb.AppendLine("    range_name    = \"pods\"");
        sb.AppendLine("    ip_cidr_range = var.pods_cidr");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  secondary_ip_range {");
        sb.AppendLine("    range_name    = \"services\"");
        sb.AppendLine("    ip_cidr_range = var.services_cidr");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Enable private Google access");
        sb.AppendLine("  private_ip_google_access = true");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Cloud Router for NAT
        sb.AppendLine("# Cloud Router for Cloud NAT");
        sb.AppendLine("resource \"google_compute_router\" \"router\" {");
        sb.AppendLine("  name    = \"${var.cluster_name}-router\"");
        sb.AppendLine("  region  = var.region");
        sb.AppendLine("  network = google_compute_network.vpc.id");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine();
        sb.AppendLine("  bgp {");
        sb.AppendLine("    asn = 64514");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Cloud NAT
        sb.AppendLine("# Cloud NAT for outbound internet access");
        sb.AppendLine("resource \"google_compute_router_nat\" \"nat\" {");
        sb.AppendLine("  name                               = \"${var.cluster_name}-nat\"");
        sb.AppendLine("  router                             = google_compute_router.router.name");
        sb.AppendLine("  region                             = google_compute_router.router.region");
        sb.AppendLine("  project                            = var.project_id");
        sb.AppendLine("  nat_ip_allocate_option             = \"AUTO_ONLY\"");
        sb.AppendLine("  source_subnetwork_ip_ranges_to_nat = \"ALL_SUBNETWORKS_ALL_IP_RANGES\"");
        sb.AppendLine();
        sb.AppendLine("  log_config {");
        sb.AppendLine("    enable = true");
        sb.AppendLine("    filter = \"ERRORS_ONLY\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateFirewallConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Firewall Rules for GKE");
        sb.AppendLine();
        sb.AppendLine("# Allow internal communication within VPC");
        sb.AppendLine("resource \"google_compute_firewall\" \"allow_internal\" {");
        sb.AppendLine("  name    = \"${var.cluster_name}-allow-internal\"");
        sb.AppendLine("  network = google_compute_network.vpc.name");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine();
        sb.AppendLine("  allow {");
        sb.AppendLine("    protocol = \"tcp\"");
        sb.AppendLine("    ports    = [\"0-65535\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  allow {");
        sb.AppendLine("    protocol = \"udp\"");
        sb.AppendLine("    ports    = [\"0-65535\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  allow {");
        sb.AppendLine("    protocol = \"icmp\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  source_ranges = [");
        sb.AppendLine("    var.subnet_cidr,");
        sb.AppendLine("    var.pods_cidr,");
        sb.AppendLine("    var.services_cidr");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Allow SSH from IAP");
        sb.AppendLine("resource \"google_compute_firewall\" \"allow_ssh_iap\" {");
        sb.AppendLine("  name    = \"${var.cluster_name}-allow-ssh-iap\"");
        sb.AppendLine("  network = google_compute_network.vpc.name");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine();
        sb.AppendLine("  allow {");
        sb.AppendLine("    protocol = \"tcp\"");
        sb.AppendLine("    ports    = [\"22\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # IAP IP range");
        sb.AppendLine("  source_ranges = [\"35.235.240.0/20\"]");
        sb.AppendLine("  target_tags   = [\"gke-node\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Allow health checks from GCP load balancers");
        sb.AppendLine("resource \"google_compute_firewall\" \"allow_health_checks\" {");
        sb.AppendLine("  name    = \"${var.cluster_name}-allow-health-checks\"");
        sb.AppendLine("  network = google_compute_network.vpc.name");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine();
        sb.AppendLine("  allow {");
        sb.AppendLine("    protocol = \"tcp\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  source_ranges = [");
        sb.AppendLine("    \"35.191.0.0/16\",");
        sb.AppendLine("    \"130.211.0.0/22\"");
        sb.AppendLine("  ]");
        sb.AppendLine("  target_tags = [\"gke-node\"]");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateServiceAccountConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Service Account for GKE Nodes");
        sb.AppendLine();
        sb.AppendLine("resource \"google_service_account\" \"node\" {");
        sb.AppendLine("  account_id   = \"${var.cluster_name}-node\"");
        sb.AppendLine("  display_name = \"GKE Node Service Account\"");
        sb.AppendLine("  project      = var.project_id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Grant necessary permissions to node service account");
        sb.AppendLine("resource \"google_project_iam_member\" \"node_logging\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/logging.logWriter\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.node.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"google_project_iam_member\" \"node_monitoring\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/monitoring.metricWriter\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.node.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"google_project_iam_member\" \"node_monitoring_viewer\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/monitoring.viewer\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.node.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"google_project_iam_member\" \"node_artifact_registry\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/artifactregistry.reader\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.node.email}\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateAutoScalingConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        if (!deployment.AutoScaling)
        {
            sb.AppendLine("# Auto-scaling is disabled");
            return sb.ToString();
        }
        
        sb.AppendLine("# Horizontal Pod Autoscaler is enabled by default in GKE");
        sb.AppendLine("# Cluster autoscaler is configured at the node pool level");
        sb.AppendLine();
        sb.AppendLine("# To use Vertical Pod Autoscaler:");
        sb.AppendLine("# resource \"google_container_cluster\" \"primary\" {");
        sb.AppendLine("#   vertical_pod_autoscaling {");
        sb.AppendLine("#     enabled = true");
        sb.AppendLine("#   }");
        sb.AppendLine("# }");
        
        return sb.ToString();
    }
    
    private string GenerateLoggingConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var observability = request.Observability ?? new ObservabilitySpec();
        
        sb.AppendLine("# Logging Configuration for GKE");
        sb.AppendLine();
        sb.AppendLine("# GKE automatically enables Cloud Logging");
        sb.AppendLine("# Logs are sent to Cloud Logging by default");
        sb.AppendLine();
        
        if (observability.Prometheus)
        {
            sb.AppendLine("# For Prometheus monitoring, consider Google Cloud Managed Service for Prometheus");
            sb.AppendLine("# https://cloud.google.com/stackdriver/docs/managed-prometheus");
        }
        
        if (observability.Grafana)
        {
            sb.AppendLine("# For Grafana, you can deploy it to the cluster or use Cloud Monitoring dashboards");
        }
        
        return sb.ToString();
    }
    
    private string GenerateVariablesConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "gke-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("# Variables for GKE Module");
        sb.AppendLine();
        sb.AppendLine("variable \"project_id\" {");
        sb.AppendLine("  description = \"GCP project ID\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cluster_name\" {");
        sb.AppendLine("  description = \"Name of the GKE cluster\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{serviceName}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"region\" {");
        sb.AppendLine("  description = \"GCP region for the cluster\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{infrastructure.Region ?? "us-central1"}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"subnet_cidr\" {");
        sb.AppendLine("  description = \"CIDR block for the subnet\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.0.0/24\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"pods_cidr\" {");
        sb.AppendLine("  description = \"CIDR block for pods\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.1.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"services_cidr\" {");
        sb.AppendLine("  description = \"CIDR block for services\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.2.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment\" {");
        sb.AppendLine("  description = \"Environment name (dev, staging, prod)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"dev\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"namespace\" {");
        sb.AppendLine("  description = \"Kubernetes namespace for workload identity\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"default\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"service_account_name\" {");
        sb.AppendLine("  description = \"Kubernetes service account name for workload identity\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"default\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === ZERO TRUST SECURITY PARAMETERS ===
        sb.AppendLine("# === ZERO TRUST SECURITY PARAMETERS ===");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_private_cluster\" {");
        sb.AppendLine("  description = \"Enable private cluster (nodes have no public IPs)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"master_ipv4_cidr_block\" {");
        sb.AppendLine("  description = \"CIDR block for master (control plane) private endpoint\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"172.16.0.0/28\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_workload_identity\" {");
        sb.AppendLine("  description = \"Enable Workload Identity for pod-level IAM\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_binary_authorization\" {");
        sb.AppendLine("  description = \"Enable Binary Authorization for image signing\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_shielded_nodes\" {");
        sb.AppendLine("  description = \"Enable Shielded GKE nodes\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_gke_autopilot\" {");
        sb.AppendLine("  description = \"Use GKE Autopilot mode (Google-managed nodes)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_network_policy\" {");
        sb.AppendLine("  description = \"Enable network policies (Dataplane V2 recommended)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloud_armor\" {");
        sb.AppendLine("  description = \"Enable Cloud Armor for ingress protection\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_pod_security_policy\" {");
        sb.AppendLine("  description = \"Enable Pod Security Policy (deprecated, use Pod Security Standards)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_secure_boot\" {");
        sb.AppendLine("  description = \"Enable Secure Boot for nodes\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_integrity_monitoring\" {");
        sb.AppendLine("  description = \"Enable integrity monitoring for nodes\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kms_key_name\" {");
        sb.AppendLine("  description = \"Cloud KMS key for application-layer secrets encryption\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"master_authorized_networks_cidr_blocks\" {");
        sb.AppendLine("  description = \"CIDR blocks allowed to access cluster master\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_vpc_native\" {");
        sb.AppendLine("  description = \"Use VPC-native cluster (alias IPs)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_private_endpoint\" {");
        sb.AppendLine("  description = \"Disable public access to cluster endpoint\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_intranode_visibility\" {");
        sb.AppendLine("  description = \"Enable intranode visibility for network monitoring\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_dataplane_v2\" {");
        sb.AppendLine("  description = \"Enable GKE Dataplane V2 (eBPF-based networking)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_workload_vulnerability_scanning\" {");
        sb.AppendLine("  description = \"Enable Container Analysis for vulnerability scanning\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_security_posture_dashboard\" {");
        sb.AppendLine("  description = \"Enable GKE Security Posture dashboard\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cost_optimization\" {");
        sb.AppendLine("  description = \"Enable cost optimization features (auto-scaling, bin packing)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"labels\" {");
        sb.AppendLine("  description = \"Labels to apply to all resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateOutputsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Outputs for GKE Module");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_id\" {");
        sb.AppendLine("  description = \"GKE cluster ID\"");
        sb.AppendLine("  value       = google_container_cluster.primary.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_name\" {");
        sb.AppendLine("  description = \"GKE cluster name\"");
        sb.AppendLine("  value       = google_container_cluster.primary.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_endpoint\" {");
        sb.AppendLine("  description = \"GKE cluster endpoint\"");
        sb.AppendLine("  value       = google_container_cluster.primary.endpoint");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_ca_certificate\" {");
        sb.AppendLine("  description = \"GKE cluster CA certificate\"");
        sb.AppendLine("  value       = google_container_cluster.primary.master_auth[0].cluster_ca_certificate");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_location\" {");
        sb.AppendLine("  description = \"GKE cluster location (region or zone)\"");
        sb.AppendLine("  value       = google_container_cluster.primary.location");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"vpc_name\" {");
        sb.AppendLine("  description = \"VPC network name\"");
        sb.AppendLine("  value       = google_compute_network.vpc.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"subnet_name\" {");
        sb.AppendLine("  description = \"Subnet name\"");
        sb.AppendLine("  value       = google_compute_subnetwork.subnet.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"node_service_account_email\" {");
        sb.AppendLine("  description = \"Node service account email\"");
        sb.AppendLine("  value       = google_service_account.node.email");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"workload_identity_service_account_email\" {");
        sb.AppendLine("  description = \"Workload Identity service account email\"");
        sb.AppendLine("  value       = google_service_account.workload.email");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"node_pool_name\" {");
        sb.AppendLine("  description = \"Node pool name\"");
        sb.AppendLine("  value       = google_container_node_pool.primary.name");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string DetermineMachineType(ResourceRequirements resources)
    {
        var cpuLimit = resources.CpuLimit?.ToLower() ?? "1 vcpu";
        var memoryLimit = resources.MemoryLimit?.ToLower() ?? "2 gb";
        
        // Parse CPU requirements
        var cpuValue = 1.0;
        if (cpuLimit.Contains("vcpu"))
        {
            var parts = cpuLimit.Split(' ');
            if (parts.Length > 0 && double.TryParse(parts[0], out var cpu))
            {
                cpuValue = cpu;
            }
        }
        
        // Determine machine type based on CPU and memory
        // GCP machine types: e2, n1, n2, n2d series
        if (cpuValue <= 2)
        {
            return "e2-medium"; // 2 vCPU, 4 GB
        }
        else if (cpuValue <= 4)
        {
            return "e2-standard-4"; // 4 vCPU, 16 GB
        }
        else if (cpuValue <= 8)
        {
            return "e2-standard-8"; // 8 vCPU, 32 GB
        }
        else
        {
            return "e2-standard-16"; // 16 vCPU, 64 GB
        }
    }
}
