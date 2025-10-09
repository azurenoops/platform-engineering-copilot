using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform module for AWS EKS (Elastic Kubernetes Service)
/// Follows the same modular pattern as ECSModuleGenerator
/// </summary>
public class EKSModuleGenerator
{
    public Dictionary<string, string> GenerateEKSModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var serviceName = request.ServiceName ?? "eks-service";
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Generate all EKS module files
        files["infra/terraform/modules/eks/cluster.tf"] = GenerateClusterConfig(request);
        files["infra/terraform/modules/eks/node_groups.tf"] = GenerateNodeGroupsConfig(request);
        files["infra/terraform/modules/eks/iam.tf"] = GenerateIAMConfig(request);
        files["infra/terraform/modules/eks/vpc.tf"] = GenerateVPCConfig(request);
        files["infra/terraform/modules/eks/security_groups.tf"] = GenerateSecurityGroupsConfig(request);
        files["infra/terraform/modules/eks/addons.tf"] = GenerateAddonsConfig(request);
        files["infra/terraform/modules/eks/autoscaling.tf"] = GenerateAutoScalingConfig(request);
        files["infra/terraform/modules/eks/cloudwatch.tf"] = GenerateCloudWatchConfig(request);
        files["infra/terraform/modules/eks/variables.tf"] = GenerateVariablesConfig(request);
        files["infra/terraform/modules/eks/outputs.tf"] = GenerateOutputsConfig(request);
        
