using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

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
        IProgress<EvidenceCollectionProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<RemediationPlan> GenerateRemediationPlanAsync(string subscriptionId, List<AtoFinding> findings, CancellationToken cancellationToken = default);
    Task<ComplianceTimeline> GetComplianceTimelineAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<RiskAssessment> PerformRiskAssessmentAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<ComplianceCertificate> GenerateComplianceCertificateAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

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