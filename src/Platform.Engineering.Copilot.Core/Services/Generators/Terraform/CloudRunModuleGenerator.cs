using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates production-ready Google Cloud Run Terraform modules
/// </summary>
public class CloudRunModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        // Use correct properties from model
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Core Cloud Run resources
        files["cloudrun/service.tf"] = GenerateService(request);
        files["cloudrun/iam.tf"] = GenerateIAM(request);
        files["cloudrun/variables.tf"] = GenerateVariables();
        files["cloudrun/outputs.tf"] = GenerateOutputs();
        
        // Optional VPC connector for private networking
        if (infrastructure.IncludeNetworking == true)
        {
            files["cloudrun/vpc_connector.tf"] = GenerateVPCConnector(request);
        }
        
        // Optional Cloud Build trigger for CI/CD
        if (app.Type == ApplicationType.WebAPI || app.Type == ApplicationType.WebApp)
        {
            files["cloudrun/cloud_build.tf"] = GenerateCloudBuild(request);
        }
        
        return files;
    }
    
    private string GenerateService(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "cloudrun-service";
        var app = request.Application ?? new ApplicationSpec();
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        var port = app.Port > 0 ? app.Port : 8080;
        var cpu = ParseCPUValue(deployment.Resources.CpuLimit ?? "1");
        var memory = deployment.Resources.MemoryLimit ?? "512Mi";
        var minInstances = deployment.AutoScaling ? deployment.MinReplicas : deployment.Replicas;
        var maxInstances = deployment.AutoScaling ? deployment.MaxReplicas : deployment.Replicas;
        
        sb.AppendLine("# Cloud Run Service Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"google_cloud_run_service\" \"service\" {");
        sb.AppendLine($"  name     = var.service_name");
        sb.AppendLine($"  location = var.region");
        sb.AppendLine();
        sb.AppendLine("  template {");
        sb.AppendLine("    metadata {");
        sb.AppendLine("      annotations = {");
        sb.AppendLine($"        \"autoscaling.knative.dev/minScale\" = \"{minInstances}\"");
        sb.AppendLine($"        \"autoscaling.knative.dev/maxScale\" = \"{maxInstances}\"");
        sb.AppendLine("        \"run.googleapis.com/execution-environment\" = \"gen2\"");
        
        if (infrastructure.IncludeNetworking)
        {
            sb.AppendLine("        \"run.googleapis.com/vpc-access-connector\" = google_vpc_access_connector.connector.id");
            sb.AppendLine("        \"run.googleapis.com/vpc-access-egress\" = \"private-ranges-only\"");
        }
        
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    spec {");
        sb.AppendLine("      container_concurrency = var.container_concurrency");
        sb.AppendLine($"      timeout_seconds       = var.timeout_seconds");
        sb.AppendLine("      service_account_name  = google_service_account.cloudrun_sa.email");
        sb.AppendLine();
        sb.AppendLine("      containers {");
        sb.AppendLine("        image = var.container_image");
        sb.AppendLine();
        sb.AppendLine("        ports {");
        sb.AppendLine($"          container_port = {port}");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        resources {");
        sb.AppendLine("          limits = {");
        sb.AppendLine($"            cpu    = \"{cpu}\"");
        sb.AppendLine($"            memory = \"{memory}\"");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Environment variables
        if (app.EnvironmentVariables != null && app.EnvironmentVariables.Any())
        {
            foreach (var envVar in app.EnvironmentVariables)
            {
                sb.AppendLine("        env {");
                sb.AppendLine($"          name  = \"{envVar.Key}\"");
                sb.AppendLine($"          value = \"{envVar.Value}\"");
                sb.AppendLine("        }");
            }
        }
        
        // Health check (if enabled)
        if (app.IncludeHealthCheck)
        {
            sb.AppendLine();
            sb.AppendLine("        startup_probe {");
            sb.AppendLine("          http_get {");
            sb.AppendLine("            path = \"/health\"");
            sb.AppendLine($"            port = {port}");
            sb.AppendLine("          }");
            sb.AppendLine("          initial_delay_seconds = 10");
            sb.AppendLine("          timeout_seconds       = 3");
            sb.AppendLine("          period_seconds        = 10");
            sb.AppendLine("          failure_threshold     = 3");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        liveness_probe {");
            sb.AppendLine("          http_get {");
            sb.AppendLine("            path = \"/health\"");
            sb.AppendLine($"            port = {port}");
            sb.AppendLine("          }");
            sb.AppendLine("          initial_delay_seconds = 30");
            sb.AppendLine("          timeout_seconds       = 3");
            sb.AppendLine("          period_seconds        = 30");
            sb.AppendLine("          failure_threshold     = 3");
            sb.AppendLine("        }");
        }
        
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  traffic {");
        sb.AppendLine("    percent         = 100");
        sb.AppendLine("    latest_revision = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  autogenerate_revision_name = true");
        sb.AppendLine();
        sb.AppendLine("  lifecycle {");
        sb.AppendLine("    ignore_changes = [");
        sb.AppendLine("      template[0].metadata[0].annotations[\"run.googleapis.com/client-name\"],");
        sb.AppendLine("      template[0].metadata[0].annotations[\"run.googleapis.com/client-version\"],");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // IAM policy for public or authenticated access
        sb.AppendLine("# IAM policy for service access");
        sb.AppendLine("resource \"google_cloud_run_service_iam_member\" \"public_access\" {");
        sb.AppendLine("  count    = var.allow_unauthenticated ? 1 : 0");
        sb.AppendLine("  service  = google_cloud_run_service.service.name");
        sb.AppendLine("  location = google_cloud_run_service.service.location");
        sb.AppendLine("  role     = \"roles/run.invoker\"");
        sb.AppendLine("  member   = \"allUsers\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateIAM(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Service Account for Cloud Run");
        sb.AppendLine("resource \"google_service_account\" \"cloudrun_sa\" {");
        sb.AppendLine("  account_id   = \"${var.service_name}-sa\"");
        sb.AppendLine("  display_name = \"Service Account for ${var.service_name}\"");
        sb.AppendLine("  description  = \"Managed by Terraform\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Basic permissions for Cloud Run
        sb.AppendLine("# IAM Bindings for Cloud Run Service Account");
        sb.AppendLine("resource \"google_project_iam_member\" \"cloudrun_logging\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/logging.logWriter\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.cloudrun_sa.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"google_project_iam_member\" \"cloudrun_metrics\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/monitoring.metricWriter\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.cloudrun_sa.email}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"google_project_iam_member\" \"cloudrun_trace\" {");
        sb.AppendLine("  project = var.project_id");
        sb.AppendLine("  role    = \"roles/cloudtrace.agent\"");
        sb.AppendLine("  member  = \"serviceAccount:${google_service_account.cloudrun_sa.email}\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVPCConnector(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("# VPC Access Connector for private networking");
        sb.AppendLine("resource \"google_vpc_access_connector\" \"connector\" {");
        sb.AppendLine("  name          = \"${var.service_name}-connector\"");
        sb.AppendLine("  region        = var.region");
        sb.AppendLine("  ip_cidr_range = var.vpc_connector_cidr");
        sb.AppendLine("  network       = var.vpc_network");
        sb.AppendLine();
        sb.AppendLine("  min_instances = 2");
        sb.AppendLine("  max_instances = 10");
        sb.AppendLine("  machine_type  = \"e2-micro\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateCloudBuild(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        
        sb.AppendLine("# Cloud Build Trigger for CI/CD");
        sb.AppendLine("resource \"google_cloudbuild_trigger\" \"build_trigger\" {");
        sb.AppendLine("  count    = var.enable_cloud_build ? 1 : 0");
        sb.AppendLine("  name     = \"${var.service_name}-trigger\"");
        sb.AppendLine("  location = var.region");
        sb.AppendLine();
        sb.AppendLine("  github {");
        sb.AppendLine("    owner = var.github_owner");
        sb.AppendLine("    name  = var.github_repo");
        sb.AppendLine("    push {");
        sb.AppendLine("      branch = \"^main$\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  build {");
        sb.AppendLine("    step {");
        sb.AppendLine("      name = \"gcr.io/cloud-builders/docker\"");
        sb.AppendLine("      args = [");
        sb.AppendLine("        \"build\",");
        sb.AppendLine("        \"-t\",");
        sb.AppendLine("        \"gcr.io/$PROJECT_ID/${var.service_name}:$COMMIT_SHA\",");
        sb.AppendLine("        \".\"");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    step {");
        sb.AppendLine("      name = \"gcr.io/cloud-builders/docker\"");
        sb.AppendLine("      args = [");
        sb.AppendLine("        \"push\",");
        sb.AppendLine("        \"gcr.io/$PROJECT_ID/${var.service_name}:$COMMIT_SHA\"");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    step {");
        sb.AppendLine("      name = \"gcr.io/cloud-builders/gcloud\"");
        sb.AppendLine("      args = [");
        sb.AppendLine("        \"run\",");
        sb.AppendLine("        \"deploy\",");
        sb.AppendLine("        \"${var.service_name}\",");
        sb.AppendLine("        \"--image=gcr.io/$PROJECT_ID/${var.service_name}:$COMMIT_SHA\",");
        sb.AppendLine("        \"--region=${var.region}\",");
        sb.AppendLine("        \"--platform=managed\"");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    images = [\"gcr.io/$PROJECT_ID/${var.service_name}:$COMMIT_SHA\"]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVariables()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Cloud Run Variables");
        sb.AppendLine();
        sb.AppendLine("variable \"project_id\" {");
        sb.AppendLine("  description = \"GCP Project ID\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"region\" {");
        sb.AppendLine("  description = \"GCP region for Cloud Run service\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"us-central1\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"service_name\" {");
        sb.AppendLine("  description = \"Name of the Cloud Run service\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"container_image\" {");
        sb.AppendLine("  description = \"Container image to deploy\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"container_concurrency\" {");
        sb.AppendLine("  description = \"Maximum number of concurrent requests per container instance\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 80");
        sb.AppendLine();
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = var.container_concurrency >= 1 && var.container_concurrency <= 1000");
        sb.AppendLine("    error_message = \"Container concurrency must be between 1 and 1000.\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"timeout_seconds\" {");
        sb.AppendLine("  description = \"Request timeout in seconds\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 300");
        sb.AppendLine();
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = var.timeout_seconds >= 1 && var.timeout_seconds <= 3600");
        sb.AppendLine("    error_message = \"Timeout must be between 1 and 3600 seconds.\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allow_unauthenticated\" {");
        sb.AppendLine("  description = \"Allow unauthenticated access to the service\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_network\" {");
        sb.AppendLine("  description = \"VPC network name for VPC connector\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"default\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_connector_cidr\" {");
        sb.AppendLine("  description = \"CIDR range for VPC connector\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.8.0.0/28\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloud_build\" {");
        sb.AppendLine("  description = \"Enable Cloud Build trigger for CI/CD\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"github_owner\" {");
        sb.AppendLine("  description = \"GitHub repository owner\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"github_repo\" {");
        sb.AppendLine("  description = \"GitHub repository name\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === ZERO TRUST SECURITY PARAMETERS ===
        sb.AppendLine("# === ZERO TRUST SECURITY PARAMETERS ===");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_vpc_connector\" {");
        sb.AppendLine("  description = \"Enable Serverless VPC Access connector for private networking\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"ingress_settings\" {");
        sb.AppendLine("  description = \"Ingress settings: 'all', 'internal', 'internal-and-cloud-load-balancing'\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"internal-and-cloud-load-balancing\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_service_identity\" {");
        sb.AppendLine("  description = \"Use dedicated service account with least privilege\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_binary_authorization\" {");
        sb.AppendLine("  description = \"Enable Binary Authorization for container image verification\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloud_armor\" {");
        sb.AppendLine("  description = \"Enable Cloud Armor for DDoS protection\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"encryption_key\" {");
        sb.AppendLine("  description = \"Customer-managed encryption key (CMEK) for data at rest\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloud_audit_logs\" {");
        sb.AppendLine("  description = \"Enable Cloud Audit Logs for access tracking\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloud_monitoring\" {");
        sb.AppendLine("  description = \"Enable Cloud Monitoring integration\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"max_instance_request_concurrency\" {");
        sb.AppendLine("  description = \"Maximum concurrent requests per instance (Gen2 only)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 80");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_https_only\" {");
        sb.AppendLine("  description = \"Enforce HTTPS-only access\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_ingress_sources\" {");
        sb.AppendLine("  description = \"List of allowed ingress source IP ranges\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_vpc_egress\" {");
        sb.AppendLine("  description = \"Route all egress traffic through VPC\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"egress_settings\" {");
        sb.AppendLine("  description = \"Egress settings: 'all', 'private-ranges-only'\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"private-ranges-only\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_execution_environment_v2\" {");
        sb.AppendLine("  description = \"Use second generation execution environment\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cpu_throttling\" {");
        sb.AppendLine("  description = \"Enable CPU throttling outside of requests\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"startup_cpu_boost\" {");
        sb.AppendLine("  description = \"Enable startup CPU boost for faster cold starts\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_session_affinity\" {");
        sb.AppendLine("  description = \"Enable session affinity for stateful services\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateOutputs()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Cloud Run Outputs");
        sb.AppendLine();
        sb.AppendLine("output \"service_url\" {");
        sb.AppendLine("  description = \"URL of the deployed Cloud Run service\"");
        sb.AppendLine("  value       = google_cloud_run_service.service.status[0].url");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"service_name\" {");
        sb.AppendLine("  description = \"Name of the Cloud Run service\"");
        sb.AppendLine("  value       = google_cloud_run_service.service.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"service_id\" {");
        sb.AppendLine("  description = \"ID of the Cloud Run service\"");
        sb.AppendLine("  value       = google_cloud_run_service.service.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"service_account_email\" {");
        sb.AppendLine("  description = \"Email of the service account\"");
        sb.AppendLine("  value       = google_service_account.cloudrun_sa.email");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"latest_ready_revision\" {");
        sb.AppendLine("  description = \"Latest ready revision name\"");
        sb.AppendLine("  value       = google_cloud_run_service.service.status[0].latest_ready_revision_name");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    // Helper methods
    private string ParseCPUValue(string cpu)
    {
        // Cloud Run supports: "1", "2", "4", "8" (vCPUs)
        // Also supports milliCPU: "1000m" = 1 vCPU
        if (cpu.Contains("vCPU"))
        {
            var value = cpu.Replace("vCPU", "").Trim();
            return double.TryParse(value, out var cpuValue) ? cpuValue.ToString() : "1";
        }
        
        if (cpu.EndsWith("m"))
        {
            var value = cpu.Replace("m", "").Trim();
            if (int.TryParse(value, out var milliCpu))
            {
                return (milliCpu / 1000.0).ToString();
            }
        }
        
        return cpu;
    }
}
