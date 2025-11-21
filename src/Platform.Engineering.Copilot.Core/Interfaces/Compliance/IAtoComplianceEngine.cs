using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Comprehensive ATO Compliance Engine that orchestrates compliance scanning, 
/// evidence collection, continuous monitoring, and automated remediation
/// </summary>
public interface IAtoComplianceEngine
{
    Task<AtoComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId, 
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<AtoComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<AtoComplianceAssessment?> GetLatestAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);
    
    Task<ContinuousComplianceStatus> GetContinuousComplianceStatusAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    Task<EvidencePackage> CollectComplianceEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        IProgress<EvidenceCollectionProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<ComplianceTimeline> GetComplianceTimelineAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<RiskAssessment> PerformRiskAssessmentAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<ComplianceCertificate> GenerateComplianceCertificateAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

