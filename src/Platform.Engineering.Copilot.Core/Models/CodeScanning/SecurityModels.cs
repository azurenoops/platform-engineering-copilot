namespace Platform.Engineering.Copilot.Core.Models.CodeScanning;

/// <summary>
/// Progress tracking for security scanning operations
/// </summary>
public class SecurityScanProgress
{
    public int TotalPhases { get; set; }
    public int CompletedPhases { get; set; }
    public string CurrentPhase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int PercentComplete => TotalPhases > 0 ? (CompletedPhases * 100) / TotalPhases : 0;
}

/// <summary>
/// Comprehensive security assessment result for a codebase
/// </summary>
public class CodeSecurityAssessment
{
    public string AssessmentId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string? RepositoryUrl { get; set; }
    public string? Branch { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public double OverallSecurityScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public int MediumFindings { get; set; }
    public int LowFindings { get; set; }
    public Dictionary<string, SecurityDomainResult> SecurityDomains { get; set; } = new();
    public List<SecurityFinding> AllFindings { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
    public SecurityRiskProfile RiskProfile { get; set; } = new();
}

/// <summary>
/// Security assessment result for a specific domain (SAST, dependencies, secrets, etc.)
/// </summary>
public class SecurityDomainResult
{
    public string Domain { get; set; } = string.Empty;
    public double SecurityScore { get; set; }
    public List<SecurityFinding> Findings { get; set; } = new();
    public int PassedChecks { get; set; }
    public int TotalChecks { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Individual security finding
/// </summary>
public class SecurityFinding
{
    public string FindingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public bool IsAutoRemediable { get; set; }
    public List<string> AffectedNistControls { get; set; } = new();
    public List<string> CweIds { get; set; } = new();
    public List<string> OwaspCategories { get; set; } = new();
    public string RemediationGuidance { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Security finding severity levels
/// </summary>
public enum SecurityFindingSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Security risk profile for the codebase
/// </summary>
public class SecurityRiskProfile
{
    public string RiskLevel { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public List<string> TopRisks { get; set; } = new();
    public Dictionary<string, double> RiskCategories { get; set; } = new();
    public string RiskTrend { get; set; } = string.Empty;
    public List<string> MitigationRecommendations { get; set; } = new();
}

/// <summary>
/// Dependency security assessment result
/// </summary>
public class DependencySecurityAssessment
{
    public string AssessmentId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset ScanTime { get; set; }
    public int TotalDependencies { get; set; }
    public int VulnerableDependencies { get; set; }
    public List<DependencyVulnerability> Vulnerabilities { get; set; } = new();
    public Dictionary<string, int> VulnerabilitiesBySeverity { get; set; } = new();
    public List<string> PackageManagers { get; set; } = new();
    public double SecurityScore { get; set; }
}

/// <summary>
/// Individual dependency vulnerability
/// </summary>
public class DependencyVulnerability
{
    public string VulnerabilityId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string AffectedVersions { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string FixedVersion { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> CveIds { get; set; } = new();
    public string RemediationAdvice { get; set; } = string.Empty;
    public bool IsAutoRemediable { get; set; }
}

/// <summary>
/// Secret detection result
/// </summary>
public class SecretDetectionResult
{
    public string ScanId { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public DateTimeOffset ScanTime { get; set; }
    public int TotalSecretsFound { get; set; }
    public List<DetectedSecret> Secrets { get; set; } = new();
    public Dictionary<string, int> SecretsByType { get; set; } = new();
    public List<string> ScannedPaths { get; set; } = new();
    public List<string> PatternsUsed { get; set; } = new();
    public double SecurityScore { get; set; }
}

/// <summary>
/// Individual detected secret
/// </summary>
public class DetectedSecret
{
    public string SecretId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Context { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string RemediationAdvice { get; set; } = string.Empty;
    public bool IsConfirmed { get; set; }
    public DateTimeOffset FirstDetected { get; set; }
    public string Pattern { get; set; } = string.Empty;
}

/// <summary>
/// Infrastructure as Code security assessment
/// </summary>
public class IacSecurityAssessment
{
    public string AssessmentId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset ScanTime { get; set; }
    public List<string> TemplateTypes { get; set; } = new();
    public int TotalTemplates { get; set; }
    public int TemplatesWithIssues { get; set; }
    public List<IacSecurityFinding> Findings { get; set; } = new();
    public Dictionary<string, int> FindingsByCategory { get; set; } = new();
    public double SecurityScore { get; set; }
    public bool StigChecksEnabled { get; set; }
}

/// <summary>
/// Infrastructure as Code security finding
/// </summary>
public class IacSecurityFinding
{
    public string FindingId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string TemplateFile { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RemediationAdvice { get; set; } = string.Empty;
    public List<string> ComplianceFrameworks { get; set; } = new();
    public bool IsAutoRemediable { get; set; }
    public string? Category { get; set; }
    public string? StigId { get; set; }
}

/// <summary>
/// Static Application Security Testing result
/// </summary>
public class SastAnalysisResult
{
    public string AnalysisId { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public DateTimeOffset AnalysisTime { get; set; }
    public List<string> Languages { get; set; } = new();
    public int TotalIssues { get; set; }
    public List<SastFinding> Findings { get; set; } = new();
    public Dictionary<string, int> FindingsByCategory { get; set; } = new();
    public Dictionary<string, int> FindingsByOwaspTop10 { get; set; } = new();
    public double SecurityScore { get; set; }
}

/// <summary>
/// SAST security finding
/// </summary>
public class SastFinding
{
    public string FindingId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public SecurityFindingSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CodeSnippet { get; set; } = string.Empty;
    public List<string> OwaspCategories { get; set; } = new();
    public List<string> CweIds { get; set; } = new();
    public string RemediationAdvice { get; set; } = string.Empty;
    public bool IsAutoRemediable { get; set; }
}

/// <summary>
/// Container security assessment result
/// </summary>
public class ContainerSecurityAssessment
{
    public string AssessmentId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset ScanTime { get; set; }
    public List<ContainerScanResult> ContainerResults { get; set; } = new();
    public int TotalContainers { get; set; }
    public int ContainersWithIssues { get; set; }
    public Dictionary<string, int> VulnerabilitiesBySeverity { get; set; } = new();
    public double SecurityScore { get; set; }
}

/// <summary>
/// Individual container scan result
/// </summary>
public class ContainerScanResult
{
    public string ContainerName { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public List<ContainerVulnerability> Vulnerabilities { get; set; } = new();
    public List<ConfigurationIssue> ConfigurationIssues { get; set; } = new();
    public double SecurityScore { get; set; }
}

/// <summary>
/// Container vulnerability
/// </summary>
public class ContainerVulnerability
{
    public string VulnerabilityId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string FixedVersion { get; set; } = string.Empty;
    public List<string> CveIds { get; set; } = new();
}

/// <summary>
/// Container configuration issue
/// </summary>
public class ConfigurationIssue
{
    public string IssueId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RemediationAdvice { get; set; } = string.Empty;
    public bool IsAutoRemediable { get; set; }
}

/// <summary>
/// GitHub security assessment result
/// </summary>
public class GitHubSecurityAssessment
{
    public string AssessmentId { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public DateTimeOffset ScanTime { get; set; }
    public GitHubSecurityFeatures SecurityFeatures { get; set; } = new();
    public List<GitHubSecurityAlert> SecurityAlerts { get; set; } = new();
    public List<CodeScanningAlert> CodeScanningAlerts { get; set; } = new();
    public List<SecretScanningAlert> SecretAlerts { get; set; } = new();
    public double SecurityScore { get; set; }
}

/// <summary>
/// GitHub security features status
/// </summary>
public class GitHubSecurityFeatures
{
    public bool DependabotEnabled { get; set; }
    public bool CodeScanningEnabled { get; set; }
    public bool SecretScanningEnabled { get; set; }
    public bool VulnerabilityReportsEnabled { get; set; }
    public bool AdvancedSecurityEnabled { get; set; }
}

/// <summary>
/// GitHub security alert
/// </summary>
public class GitHubSecurityAlert
{
    public string AlertId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
}

/// <summary>
/// GitHub code scanning alert
/// </summary>
public class CodeScanningAlert : GitHubSecurityAlert
{
    public string RuleName { get; set; } = string.Empty;
    public string Tool { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

/// <summary>
/// GitHub secret scanning alert
/// </summary>
public class SecretScanningAlert : GitHubSecurityAlert
{
    public string SecretType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
}

/// <summary>
/// Security evidence package for compliance
/// </summary>
public class SecurityEvidencePackage
{
    public string PackageId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public List<string> ComplianceFrameworks { get; set; } = new();
    public Dictionary<string, SecurityEvidence> Evidence { get; set; } = new();
    public List<ComplianceMapping> ComplianceMappings { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string? StorageUri { get; set; }
}

/// <summary>
/// Individual security evidence item
/// </summary>
public class SecurityEvidence
{
    public string EvidenceId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTimeOffset CollectedAt { get; set; }
}

/// <summary>
/// Compliance framework mapping
/// </summary>
public class ComplianceMapping
{
    public string Framework { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public List<string> EvidenceIds { get; set; } = new();
    public bool IsCompliant { get; set; }
    public string ComplianceStatus { get; set; } = string.Empty;
}

/// <summary>
/// Remediation execution result
/// </summary>
public class RemediationExecutionResult
{
    public string ExecutionId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset ExecutedAt { get; set; }
    public string ExecutionMode { get; set; } = string.Empty;
    public int TotalRemediations { get; set; }
    public int SuccessfulRemediations { get; set; }
    public int FailedRemediations { get; set; }
    public List<RemediationResult> Results { get; set; } = new();
    public string PullRequestUrl { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Individual remediation result
/// </summary>
public class RemediationResult
{
    public string FindingId { get; set; } = string.Empty;
    public string RemediationType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ChangedFiles { get; set; } = new();
    public string RemediationDetails { get; set; } = string.Empty;
}

/// <summary>
/// Security remediation plan
/// </summary>
public class SecurityRemediationPlan
{
    public string PlanId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<SecurityRemediationStep> Steps { get; set; } = new();
    public Dictionary<string, int> RemediationsByPriority { get; set; } = new();
    public TimeSpan EstimatedDuration { get; set; }
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// Individual security remediation step
/// </summary>
public class SecurityRemediationStep
{
    public int StepNumber { get; set; }
    public string FindingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RemediationType { get; set; } = string.Empty;
    public SecurityFindingSeverity Priority { get; set; }
    public bool IsAutoRemediable { get; set; }
    public TimeSpan EstimatedEffort { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public string Implementation { get; set; } = string.Empty;
}

/// <summary>
/// Continuous security monitoring status
/// </summary>
public class ContinuousSecurityStatus
{
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset LastScanTime { get; set; }
    public double CurrentSecurityScore { get; set; }
    public string SecurityTrend { get; set; } = string.Empty;
    public List<SecurityAlert> ActiveAlerts { get; set; } = new();
    public Dictionary<string, int> FindingsTrend { get; set; } = new();
    public List<string> RecentChanges { get; set; } = new();
    public string OverallStatus { get; set; } = string.Empty;
}

/// <summary>
/// Active security alert
/// </summary>
public class SecurityAlert
{
    public string AlertId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public SecurityFindingSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; set; }
    public bool IsAcknowledged { get; set; }
}