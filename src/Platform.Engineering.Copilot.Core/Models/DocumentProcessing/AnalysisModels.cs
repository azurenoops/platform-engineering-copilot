namespace Platform.Engineering.Copilot.DocumentProcessing.Models;

// Architecture Analysis Models
public class ArchitectureAnalysis
{
    public List<ArchitecturePattern> DetectedPatterns { get; set; } = new();
    public List<SystemComponent> SystemComponents { get; set; } = new();
    public List<DataFlow> DataFlows { get; set; } = new();
    public List<SecurityBoundary> SecurityBoundaries { get; set; } = new();
    public List<string> TechnologyStack { get; set; } = new();
    public List<ArchitectureRecommendation> Recommendations { get; set; } = new();
}

public class ArchitecturePattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class SystemComponent
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class DataFlow
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public SecurityClassification Classification { get; set; }
}

public class SecurityBoundary
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Components { get; set; } = new();
}

public class ArchitectureRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; }
}

// Security Analysis Models
public class SecurityAnalysis
{
    public List<SecurityControl> IdentifiedControls { get; set; } = new();
    public List<SecurityRisk> IdentifiedRisks { get; set; } = new();
    public List<ComplianceGap> ComplianceGaps { get; set; } = new();
    public SecurityPosture OverallPosture { get; set; } = new();
    public List<SecurityRecommendation> Recommendations { get; set; } = new();
}

public class SecurityControl
{
    public string ControlId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ControlImplementationStatus Status { get; set; }
}

public class SecurityRisk
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskLevel Level { get; set; }
}

public class ComplianceGap
{
    public string Description { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public string Remediation { get; set; } = string.Empty;
}

public class SecurityPosture
{
    public string OverallRating { get; set; } = string.Empty;
    public Dictionary<string, string> CategoryRatings { get; set; } = new();
}

public class SecurityRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; }
}