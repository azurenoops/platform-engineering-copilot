using Platform.Engineering.Copilot.Core.Models.CodeScanning;
using Platform.Engineering.Copilot.Core.Models.Compliance;


namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Comprehensive Code Scanning Engine that orchestrates security analysis, 
/// vulnerability detection, compliance checking, and automated remediation for codebases
/// </summary>
public interface ICodeScanningEngine
{
    /// <summary>
    /// Runs comprehensive security scan of a codebase for vulnerabilities, compliance violations, and ATO requirements
    /// </summary>
    Task<CodeSecurityAssessment> RunSecurityScanAsync(
        string workspacePath,
        string? filePatterns = null,
        string? complianceFrameworks = null,
        string scanDepth = "deep",
        IProgress<SecurityScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes dependencies for known vulnerabilities and security issues
    /// </summary>
    Task<DependencySecurityAssessment> AnalyzeDependenciesAsync(
        string projectPath,
        string? packageManagers = null,
        bool includeTransitiveDependencies = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects exposed secrets, credentials, and sensitive information in codebase
    /// </summary>
    Task<SecretDetectionResult> DetectSecretsAsync(
        string workspacePath,
        string? scanPatterns = null,
        bool includeHistoricalScanning = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans Infrastructure as Code templates for security misconfigurations
    /// </summary>
    Task<IacSecurityAssessment> ScanInfrastructureAsCodeAsync(
        string projectPath,
        string? templateTypes = null,
        string? complianceFrameworks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs Static Application Security Testing (SAST) analysis
    /// </summary>
    Task<SastAnalysisResult> PerformStaticSecurityAnalysisAsync(
        string workspacePath,
        string? languages = null,
        string? securityRules = null,
        bool includeOwaspTop10 = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans container configurations and Docker images for security issues
    /// </summary>
    Task<ContainerSecurityAssessment> ScanContainerSecurityAsync(
        string projectPath,
        string? containerRegistry = null,
        bool scanImages = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes GitHub security alerts and repository security posture
    /// </summary>
    Task<GitHubSecurityAssessment> AnalyzeGitHubSecurityAlertsAsync(
        string repositoryUrl,
        string? owner = null,
        string? repository = null,
        bool includeAdvancedSecurity = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Collects security evidence and compliance artifacts for ATO documentation
    /// </summary>
    Task<SecurityEvidencePackage> CollectSecurityEvidenceAsync(
        string workspacePath,
        string? complianceFrameworks = null,
        string evidenceTypes = "all",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes automated remediation for identified security vulnerabilities
    /// </summary>
    Task<RemediationExecutionResult> ExecuteAutomatedRemediationAsync(
        string projectPath,
        List<SecurityFinding> findings,
        string? remediationTypes = null,
        string executionMode = "dry_run",
        bool createPullRequest = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest security scan results for a project
    /// </summary>
    Task<CodeSecurityAssessment?> GetLatestSecurityAssessmentAsync(
        string projectIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates comprehensive remediation plan for security findings
    /// </summary>
    Task<SecurityRemediationPlan> GenerateRemediationPlanAsync(
        List<SecurityFinding> findings,
        string? prioritizationStrategy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors continuous security status of codebase
    /// </summary>
    Task<ContinuousSecurityStatus> GetContinuousSecurityStatusAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs comprehensive security scan of a remote repository (GitHub, Azure DevOps, GitHub Enterprise)
    /// </summary>
    Task<CodeSecurityAssessment> ScanRepositoryAsync(
        string repositoryUrl,
        string? branch = null,
        string? filePatterns = null,
        string? complianceFrameworks = null,
        string scanDepth = "deep",
        IProgress<SecurityScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a GitHub repository by owner and name
    /// </summary>
    Task<CodeSecurityAssessment> ScanGitHubRepositoryAsync(
        string owner,
        string repository,
        string? branch = null,
        string? complianceFrameworks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans an Azure DevOps repository
    /// </summary>
    Task<CodeSecurityAssessment> ScanAzureDevOpsRepositoryAsync(
        string organization,
        string project,
        string repository,
        string? branch = null,
        string? complianceFrameworks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a GitHub Enterprise repository
    /// </summary>
    Task<CodeSecurityAssessment> ScanGitHubEnterpriseRepositoryAsync(
        string enterpriseUrl,
        string owner,
        string repository,
        string? branch = null,
        string? complianceFrameworks = null,
        CancellationToken cancellationToken = default);
}