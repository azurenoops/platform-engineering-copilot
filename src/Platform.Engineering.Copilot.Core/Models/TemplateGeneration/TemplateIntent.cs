namespace Platform.Engineering.Copilot.Core.Models
{
    /// <summary>
    /// Structured representation of user intent for template generation
    /// </summary>
    public class TemplateIntent
    {
        public string RawRequest { get; set; } = "";
        public string ServiceType { get; set; } = "";
        public List<string> SecurityRequirements { get; set; } = new();
        public List<string> InfrastructureComponents { get; set; } = new();
        public List<string> MonitoringRequirements { get; set; } = new();
        public ScalingConfig ScalingRequirements { get; set; } = new();
        public List<string> DatabaseRequirements { get; set; } = new();
        public NetworkConfig NetworkingRequirements { get; set; } = new();
        public string DeploymentTier { get; set; } = "standard";
    }

    public class ScalingConfig
    {
        public bool AutoScaling { get; set; }
        public int MinReplicas { get; set; } = 1;
        public int MaxReplicas { get; set; } = 10;
        public int TargetCpuUtilization { get; set; } = 70;
    }

    public class NetworkConfig
    {
        public bool PublicAccess { get; set; }
        public bool PrivateNetwork { get; set; }
        public bool LoadBalancer { get; set; }
        public bool CustomDomain { get; set; }
    }
}