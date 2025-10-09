using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform modules for AWS Lambda
/// Supports Python, NodeJS, Java, .NET, Go runtimes with API Gateway integration
/// </summary>
public class LambdaModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var deployment = request.Deployment ?? new DeploymentSpec();
        var app = request.Application ?? new ApplicationSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        // Generate all Lambda Terraform files
        files["lambda/function.tf"] = GenerateFunction(request);
        files["lambda/iam.tf"] = GenerateIAM(request);
        files["lambda/cloudwatch.tf"] = GenerateCloudWatch(request);
        files["lambda/variables.tf"] = GenerateVariables();
        files["lambda/outputs.tf"] = GenerateOutputs();
        
        // Optional API Gateway for HTTP APIs
        if (app.Type == ApplicationType.WebAPI || app.Type == ApplicationType.Serverless)
        {
            files["lambda/api_Core.tf"] = GenerateAPIGateway(request);
        }
        
        // VPC configuration for private resources
        if (infrastructure.IncludeNetworking == true)
        {
            files["lambda/vpc.tf"] = GenerateVPC(request);
        }
        
        return files;
    }
    
    private string GenerateFunction(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "function";
        var app = request.Application ?? new ApplicationSpec();
        var deployment = request.Deployment ?? new DeploymentSpec();
        
        var runtime = GetLambdaRuntime(app.Language, app.Framework);
        var memory = ParseMemoryMB(deployment.Resources.MemoryLimit ?? "1024 MB");
        var timeout = 30; // Default timeout
        var handler = GetDefaultHandler(app.Language);
        
        sb.AppendLine("# Lambda Function Configuration");
        sb.AppendLine($"# Runtime: {runtime}, Memory: {memory}MB, Timeout: {timeout}s");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_lambda_function\" \"main\" {");
        sb.AppendLine("  function_name = var.function_name");
        sb.AppendLine("  role          = aws_iam_role.lambda.arn");
        sb.AppendLine();
        sb.AppendLine("  # Code deployment");
        sb.AppendLine("  filename         = var.deployment_package");
        sb.AppendLine("  source_code_hash = filebase64sha256(var.deployment_package)");
        sb.AppendLine();
        sb.AppendLine("  # Runtime configuration");
        sb.AppendLine($"  runtime = var.runtime");
        sb.AppendLine($"  handler = var.handler");
        sb.AppendLine();
        sb.AppendLine("  # Resource limits");
        sb.AppendLine($"  memory_size = var.memory_size");
        sb.AppendLine($"  timeout     = var.timeout");
        sb.AppendLine();
        sb.AppendLine("  # Environment variables");
        sb.AppendLine("  environment {");
        sb.AppendLine("    variables = merge(var.environment_variables, {");
        sb.AppendLine("      ENVIRONMENT = var.environment");
        sb.AppendLine("      LOG_LEVEL   = var.log_level");
        sb.AppendLine("    })");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // VPC configuration if needed
        sb.AppendLine("  # VPC configuration (optional)");
        sb.AppendLine("  dynamic \"vpc_config\" {");
        sb.AppendLine("    for_each = var.subnet_ids != null ? [1] : []");
        sb.AppendLine("    content {");
        sb.AppendLine("      subnet_ids         = var.subnet_ids");
        sb.AppendLine("      security_group_ids = var.security_group_ids");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Reserved concurrency
        sb.AppendLine("  # Concurrency configuration");
        sb.AppendLine("  reserved_concurrent_executions = var.reserved_concurrency");
        sb.AppendLine();
        
        // X-Ray tracing
        sb.AppendLine("  # AWS X-Ray tracing");
        sb.AppendLine("  tracing_config {");
        sb.AppendLine("    mode = var.enable_xray ? \"Active\" : \"PassThrough\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Dead letter queue
        sb.AppendLine("  # Dead letter queue configuration");
        sb.AppendLine("  dynamic \"dead_letter_config\" {");
        sb.AppendLine("    for_each = var.dlq_arn != null ? [1] : []");
        sb.AppendLine("    content {");
        sb.AppendLine("      target_arn = var.dlq_arn");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Lambda layers
        sb.AppendLine("  # Lambda layers");
        sb.AppendLine("  layers = var.lambda_layers");
        sb.AppendLine();
        
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name        = var.function_name");
        sb.AppendLine("    Environment = var.environment");
        sb.AppendLine("    ManagedBy   = \"Terraform\"");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [");
        sb.AppendLine("    aws_cloudwatch_log_group.lambda,");
        sb.AppendLine("    aws_iam_role_policy_attachment.lambda_basic");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Provisioned concurrency (optional)
        sb.AppendLine("# Provisioned Concurrency (optional, for consistent performance)");
        sb.AppendLine("resource \"aws_lambda_provisioned_concurrency_config\" \"main\" {");
        sb.AppendLine("  count                             = var.provisioned_concurrency > 0 ? 1 : 0");
        sb.AppendLine("  function_name                     = aws_lambda_function.main.function_name");
        sb.AppendLine("  provisioned_concurrent_executions = var.provisioned_concurrency");
        sb.AppendLine("  qualifier                         = aws_lambda_function.main.version");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Lambda alias
        sb.AppendLine("# Lambda Alias for version management");
        sb.AppendLine("resource \"aws_lambda_alias\" \"main\" {");
        sb.AppendLine("  name             = var.alias_name");
        sb.AppendLine("  function_name    = aws_lambda_function.main.arn");
        sb.AppendLine("  function_version = \"$LATEST\"");
        sb.AppendLine();
        sb.AppendLine("  lifecycle {");
        sb.AppendLine("    ignore_changes = [function_version]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateIAM(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# IAM Role for Lambda Function");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role\" \"lambda\" {");
        sb.AppendLine("  name = \"${var.function_name}-execution-role\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Action = \"sts:AssumeRole\"");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Service = \"lambda.amazonaws.com\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Basic Lambda execution policy
        sb.AppendLine("# Basic Lambda execution policy");
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"lambda_basic\" {");
        sb.AppendLine("  role       = aws_iam_role.lambda.name");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // VPC execution policy
        sb.AppendLine("# VPC execution policy (if Lambda is in VPC)");
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"lambda_vpc\" {");
        sb.AppendLine("  count      = var.subnet_ids != null ? 1 : 0");
        sb.AppendLine("  role       = aws_iam_role.lambda.name");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // X-Ray policy
        sb.AppendLine("# X-Ray tracing policy");
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"lambda_xray\" {");
        sb.AppendLine("  count      = var.enable_xray ? 1 : 0");
        sb.AppendLine("  role       = aws_iam_role.lambda.name");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AWSXRayDaemonWriteAccess\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Custom policy for application-specific permissions
        sb.AppendLine("# Custom policy for application-specific permissions");
        sb.AppendLine("resource \"aws_iam_role_policy\" \"lambda_custom\" {");
        sb.AppendLine("  name = \"${var.function_name}-custom-policy\"");
        sb.AppendLine("  role = aws_iam_role.lambda.id");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = concat(");
        sb.AppendLine("      [");
        sb.AppendLine("        {");
        sb.AppendLine("          Effect = \"Allow\"");
        sb.AppendLine("          Action = [");
        sb.AppendLine("            \"logs:CreateLogGroup\",");
        sb.AppendLine("            \"logs:CreateLogStream\",");
        sb.AppendLine("            \"logs:PutLogEvents\"");
        sb.AppendLine("          ]");
        sb.AppendLine("          Resource = \"${aws_cloudwatch_log_group.lambda.arn}:*\"");
        sb.AppendLine("        }");
        sb.AppendLine("      ],");
        sb.AppendLine("      var.additional_iam_statements");
        sb.AppendLine("    )");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Example additional policies (commented out)
        sb.AppendLine("# Example: Add policies for S3, DynamoDB, SQS, SNS, etc.");
        sb.AppendLine("# Uncomment and modify based on your needs");
        sb.AppendLine();
        sb.AppendLine("# resource \"aws_iam_role_policy\" \"s3_access\" {");
        sb.AppendLine("#   name = \"${var.function_name}-s3-access\"");
        sb.AppendLine("#   role = aws_iam_role.lambda.id");
        sb.AppendLine("#");
        sb.AppendLine("#   policy = jsonencode({");
        sb.AppendLine("#     Version = \"2012-10-17\"");
        sb.AppendLine("#     Statement = [{");
        sb.AppendLine("#       Effect = \"Allow\"");
        sb.AppendLine("#       Action = [");
        sb.AppendLine("#         \"s3:GetObject\",");
        sb.AppendLine("#         \"s3:PutObject\"");
        sb.AppendLine("#       ]");
        sb.AppendLine("#       Resource = \"arn:aws:s3:::your-bucket/*\"");
        sb.AppendLine("#     }]");
        sb.AppendLine("#   })");
        sb.AppendLine("# }");
        
        return sb.ToString();
    }
    
    private string GenerateAPIGateway(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        
        sb.AppendLine("# API Gateway HTTP API for Lambda");
        sb.AppendLine("# Provides HTTP endpoints for the Lambda function");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_apigatewayv2_api\" \"main\" {");
        sb.AppendLine("  name          = \"${var.function_name}-api\"");
        sb.AppendLine("  protocol_type = \"HTTP\"");
        sb.AppendLine("  description   = \"HTTP API for ${var.function_name}\"");
        sb.AppendLine();
        sb.AppendLine("  cors_configuration {");
        sb.AppendLine("    allow_origins = var.cors_allowed_origins");
        sb.AppendLine("    allow_methods = [\"GET\", \"POST\", \"PUT\", \"DELETE\", \"OPTIONS\"]");
        sb.AppendLine("    allow_headers = [\"Content-Type\", \"Authorization\", \"X-Amz-Date\", \"X-Api-Key\"]");
        sb.AppendLine("    max_age       = 300");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // API Gateway integration
        sb.AppendLine("resource \"aws_apigatewayv2_integration\" \"lambda\" {");
        sb.AppendLine("  api_id             = aws_apigatewayv2_api.main.id");
        sb.AppendLine("  integration_type   = \"AWS_PROXY\"");
        sb.AppendLine("  integration_method = \"POST\"");
        sb.AppendLine("  integration_uri    = aws_lambda_function.main.invoke_arn");
        sb.AppendLine("  payload_format_version = \"2.0\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Default route (catch-all)
        sb.AppendLine("resource \"aws_apigatewayv2_route\" \"default\" {");
        sb.AppendLine("  api_id    = aws_apigatewayv2_api.main.id");
        sb.AppendLine("  route_key = \"$default\"");
        sb.AppendLine("  target    = \"integrations/${aws_apigatewayv2_integration.lambda.id}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Specific routes (examples)
        sb.AppendLine("# Example: Specific routes");
        sb.AppendLine("resource \"aws_apigatewayv2_route\" \"get\" {");
        sb.AppendLine("  api_id    = aws_apigatewayv2_api.main.id");
        sb.AppendLine("  route_key = \"GET /\"");
        sb.AppendLine("  target    = \"integrations/${aws_apigatewayv2_integration.lambda.id}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        if (app.IncludeHealthCheck == true)
        {
            sb.AppendLine("resource \"aws_apigatewayv2_route\" \"health\" {");
            sb.AppendLine("  api_id    = aws_apigatewayv2_api.main.id");
            sb.AppendLine("  route_key = \"GET /health\"");
            sb.AppendLine("  target    = \"integrations/${aws_apigatewayv2_integration.lambda.id}\"");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        // API Gateway stage
        sb.AppendLine("resource \"aws_apigatewayv2_stage\" \"default\" {");
        sb.AppendLine("  api_id      = aws_apigatewayv2_api.main.id");
        sb.AppendLine("  name        = \"$default\"");
        sb.AppendLine("  auto_deploy = true");
        sb.AppendLine();
        sb.AppendLine("  access_log_settings {");
        sb.AppendLine("    destination_arn = aws_cloudwatch_log_group.api_Core.arn");
        sb.AppendLine("    format = jsonencode({");
        sb.AppendLine("      requestId      = \"$context.requestId\"");
        sb.AppendLine("      ip             = \"$context.identity.sourceIp\"");
        sb.AppendLine("      requestTime    = \"$context.requestTime\"");
        sb.AppendLine("      httpMethod     = \"$context.httpMethod\"");
        sb.AppendLine("      routeKey       = \"$context.routeKey\"");
        sb.AppendLine("      status         = \"$context.status\"");
        sb.AppendLine("      protocol       = \"$context.protocol\"");
        sb.AppendLine("      responseLength = \"$context.responseLength\"");
        sb.AppendLine("      integrationError = \"$context.integrationErrorMessage\"");
        sb.AppendLine("    })");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Lambda permission for API Gateway
        sb.AppendLine("resource \"aws_lambda_permission\" \"api_gateway\" {");
        sb.AppendLine("  statement_id  = \"AllowAPIGatewayInvoke\"");
        sb.AppendLine("  action        = \"lambda:InvokeFunction\"");
        sb.AppendLine("  function_name = aws_lambda_function.main.function_name");
        sb.AppendLine("  principal     = \"apiCore.amazonaws.com\"");
        sb.AppendLine("  source_arn    = \"${aws_apigatewayv2_api.main.execution_arn}/*/*\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Custom domain (optional, commented out)
        sb.AppendLine("# Custom domain name (optional)");
        sb.AppendLine("# resource \"aws_apigatewayv2_domain_name\" \"main\" {");
        sb.AppendLine("#   domain_name = var.custom_domain_name");
        sb.AppendLine("#");
        sb.AppendLine("#   domain_name_configuration {");
        sb.AppendLine("#     certificate_arn = var.certificate_arn");
        sb.AppendLine("#     endpoint_type   = \"REGIONAL\"");
        sb.AppendLine("#     security_policy = \"TLS_1_2\"");
        sb.AppendLine("#   }");
        sb.AppendLine("# }");
        sb.AppendLine();
        sb.AppendLine("# resource \"aws_apigatewayv2_api_mapping\" \"main\" {");
        sb.AppendLine("#   api_id      = aws_apigatewayv2_api.main.id");
        sb.AppendLine("#   domain_name = aws_apigatewayv2_domain_name.main.id");
        sb.AppendLine("#   stage       = aws_apigatewayv2_stage.default.id");
        sb.AppendLine("# }");
        
        return sb.ToString();
    }
    
    private string GenerateCloudWatch(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# CloudWatch Log Groups");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_cloudwatch_log_group\" \"lambda\" {");
        sb.AppendLine("  name              = \"/aws/lambda/${var.function_name}\"");
        sb.AppendLine("  retention_in_days = var.log_retention_days");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.function_name}-logs\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_cloudwatch_log_group\" \"api_gateway\" {");
        sb.AppendLine("  name              = \"/aws/apigateway/${var.function_name}\"");
        sb.AppendLine("  retention_in_days = var.log_retention_days");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.function_name}-api-logs\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // CloudWatch Alarms
        sb.AppendLine("# CloudWatch Alarms");
        sb.AppendLine("resource \"aws_cloudwatch_metric_alarm\" \"errors\" {");
        sb.AppendLine("  alarm_name          = \"${var.function_name}-errors\"");
        sb.AppendLine("  comparison_operator = \"GreaterThanThreshold\"");
        sb.AppendLine("  evaluation_periods  = \"1\"");
        sb.AppendLine("  metric_name         = \"Errors\"");
        sb.AppendLine("  namespace           = \"AWS/Lambda\"");
        sb.AppendLine("  period              = \"300\"");
        sb.AppendLine("  statistic           = \"Sum\"");
        sb.AppendLine("  threshold           = \"5\"");
        sb.AppendLine("  alarm_description   = \"This metric monitors lambda errors\"");
        sb.AppendLine();
        sb.AppendLine("  dimensions = {");
        sb.AppendLine("    FunctionName = aws_lambda_function.main.function_name");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  alarm_actions = var.alarm_actions");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_cloudwatch_metric_alarm\" \"throttles\" {");
        sb.AppendLine("  alarm_name          = \"${var.function_name}-throttles\"");
        sb.AppendLine("  comparison_operator = \"GreaterThanThreshold\"");
        sb.AppendLine("  evaluation_periods  = \"1\"");
        sb.AppendLine("  metric_name         = \"Throttles\"");
        sb.AppendLine("  namespace           = \"AWS/Lambda\"");
        sb.AppendLine("  period              = \"300\"");
        sb.AppendLine("  statistic           = \"Sum\"");
        sb.AppendLine("  threshold           = \"10\"");
        sb.AppendLine("  alarm_description   = \"This metric monitors lambda throttles\"");
        sb.AppendLine();
        sb.AppendLine("  dimensions = {");
        sb.AppendLine("    FunctionName = aws_lambda_function.main.function_name");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  alarm_actions = var.alarm_actions");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("resource \"aws_cloudwatch_metric_alarm\" \"duration\" {");
        sb.AppendLine("  alarm_name          = \"${var.function_name}-duration\"");
        sb.AppendLine("  comparison_operator = \"GreaterThanThreshold\"");
        sb.AppendLine("  evaluation_periods  = \"2\"");
        sb.AppendLine("  metric_name         = \"Duration\"");
        sb.AppendLine("  namespace           = \"AWS/Lambda\"");
        sb.AppendLine("  period              = \"300\"");
        sb.AppendLine("  statistic           = \"Average\"");
        sb.AppendLine("  threshold           = tostring(var.timeout * 1000 * 0.8) # 80% of timeout");
        sb.AppendLine("  alarm_description   = \"This metric monitors lambda duration\"");
        sb.AppendLine();
        sb.AppendLine("  dimensions = {");
        sb.AppendLine("    FunctionName = aws_lambda_function.main.function_name");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  alarm_actions = var.alarm_actions");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVPC(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("# VPC Configuration for Lambda");
        sb.AppendLine("# Enables Lambda to access resources in private VPC");
        sb.AppendLine();
        sb.AppendLine("# Security group for Lambda function");
        sb.AppendLine("resource \"aws_security_group\" \"lambda\" {");
        sb.AppendLine("  name        = \"${var.function_name}-lambda\"");
        sb.AppendLine("  description = \"Security group for Lambda function\"");
        sb.AppendLine("  vpc_id      = var.vpc_id");
        sb.AppendLine();
        sb.AppendLine("  egress {");
        sb.AppendLine("    from_port   = 0");
        sb.AppendLine("    to_port     = 0");
        sb.AppendLine("    protocol    = \"-1\"");
        sb.AppendLine("    cidr_blocks = [\"0.0.0.0/0\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.function_name}-lambda-sg\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVariables()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Input Variables for Lambda Module");
        sb.AppendLine();
        sb.AppendLine("variable \"function_name\" {");
        sb.AppendLine("  description = \"Name of the Lambda function\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"runtime\" {");
        sb.AppendLine("  description = \"Lambda runtime (e.g., python3.11, nodejs20.x, java17)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"python3.11\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"handler\" {");
        sb.AppendLine("  description = \"Lambda function handler\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"index.handler\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"deployment_package\" {");
        sb.AppendLine("  description = \"Path to the deployment package (zip file)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"memory_size\" {");
        sb.AppendLine("  description = \"Memory size in MB (128 to 10240)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 1024");
        sb.AppendLine();
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = var.memory_size >= 128 && var.memory_size <= 10240");
        sb.AppendLine("    error_message = \"Memory size must be between 128 MB and 10,240 MB.\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"timeout\" {");
        sb.AppendLine("  description = \"Function timeout in seconds (1 to 900)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 30");
        sb.AppendLine();
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = var.timeout >= 1 && var.timeout <= 900");
        sb.AppendLine("    error_message = \"Timeout must be between 1 and 900 seconds.\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment\" {");
        sb.AppendLine("  description = \"Environment name (dev, staging, prod)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"dev\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment_variables\" {");
        sb.AppendLine("  description = \"Environment variables for the function\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"log_level\" {");
        sb.AppendLine("  description = \"Log level for the function\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"INFO\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"log_retention_days\" {");
        sb.AppendLine("  description = \"CloudWatch log retention in days\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 14");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"reserved_concurrency\" {");
        sb.AppendLine("  description = \"Reserved concurrent executions (-1 for unreserved)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = -1");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"provisioned_concurrency\" {");
        sb.AppendLine("  description = \"Provisioned concurrent executions\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"alias_name\" {");
        sb.AppendLine("  description = \"Name for Lambda alias\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"live\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_xray\" {");
        sb.AppendLine("  description = \"Enable AWS X-Ray tracing\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"lambda_layers\" {");
        sb.AppendLine("  description = \"List of Lambda layer ARNs\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"dlq_arn\" {");
        sb.AppendLine("  description = \"Dead letter queue ARN (SQS or SNS)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_id\" {");
        sb.AppendLine("  description = \"VPC ID for Lambda function\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"subnet_ids\" {");
        sb.AppendLine("  description = \"Subnet IDs for Lambda function\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"security_group_ids\" {");
        sb.AppendLine("  description = \"Security group IDs for Lambda function\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cors_allowed_origins\" {");
        sb.AppendLine("  description = \"CORS allowed origins for API Gateway\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = [\"*\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"additional_iam_statements\" {");
        sb.AppendLine("  description = \"Additional IAM policy statements\"");
        sb.AppendLine("  type        = list(any)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"alarm_actions\" {");
        sb.AppendLine("  description = \"List of alarm action ARNs (SNS topics)\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === ZERO TRUST SECURITY PARAMETERS ===
        sb.AppendLine("# === ZERO TRUST SECURITY PARAMETERS ===");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_vpc_config\" {");
        sb.AppendLine("  description = \"Deploy Lambda in VPC for network isolation\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kms_key_arn\" {");
        sb.AppendLine("  description = \"KMS key ARN for environment variable encryption\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_secrets_manager\" {");
        sb.AppendLine("  description = \"Use AWS Secrets Manager for sensitive configuration\"");
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
        sb.AppendLine("variable \"enable_code_signing\" {");
        sb.AppendLine("  description = \"Enable code signing for Lambda deployments\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"code_signing_config_arn\" {");
        sb.AppendLine("  description = \"Code signing configuration ARN\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_function_url_auth\" {");
        sb.AppendLine("  description = \"Require IAM authentication for function URLs\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_private_api\" {");
        sb.AppendLine("  description = \"Make API Gateway private (VPC endpoint only)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_waf\" {");
        sb.AppendLine("  description = \"Enable AWS WAF for API Gateway\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_api_key_required\" {");
        sb.AppendLine("  description = \"Require API keys for API Gateway access\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloudwatch_logs_encryption\" {");
        sb.AppendLine("  description = \"Encrypt CloudWatch Logs with KMS\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_guardduty\" {");
        sb.AppendLine("  description = \"Enable GuardDuty for Lambda protection\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_resource_based_policy\" {");
        sb.AppendLine("  description = \"Use resource-based policy for fine-grained access control\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_principals\" {");
        sb.AppendLine("  description = \"List of AWS principals allowed to invoke the function\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_layer_version_validation\" {");
        sb.AppendLine("  description = \"Validate Lambda layer versions for security\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
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
        sb.AppendLine("output \"function_name\" {");
        sb.AppendLine("  description = \"Lambda function name\"");
        sb.AppendLine("  value       = aws_lambda_function.main.function_name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"function_arn\" {");
        sb.AppendLine("  description = \"Lambda function ARN\"");
        sb.AppendLine("  value       = aws_lambda_function.main.arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"function_invoke_arn\" {");
        sb.AppendLine("  description = \"Lambda function invoke ARN\"");
        sb.AppendLine("  value       = aws_lambda_function.main.invoke_arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"function_version\" {");
        sb.AppendLine("  description = \"Latest published version\"");
        sb.AppendLine("  value       = aws_lambda_function.main.version");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"function_role_arn\" {");
        sb.AppendLine("  description = \"IAM role ARN for Lambda function\"");
        sb.AppendLine("  value       = aws_iam_role.lambda.arn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"log_group_name\" {");
        sb.AppendLine("  description = \"CloudWatch Log Group name\"");
        sb.AppendLine("  value       = aws_cloudwatch_log_group.lambda.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"api_endpoint\" {");
        sb.AppendLine("  description = \"API Gateway endpoint URL\"");
        sb.AppendLine("  value       = try(aws_apigatewayv2_stage.default.invoke_url, \"\")");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"api_id\" {");
        sb.AppendLine("  description = \"API Gateway ID\"");
        sb.AppendLine("  value       = try(aws_apigatewayv2_api.main.id, \"\")");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    // Helper methods
    private string GetLambdaRuntime(ProgrammingLanguage language, string? framework)
    {
        return language switch
        {
            ProgrammingLanguage.Python => "python3.11",
            ProgrammingLanguage.NodeJS => "nodejs20.x",
            ProgrammingLanguage.Java => "java17",
            ProgrammingLanguage.DotNet => "dotnet8",
            ProgrammingLanguage.Go => "go1.x",
            ProgrammingLanguage.Ruby => "ruby3.2",
            _ => "python3.11"
        };
    }
    
    private string GetDefaultHandler(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.Python => "lambda_function.lambda_handler",
            ProgrammingLanguage.NodeJS => "index.handler",
            ProgrammingLanguage.Java => "com.example.Handler::handleRequest",
            ProgrammingLanguage.DotNet => "Assembly::Namespace.ClassName::MethodName",
            ProgrammingLanguage.Go => "main",
            ProgrammingLanguage.Ruby => "lambda_function.lambda_handler",
            _ => "index.handler"
        };
    }
    
    private int ParseMemoryMB(string memoryLimit)
    {
        var number = new string(memoryLimit.Where(char.IsDigit).ToArray());
        if (int.TryParse(number, out int value))
        {
            if (memoryLimit.Contains("GB", StringComparison.OrdinalIgnoreCase))
                return value * 1024;
            return value;
        }
        return 1024; // Default 1GB
    }
}
