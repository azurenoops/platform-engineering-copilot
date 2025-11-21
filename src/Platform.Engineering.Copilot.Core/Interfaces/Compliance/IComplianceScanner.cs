using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Interface for compliance scanners
/// </summary>
public interface IComplianceScanner
{
    /// <summary>
    /// Scans a control at subscription level
    /// </summary>
    Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a control at resource group level for more targeted compliance checks
    /// </summary>
    Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId,
        string resourceGroupName,
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
        string collectedBy,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectLogEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectMetricEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectPolicyEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> CollectAccessControlEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default);
}

