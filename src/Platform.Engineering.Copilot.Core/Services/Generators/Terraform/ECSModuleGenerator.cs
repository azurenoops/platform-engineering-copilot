using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform modules for AWS Elastic Container Service (ECS)
/// Supports both Fargate and EC2 launch types with auto-scaling, load balancing, and monitoring
/// </summary>
public class ECSModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Generate all ECS Terraform files
        files["ecs/cluster.tf"] = GenerateCluster(request);
        files["ecs/task_definition.tf"] = GenerateTaskDefinition(request);
        files["ecs/service.tf"] = GenerateService(request);
        files["ecs/iam.tf"] = GenerateIAM(request);
        files["ecs/cloudwatch.tf"] = GenerateCloudWatch(request);
        files["ecs/variables.tf"] = GenerateVariables();
        files["ecs/outputs.tf"] = GenerateOutputs();
        
        // Optional components based on configuration
        if (infrastructure.IncludeLoadBalancer == true || app.Type == ApplicationType.WebAPI)
        {
            files["ecs/load_balancer.tf"] = GenerateLoadBalancer(request);
        }
        
        if (deployment.AutoScaling == true)
        {
            files["ecs/auto_scaling.tf"] = GenerateAutoScaling(request);
        }
        
        if (infrastructure.IncludeNetworking == true)
        {
            files["ecs/vpc.tf"] = GenerateVPC(request);
        }
        
        // Zero Trust Security Components
        files["ecs/security.tf"] = GenerateZeroTrustSecurity(request);
        
        return files;
    }
    
    private string GenerateCluster(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "app";
        
        sb.AppendLine("# ECS Cluster Configuration");
        sb.AppendLine("# This cluster will host your containerized services");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_ecs_cluster\" \"main\" {");
        sb.AppendLine($"  name = \"${{var.cluster_name}}\"");
        sb.AppendLine();
        sb.AppendLine("  setting {");
        sb.AppendLine("    name  = \"containerInsights\"");
        sb.AppendLine("    value = var.enable_container_insights ? \"enabled\" : \"disabled\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine($"    Name        = \"${{var.cluster_name}}\"");
        sb.AppendLine("    Environment = var.environment");
        sb.AppendLine("    ManagedBy   = \"Terraform\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Add capacity providers for Fargate
        sb.AppendLine("resource \"aws_ecs_cluster_capacity_providers\" \"main\" {");
        sb.AppendLine("  cluster_name = aws_ecs_cluster.main.name");
        sb.AppendLine();
        sb.AppendLine("  capacity_providers = [\"FARGATE\", \"FARGATE_SPOT\"]");
        sb.AppendLine();
        sb.AppendLine("  default_capacity_provider_strategy {");
        sb.AppendLine("    capacity_provider = var.enable_spot_instances ? \"FARGATE_SPOT\" : \"FARGATE\"");
        sb.AppendLine("    weight            = 1");
        sb.AppendLine("    base              = 1");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateTaskDefinition(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "app";
        var app = request.Application ?? new ApplicationSpec();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        // Parse CPU and memory limits
        var cpu = ParseCPUUnits(deployment.Resources.CpuLimit ?? "1 vCPU");
        var memory = ParseMemoryMB(deployment.Resources.MemoryLimit ?? "2 GB");
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# ECS Task Definition");
        sb.AppendLine("# Defines the container specifications, resources, and configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_ecs_task_definition\" \"main\" {");
        sb.AppendLine($"  family                   = var.service_name");
        sb.AppendLine("  network_mode             = \"awsvpc\"");
        sb.AppendLine("  requires_compatibilities = [var.launch_type]");
        sb.AppendLine($"  cpu                      = var.cpu_units");
        sb.AppendLine($"  memory                   = var.memory_mb");
        sb.AppendLine("  execution_role_arn       = aws_iam_role.ecs_execution.arn");
        sb.AppendLine("  task_role_arn            = aws_iam_role.ecs_task.arn");
        sb.AppendLine();
        sb.AppendLine("  container_definitions = jsonencode([{");
        sb.AppendLine("    name      = var.service_name");
        sb.AppendLine("    image     = var.container_image");
        sb.AppendLine("    essential = true");
        sb.AppendLine();
        sb.AppendLine("    portMappings = [{");
        sb.AppendLine($"      containerPort = var.container_port");
        sb.AppendLine("      hostPort      = var.container_port");
        sb.AppendLine("      protocol      = \"tcp\"");
        sb.AppendLine("    }]");
        sb.AppendLine();
        sb.AppendLine("    environment = var.enable_secrets_manager ? [] : [");
        sb.AppendLine("      for key, value in var.environment_variables : {");
        sb.AppendLine("        name  = key");
        sb.AppendLine("        value = value");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine();
        sb.AppendLine("    # Use Secrets Manager for sensitive environment variables");
        sb.AppendLine("    secrets = var.enable_secrets_manager ? [");
        sb.AppendLine("      for arn in var.secrets_arns : {");
        sb.AppendLine("        name      = element(split(\"/\", arn), length(split(\"/\", arn)) - 1)");
        sb.AppendLine("        valueFrom = arn");
        sb.AppendLine("      }");
        sb.AppendLine("    ] : []");
        sb.AppendLine();
        sb.AppendLine("    # Zero Trust: Read-only root filesystem");
        sb.AppendLine("    readonlyRootFilesystem = var.enable_read_only_root_filesystem");
        sb.AppendLine();
        sb.AppendLine("    # Zero Trust: Drop Linux capabilities");
        sb.AppendLine("    linuxParameters = {");
        sb.AppendLine("      capabilities = {");
        sb.AppendLine("        drop = var.drop_capabilities");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    logConfiguration = {");
        sb.AppendLine("      logDriver = \"awslogs\"");
        sb.AppendLine("      options = {");
        sb.AppendLine("        \"awslogs-group\"         = aws_cloudwatch_log_group.ecs.name");
        sb.AppendLine("        \"awslogs-region\"        = var.aws_region");
        sb.AppendLine("        \"awslogs-stream-prefix\" = \"ecs\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Add health check if configured
        if (app.IncludeHealthCheck == true)
        {
            sb.AppendLine("    healthCheck = {");
            sb.AppendLine("      command     = [\"CMD-SHELL\", \"curl -f http://localhost:${var.container_port}/health || exit 1\"]");
            sb.AppendLine("      interval    = 30");
            sb.AppendLine("      timeout     = 5");
            sb.AppendLine("      retries     = 3");
            sb.AppendLine("      startPeriod = 60");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("    # Resource limits");
        sb.AppendLine("    cpu    = var.cpu_units");
        sb.AppendLine("    memory = var.memory_mb");
        sb.AppendLine("  }])");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-task\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateService(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        var desiredCount = deployment.Replicas > 0 ? deployment.Replicas : 2;
        var enableLB = infrastructure.IncludeLoadBalancer == true || app.Type == ApplicationType.WebAPI;
        
        sb.AppendLine("# ECS Service Configuration");
        sb.AppendLine("# Manages the running tasks and integrates with load balancer");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_ecs_service\" \"main\" {");
        sb.AppendLine("  name            = var.service_name");
        sb.AppendLine("  cluster         = aws_ecs_cluster.main.id");
        sb.AppendLine("  task_definition = aws_ecs_task_definition.main.arn");
        sb.AppendLine("  desired_count   = var.desired_count");
        sb.AppendLine("  launch_type     = var.launch_type");
        sb.AppendLine();
        sb.AppendLine("  network_configuration {");
        sb.AppendLine("    subnets          = var.enable_network_isolation ? var.private_subnet_ids : var.private_subnet_ids");
        sb.AppendLine("    security_groups  = [aws_security_group.ecs_tasks.id]");
        sb.AppendLine("    assign_public_ip = var.enable_network_isolation ? false : var.assign_public_ip");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        if (enableLB)
        {
            sb.AppendLine("  load_balancer {");
            sb.AppendLine("    target_group_arn = aws_lb_target_group.main.arn");
            sb.AppendLine("    container_name   = var.service_name");
            sb.AppendLine("    container_port   = var.container_port");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
        
        sb.AppendLine("  # Zero Trust: ECS Service Connect for service mesh");
        sb.AppendLine("  dynamic \"service_connect_configuration\" {");
        sb.AppendLine("    for_each = var.enable_service_connect ? [1] : []");
        sb.AppendLine("    content {");
        sb.AppendLine("      enabled = true");
        sb.AppendLine("      namespace = var.service_name");
        sb.AppendLine("      service {");
        sb.AppendLine("        port_name = var.service_name");
        sb.AppendLine("        discovery_name = var.service_name");
        sb.AppendLine("        client_alias {");
        sb.AppendLine("          port = var.container_port");
        sb.AppendLine("          dns_name = var.service_name");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Zero Trust: Enable ECS Exec for secure task access");
        sb.AppendLine("  enable_execute_command = var.enable_execute_command");
        sb.AppendLine();
        sb.AppendLine("  deployment_configuration {");
        sb.AppendLine("    maximum_percent         = 200");
        sb.AppendLine("    minimum_healthy_percent = 100");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  deployment_circuit_breaker {");
        sb.AppendLine("    enable   = true");
        sb.AppendLine("    rollback = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Health check grace period (if using load balancer)");
        
        if (enableLB)
        {
            sb.AppendLine("  health_check_grace_period_seconds = 60");
            sb.AppendLine();
        }
        
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-service\"");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [");
        
        if (enableLB)
        {
            sb.AppendLine("    aws_lb_listener.main,");
        }
        
        sb.AppendLine("    aws_iam_role.ecs_execution");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Security group for ECS tasks
        sb.AppendLine("resource \"aws_security_group\" \"ecs_tasks\" {");
        sb.AppendLine("  name        = \"${var.service_name}-ecs-tasks\"");
        sb.AppendLine("  description = \"Security group for ECS tasks\"");
        sb.AppendLine("  vpc_id      = var.vpc_id");
        sb.AppendLine();
        sb.AppendLine("  ingress {");
        sb.AppendLine("    from_port   = var.container_port");
        sb.AppendLine("    to_port     = var.container_port");
        sb.AppendLine("    protocol    = \"tcp\"");
        
        if (enableLB)
        {
            sb.AppendLine("    security_groups = [aws_security_group.alb.id]");
        }
        else
        {
            sb.AppendLine("    cidr_blocks = [\"0.0.0.0/0\"]");
        }
        
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  egress {");
        sb.AppendLine("    from_port   = 0");
        sb.AppendLine("    to_port     = 0");
        sb.AppendLine("    protocol    = \"-1\"");
        sb.AppendLine("    cidr_blocks = [\"0.0.0.0/0\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-ecs-tasks-sg\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateLoadBalancer(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# Application Load Balancer Configuration");
        sb.AppendLine("# Distributes traffic across ECS tasks");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_lb\" \"main\" {");
        sb.AppendLine("  name               = \"${var.service_name}-alb\"");
        sb.AppendLine("  internal           = var.internal_load_balancer");
        sb.AppendLine("  load_balancer_type = \"application\"");
        sb.AppendLine("  security_groups    = [aws_security_group.alb.id]");
        sb.AppendLine("  subnets            = var.public_subnet_ids");
        sb.AppendLine();
        sb.AppendLine("  enable_deletion_protection = var.enable_deletion_protection");
        sb.AppendLine("  enable_http2              = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-alb\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Target group
        sb.AppendLine("resource \"aws_lb_target_group\" \"main\" {");
        sb.AppendLine("  name        = \"${var.service_name}-tg\"");
        sb.AppendLine("  port        = var.container_port");
        sb.AppendLine("  protocol    = \"HTTP\"");
        sb.AppendLine("  vpc_id      = var.vpc_id");
        sb.AppendLine("  target_type = \"ip\"");
        sb.AppendLine();
        sb.AppendLine("  health_check {");
        sb.AppendLine($"    path                = \"{(app.IncludeHealthCheck == true ? "/health" : "/")}\"");
        sb.AppendLine("    protocol            = \"HTTP\"");
        sb.AppendLine("    matcher             = \"200\"");
        sb.AppendLine("    interval            = 30");
        sb.AppendLine("    timeout             = 5");
        sb.AppendLine("    healthy_threshold   = 2");
        sb.AppendLine("    unhealthy_threshold = 3");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  deregistration_delay = 30");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-tg\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // HTTP Listener with HTTPS redirect
        sb.AppendLine("resource \"aws_lb_listener\" \"main\" {");
        sb.AppendLine("  load_balancer_arn = aws_lb.main.arn");
        sb.AppendLine("  port              = \"80\"");
        sb.AppendLine("  protocol          = \"HTTP\"");
        sb.AppendLine();
        sb.AppendLine("  default_action {");
        sb.AppendLine("    type = var.enable_https_only && var.ssl_certificate_arn != \"\" ? \"redirect\" : \"forward\"");
        sb.AppendLine();
        sb.AppendLine("    # Zero Trust: Redirect HTTP to HTTPS");
        sb.AppendLine("    dynamic \"redirect\" {");
        sb.AppendLine("      for_each = var.enable_https_only && var.ssl_certificate_arn != \"\" ? [1] : []");
        sb.AppendLine("      content {");
        sb.AppendLine("        port        = \"443\"");
        sb.AppendLine("        protocol    = \"HTTPS\"");
        sb.AppendLine("        status_code = \"HTTP_301\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    # Forward to target group if HTTPS not enabled");
        sb.AppendLine("    target_group_arn = var.enable_https_only && var.ssl_certificate_arn != \"\" ? null : aws_lb_target_group.main.arn");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // HTTPS Listener
        sb.AppendLine("# HTTPS Listener with TLS 1.2+");
        sb.AppendLine("resource \"aws_lb_listener\" \"https\" {");
        sb.AppendLine("  count = var.ssl_certificate_arn != \"\" ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  load_balancer_arn = aws_lb.main.arn");
        sb.AppendLine("  port              = \"443\"");
        sb.AppendLine("  protocol          = \"HTTPS\"");
        sb.AppendLine("  ssl_policy        = \"ELBSecurityPolicy-TLS13-1-2-2021-06\"  # Zero Trust: TLS 1.3 and 1.2");
        sb.AppendLine("  certificate_arn   = var.ssl_certificate_arn");
        sb.AppendLine();
        sb.AppendLine("  default_action {");
        sb.AppendLine("    type             = \"forward\"");
        sb.AppendLine("    target_group_arn = aws_lb_target_group.main.arn");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // ALB Security Group with allowed CIDR blocks
        sb.AppendLine("resource \"aws_security_group\" \"alb\" {");
        sb.AppendLine("  name        = \"${var.service_name}-alb\"");
        sb.AppendLine("  description = \"Security group for Application Load Balancer\"");
        sb.AppendLine("  vpc_id      = var.vpc_id");
        sb.AppendLine();
        sb.AppendLine("  # Zero Trust: Restrict access to allowed CIDR blocks");
        sb.AppendLine("  ingress {");
        sb.AppendLine("    from_port   = 80");
        sb.AppendLine("    to_port     = 80");
        sb.AppendLine("    protocol    = \"tcp\"");
        sb.AppendLine("    cidr_blocks = var.allowed_cidr_blocks");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  ingress {");
        sb.AppendLine("    from_port   = 443");
        sb.AppendLine("    to_port     = 443");
        sb.AppendLine("    protocol    = \"tcp\"");
        sb.AppendLine("    cidr_blocks = var.allowed_cidr_blocks");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  egress {");
        sb.AppendLine("    from_port   = 0");
        sb.AppendLine("    to_port     = 0");
        sb.AppendLine("    protocol    = \"-1\"");
        sb.AppendLine("    cidr_blocks = [\"0.0.0.0/0\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-alb-sg\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Zero Trust: WAF Web ACL for ALB");
        sb.AppendLine("resource \"aws_wafv2_web_acl_association\" \"alb\" {");
        sb.AppendLine("  count = var.enable_waf ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  resource_arn = aws_lb.main.arn");
        sb.AppendLine("  web_acl_arn  = aws_wafv2_web_acl.main[0].arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_wafv2_web_acl\" \"main\" {");
        sb.AppendLine("  count = var.enable_waf ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  name  = \"${var.service_name}-waf\"");
        sb.AppendLine("  scope = \"REGIONAL\"");
        sb.AppendLine();
        sb.AppendLine("  default_action {");
        sb.AppendLine("    allow {}");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # AWS Managed Rules");
        sb.AppendLine("  rule {");
        sb.AppendLine("    name     = \"AWSManagedRulesCommonRuleSet\"");
        sb.AppendLine("    priority = 1");
        sb.AppendLine();
        sb.AppendLine("    override_action {");
        sb.AppendLine("      none {}");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    statement {");
        sb.AppendLine("      managed_rule_group_statement {");
        sb.AppendLine("        vendor_name = \"AWS\"");
        sb.AppendLine("        name        = \"AWSManagedRulesCommonRuleSet\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    visibility_config {");
        sb.AppendLine("      cloudwatch_metrics_enabled = true");
        sb.AppendLine("      metric_name                = \"AWSManagedRulesCommonRuleSetMetric\"");
        sb.AppendLine("      sampled_requests_enabled   = true");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  visibility_config {");
        sb.AppendLine("    cloudwatch_metrics_enabled = true");
        sb.AppendLine("    metric_name                = \"${var.service_name}-waf\"");
        sb.AppendLine("    sampled_requests_enabled   = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateAutoScaling(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        var minCapacity = deployment.MinReplicas > 0 ? deployment.MinReplicas : 2;
        var maxCapacity = deployment.MaxReplicas > 0 ? deployment.MaxReplicas : 10;
        
        sb.AppendLine("# Auto Scaling Configuration");
        sb.AppendLine("# Automatically scales ECS service based on metrics");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_appautoscaling_target\" \"ecs\" {");
        sb.AppendLine("  max_capacity       = var.max_capacity");
        sb.AppendLine("  min_capacity       = var.min_capacity");
        sb.AppendLine("  resource_id        = \"service/${aws_ecs_cluster.main.name}/${aws_ecs_service.main.name}\"");
        sb.AppendLine("  scalable_dimension = \"ecs:service:DesiredCount\"");
        sb.AppendLine("  service_namespace  = \"ecs\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // CPU-based scaling
        sb.AppendLine("# Scale up when CPU utilization is high");
        sb.AppendLine("resource \"aws_appautoscaling_policy\" \"cpu_high\" {");
        sb.AppendLine("  name               = \"${var.service_name}-scale-up\"");
        sb.AppendLine("  policy_type        = \"TargetTrackingScaling\"");
        sb.AppendLine("  resource_id        = aws_appautoscaling_target.ecs.resource_id");
        sb.AppendLine("  scalable_dimension = aws_appautoscaling_target.ecs.scalable_dimension");
        sb.AppendLine("  service_namespace  = aws_appautoscaling_target.ecs.service_namespace");
        sb.AppendLine();
        sb.AppendLine("  target_tracking_scaling_policy_configuration {");
        sb.AppendLine("    target_value       = var.cpu_target_value");
        sb.AppendLine("    scale_in_cooldown  = 300");
        sb.AppendLine("    scale_out_cooldown = 60");
        sb.AppendLine();
        sb.AppendLine("    predefined_metric_specification {");
        sb.AppendLine("      predefined_metric_type = \"ECSServiceAverageCPUUtilization\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Memory-based scaling
        sb.AppendLine("# Scale up when memory utilization is high");
        sb.AppendLine("resource \"aws_appautoscaling_policy\" \"memory_high\" {");
        sb.AppendLine("  name               = \"${var.service_name}-memory-scale-up\"");
        sb.AppendLine("  policy_type        = \"TargetTrackingScaling\"");
        sb.AppendLine("  resource_id        = aws_appautoscaling_target.ecs.resource_id");
        sb.AppendLine("  scalable_dimension = aws_appautoscaling_target.ecs.scalable_dimension");
        sb.AppendLine("  service_namespace  = aws_appautoscaling_target.ecs.service_namespace");
        sb.AppendLine();
        sb.AppendLine("  target_tracking_scaling_policy_configuration {");
        sb.AppendLine("    target_value       = var.memory_target_value");
        sb.AppendLine("    scale_in_cooldown  = 300");
        sb.AppendLine("    scale_out_cooldown = 60");
        sb.AppendLine();
        sb.AppendLine("    predefined_metric_specification {");
        sb.AppendLine("      predefined_metric_type = \"ECSServiceAverageMemoryUtilization\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateIAM(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# IAM Roles for ECS");
        sb.AppendLine();
        sb.AppendLine("# ECS Task Execution Role (for ECS agent)");
        sb.AppendLine("resource \"aws_iam_role\" \"ecs_execution\" {");
        sb.AppendLine("  name = \"${var.service_name}-ecs-execution-role\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Action = \"sts:AssumeRole\"");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Service = \"ecs-tasks.amazonaws.com\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"ecs_execution\" {");
        sb.AppendLine("  role       = aws_iam_role.ecs_execution.name");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Additional policy for ECR, CloudWatch, Secrets Manager, KMS
        sb.AppendLine("resource \"aws_iam_role_policy\" \"ecs_execution_additional\" {");
        sb.AppendLine("  name = \"${var.service_name}-execution-additional\"");
        sb.AppendLine("  role = aws_iam_role.ecs_execution.id");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = concat([");
        sb.AppendLine("      {");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Action = [");
        sb.AppendLine("          \"ecr:GetAuthorizationToken\",");
        sb.AppendLine("          \"ecr:BatchCheckLayerAvailability\",");
        sb.AppendLine("          \"ecr:GetDownloadUrlForLayer\",");
        sb.AppendLine("          \"ecr:BatchGetImage\"");
        sb.AppendLine("        ]");
        sb.AppendLine("        Resource = \"*\"");
        sb.AppendLine("      },");
        sb.AppendLine("      {");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Action = [");
        sb.AppendLine("          \"logs:CreateLogStream\",");
        sb.AppendLine("          \"logs:PutLogEvents\"");
        sb.AppendLine("        ]");
        sb.AppendLine("        Resource = \"${aws_cloudwatch_log_group.ecs.arn}:*\"");
        sb.AppendLine("      }");
        sb.AppendLine("    ],");
        sb.AppendLine("    # Zero Trust: Secrets Manager permissions (conditional)");
        sb.AppendLine("    var.enable_secrets_manager ? [{");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Action = [");
        sb.AppendLine("        \"secretsmanager:GetSecretValue\"");
        sb.AppendLine("      ]");
        sb.AppendLine("      Resource = var.secrets_arns");
        sb.AppendLine("    }] : [],");
        sb.AppendLine("    # Zero Trust: KMS permissions for ECS Exec and logs (conditional)");
        sb.AppendLine("    var.kms_key_arn != \"\" ? [{");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Action = [");
        sb.AppendLine("        \"kms:Decrypt\",");
        sb.AppendLine("        \"kms:DescribeKey\"");
        sb.AppendLine("      ]");
        sb.AppendLine("      Resource = var.kms_key_arn");
        sb.AppendLine("    }] : [])");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // ECS Task Role (for application)
        sb.AppendLine("# ECS Task Role (for your application code)");
        sb.AppendLine("resource \"aws_iam_role\" \"ecs_task\" {");
        sb.AppendLine("  name = \"${var.service_name}-ecs-task-role\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Action = \"sts:AssumeRole\"");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Service = \"ecs-tasks.amazonaws.com\"");
        sb.AppendLine("      }");
        sb.AppendLine("      # Zero Trust: MFA condition (optional)");
        sb.AppendLine("      Condition = var.enable_task_role_mfa ? {");
        sb.AppendLine("        Bool = {");
        sb.AppendLine("          \"aws:MultiFactorAuthPresent\" = \"true\"");
        sb.AppendLine("        }");
        sb.AppendLine("      } : null");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Zero Trust: ECS Exec permissions (conditional)");
        sb.AppendLine("resource \"aws_iam_role_policy\" \"ecs_exec\" {");
        sb.AppendLine("  count = var.enable_execute_command ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  name = \"${var.service_name}-ecs-exec\"");
        sb.AppendLine("  role = aws_iam_role.ecs_task.id");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Action = [");
        sb.AppendLine("        \"ssmmessages:CreateControlChannel\",");
        sb.AppendLine("        \"ssmmessages:CreateDataChannel\",");
        sb.AppendLine("        \"ssmmessages:OpenControlChannel\",");
        sb.AppendLine("        \"ssmmessages:OpenDataChannel\"");
        sb.AppendLine("      ]");
        sb.AppendLine("      Resource = \"*\"");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Add application-specific policies to ecs_task role as needed");
        sb.AppendLine("# Example: S3, DynamoDB, SQS, etc.");
        
        return sb.ToString();
    }
    
    private string GenerateCloudWatch(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# CloudWatch Log Group for ECS");
        sb.AppendLine("resource \"aws_cloudwatch_log_group\" \"ecs\" {");
        sb.AppendLine("  name              = \"/ecs/${var.service_name}\"");
        sb.AppendLine("  retention_in_days = var.log_retention_days");
        sb.AppendLine();
        sb.AppendLine("  # Zero Trust: KMS encryption for logs");
        sb.AppendLine("  kms_key_id = var.kms_key_arn != \"\" ? var.kms_key_arn : null");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-logs\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // CloudWatch Alarms
        sb.AppendLine("# CloudWatch Alarms");
        sb.AppendLine("resource \"aws_cloudwatch_metric_alarm\" \"cpu_high\" {");
        sb.AppendLine("  alarm_name          = \"${var.service_name}-cpu-high\"");
        sb.AppendLine("  comparison_operator = \"GreaterThanThreshold\"");
        sb.AppendLine("  evaluation_periods  = \"2\"");
        sb.AppendLine("  metric_name         = \"CPUUtilization\"");
        sb.AppendLine("  namespace           = \"AWS/ECS\"");
        sb.AppendLine("  period              = \"300\"");
        sb.AppendLine("  statistic           = \"Average\"");
        sb.AppendLine("  threshold           = \"80\"");
        sb.AppendLine();
        sb.AppendLine("  dimensions = {");
        sb.AppendLine("    ClusterName = aws_ecs_cluster.main.name");
        sb.AppendLine("    ServiceName = aws_ecs_service.main.name");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  alarm_description = \"This metric monitors ECS CPU utilization\"");
        sb.AppendLine("  alarm_actions     = var.alarm_actions");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_cloudwatch_metric_alarm\" \"memory_high\" {");
        sb.AppendLine("  alarm_name          = \"${var.service_name}-memory-high\"");
        sb.AppendLine("  comparison_operator = \"GreaterThanThreshold\"");
        sb.AppendLine("  evaluation_periods  = \"2\"");
        sb.AppendLine("  metric_name         = \"MemoryUtilization\"");
        sb.AppendLine("  namespace           = \"AWS/ECS\"");
        sb.AppendLine("  period              = \"300\"");
        sb.AppendLine("  statistic           = \"Average\"");
        sb.AppendLine("  threshold           = \"80\"");
        sb.AppendLine();
        sb.AppendLine("  dimensions = {");
        sb.AppendLine("    ClusterName = aws_ecs_cluster.main.name");
        sb.AppendLine("    ServiceName = aws_ecs_service.main.name");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  alarm_description = \"This metric monitors ECS memory utilization\"");
        sb.AppendLine("  alarm_actions     = var.alarm_actions");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVPC(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        var vpcCidr = "10.0.0.0/16";
        
        sb.AppendLine("# VPC Configuration for ECS");
        sb.AppendLine("# Creates VPC, subnets, NAT gateway, and route tables");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_vpc\" \"main\" {");
        sb.AppendLine("  cidr_block           = var.vpc_cidr");
        sb.AppendLine("  enable_dns_hostnames = true");
        sb.AppendLine("  enable_dns_support   = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-vpc\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Public Subnets (for Load Balancer)");
        sb.AppendLine("resource \"aws_subnet\" \"public\" {");
        sb.AppendLine("  count                   = length(var.availability_zones)");
        sb.AppendLine("  vpc_id                  = aws_vpc.main.id");
        sb.AppendLine("  cidr_block              = cidrsubnet(aws_vpc.main.cidr_block, 8, count.index)");
        sb.AppendLine("  availability_zone       = var.availability_zones[count.index]");
        sb.AppendLine("  map_public_ip_on_launch = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-public-${count.index + 1}\"");
        sb.AppendLine("    Type = \"public\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Private Subnets (for ECS Tasks)");
        sb.AppendLine("resource \"aws_subnet\" \"private\" {");
        sb.AppendLine("  count             = length(var.availability_zones)");
        sb.AppendLine("  vpc_id            = aws_vpc.main.id");
        sb.AppendLine("  cidr_block        = cidrsubnet(aws_vpc.main.cidr_block, 8, count.index + 10)");
        sb.AppendLine("  availability_zone = var.availability_zones[count.index]");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-private-${count.index + 1}\"");
        sb.AppendLine("    Type = \"private\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Internet Gateway");
        sb.AppendLine("resource \"aws_internet_gateway\" \"main\" {");
        sb.AppendLine("  vpc_id = aws_vpc.main.id");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-igw\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# NAT Gateway (for private subnet internet access)");
        sb.AppendLine("resource \"aws_eip\" \"nat\" {");
        sb.AppendLine("  count  = length(var.availability_zones)");
        sb.AppendLine("  domain = \"vpc\"");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-nat-${count.index + 1}\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_nat_gateway\" \"main\" {");
        sb.AppendLine("  count         = length(var.availability_zones)");
        sb.AppendLine("  allocation_id = aws_eip.nat[count.index].id");
        sb.AppendLine("  subnet_id     = aws_subnet.public[count.index].id");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-nat-${count.index + 1}\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVariables()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Input Variables for ECS Module");
        sb.AppendLine();
        sb.AppendLine("variable \"service_name\" {");
        sb.AppendLine("  description = \"Name of the service\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cluster_name\" {");
        sb.AppendLine("  description = \"Name of the ECS cluster\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"aws_region\" {");
        sb.AppendLine("  description = \"AWS region\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"us-east-1\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment\" {");
        sb.AppendLine("  description = \"Environment name (dev, staging, prod)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"dev\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"container_image\" {");
        sb.AppendLine("  description = \"Docker image for the container\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"container_port\" {");
        sb.AppendLine("  description = \"Port exposed by the container\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 8080");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cpu_units\" {");
        sb.AppendLine("  description = \"CPU units for the task (256, 512, 1024, 2048, 4096)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 256");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"memory_mb\" {");
        sb.AppendLine("  description = \"Memory in MB for the task\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 512");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"launch_type\" {");
        sb.AppendLine("  description = \"Launch type: FARGATE or EC2\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"FARGATE\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"desired_count\" {");
        sb.AppendLine("  description = \"Desired number of tasks\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 2");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"min_capacity\" {");
        sb.AppendLine("  description = \"Minimum number of tasks for auto-scaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 2");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"max_capacity\" {");
        sb.AppendLine("  description = \"Maximum number of tasks for auto-scaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 10");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cpu_target_value\" {");
        sb.AppendLine("  description = \"Target CPU utilization percentage for auto-scaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 70");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"memory_target_value\" {");
        sb.AppendLine("  description = \"Target memory utilization percentage for auto-scaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 80");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_id\" {");
        sb.AppendLine("  description = \"VPC ID (if not creating new VPC)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_cidr\" {");
        sb.AppendLine("  description = \"CIDR block for VPC (if creating new VPC)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"availability_zones\" {");
        sb.AppendLine("  description = \"List of availability zones\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = [\"us-east-1a\", \"us-east-1b\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"private_subnet_ids\" {");
        sb.AppendLine("  description = \"List of private subnet IDs\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"public_subnet_ids\" {");
        sb.AppendLine("  description = \"List of public subnet IDs\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"route_table_ids\" {");
        sb.AppendLine("  description = \"List of route table IDs for VPC endpoints\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"assign_public_ip\" {");
        sb.AppendLine("  description = \"Assign public IP to tasks\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"internal_load_balancer\" {");
        sb.AppendLine("  description = \"Create internal load balancer\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_deletion_protection\" {");
        sb.AppendLine("  description = \"Enable deletion protection for load balancer\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_container_insights\" {");
        sb.AppendLine("  description = \"Enable Container Insights\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_spot_instances\" {");
        sb.AppendLine("  description = \"Enable Fargate Spot instances\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"log_retention_days\" {");
        sb.AppendLine("  description = \"CloudWatch log retention in days\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 30");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment_variables\" {");
        sb.AppendLine("  description = \"Environment variables for the container\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# === ZERO TRUST SECURITY PARAMETERS ===");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_vpc_endpoints\" {");
        sb.AppendLine("  description = \"Enable VPC endpoints for AWS services (ECR, S3, CloudWatch, Secrets Manager)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_service_connect\" {");
        sb.AppendLine("  description = \"Enable ECS Service Connect for service mesh capabilities\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_execute_command\" {");
        sb.AppendLine("  description = \"Enable ECS Exec for secure task access (requires KMS encryption)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kms_key_arn\" {");
        sb.AppendLine("  description = \"KMS key ARN for ECS Exec and log encryption\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_secrets_manager\" {");
        sb.AppendLine("  description = \"Use AWS Secrets Manager for sensitive environment variables\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"secrets_arns\" {");
        sb.AppendLine("  description = \"List of Secrets Manager ARNs to grant access\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_https_only\" {");
        sb.AppendLine("  description = \"Redirect HTTP to HTTPS on load balancer\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"ssl_certificate_arn\" {");
        sb.AppendLine("  description = \"ACM certificate ARN for HTTPS\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_cidr_blocks\" {");
        sb.AppendLine("  description = \"CIDR blocks allowed to access the load balancer\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = [\"0.0.0.0/0\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_waf\" {");
        sb.AppendLine("  description = \"Enable AWS WAF for load balancer\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_guardduty\" {");
        sb.AppendLine("  description = \"Enable GuardDuty for threat detection\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloudtrail\" {");
        sb.AppendLine("  description = \"Enable CloudTrail for audit logging\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_task_role_mfa\" {");
        sb.AppendLine("  description = \"Require MFA for task role assumption\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_network_isolation\" {");
        sb.AppendLine("  description = \"Deploy tasks in private subnets with no public IP\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_read_only_root_filesystem\" {");
        sb.AppendLine("  description = \"Make container root filesystem read-only\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"drop_capabilities\" {");
        sb.AppendLine("  description = \"Linux capabilities to drop from containers\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = [\"ALL\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_container_scan\" {");
        sb.AppendLine("  description = \"Enable ECR image scanning\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_image_registries\" {");
        sb.AppendLine("  description = \"List of allowed ECR registries (for policy enforcement)\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"alarm_actions\" {");
        sb.AppendLine("  description = \"List of alarm action ARNs (SNS topics)\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateOutputs()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Output Values");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_id\" {");
        sb.AppendLine("  description = \"ECS Cluster ID\"");
        sb.AppendLine("  value       = aws_ecs_cluster.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_name\" {");
        sb.AppendLine("  description = \"ECS Cluster name\"");
        sb.AppendLine("  value       = aws_ecs_cluster.main.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"service_name\" {");
        sb.AppendLine("  description = \"ECS Service name\"");
        sb.AppendLine("  value       = aws_ecs_service.main.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"task_definition_arn\" {");
        sb.AppendLine("  description = \"Task Definition ARN\"");
        sb.AppendLine("  value       = aws_ecs_task_definition.main.arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"task_execution_role_arn\" {");
        sb.AppendLine("  description = \"Task Execution Role ARN\"");
        sb.AppendLine("  value       = aws_iam_role.ecs_execution.arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"task_role_arn\" {");
        sb.AppendLine("  description = \"Task Role ARN\"");
        sb.AppendLine("  value       = aws_iam_role.ecs_task.arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"log_group_name\" {");
        sb.AppendLine("  description = \"CloudWatch Log Group name\"");
        sb.AppendLine("  value       = aws_cloudwatch_log_group.ecs.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"security_group_id\" {");
        sb.AppendLine("  description = \"Security Group ID for ECS tasks\"");
        sb.AppendLine("  value       = aws_security_group.ecs_tasks.id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateZeroTrustSecurity(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Zero Trust Security Components");
        sb.AppendLine("# VPC Endpoints, GuardDuty, CloudTrail, and Container Scanning");
        sb.AppendLine();
        
        // VPC Endpoints for AWS services
        sb.AppendLine("# VPC Endpoints for secure AWS service access");
        sb.AppendLine("resource \"aws_vpc_endpoint\" \"ecr_dkr\" {");
        sb.AppendLine("  count = var.enable_vpc_endpoints ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  vpc_id              = var.vpc_id");
        sb.AppendLine("  service_name        = \"com.amazonaws.${var.aws_region}.ecr.dkr\"");
        sb.AppendLine("  vpc_endpoint_type   = \"Interface\"");
        sb.AppendLine("  subnet_ids          = var.private_subnet_ids");
        sb.AppendLine("  security_group_ids  = [aws_security_group.vpc_endpoints[0].id]");
        sb.AppendLine("  private_dns_enabled = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-ecr-dkr-endpoint\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_vpc_endpoint\" \"ecr_api\" {");
        sb.AppendLine("  count = var.enable_vpc_endpoints ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  vpc_id              = var.vpc_id");
        sb.AppendLine("  service_name        = \"com.amazonaws.${var.aws_region}.ecr.api\"");
        sb.AppendLine("  vpc_endpoint_type   = \"Interface\"");
        sb.AppendLine("  subnet_ids          = var.private_subnet_ids");
        sb.AppendLine("  security_group_ids  = [aws_security_group.vpc_endpoints[0].id]");
        sb.AppendLine("  private_dns_enabled = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-ecr-api-endpoint\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_vpc_endpoint\" \"s3\" {");
        sb.AppendLine("  count = var.enable_vpc_endpoints ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  vpc_id            = var.vpc_id");
        sb.AppendLine("  service_name      = \"com.amazonaws.${var.aws_region}.s3\"");
        sb.AppendLine("  vpc_endpoint_type = \"Gateway\"");
        sb.AppendLine("  route_table_ids   = var.route_table_ids");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-s3-endpoint\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_vpc_endpoint\" \"logs\" {");
        sb.AppendLine("  count = var.enable_vpc_endpoints ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  vpc_id              = var.vpc_id");
        sb.AppendLine("  service_name        = \"com.amazonaws.${var.aws_region}.logs\"");
        sb.AppendLine("  vpc_endpoint_type   = \"Interface\"");
        sb.AppendLine("  subnet_ids          = var.private_subnet_ids");
        sb.AppendLine("  security_group_ids  = [aws_security_group.vpc_endpoints[0].id]");
        sb.AppendLine("  private_dns_enabled = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-logs-endpoint\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_vpc_endpoint\" \"secretsmanager\" {");
        sb.AppendLine("  count = var.enable_vpc_endpoints && var.enable_secrets_manager ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  vpc_id              = var.vpc_id");
        sb.AppendLine("  service_name        = \"com.amazonaws.${var.aws_region}.secretsmanager\"");
        sb.AppendLine("  vpc_endpoint_type   = \"Interface\"");
        sb.AppendLine("  subnet_ids          = var.private_subnet_ids");
        sb.AppendLine("  security_group_ids  = [aws_security_group.vpc_endpoints[0].id]");
        sb.AppendLine("  private_dns_enabled = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-secretsmanager-endpoint\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // VPC Endpoint Security Group
        sb.AppendLine("resource \"aws_security_group\" \"vpc_endpoints\" {");
        sb.AppendLine("  count = var.enable_vpc_endpoints ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  name        = \"${var.service_name}-vpc-endpoints\"");
        sb.AppendLine("  description = \"Security group for VPC endpoints\"");
        sb.AppendLine("  vpc_id      = var.vpc_id");
        sb.AppendLine();
        sb.AppendLine("  ingress {");
        sb.AppendLine("    from_port   = 443");
        sb.AppendLine("    to_port     = 443");
        sb.AppendLine("    protocol    = \"tcp\"");
        sb.AppendLine("    cidr_blocks = [data.aws_vpc.selected.cidr_block]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  egress {");
        sb.AppendLine("    from_port   = 0");
        sb.AppendLine("    to_port     = 0");
        sb.AppendLine("    protocol    = \"-1\"");
        sb.AppendLine("    cidr_blocks = [\"0.0.0.0/0\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-vpc-endpoints-sg\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("data \"aws_vpc\" \"selected\" {");
        sb.AppendLine("  id = var.vpc_id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // GuardDuty
        sb.AppendLine("# GuardDuty for threat detection");
        sb.AppendLine("resource \"aws_guardduty_detector\" \"main\" {");
        sb.AppendLine("  count = var.enable_guardduty ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  enable = true");
        sb.AppendLine();
        sb.AppendLine("  datasources {");
        sb.AppendLine("    s3_logs {");
        sb.AppendLine("      enable = true");
        sb.AppendLine("    }");
        sb.AppendLine("    kubernetes {");
        sb.AppendLine("      audit_logs {");
        sb.AppendLine("        enable = false");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-guardduty\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // CloudTrail
        sb.AppendLine("# CloudTrail for audit logging");
        sb.AppendLine("resource \"aws_cloudtrail\" \"main\" {");
        sb.AppendLine("  count = var.enable_cloudtrail ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  name                          = \"${var.service_name}-trail\"");
        sb.AppendLine("  s3_bucket_name                = aws_s3_bucket.cloudtrail[0].id");
        sb.AppendLine("  include_global_service_events = true");
        sb.AppendLine("  is_multi_region_trail         = true");
        sb.AppendLine("  enable_log_file_validation    = true");
        sb.AppendLine();
        sb.AppendLine("  event_selector {");
        sb.AppendLine("    read_write_type           = \"All\"");
        sb.AppendLine("    include_management_events = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-cloudtrail\"");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [aws_s3_bucket_policy.cloudtrail]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_s3_bucket\" \"cloudtrail\" {");
        sb.AppendLine("  count = var.enable_cloudtrail ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  bucket        = \"${var.service_name}-cloudtrail-${data.aws_caller_identity.current.account_id}\"");
        sb.AppendLine("  force_destroy = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.service_name}-cloudtrail\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_s3_bucket_policy\" \"cloudtrail\" {");
        sb.AppendLine("  count = var.enable_cloudtrail ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  bucket = aws_s3_bucket.cloudtrail[0].id");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [");
        sb.AppendLine("      {");
        sb.AppendLine("        Sid    = \"AWSCloudTrailAclCheck\"");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Principal = {");
        sb.AppendLine("          Service = \"cloudtrail.amazonaws.com\"");
        sb.AppendLine("        }");
        sb.AppendLine("        Action   = \"s3:GetBucketAcl\"");
        sb.AppendLine("        Resource = aws_s3_bucket.cloudtrail[0].arn");
        sb.AppendLine("      },");
        sb.AppendLine("      {");
        sb.AppendLine("        Sid    = \"AWSCloudTrailWrite\"");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Principal = {");
        sb.AppendLine("          Service = \"cloudtrail.amazonaws.com\"");
        sb.AppendLine("        }");
        sb.AppendLine("        Action   = \"s3:PutObject\"");
        sb.AppendLine("        Resource = \"${aws_s3_bucket.cloudtrail[0].arn}/*\"");
        sb.AppendLine("        Condition = {");
        sb.AppendLine("          StringEquals = {");
        sb.AppendLine("            \"s3:x-amz-acl\" = \"bucket-owner-full-control\"");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("data \"aws_caller_identity\" \"current\" {}");
        sb.AppendLine();
        
        // ECR Repository with scanning
        sb.AppendLine("# ECR Repository with image scanning");
        sb.AppendLine("resource \"aws_ecr_repository\" \"main\" {");
        sb.AppendLine("  count = var.enable_container_scan ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  name                 = var.service_name");
        sb.AppendLine("  image_tag_mutability = \"IMMUTABLE\"");
        sb.AppendLine();
        sb.AppendLine("  image_scanning_configuration {");
        sb.AppendLine("    scan_on_push = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  encryption_configuration {");
        sb.AppendLine("    encryption_type = var.kms_key_arn != \"\" ? \"KMS\" : \"AES256\"");
        sb.AppendLine("    kms_key         = var.kms_key_arn != \"\" ? var.kms_key_arn : null");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = var.service_name");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // ECR Repository Policy (restrict to allowed registries)
        sb.AppendLine("resource \"aws_ecr_repository_policy\" \"main\" {");
        sb.AppendLine("  count = var.enable_container_scan && length(var.allowed_image_registries) > 0 ? 1 : 0");
        sb.AppendLine();
        sb.AppendLine("  repository = aws_ecr_repository.main[0].name");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [");
        sb.AppendLine("      {");
        sb.AppendLine("        Sid    = \"AllowPullFromAllowedRegistries\"");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Principal = {");
        sb.AppendLine("          AWS = var.allowed_image_registries");
        sb.AppendLine("        }");
        sb.AppendLine("        Action = [");
        sb.AppendLine("          \"ecr:GetDownloadUrlForLayer\",");
        sb.AppendLine("          \"ecr:BatchGetImage\",");
        sb.AppendLine("          \"ecr:BatchCheckLayerAvailability\"");
        sb.AppendLine("        ]");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    // Helper methods
    private int ParseCPUUnits(string cpuLimit)
    {
        // Convert CPU limit to Fargate CPU units
        // Fargate: 256 (.25 vCPU), 512 (.5 vCPU), 1024 (1 vCPU), 2048 (2 vCPU), 4096 (4 vCPU)
        if (cpuLimit.Contains("0.25") || cpuLimit.Contains("256")) return 256;
        if (cpuLimit.Contains("0.5") || cpuLimit.Contains("512")) return 512;
        if (cpuLimit.Contains("2") && !cpuLimit.Contains("0.25")) return 2048;
        if (cpuLimit.Contains("4")) return 4096;
        return 1024; // Default 1 vCPU
    }
    
    private int ParseMemoryMB(string memoryLimit)
    {
        // Extract number from memory string
        var number = new string(memoryLimit.Where(char.IsDigit).ToArray());
        if (int.TryParse(number, out int value))
        {
            if (memoryLimit.Contains("GB", StringComparison.OrdinalIgnoreCase))
                return value * 1024;
            return value;
        }
        return 2048; // Default 2GB
    }
}
