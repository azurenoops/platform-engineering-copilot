using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.Code;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.GitHub;
using Platform.Engineering.Copilot.Core.Models.CodeScanning;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins.Compliance;

/// <summary>
/// Unit tests for CodeScanningPlugin
/// Tests code scanning, dependency analysis, secret detection, and IaC scanning
/// </summary>
public class CodeScanningPluginTests
{
    private readonly Mock<ILogger<CodeScanningPlugin>> _loggerMock;
    private readonly Mock<ICodeScanningEngine> _codeScanningEngineMock;
    private readonly Mock<IGitHubServices> _gitHubServicesMock;
    private readonly Kernel _kernel;

    public CodeScanningPluginTests()
    {
        _loggerMock = new Mock<ILogger<CodeScanningPlugin>>();
        _codeScanningEngineMock = new Mock<ICodeScanningEngine>();
        _gitHubServicesMock = new Mock<IGitHubServices>();
        _kernel = Kernel.CreateBuilder().Build();
    }

    private CodeScanningPlugin CreatePlugin()
    {
        return new CodeScanningPlugin(
            _loggerMock.Object,
            _kernel,
            _codeScanningEngineMock.Object,
            _gitHubServicesMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var plugin = CreatePlugin();

        // Assert
        plugin.Should().NotBeNull();
    }

    #endregion

    #region ScanCodebaseForComplianceAsync Tests

    [Fact]
    public async Task ScanCodebaseForComplianceAsync_WithValidPath_ReturnsScanResults()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.RunSecurityScanAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                It.IsAny<string>(),
                It.IsAny<IProgress<SecurityScanProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSecurityAssessment());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanCodebaseForComplianceAsync(workspacePath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScanCodebaseForComplianceAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.RunSecurityScanAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                It.IsAny<string>(),
                It.IsAny<IProgress<SecurityScanProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Path not found"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanCodebaseForComplianceAsync(workspacePath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Path not found");
    }

    #endregion

    #region ScanDependencyVulnerabilitiesAsync Tests

    [Fact]
    public async Task ScanDependencyVulnerabilitiesAsync_WithValidPath_ReturnsDependencyResults()
    {
        // Arrange
        var projectPath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.AnalyzeDependenciesAsync(
                projectPath, 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockDependencyAssessment());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanDependencyVulnerabilitiesAsync(projectPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScanDependencyVulnerabilitiesAsync_WithVulnerabilities_ShowsDetails()
    {
        // Arrange
        var projectPath = "/path/to/project";
        var assessment = CreateMockDependencyAssessment();
        assessment.Vulnerabilities.Add(new DependencyVulnerability
        {
            PackageName = "vulnerable-package",
            CurrentVersion = "1.0.0",
            FixedVersion = "1.1.0",
            VulnerabilityId = "CVE-2024-1234"
        });
        _codeScanningEngineMock
            .Setup(e => e.AnalyzeDependenciesAsync(
                projectPath, 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(assessment);
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanDependencyVulnerabilitiesAsync(projectPath);

        // Assert
        result.Should().Contain("vulnerable-package");
    }

    [Fact]
    public async Task ScanDependencyVulnerabilitiesAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var projectPath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.AnalyzeDependenciesAsync(
                projectPath, 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Package manifest not found"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanDependencyVulnerabilitiesAsync(projectPath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Package manifest not found");
    }

    #endregion

    #region DetectExposedSecretsAsync Tests

    [Fact]
    public async Task DetectExposedSecretsAsync_WithValidPath_ReturnsSecretResults()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.DetectSecretsAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                false, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSecretDetectionResult());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.DetectExposedSecretsAsync(workspacePath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DetectExposedSecretsAsync_WithSecretsFound_ShowsFindings()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        var secretResult = CreateMockSecretDetectionResult();
        secretResult.TotalSecretsFound = 1;
        secretResult.Secrets.Add(new DetectedSecret
        {
            FilePath = "config/settings.json",
            Type = "API Key",
            LineNumber = 15,
            Severity = SecurityFindingSeverity.Critical,
            FirstDetected = DateTimeOffset.UtcNow.AddDays(-1)
        });
        _codeScanningEngineMock
            .Setup(e => e.DetectSecretsAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                false, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(secretResult);
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.DetectExposedSecretsAsync(workspacePath);

        // Assert
        result.Should().Contain("API Key");
    }

    [Fact]
    public async Task DetectExposedSecretsAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.DetectSecretsAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                false, 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Git repository not found"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.DetectExposedSecretsAsync(workspacePath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Git repository not found");
    }

    #endregion

    #region ScanInfrastructureAsCodeAsync Tests

    [Fact]
    public async Task ScanInfrastructureAsCodeAsync_WithValidPath_ReturnsIacResults()
    {
        // Arrange
        var projectPath = "/path/to/infra";
        _codeScanningEngineMock
            .Setup(e => e.ScanInfrastructureAsCodeAsync(
                projectPath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockIacAssessment());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanInfrastructureAsCodeAsync(projectPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScanInfrastructureAsCodeAsync_WithMisconfigurations_ShowsFindings()
    {
        // Arrange
        var projectPath = "/path/to/infra";
        var iacAssessment = CreateMockIacAssessment();
        iacAssessment.Findings.Add(new IacSecurityFinding
        {
            ResourceType = "Microsoft.Storage/storageAccounts",
            RuleName = "EnableHttpsTrafficOnly",
            Severity = SecurityFindingSeverity.High,
            Description = "Storage account allows HTTP traffic"
        });
        _codeScanningEngineMock
            .Setup(e => e.ScanInfrastructureAsCodeAsync(
                projectPath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(iacAssessment);
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanInfrastructureAsCodeAsync(projectPath);

        // Assert
        result.Should().Contain("Storage");
    }

    [Fact]
    public async Task ScanInfrastructureAsCodeAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var projectPath = "/path/to/infra";
        _codeScanningEngineMock
            .Setup(e => e.ScanInfrastructureAsCodeAsync(
                projectPath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("No IaC templates found"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanInfrastructureAsCodeAsync(projectPath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("No IaC templates found");
    }

    #endregion

    #region PerformStaticSecurityAnalysisAsync Tests

    [Fact]
    public async Task PerformStaticSecurityAnalysisAsync_WithValidPath_ReturnsSastResults()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.PerformStaticSecurityAnalysisAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSastResult());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.PerformStaticSecurityAnalysisAsync(workspacePath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PerformStaticSecurityAnalysisAsync_WithVulnerabilities_ShowsOwaspFindings()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        var sastResult = CreateMockSastResult();
        sastResult.Findings.Add(new SastFinding
        {
            RuleName = "SQL-INJECTION",
            Category = "OWASP A03:2021",
            Severity = SecurityFindingSeverity.Critical,
            FilePath = "src/Data/Repository.cs",
            LineNumber = 42,
            Description = "SQL Injection vulnerability detected"
        });
        _codeScanningEngineMock
            .Setup(e => e.PerformStaticSecurityAnalysisAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sastResult);
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.PerformStaticSecurityAnalysisAsync(workspacePath);

        // Assert
        result.Should().Contain("SQL");
    }

    [Fact]
    public async Task PerformStaticSecurityAnalysisAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.PerformStaticSecurityAnalysisAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unsupported language"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.PerformStaticSecurityAnalysisAsync(workspacePath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Unsupported language");
    }

    #endregion

    #region ScanContainerSecurityAsync Tests

    [Fact]
    public async Task ScanContainerSecurityAsync_WithValidPath_ReturnsContainerResults()
    {
        // Arrange
        var projectPath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.ScanContainerSecurityAsync(
                projectPath, 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockContainerAssessment());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanContainerSecurityAsync(projectPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScanContainerSecurityAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var projectPath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.ScanContainerSecurityAsync(
                projectPath, 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Docker daemon not running"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.ScanContainerSecurityAsync(projectPath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Docker daemon not running");
    }

    #endregion

    #region AnalyzeGitHubSecurityAlertsAsync Tests

    [Fact]
    public async Task AnalyzeGitHubSecurityAlertsAsync_WithValidRepo_ReturnsAlerts()
    {
        // Arrange
        var repoUrl = "https://github.com/owner/repo";
        _codeScanningEngineMock
            .Setup(e => e.AnalyzeGitHubSecurityAlertsAsync(
                repoUrl, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockGitHubSecurityAssessment());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.AnalyzeGitHubSecurityAlertsAsync(repoUrl);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeGitHubSecurityAlertsAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var repoUrl = "https://github.com/owner/repo";
        _codeScanningEngineMock
            .Setup(e => e.AnalyzeGitHubSecurityAlertsAsync(
                repoUrl, 
                It.IsAny<string?>(), 
                It.IsAny<string?>(), 
                true, 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Repository not found"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.AnalyzeGitHubSecurityAlertsAsync(repoUrl);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Repository not found");
    }

    #endregion

    #region CollectSecurityEvidenceAsync Tests

    [Fact]
    public async Task CollectSecurityEvidenceAsync_WithValidPath_ReturnsEvidencePackage()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.CollectSecurityEvidenceAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockEvidencePackage());
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.CollectSecurityEvidenceAsync(workspacePath);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CollectSecurityEvidenceAsync_WithException_ReturnsErrorMessage()
    {
        // Arrange
        var workspacePath = "/path/to/project";
        _codeScanningEngineMock
            .Setup(e => e.CollectSecurityEvidenceAsync(
                workspacePath, 
                It.IsAny<string?>(), 
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Insufficient permissions"));
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.CollectSecurityEvidenceAsync(workspacePath);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("Insufficient permissions");
    }

    #endregion

    #region Helper Methods

    private static CodeSecurityAssessment CreateMockSecurityAssessment()
    {
        return new CodeSecurityAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            ProjectPath = "/path/to/project",
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndTime = DateTimeOffset.UtcNow,
            AllFindings = new List<SecurityFinding>(),
            OverallSecurityScore = 85
        };
    }

    private static DependencySecurityAssessment CreateMockDependencyAssessment()
    {
        return new DependencySecurityAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            ProjectPath = "/path/to/project",
            ScanTime = DateTimeOffset.UtcNow,
            TotalDependencies = 150,
            VulnerableDependencies = 0,
            Vulnerabilities = new List<DependencyVulnerability>()
        };
    }

    private static SecretDetectionResult CreateMockSecretDetectionResult()
    {
        return new SecretDetectionResult
        {
            ScanId = Guid.NewGuid().ToString(),
            WorkspacePath = "/path/to/project",
            ScanTime = DateTimeOffset.UtcNow,
            TotalSecretsFound = 0,
            Secrets = new List<DetectedSecret>()
        };
    }

    private static IacSecurityAssessment CreateMockIacAssessment()
    {
        return new IacSecurityAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            ProjectPath = "/path/to/infra",
            ScanTime = DateTimeOffset.UtcNow,
            TotalTemplates = 15,
            Findings = new List<IacSecurityFinding>()
        };
    }

    private static SastAnalysisResult CreateMockSastResult()
    {
        return new SastAnalysisResult
        {
            AnalysisId = Guid.NewGuid().ToString(),
            WorkspacePath = "/path/to/project",
            AnalysisTime = DateTimeOffset.UtcNow,
            TotalIssues = 0,
            Findings = new List<SastFinding>()
        };
    }

    private static ContainerSecurityAssessment CreateMockContainerAssessment()
    {
        return new ContainerSecurityAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            ProjectPath = "/path/to/project",
            ScanTime = DateTimeOffset.UtcNow,
            TotalContainers = 5,
            ContainerResults = new List<ContainerScanResult>()
        };
    }

    private static GitHubSecurityAssessment CreateMockGitHubSecurityAssessment()
    {
        return new GitHubSecurityAssessment
        {
            AssessmentId = Guid.NewGuid().ToString(),
            RepositoryUrl = "https://github.com/owner/repo",
            ScanTime = DateTimeOffset.UtcNow,
            SecurityAlerts = new List<GitHubSecurityAlert>(),
            CodeScanningAlerts = new List<CodeScanningAlert>(),
            SecretAlerts = new List<SecretScanningAlert>()
        };
    }

    private static SecurityEvidencePackage CreateMockEvidencePackage()
    {
        return new SecurityEvidencePackage
        {
            PackageId = Guid.NewGuid().ToString(),
            ProjectPath = "/path/to/project",
            GeneratedAt = DateTimeOffset.UtcNow,
            Evidence = new Dictionary<string, SecurityEvidence>()
        };
    }

    #endregion
}