        return files;
    }
    
    private string GenerateClusterConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "eks-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        
        sb.AppendLine("# EKS Cluster Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_eks_cluster\" \"main\" {");
        sb.AppendLine($"  name     = \"${{var.cluster_name}}\"");
        sb.AppendLine($"  version  = var.kubernetes_version");
        sb.AppendLine($"  role_arn = aws_iam_role.cluster.arn");
        sb.AppendLine();
        sb.AppendLine("  vpc_config {");
        sb.AppendLine("    subnet_ids              = concat(aws_subnet.private[*].id, aws_subnet.public[*].id)");
        sb.AppendLine("    endpoint_private_access = true");
        sb.AppendLine("    endpoint_public_access  = true");
        sb.AppendLine("    public_access_cidrs     = var.cluster_endpoint_public_access_cidrs");
        sb.AppendLine("    security_group_ids      = [aws_security_group.cluster.id]");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        if (security.RBAC)
        {
            sb.AppendLine("  # Enable RBAC");
            sb.AppendLine("  # RBAC is enabled by default in EKS");
        }
        
        sb.AppendLine("  # Enable control plane logging");
        sb.AppendLine("  enabled_cluster_log_types = [");
        sb.AppendLine("    \"api\",");
        sb.AppendLine("    \"audit\",");
        sb.AppendLine("    \"authenticator\",");
        sb.AppendLine("    \"controllerManager\",");
        sb.AppendLine("    \"scheduler\"");
        sb.AppendLine("  ]");
        sb.AppendLine();
        sb.AppendLine("  # Encryption configuration");
        sb.AppendLine("  encryption_config {");
        sb.AppendLine("    provider {");
        sb.AppendLine("      key_arn = aws_kms_key.eks.arn");
        sb.AppendLine("    }");
        sb.AppendLine("    resources = [\"secrets\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine($"    Name = \"${{var.cluster_name}}\"");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [");
        sb.AppendLine("    aws_iam_role_policy_attachment.cluster_AmazonEKSClusterPolicy,");
        sb.AppendLine("    aws_iam_role_policy_attachment.cluster_AmazonEKSVPCResourceController,");
        sb.AppendLine("    aws_cloudwatch_log_group.cluster");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // KMS key for encryption
        sb.AppendLine("resource \"aws_kms_key\" \"eks\" {");
        sb.AppendLine("  description             = \"EKS Secret Encryption Key\"");
        sb.AppendLine("  deletion_window_in_days = 10");
        sb.AppendLine("  enable_key_rotation     = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-eks-secrets\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_kms_alias\" \"eks\" {");
        sb.AppendLine("  name          = \"alias/${var.cluster_name}-eks\"");
        sb.AppendLine("  target_key_id = aws_kms_key.eks.key_id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateNodeGroupsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        var resources = deployment.Resources ?? new ResourceRequirements();
        
        var minSize = deployment.MinReplicas > 0 ? deployment.MinReplicas : 2;
        var maxSize = deployment.MaxReplicas > 0 ? deployment.MaxReplicas : 10;
        var desiredSize = deployment.Replicas > 0 ? deployment.Replicas : 3;
        
        sb.AppendLine("# EKS Node Groups Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_eks_node_group\" \"main\" {");
        sb.AppendLine("  cluster_name    = aws_eks_cluster.main.name");
        sb.AppendLine("  node_group_name = \"${var.cluster_name}-node-group\"");
        sb.AppendLine("  node_role_arn   = aws_iam_role.node.arn");
        sb.AppendLine("  subnet_ids      = aws_subnet.private[*].id");
        sb.AppendLine();
        sb.AppendLine("  scaling_config {");
        sb.AppendLine($"    desired_size = {desiredSize}");
        sb.AppendLine($"    min_size     = {minSize}");
        sb.AppendLine($"    max_size     = {maxSize}");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  update_config {");
        sb.AppendLine("    max_unavailable = 1");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Determine instance types based on resource requirements
        var instanceTypes = DetermineInstanceTypes(resources);
        sb.AppendLine("  # Instance types");
        sb.AppendLine("  instance_types = [");
        foreach (var instanceType in instanceTypes)
        {
            sb.AppendLine($"    \"{instanceType}\",");
        }
        sb.AppendLine("  ]");
        sb.AppendLine();
        
        sb.AppendLine("  # Disk size");
        sb.AppendLine("  disk_size = 50");
        sb.AppendLine();
        sb.AppendLine("  # Launch template for advanced configuration");
        sb.AppendLine("  launch_template {");
        sb.AppendLine("    id      = aws_launch_template.node_group.id");
        sb.AppendLine("    version = \"$Latest\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  labels = {");
        sb.AppendLine("    role        = \"worker\"");
        sb.AppendLine("    environment = var.environment");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-node-group\"");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [");
        sb.AppendLine("    aws_iam_role_policy_attachment.node_AmazonEKSWorkerNodePolicy,");
        sb.AppendLine("    aws_iam_role_policy_attachment.node_AmazonEKS_CNI_Policy,");
        sb.AppendLine("    aws_iam_role_policy_attachment.node_AmazonEC2ContainerRegistryReadOnly,");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Launch template for advanced node configuration
        sb.AppendLine("resource \"aws_launch_template\" \"node_group\" {");
        sb.AppendLine("  name_prefix = \"${var.cluster_name}-node-\"");
        sb.AppendLine();
        sb.AppendLine("  metadata_options {");
        sb.AppendLine("    http_endpoint               = \"enabled\"");
        sb.AppendLine("    http_tokens                 = \"required\"");
        sb.AppendLine("    http_put_response_hop_limit = 2");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  monitoring {");
        sb.AppendLine("    enabled = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  network_interfaces {");
        sb.AppendLine("    associate_public_ip_address = false");
        sb.AppendLine("    delete_on_termination       = true");
        sb.AppendLine("    security_groups             = [aws_security_group.node.id]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tag_specifications {");
        sb.AppendLine("    resource_type = \"instance\"");
        sb.AppendLine("    tags = merge(var.tags, {");
        sb.AppendLine("      Name = \"${var.cluster_name}-node\"");
        sb.AppendLine("    })");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  user_data = base64encode(templatefile(\"${path.module}/templates/user_data.sh\", {");
        sb.AppendLine("    cluster_name        = var.cluster_name");
        sb.AppendLine("    cluster_endpoint    = aws_eks_cluster.main.endpoint");
        sb.AppendLine("    cluster_ca          = aws_eks_cluster.main.certificate_authority[0].data");
        sb.AppendLine("  }))");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateIAMConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# IAM Roles and Policies for EKS");
        sb.AppendLine();
        sb.AppendLine("# Cluster IAM Role");
        sb.AppendLine("resource \"aws_iam_role\" \"cluster\" {");
        sb.AppendLine("  name = \"${var.cluster_name}-cluster-role\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Action = \"sts:AssumeRole\"");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Service = \"eks.amazonaws.com\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"cluster_AmazonEKSClusterPolicy\" {");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AmazonEKSClusterPolicy\"");
        sb.AppendLine("  role       = aws_iam_role.cluster.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"cluster_AmazonEKSVPCResourceController\" {");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AmazonEKSVPCResourceController\"");
        sb.AppendLine("  role       = aws_iam_role.cluster.name");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Node IAM Role");
        sb.AppendLine("resource \"aws_iam_role\" \"node\" {");
        sb.AppendLine("  name = \"${var.cluster_name}-node-role\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Action = \"sts:AssumeRole\"");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Service = \"ec2.amazonaws.com\"");
        sb.AppendLine("      }");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"node_AmazonEKSWorkerNodePolicy\" {");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy\"");
        sb.AppendLine("  role       = aws_iam_role.node.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"node_AmazonEKS_CNI_Policy\" {");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy\"");
        sb.AppendLine("  role       = aws_iam_role.node.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"node_AmazonEC2ContainerRegistryReadOnly\" {");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly\"");
        sb.AppendLine("  role       = aws_iam_role.node.name");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Additional policy for CloudWatch logging
        sb.AppendLine("# CloudWatch Logs Policy");
        sb.AppendLine("resource \"aws_iam_role_policy\" \"node_cloudwatch_logs\" {");
        sb.AppendLine("  name = \"${var.cluster_name}-node-cloudwatch-logs\"");
        sb.AppendLine("  role = aws_iam_role.node.id");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Action = [");
        sb.AppendLine("        \"logs:CreateLogGroup\",");
        sb.AppendLine("        \"logs:CreateLogStream\",");
        sb.AppendLine("        \"logs:PutLogEvents\",");
        sb.AppendLine("        \"logs:DescribeLogStreams\"");
        sb.AppendLine("      ]");
        sb.AppendLine("      Resource = \"arn:aws:logs:*:*:*\"");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // OIDC provider for IRSA (IAM Roles for Service Accounts)
        sb.AppendLine("# OIDC Provider for IRSA");
        sb.AppendLine("data \"tls_certificate\" \"cluster\" {");
        sb.AppendLine("  url = aws_eks_cluster.main.identity[0].oidc[0].issuer");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_openid_connect_provider\" \"cluster\" {");
        sb.AppendLine("  client_id_list  = [\"sts.amazonaws.com\"]");
        sb.AppendLine("  thumbprint_list = [data.tls_certificate.cluster.certificates[0].sha1_fingerprint]");
        sb.AppendLine("  url             = aws_eks_cluster.main.identity[0].oidc[0].issuer");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateVPCConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("# VPC Configuration for EKS");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_vpc\" \"main\" {");
        sb.AppendLine("  cidr_block           = var.vpc_cidr");
        sb.AppendLine("  enable_dns_hostnames = true");
        sb.AppendLine("  enable_dns_support   = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name                                        = \"${var.cluster_name}-vpc\"");
        sb.AppendLine("    \"kubernetes.io/cluster/${var.cluster_name}\" = \"shared\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Internet Gateway
        sb.AppendLine("resource \"aws_internet_gateway\" \"main\" {");
        sb.AppendLine("  vpc_id = aws_vpc.main.id");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-igw\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Public Subnets
        sb.AppendLine("# Public Subnets");
        sb.AppendLine("resource \"aws_subnet\" \"public\" {");
        sb.AppendLine("  count = var.availability_zones_count");
        sb.AppendLine();
        sb.AppendLine("  vpc_id                  = aws_vpc.main.id");
        sb.AppendLine("  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index)");
        sb.AppendLine("  availability_zone       = data.aws_availability_zones.available.names[count.index]");
        sb.AppendLine("  map_public_ip_on_launch = true");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name                                        = \"${var.cluster_name}-public-${count.index + 1}\"");
        sb.AppendLine("    \"kubernetes.io/cluster/${var.cluster_name}\" = \"shared\"");
        sb.AppendLine("    \"kubernetes.io/role/elb\"                    = \"1\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Private Subnets
        sb.AppendLine("# Private Subnets");
        sb.AppendLine("resource \"aws_subnet\" \"private\" {");
        sb.AppendLine("  count = var.availability_zones_count");
        sb.AppendLine();
        sb.AppendLine("  vpc_id            = aws_vpc.main.id");
        sb.AppendLine("  cidr_block        = cidrsubnet(var.vpc_cidr, 8, count.index + var.availability_zones_count)");
        sb.AppendLine("  availability_zone = data.aws_availability_zones.available.names[count.index]");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name                                        = \"${var.cluster_name}-private-${count.index + 1}\"");
        sb.AppendLine("    \"kubernetes.io/cluster/${var.cluster_name}\" = \"shared\"");
        sb.AppendLine("    \"kubernetes.io/role/internal-elb\"           = \"1\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // NAT Gateway
        sb.AppendLine("# NAT Gateway for private subnets");
        sb.AppendLine("resource \"aws_eip\" \"nat\" {");
        sb.AppendLine("  count  = var.availability_zones_count");
        sb.AppendLine("  domain = \"vpc\"");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-nat-${count.index + 1}\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_nat_gateway\" \"main\" {");
        sb.AppendLine("  count = var.availability_zones_count");
        sb.AppendLine();
        sb.AppendLine("  allocation_id = aws_eip.nat[count.index].id");
        sb.AppendLine("  subnet_id     = aws_subnet.public[count.index].id");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-nat-${count.index + 1}\"");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [aws_internet_Core.main]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Route Tables
        sb.AppendLine("# Public Route Table");
        sb.AppendLine("resource \"aws_route_table\" \"public\" {");
        sb.AppendLine("  vpc_id = aws_vpc.main.id");
        sb.AppendLine();
        sb.AppendLine("  route {");
        sb.AppendLine("    cidr_block = \"0.0.0.0/0\"");
        sb.AppendLine("    gateway_id = aws_internet_Core.main.id");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-public-rt\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_route_table_association\" \"public\" {");
        sb.AppendLine("  count = var.availability_zones_count");
        sb.AppendLine();
        sb.AppendLine("  subnet_id      = aws_subnet.public[count.index].id");
        sb.AppendLine("  route_table_id = aws_route_table.public.id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Private Route Tables");
        sb.AppendLine("resource \"aws_route_table\" \"private\" {");
        sb.AppendLine("  count = var.availability_zones_count");
        sb.AppendLine();
        sb.AppendLine("  vpc_id = aws_vpc.main.id");
        sb.AppendLine();
        sb.AppendLine("  route {");
        sb.AppendLine("    cidr_block     = \"0.0.0.0/0\"");
        sb.AppendLine("    nat_gateway_id = aws_nat_Core.main[count.index].id");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-private-rt-${count.index + 1}\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_route_table_association\" \"private\" {");
        sb.AppendLine("  count = var.availability_zones_count");
        sb.AppendLine();
        sb.AppendLine("  subnet_id      = aws_subnet.private[count.index].id");
        sb.AppendLine("  route_table_id = aws_route_table.private[count.index].id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Data source for availability zones");
        sb.AppendLine("data \"aws_availability_zones\" \"available\" {");
        sb.AppendLine("  state = \"available\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateSecurityGroupsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Security Groups for EKS");
        sb.AppendLine();
        sb.AppendLine("# Cluster Security Group");
        sb.AppendLine("resource \"aws_security_group\" \"cluster\" {");
        sb.AppendLine("  name_prefix = \"${var.cluster_name}-cluster-\"");
        sb.AppendLine("  description = \"EKS cluster security group\"");
        sb.AppendLine("  vpc_id      = aws_vpc.main.id");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name = \"${var.cluster_name}-cluster-sg\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_security_group_rule\" \"cluster_ingress_workstation_https\" {");
        sb.AppendLine("  description       = \"Allow workstation to communicate with the cluster API Server\"");
        sb.AppendLine("  type              = \"ingress\"");
        sb.AppendLine("  from_port         = 443");
        sb.AppendLine("  to_port           = 443");
        sb.AppendLine("  protocol          = \"tcp\"");
        sb.AppendLine("  cidr_blocks       = var.cluster_endpoint_public_access_cidrs");
        sb.AppendLine("  security_group_id = aws_security_group.cluster.id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Node Security Group");
        sb.AppendLine("resource \"aws_security_group\" \"node\" {");
        sb.AppendLine("  name_prefix = \"${var.cluster_name}-node-\"");
        sb.AppendLine("  description = \"Security group for all nodes in the cluster\"");
        sb.AppendLine("  vpc_id      = aws_vpc.main.id");
        sb.AppendLine();
        sb.AppendLine("  egress {");
        sb.AppendLine("    from_port   = 0");
        sb.AppendLine("    to_port     = 0");
        sb.AppendLine("    protocol    = \"-1\"");
        sb.AppendLine("    cidr_blocks = [\"0.0.0.0/0\"]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = merge(var.tags, {");
        sb.AppendLine("    Name                                        = \"${var.cluster_name}-node-sg\"");
        sb.AppendLine("    \"kubernetes.io/cluster/${var.cluster_name}\" = \"owned\"");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_security_group_rule\" \"node_ingress_self\" {");
        sb.AppendLine("  description              = \"Allow nodes to communicate with each other\"");
        sb.AppendLine("  type                     = \"ingress\"");
        sb.AppendLine("  from_port                = 0");
        sb.AppendLine("  to_port                  = 65535");
        sb.AppendLine("  protocol                 = \"-1\"");
        sb.AppendLine("  source_security_group_id = aws_security_group.node.id");
        sb.AppendLine("  security_group_id        = aws_security_group.node.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_security_group_rule\" \"node_ingress_cluster\" {");
        sb.AppendLine("  description              = \"Allow pods to communicate with the cluster API Server\"");
        sb.AppendLine("  type                     = \"ingress\"");
        sb.AppendLine("  from_port                = 443");
        sb.AppendLine("  to_port                  = 443");
        sb.AppendLine("  protocol                 = \"tcp\"");
        sb.AppendLine("  source_security_group_id = aws_security_group.cluster.id");
        sb.AppendLine("  security_group_id        = aws_security_group.node.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_security_group_rule\" \"cluster_ingress_node_https\" {");
        sb.AppendLine("  description              = \"Allow pods to communicate with the cluster API Server\"");
        sb.AppendLine("  type                     = \"ingress\"");
        sb.AppendLine("  from_port                = 443");
        sb.AppendLine("  to_port                  = 443");
        sb.AppendLine("  protocol                 = \"tcp\"");
        sb.AppendLine("  source_security_group_id = aws_security_group.node.id");
        sb.AppendLine("  security_group_id        = aws_security_group.cluster.id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateAddonsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# EKS Add-ons");
        sb.AppendLine();
        sb.AppendLine("# VPC CNI Add-on");
        sb.AppendLine("resource \"aws_eks_addon\" \"vpc_cni\" {");
        sb.AppendLine("  cluster_name             = aws_eks_cluster.main.name");
        sb.AppendLine("  addon_name               = \"vpc-cni\"");
        sb.AppendLine("  addon_version            = var.vpc_cni_version");
        sb.AppendLine("  resolve_conflicts        = \"OVERWRITE\"");
        sb.AppendLine("  service_account_role_arn = aws_iam_role.vpc_cni.arn");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# CoreDNS Add-on");
        sb.AppendLine("resource \"aws_eks_addon\" \"coredns\" {");
        sb.AppendLine("  cluster_name      = aws_eks_cluster.main.name");
        sb.AppendLine("  addon_name        = \"coredns\"");
        sb.AppendLine("  addon_version     = var.coredns_version");
        sb.AppendLine("  resolve_conflicts = \"OVERWRITE\"");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine();
        sb.AppendLine("  depends_on = [aws_eks_node_group.main]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# kube-proxy Add-on");
        sb.AppendLine("resource \"aws_eks_addon\" \"kube_proxy\" {");
        sb.AppendLine("  cluster_name      = aws_eks_cluster.main.name");
        sb.AppendLine("  addon_name        = \"kube-proxy\"");
        sb.AppendLine("  addon_version     = var.kube_proxy_version");
        sb.AppendLine("  resolve_conflicts = \"OVERWRITE\"");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // IAM role for VPC CNI
        sb.AppendLine("# IAM Role for VPC CNI (IRSA)");
        sb.AppendLine("resource \"aws_iam_role\" \"vpc_cni\" {");
        sb.AppendLine("  name = \"${var.cluster_name}-vpc-cni-role\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Federated = aws_iam_openid_connect_provider.cluster.arn");
        sb.AppendLine("      }");
        sb.AppendLine("      Action = \"sts:AssumeRoleWithWebIdentity\"");
        sb.AppendLine("      Condition = {");
        sb.AppendLine("        StringEquals = {");
        sb.AppendLine("          \"${replace(aws_iam_openid_connect_provider.cluster.url, \"https://\", \"\")}:sub\" = \"system:serviceaccount:kube-system:aws-node\"");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"vpc_cni\" {");
        sb.AppendLine("  policy_arn = \"arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy\"");
        sb.AppendLine("  role       = aws_iam_role.vpc_cni.name");
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
        
        sb.AppendLine("# Cluster Autoscaler IAM Policy");
        sb.AppendLine("resource \"aws_iam_policy\" \"cluster_autoscaler\" {");
        sb.AppendLine("  name        = \"${var.cluster_name}-cluster-autoscaler\"");
        sb.AppendLine("  description = \"EKS cluster autoscaler policy\"");
        sb.AppendLine();
        sb.AppendLine("  policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [");
        sb.AppendLine("      {");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Action = [");
        sb.AppendLine("          \"autoscaling:DescribeAutoScalingGroups\",");
        sb.AppendLine("          \"autoscaling:DescribeAutoScalingInstances\",");
        sb.AppendLine("          \"autoscaling:DescribeLaunchConfigurations\",");
        sb.AppendLine("          \"autoscaling:DescribeScalingActivities\",");
        sb.AppendLine("          \"autoscaling:DescribeTags\",");
        sb.AppendLine("          \"ec2:DescribeImages\",");
        sb.AppendLine("          \"ec2:DescribeInstanceTypes\",");
        sb.AppendLine("          \"ec2:DescribeLaunchTemplateVersions\",");
        sb.AppendLine("          \"ec2:GetInstanceTypesFromInstanceRequirements\",");
        sb.AppendLine("          \"eks:DescribeNodegroup\"");
        sb.AppendLine("        ]");
        sb.AppendLine("        Resource = \"*\"");
        sb.AppendLine("      },");
        sb.AppendLine("      {");
        sb.AppendLine("        Effect = \"Allow\"");
        sb.AppendLine("        Action = [");
        sb.AppendLine("          \"autoscaling:SetDesiredCapacity\",");
        sb.AppendLine("          \"autoscaling:TerminateInstanceInAutoScalingGroup\"");
        sb.AppendLine("        ]");
        sb.AppendLine("        Resource = \"*\"");
        sb.AppendLine("        Condition = {");
        sb.AppendLine("          StringEquals = {");
        sb.AppendLine("            \"autoscaling:ResourceTag/k8s.io/cluster-autoscaler/${var.cluster_name}\" = \"owned\"");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Cluster Autoscaler IAM Role (IRSA)");
        sb.AppendLine("resource \"aws_iam_role\" \"cluster_autoscaler\" {");
        sb.AppendLine("  name = \"${var.cluster_name}-cluster-autoscaler\"");
        sb.AppendLine();
        sb.AppendLine("  assume_role_policy = jsonencode({");
        sb.AppendLine("    Version = \"2012-10-17\"");
        sb.AppendLine("    Statement = [{");
        sb.AppendLine("      Effect = \"Allow\"");
        sb.AppendLine("      Principal = {");
        sb.AppendLine("        Federated = aws_iam_openid_connect_provider.cluster.arn");
        sb.AppendLine("      }");
        sb.AppendLine("      Action = \"sts:AssumeRoleWithWebIdentity\"");
        sb.AppendLine("      Condition = {");
        sb.AppendLine("        StringEquals = {");
        sb.AppendLine("          \"${replace(aws_iam_openid_connect_provider.cluster.url, \"https://\", \"\")}:sub\" = \"system:serviceaccount:kube-system:cluster-autoscaler\"");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }]");
        sb.AppendLine("  })");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_iam_role_policy_attachment\" \"cluster_autoscaler\" {");
        sb.AppendLine("  role       = aws_iam_role.cluster_autoscaler.name");
        sb.AppendLine("  policy_arn = aws_iam_policy.cluster_autoscaler.arn");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateCloudWatchConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var observability = request.Observability ?? new ObservabilitySpec();
        
        sb.AppendLine("# CloudWatch Logging for EKS");
        sb.AppendLine();
        sb.AppendLine("resource \"aws_cloudwatch_log_group\" \"cluster\" {");
        sb.AppendLine("  name              = \"/aws/eks/${var.cluster_name}/cluster\"");
        sb.AppendLine("  retention_in_days = var.log_retention_days");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        if (observability.Prometheus)
        {
            sb.AppendLine("# Container Insights for Prometheus metrics");
            sb.AppendLine("# Note: Requires installation of CloudWatch Observability EKS add-on or manual installation");
            sb.AppendLine("# https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/Container-Insights-setup-EKS-quickstart.html");
        }
        
        return sb.ToString();
    }
    
    private string GenerateVariablesConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "eks-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("# Variables for EKS Module");
        sb.AppendLine();
        sb.AppendLine("variable \"cluster_name\" {");
        sb.AppendLine("  description = \"Name of the EKS cluster\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{serviceName}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kubernetes_version\" {");
        sb.AppendLine("  description = \"Kubernetes version to use for the EKS cluster\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"1.28\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_cidr\" {");
        sb.AppendLine("  description = \"CIDR block for VPC\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"availability_zones_count\" {");
        sb.AppendLine("  description = \"Number of availability zones to use\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 3");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"cluster_endpoint_public_access_cidrs\" {");
        sb.AppendLine("  description = \"List of CIDR blocks that can access the cluster endpoint\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = [\"0.0.0.0/0\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vpc_cni_version\" {");
        sb.AppendLine("  description = \"Version of the VPC CNI add-on\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"v1.15.1-eksbuild.1\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"coredns_version\" {");
        sb.AppendLine("  description = \"Version of the CoreDNS add-on\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"v1.10.1-eksbuild.4\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kube_proxy_version\" {");
        sb.AppendLine("  description = \"Version of the kube-proxy add-on\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"v1.28.2-eksbuild.2\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"log_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain CloudWatch logs\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 7");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment\" {");
        sb.AppendLine("  description = \"Environment name (dev, staging, prod)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"dev\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === ZERO TRUST SECURITY PARAMETERS ===
        sb.AppendLine("# === ZERO TRUST SECURITY PARAMETERS ===");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_private_endpoint\" {");
        sb.AppendLine("  description = \"Enable private API server endpoint (disable public access)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_irsa\" {");
        sb.AppendLine("  description = \"Enable IAM Roles for Service Accounts (OIDC)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_ecr_private_endpoint\" {");
        sb.AppendLine("  description = \"Enable VPC endpoint for Amazon ECR\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_pod_security_standards\" {");
        sb.AppendLine("  description = \"Enable Kubernetes Pod Security Standards\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_network_policies\" {");
        sb.AppendLine("  description = \"Enable network policies (requires Calico or other CNI)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_guardduty_eks\" {");
        sb.AppendLine("  description = \"Enable GuardDuty for EKS protection\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kms_key_arn\" {");
        sb.AppendLine("  description = \"KMS key ARN for cluster encryption (secrets, EBS volumes)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_secrets_encryption\" {");
        sb.AppendLine("  description = \"Enable envelope encryption of Kubernetes secrets using KMS\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cloudwatch_logging\" {");
        sb.AppendLine("  description = \"Enable CloudWatch logging for control plane\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_vpc_cni_encryption\" {");
        sb.AppendLine("  description = \"Enable encryption for VPC CNI plugin\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_api_cidr_blocks\" {");
        sb.AppendLine("  description = \"CIDR blocks allowed to access cluster API server\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_fargate_profiles\" {");
        sb.AppendLine("  description = \"Enable Fargate profiles for serverless pods\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_managed_node_groups\" {");
        sb.AppendLine("  description = \"Use managed node groups with automatic AMI updates\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_spot_instances\" {");
        sb.AppendLine("  description = \"Use Spot instances for cost savings\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_cluster_autoscaler\" {");
        sb.AppendLine("  description = \"Enable Kubernetes Cluster Autoscaler\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_aws_loadbalancer_controller\" {");
        sb.AppendLine("  description = \"Enable AWS Load Balancer Controller for ingress\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_ebs_csi_driver\" {");
        sb.AppendLine("  description = \"Enable EBS CSI driver for persistent volumes\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_efs_csi_driver\" {");
        sb.AppendLine("  description = \"Enable EFS CSI driver for shared storage\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_container_insights\" {");
        sb.AppendLine("  description = \"Enable Container Insights for monitoring\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_image_registries\" {");
        sb.AppendLine("  description = \"List of allowed container image registries (ECR ARNs)\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_pod_identity\" {");
        sb.AppendLine("  description = \"Enable EKS Pod Identity (newer alternative to IRSA)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_imds_v2\" {");
        sb.AppendLine("  description = \"Require IMDSv2 on node instances\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to all resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateOutputsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Outputs for EKS Module");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_id\" {");
        sb.AppendLine("  description = \"EKS cluster ID\"");
        sb.AppendLine("  value       = aws_eks_cluster.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_endpoint\" {");
        sb.AppendLine("  description = \"Endpoint for EKS control plane\"");
        sb.AppendLine("  value       = aws_eks_cluster.main.endpoint");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_security_group_id\" {");
        sb.AppendLine("  description = \"Security group ID attached to the EKS cluster\"");
        sb.AppendLine("  value       = aws_security_group.cluster.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_certificate_authority_data\" {");
        sb.AppendLine("  description = \"Base64 encoded certificate data required to communicate with the cluster\"");
        sb.AppendLine("  value       = aws_eks_cluster.main.certificate_authority[0].data");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_name\" {");
        sb.AppendLine("  description = \"Name of the EKS cluster\"");
        sb.AppendLine("  value       = aws_eks_cluster.main.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_oidc_issuer_url\" {");
        sb.AppendLine("  description = \"The URL on the EKS cluster OIDC Issuer\"");
        sb.AppendLine("  value       = try(aws_eks_cluster.main.identity[0].oidc[0].issuer, null)");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"node_group_id\" {");
        sb.AppendLine("  description = \"EKS node group ID\"");
        sb.AppendLine("  value       = aws_eks_node_group.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"vpc_id\" {");
        sb.AppendLine("  description = \"VPC ID\"");
        sb.AppendLine("  value       = aws_vpc.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"private_subnet_ids\" {");
        sb.AppendLine("  description = \"IDs of the private subnets\"");
        sb.AppendLine("  value       = aws_subnet.private[*].id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"public_subnet_ids\" {");
        sb.AppendLine("  description = \"IDs of the public subnets\"");
        sb.AppendLine("  value       = aws_subnet.public[*].id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_autoscaler_role_arn\" {");
        sb.AppendLine("  description = \"ARN of IAM role for cluster autoscaler\"");
        sb.AppendLine("  value       = try(aws_iam_role.cluster_autoscaler.arn, null)");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private List<string> DetermineInstanceTypes(ResourceRequirements resources)
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
        
        // Determine instance types based on CPU and memory
        if (cpuValue <= 2)
        {
            return new List<string> { "t3.medium", "t3a.medium" };
        }
        else if (cpuValue <= 4)
        {
            return new List<string> { "t3.large", "t3a.large" };
        }
        else if (cpuValue <= 8)
        {
            return new List<string> { "t3.xlarge", "t3a.xlarge" };
        }
        else
        {
            return new List<string> { "t3.2xlarge", "t3a.2xlarge" };
        }
    }
}
