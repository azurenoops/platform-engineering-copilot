namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Environment health status.</summary>
public class EnvironmentHealthDto
{
    public Guid EnvironmentId { get; set; }
    public string OverallStatus { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<ResourceHealthDto> ResourceHealth { get; set; } = new();
}

/// <summary>Per-resource health status.</summary>
public class ResourceHealthDto
{
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
}
