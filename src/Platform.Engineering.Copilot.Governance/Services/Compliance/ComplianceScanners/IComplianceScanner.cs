using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Interface for compliance scanners
/// </summary>
public interface IComplianceScanner
{
    Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for evidence collectors
/// </summary>
public interface IEvidenceCollector
{
    Task<List<ComplianceEvidence>> CollectConfigurationEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectLogEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectMetricEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectPolicyEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectAccessControlEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evidence model used by evidence collectors
/// </summary>
public class ComplianceEvidence
{
    public string EvidenceId { get; set; } = Guid.NewGuid().ToString();
    public required string EvidenceType { get; set; }
    public required string ControlId { get; set; }
    public required string ResourceId { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
    public string? Screenshot { get; set; }
    public string? LogExcerpt { get; set; }
    public string? ConfigSnapshot { get; set; }
}