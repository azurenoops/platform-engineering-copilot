using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.GitHub;
using Platform.Engineering.Copilot.Core.Models.CodeScanning;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins;

/// <summary>
/// Code scanning and analysis plugin for security vulnerability detection, compliance checking, and code quality assessment.
/// Acts as a thin wrapper around the CodeScanningEngine for comprehensive codebase security analysis.
/// </summary>
public class CodeScanningPlugin : BaseSupervisorPlugin
{
    private readonly ICodeScanningEngine _codeScanningEngine;
    private readonly IGitHubServices _gitHubService;

    public CodeScanningPlugin(
        ILogger<CodeScanningPlugin> logger,
        Kernel kernel,
        ICodeScanningEngine codeScanningEngine,
        IGitHubServices gitHubService) : base(logger, kernel)
    {
        _codeScanningEngine = codeScanningEngine ?? throw new ArgumentNullException(nameof(codeScanningEngine));
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
    }

    /// <summary>
    /// Scan entire codebase for security vulnerabilities, compliance violations, and ATO requirements.
    /// Analyzes all code types (TypeScript, Python, C#, etc.) against NIST 800-53, STIG, and other compliance frameworks.
    /// </summary>
    [KernelFunction, Description("Scan entire codebase for security vulnerabilities, compliance violations, and ATO requirements using AI-powered static analysis")]
    public async Task<string> ScanCodebaseForComplianceAsync(
        [Description("Path to the workspace/repository to scan")] string workspacePath,
        [Description("File patterns to include (e.g., '*.ts,*.py,*.cs')")] string? filePatterns = null,
        [Description("Compliance frameworks to check against (e.g., 'NIST-800-53,STIG,SOC2')")] string? complianceFrameworks = null,
        [Description("Analysis depth: surface, deep, or comprehensive")] string scanDepth = "deep")
    {
        _logger.LogInformation("Starting comprehensive security scan for workspace: {WorkspacePath}", workspacePath);

        try
        {
            // Use the CodeScanningEngine for comprehensive analysis
            var progress = new Progress<SecurityScanProgress>(p =>
            {
                _logger.LogInformation("Security scan progress: {Phase} ({Completed}/{Total}) - {Message}",
                    p.CurrentPhase, p.CompletedPhases, p.TotalPhases, p.Message);
            });

            var assessment = await _codeScanningEngine.RunSecurityScanAsync(
                workspacePath,
                filePatterns,
                complianceFrameworks,
                scanDepth,
                progress);

            return GenerateFormattedSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Codebase security scan failed for: {WorkspacePath}", workspacePath);
            return CreateErrorResponse("scan codebase for compliance", ex);
        }
    }

    /// <summary>
    /// Perform comprehensive dependency vulnerability scanning using multiple security databases.
    /// Analyzes package.json, requirements.txt, *.csproj, go.mod, and other dependency files.
    /// </summary>
    [KernelFunction, Description("Scan project dependencies for known vulnerabilities using OWASP, Snyk, and CVE databases. Supports npm, pip, NuGet, Maven, Go modules, and more.")]
    public async Task<string> ScanDependencyVulnerabilitiesAsync(
        [Description("Path to the project root containing dependency files")] string projectPath,
        [Description("Dependency manager types to scan: npm,pip,nuget,maven,go,composer")] string? dependencyTypes = null,
        [Description("Vulnerability severity filter: low,medium,high,critical")] string? severityFilter = "medium,high,critical",
        [Description("Include license compliance check")] bool includeLicenseCheck = true)
    {
        _logger.LogInformation("Starting dependency vulnerability scan for: {ProjectPath}", projectPath);

        try
        {
            var assessment = await _codeScanningEngine.AnalyzeDependenciesAsync(
                projectPath,
                dependencyTypes,
                true);

            return GenerateDependencyReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dependency scan failed for: {ProjectPath}", projectPath);
            return CreateErrorResponse("scan dependency vulnerabilities", ex);
        }
    }

    /// <summary>
    /// Detect exposed secrets, API keys, passwords, and sensitive data in codebase using advanced pattern matching.
    /// Scans source code, configuration files, and git history for credential leaks.
    /// </summary>
    [KernelFunction, Description("Detect exposed secrets, API keys, passwords, and credentials in source code and configuration files using advanced pattern matching and entropy analysis")]
    public async Task<string> DetectExposedSecretsAsync(
        [Description("Path to the workspace/repository to scan for secrets")] string workspacePath,
        [Description("File patterns to scan (e.g., '*.js,*.py,*.env,*.config')")] string? scanPatterns = null,
        [Description("Include git history scanning for historical leaks")] bool includeHistoricalScanning = false,
        [Description("Secret types to detect: api-keys,passwords,tokens,certificates")] string secretTypes = "api-keys,passwords,tokens")
    {
        _logger.LogInformation("Starting secret detection scan for: {WorkspacePath}", workspacePath);

        try
        {
            var result = await _codeScanningEngine.DetectSecretsAsync(
                workspacePath,
                scanPatterns,
                includeHistoricalScanning);

            return GenerateSecretDetectionReport(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Secret detection failed for: {WorkspacePath}", workspacePath);
            return CreateErrorResponse("detect exposed secrets", ex);
        }
    }

    /// <summary>
    /// Scan Infrastructure as Code templates for security misconfigurations and compliance violations.
    /// Supports ARM, Terraform, CloudFormation, Bicep, Kubernetes manifests, and Docker configurations.
    /// </summary>
    [KernelFunction, Description("Scan Infrastructure as Code templates (ARM, Terraform, CloudFormation, Bicep, K8s) for security misconfigurations and compliance violations")]
    public async Task<string> ScanInfrastructureAsCodeAsync(
        [Description("Path to the project containing IaC templates")] string projectPath,
        [Description("Template types to scan: arm,terraform,cloudformation,bicep,kubernetes,docker")] string? templateTypes = null,
        [Description("Compliance frameworks to validate against")] string? complianceFrameworks = "NIST-800-53,CIS",
        [Description("Scan severity level: low,medium,high,critical")] string severityLevel = "medium")
    {
        _logger.LogInformation("Starting Infrastructure as Code scan for: {ProjectPath}", projectPath);

        try
        {
            var assessment = await _codeScanningEngine.ScanInfrastructureAsCodeAsync(
                projectPath,
                templateTypes,
                complianceFrameworks);

            return GenerateIacSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IaC security scan failed for: {ProjectPath}", projectPath);
            return CreateErrorResponse("scan infrastructure as code", ex);
        }
    }

    /// <summary>
    /// Perform Static Application Security Testing (SAST) analysis to identify security vulnerabilities in source code.
    /// Analyzes code patterns, data flow, and identifies OWASP Top 10 vulnerabilities.
    /// </summary>
    [KernelFunction, Description("Perform Static Application Security Testing (SAST) to identify security vulnerabilities, code injection risks, and OWASP Top 10 issues in source code")]
    public async Task<string> PerformStaticSecurityAnalysisAsync(
        [Description("Path to the workspace/codebase for static analysis")] string workspacePath,
        [Description("Programming languages to analyze: csharp,javascript,python,java,go")] string? languages = null,
        [Description("Security rules to apply: owasp-top-10,cwe-top-25,sans-top-25")] string? securityRules = "owasp-top-10",
        [Description("Include OWASP Top 10 vulnerability detection")] bool includeOwaspTop10 = true)
    {
        _logger.LogInformation("Starting static security analysis for: {WorkspacePath}", workspacePath);

        try
        {
            var result = await _codeScanningEngine.PerformStaticSecurityAnalysisAsync(
                workspacePath,
                languages,
                securityRules,
                includeOwaspTop10);

            return GenerateSastReport(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Static security analysis failed for: {WorkspacePath}", workspacePath);
            return CreateErrorResponse("perform static security analysis", ex);
        }
    }

    /// <summary>
    /// Scan container configurations and Docker images for security vulnerabilities and misconfigurations.
    /// Analyzes Dockerfile, container registries, and runtime security settings.
    /// </summary>
    [KernelFunction, Description("Scan Docker containers, images, and configurations for security vulnerabilities, malware, and compliance violations")]
    public async Task<string> ScanContainerSecurityAsync(
        [Description("Path to the project containing container configurations")] string projectPath,
        [Description("Container registry to scan (optional)")] string? containerRegistry = null,
        [Description("Scan container images for vulnerabilities")] bool scanImages = true,
        [Description("Include runtime security analysis")] bool includeRuntimeAnalysis = false)
    {
        _logger.LogInformation("Starting container security scan for: {ProjectPath}", projectPath);

        try
        {
            var assessment = await _codeScanningEngine.ScanContainerSecurityAsync(
                projectPath,
                containerRegistry,
                scanImages);

            return GenerateContainerSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Container security scan failed for: {ProjectPath}", projectPath);
            return CreateErrorResponse("scan container security", ex);
        }
    }

    /// <summary>
    /// Analyze GitHub repository security alerts, Dependabot alerts, and Advanced Security features.
    /// Reviews security policies, branch protection, and vulnerability disclosure.
    /// </summary>
    [KernelFunction, Description("Analyze GitHub repository security alerts, Dependabot findings, code scanning results, and security feature configuration")]
    public async Task<string> AnalyzeGitHubSecurityAlertsAsync(
        [Description("GitHub repository URL or path")] string repositoryUrl,
        [Description("Repository owner/organization name")] string? owner = null,
        [Description("Repository name")] string? repository = null,
        [Description("Include GitHub Advanced Security features")] bool includeAdvancedSecurity = true)
    {
        _logger.LogInformation("Analyzing GitHub security for repository: {RepositoryUrl}", repositoryUrl);

        try
        {
            var assessment = await _codeScanningEngine.AnalyzeGitHubSecurityAlertsAsync(
                repositoryUrl,
                owner,
                repository,
                includeAdvancedSecurity);

            return GenerateGitHubSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub security analysis failed for: {RepositoryUrl}", repositoryUrl);
            return CreateErrorResponse("analyze GitHub security alerts", ex);
        }
    }

    /// <summary>
    /// Collect comprehensive security evidence and compliance artifacts for ATO documentation.
    /// Generates evidence packages with scan results, configurations, and compliance mappings.
    /// </summary>
    [KernelFunction, Description("Collect comprehensive security evidence and compliance artifacts for ATO documentation, audit preparation, and compliance validation")]
    public async Task<string> CollectSecurityEvidenceAsync(
        [Description("Path to the workspace/project for evidence collection")] string workspacePath,
        [Description("Compliance frameworks to generate evidence for")] string? complianceFrameworks = "NIST-800-53,SOC2",
        [Description("Evidence types to collect: scan-results,configurations,policies,logs")] string evidenceTypes = "all",
        [Description("Output format for evidence package")] string outputFormat = "json")
    {
        _logger.LogInformation("Collecting security evidence for: {WorkspacePath}", workspacePath);

        try
        {
            var evidencePackage = await _codeScanningEngine.CollectSecurityEvidenceAsync(
                workspacePath,
                complianceFrameworks,
                evidenceTypes);

            return GenerateEvidenceReport(evidencePackage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security evidence collection failed for: {WorkspacePath}", workspacePath);
            return CreateErrorResponse("collect security evidence", ex);
        }
    }

    /// <summary>
    /// Execute automated security remediation for identified vulnerabilities and misconfigurations.
    /// Applies fixes for dependency updates, configuration changes, and code transformations.
    /// </summary>
    [KernelFunction, Description("Automatically remediate security vulnerabilities through dependency updates, configuration fixes, and code transformations. Integrates with CI/CD for safe deployment.")]
    public async Task<string> ExecuteAutomatedRemediationAsync(
        [Description("Path to project requiring remediation")] string projectPath,
        [Description("Remediation types: dependency_updates,config_fixes,security_patches")] string? remediationTypes = null,
        [Description("Execution mode: dry_run,safe_apply,force_apply")] string executionMode = "dry_run",
        [Description("Create pull request with changes")] bool createPullRequest = true)
    {
        _logger.LogInformation("Starting automated security remediation for: {ProjectPath}", projectPath);

        try
        {
            // Get latest security assessment to determine what needs remediation
            var latestAssessment = await _codeScanningEngine.GetLatestSecurityAssessmentAsync(projectPath);
            
            if (latestAssessment == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No security assessment found. Please run a security scan first.",
                    recommendation = "Use 'scan codebase for compliance' to identify issues before remediation."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var autoRemediableFindings = latestAssessment.AllFindings
                .Where(f => f.IsAutoRemediable)
                .ToList();

            if (!autoRemediableFindings.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "No auto-remediable findings detected",
                    totalFindings = latestAssessment.TotalFindings,
                    manualFindings = latestAssessment.AllFindings.Count(f => !f.IsAutoRemediable),
                    recommendation = "Review manual findings that require developer intervention."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var remediationResult = await _codeScanningEngine.ExecuteAutomatedRemediationAsync(
                projectPath,
                autoRemediableFindings,
                remediationTypes,
                executionMode,
                createPullRequest);

            return GenerateRemediationReport(remediationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automated remediation failed for: {ProjectPath}", projectPath);
            return CreateErrorResponse("execute automated remediation", ex);
        }
    }

    #region Report Generation Methods

    private string GenerateFormattedSecurityReport(CodeSecurityAssessment assessment)
    {
        try
        {
            var reportBuilder = new System.Text.StringBuilder();
            reportBuilder.AppendLine("# 🔍 Comprehensive Security Assessment Report");
            reportBuilder.AppendLine($"**Project:** `{assessment.ProjectPath}`");
            reportBuilder.AppendLine($"**Assessment ID:** {assessment.AssessmentId}");
            reportBuilder.AppendLine($"**Scan Date:** {assessment.EndTime:yyyy-MM-dd HH:mm:ss UTC}");
            reportBuilder.AppendLine($"**Duration:** {assessment.Duration.TotalSeconds:F1}s");
            reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(assessment.OverallSecurityScore)} **{assessment.OverallSecurityScore:F1}%**");
            reportBuilder.AppendLine();

            reportBuilder.AppendLine("## 📊 Executive Summary");
            reportBuilder.AppendLine(assessment.ExecutiveSummary);
            reportBuilder.AppendLine();

            reportBuilder.AppendLine("## 🔒 Security Domains Analysis");
            foreach (var domain in assessment.SecurityDomains.OrderByDescending(d => d.Value.Findings.Count))
            {
                var emoji = domain.Value.SecurityScore >= 90 ? "✅" :
                           domain.Value.SecurityScore >= 70 ? "⚠️" : "❌";
                
                reportBuilder.AppendLine($"### {emoji} {domain.Key}");
                reportBuilder.AppendLine($"- **Score:** {domain.Value.SecurityScore:F1}%");
                reportBuilder.AppendLine($"- **Status:** {domain.Value.Status}");
                reportBuilder.AppendLine($"- **Findings:** {domain.Value.Findings.Count}");
                
                if (domain.Value.Findings.Any())
                {
                    var criticalCount = domain.Value.Findings.Count(f => f.Severity == SecurityFindingSeverity.Critical);
                    var highCount = domain.Value.Findings.Count(f => f.Severity == SecurityFindingSeverity.High);
                    
                    if (criticalCount > 0 || highCount > 0)
                    {
                        reportBuilder.AppendLine($"  - 🔴 Critical: {criticalCount} | 🟠 High: {highCount}");
                    }
                }
                reportBuilder.AppendLine();
            }

            reportBuilder.AppendLine("## 📈 Findings Summary");
            reportBuilder.AppendLine($"| Severity | Count | Auto-Fix Available |");
            reportBuilder.AppendLine($"|----------|-------|-------------------|");
            reportBuilder.AppendLine($"| 🔴 Critical | {assessment.CriticalFindings} | {assessment.AllFindings.Count(f => f.Severity == SecurityFindingSeverity.Critical && f.IsAutoRemediable)} |");
            reportBuilder.AppendLine($"| 🟠 High | {assessment.HighFindings} | {assessment.AllFindings.Count(f => f.Severity == SecurityFindingSeverity.High && f.IsAutoRemediable)} |");
            reportBuilder.AppendLine($"| 🟡 Medium | {assessment.MediumFindings} | {assessment.AllFindings.Count(f => f.Severity == SecurityFindingSeverity.Medium && f.IsAutoRemediable)} |");
            reportBuilder.AppendLine($"| 🟢 Low | {assessment.LowFindings} | {assessment.AllFindings.Count(f => f.Severity == SecurityFindingSeverity.Low && f.IsAutoRemediable)} |");
            reportBuilder.AppendLine();

            if (assessment.AllFindings.Any(f => f.Severity >= SecurityFindingSeverity.High))
            {
                reportBuilder.AppendLine("## 🚨 Critical & High Priority Findings");
                var priorityFindings = assessment.AllFindings
                    .Where(f => f.Severity >= SecurityFindingSeverity.High)
                    .OrderByDescending(f => f.Severity)
                    .Take(10);

                foreach (var finding in priorityFindings)
                {
                    var severityEmoji = finding.Severity == SecurityFindingSeverity.Critical ? "🔴" : "🟠";
                    var autoFixBadge = finding.IsAutoRemediable ? " ✨ *Auto-Fix*" : "";
                    
                    reportBuilder.AppendLine($"### {severityEmoji} {finding.Title}{autoFixBadge}");
                    reportBuilder.AppendLine($"**Category:** {finding.Category}");
                    if (!string.IsNullOrEmpty(finding.FilePath))
                    {
                        reportBuilder.AppendLine($"**Location:** `{finding.FilePath}:{finding.LineNumber}`");
                    }
                    reportBuilder.AppendLine($"**Description:** {finding.Description}");
                    reportBuilder.AppendLine($"**Recommendation:** {finding.Recommendation}");
                    reportBuilder.AppendLine();
                }
            }

            reportBuilder.AppendLine("## 🎯 Risk Assessment");
            reportBuilder.AppendLine($"**Risk Level:** {assessment.RiskProfile.RiskLevel}");
            reportBuilder.AppendLine($"**Risk Score:** {assessment.RiskProfile.RiskScore:F1}/10");
            reportBuilder.AppendLine($"**Risk Trend:** {assessment.RiskProfile.RiskTrend}");
            
            if (assessment.RiskProfile.TopRisks.Any())
            {
                reportBuilder.AppendLine($"**Top Risk Categories:** {string.Join(", ", assessment.RiskProfile.TopRisks.Take(3))}");
            }
            reportBuilder.AppendLine();

            var autoRemediableCount = assessment.AllFindings.Count(f => f.IsAutoRemediable);
            
            reportBuilder.AppendLine("## 🔧 Recommended Next Steps");
            if (autoRemediableCount > 0)
            {
                reportBuilder.AppendLine($"1. **Auto-remediate {autoRemediableCount} findings** - Use `execute automated remediation` command");
            }
            
            reportBuilder.AppendLine("2. **Deep dive analysis** - Run domain-specific scans:");
            reportBuilder.AppendLine("   - `scan dependency vulnerabilities` - Detailed dependency analysis");
            reportBuilder.AppendLine("   - `detect exposed secrets` - Advanced secret scanning");
            reportBuilder.AppendLine("   - `scan infrastructure as code` - IaC security review");
            reportBuilder.AppendLine("   - `perform static security analysis` - SAST analysis");
            reportBuilder.AppendLine();
            
            reportBuilder.AppendLine("3. **Generate compliance evidence** - Use `collect security evidence`");
            reportBuilder.AppendLine("4. **Monitor continuously** - Set up automated scanning in CI/CD");
            reportBuilder.AppendLine();

            var status = assessment.OverallSecurityScore >= 90 ? "✅ Excellent Security Posture" :
                        assessment.OverallSecurityScore >= 80 ? "⚠️ Good, Minor Issues" :
                        assessment.OverallSecurityScore >= 70 ? "⚠️ Needs Attention" :
                        "❌ Significant Security Risks";

            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine($"**Overall Status:** {status}");

            return reportBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating security report");
            return $"# ❌ Report Generation Failed\n\n**Error:** {ex.Message}";
        }
    }

    private string GenerateDependencyReport(DependencySecurityAssessment assessment)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 📦 Dependency Security Report");
        reportBuilder.AppendLine($"**Project:** `{assessment.ProjectPath}`");
        reportBuilder.AppendLine($"**Scan Time:** {assessment.ScanTime:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(assessment.SecurityScore)} **{assessment.SecurityScore:F1}%**");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📊 Overview");
        reportBuilder.AppendLine($"- **Total Dependencies:** {assessment.TotalDependencies}");
        reportBuilder.AppendLine($"- **Vulnerable Dependencies:** {assessment.VulnerableDependencies}");
        reportBuilder.AppendLine($"- **Package Managers:** {string.Join(", ", assessment.PackageManagers)}");
        reportBuilder.AppendLine();

        if (assessment.Vulnerabilities.Any())
        {
            reportBuilder.AppendLine("## 🚨 Vulnerabilities Found");
            foreach (var vuln in assessment.Vulnerabilities.Take(10))
            {
                var emoji = vuln.Severity == SecurityFindingSeverity.Critical ? "🔴" :
                           vuln.Severity == SecurityFindingSeverity.High ? "🟠" :
                           vuln.Severity == SecurityFindingSeverity.Medium ? "🟡" : "🟢";
                
                reportBuilder.AppendLine($"### {emoji} {vuln.PackageName} - {vuln.VulnerabilityId}");
                reportBuilder.AppendLine($"- **Current Version:** {vuln.CurrentVersion}");
                reportBuilder.AppendLine($"- **Fixed Version:** {vuln.FixedVersion}");
                reportBuilder.AppendLine($"- **Description:** {vuln.Description}");
                reportBuilder.AppendLine($"- **Auto-Fix Available:** {(vuln.IsAutoRemediable ? "✅ Yes" : "❌ No")}");
                reportBuilder.AppendLine();
            }
        }

        return reportBuilder.ToString();
    }

    private string GenerateSecretDetectionReport(SecretDetectionResult result)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 🔐 Secret Detection Report");
        reportBuilder.AppendLine($"**Workspace:** `{result.WorkspacePath}`");
        reportBuilder.AppendLine($"**Scan Time:** {result.ScanTime:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(result.SecurityScore)} **{result.SecurityScore:F1}%**");
        reportBuilder.AppendLine();

        if (result.TotalSecretsFound > 0)
        {
            reportBuilder.AppendLine($"## 🚨 {result.TotalSecretsFound} Secrets Detected");
            
            foreach (var secret in result.Secrets)
            {
                var emoji = secret.Severity == SecurityFindingSeverity.Critical ? "🔴" : "🟠";
                reportBuilder.AppendLine($"### {emoji} {secret.Type}");
                reportBuilder.AppendLine($"- **Location:** `{secret.FilePath}:{secret.LineNumber}`");
                reportBuilder.AppendLine($"- **First Detected:** {secret.FirstDetected:yyyy-MM-dd}");
                reportBuilder.AppendLine($"- **Remediation:** {secret.RemediationAdvice}");
                reportBuilder.AppendLine();
            }
        }
        else
        {
            reportBuilder.AppendLine("## ✅ No Secrets Detected");
            reportBuilder.AppendLine("Great job! No exposed secrets were found in your codebase.");
        }

        return reportBuilder.ToString();
    }

    private string GenerateIacSecurityReport(IacSecurityAssessment assessment)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 🏗️ Infrastructure as Code Security Report");
        reportBuilder.AppendLine($"**Project:** `{assessment.ProjectPath}`");
        reportBuilder.AppendLine($"**Scan Time:** {assessment.ScanTime:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(assessment.SecurityScore)} **{assessment.SecurityScore:F1}%**");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📊 Overview");
        reportBuilder.AppendLine($"- **Total Templates:** {assessment.TotalTemplates}");
        reportBuilder.AppendLine($"- **Templates with Issues:** {assessment.TemplatesWithIssues}");
        reportBuilder.AppendLine($"- **Template Types:** {string.Join(", ", assessment.TemplateTypes)}");
        reportBuilder.AppendLine();

        if (assessment.Findings.Any())
        {
            reportBuilder.AppendLine("## 🚨 Security Findings");
            foreach (var finding in assessment.Findings.Take(10))
            {
                var emoji = finding.Severity == SecurityFindingSeverity.Critical ? "🔴" :
                           finding.Severity == SecurityFindingSeverity.High ? "🟠" :
                           finding.Severity == SecurityFindingSeverity.Medium ? "🟡" : "🟢";
                
                reportBuilder.AppendLine($"### {emoji} {finding.RuleName}");
                reportBuilder.AppendLine($"- **Template:** `{finding.TemplateFile}`");
                reportBuilder.AppendLine($"- **Resource:** {finding.ResourceType} - {finding.ResourceName}");
                reportBuilder.AppendLine($"- **Description:** {finding.Description}");
                reportBuilder.AppendLine($"- **Remediation:** {finding.RemediationAdvice}");
                reportBuilder.AppendLine();
            }
        }

        return reportBuilder.ToString();
    }

    private string GenerateSastReport(SastAnalysisResult result)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 🔎 Static Application Security Testing Report");
        reportBuilder.AppendLine($"**Workspace:** `{result.WorkspacePath}`");
        reportBuilder.AppendLine($"**Analysis Time:** {result.AnalysisTime:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(result.SecurityScore)} **{result.SecurityScore:F1}%**");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📊 Overview");
        reportBuilder.AppendLine($"- **Languages Analyzed:** {string.Join(", ", result.Languages)}");
        reportBuilder.AppendLine($"- **Total Issues:** {result.TotalIssues}");
        reportBuilder.AppendLine();

        if (result.FindingsByOwaspTop10.Any())
        {
            reportBuilder.AppendLine("## 🎯 OWASP Top 10 Findings");
            foreach (var category in result.FindingsByOwaspTop10)
            {
                reportBuilder.AppendLine($"- **{category.Key}:** {category.Value} finding(s)");
            }
            reportBuilder.AppendLine();
        }

        if (result.Findings.Any())
        {
            reportBuilder.AppendLine("## 🚨 Security Issues");
            foreach (var finding in result.Findings.Take(10))
            {
                var emoji = finding.Severity == SecurityFindingSeverity.Critical ? "🔴" :
                           finding.Severity == SecurityFindingSeverity.High ? "🟠" :
                           finding.Severity == SecurityFindingSeverity.Medium ? "🟡" : "🟢";
                
                reportBuilder.AppendLine($"### {emoji} {finding.RuleName}");
                reportBuilder.AppendLine($"- **Location:** `{finding.FilePath}:{finding.LineNumber}`");
                reportBuilder.AppendLine($"- **Category:** {finding.Category}");
                reportBuilder.AppendLine($"- **Description:** {finding.Description}");
                reportBuilder.AppendLine($"- **Remediation:** {finding.RemediationAdvice}");
                reportBuilder.AppendLine();
            }
        }

        return reportBuilder.ToString();
    }

    private string GenerateContainerSecurityReport(ContainerSecurityAssessment assessment)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 🐳 Container Security Report");
        reportBuilder.AppendLine($"**Project:** `{assessment.ProjectPath}`");
        reportBuilder.AppendLine($"**Scan Time:** {assessment.ScanTime:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(assessment.SecurityScore)} **{assessment.SecurityScore:F1}%**");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📊 Overview");
        reportBuilder.AppendLine($"- **Total Containers:** {assessment.TotalContainers}");
        reportBuilder.AppendLine($"- **Containers with Issues:** {assessment.ContainersWithIssues}");
        reportBuilder.AppendLine();

        foreach (var container in assessment.ContainerResults)
        {
            reportBuilder.AppendLine($"## 📦 {container.ContainerName}");
            reportBuilder.AppendLine($"- **Image:** {container.ImageName}:{container.Tag}");
            reportBuilder.AppendLine($"- **Security Score:** {container.SecurityScore:F1}%");
            reportBuilder.AppendLine($"- **Vulnerabilities:** {container.Vulnerabilities.Count}");
            reportBuilder.AppendLine($"- **Config Issues:** {container.ConfigurationIssues.Count}");
            reportBuilder.AppendLine();
        }

        return reportBuilder.ToString();
    }

    private string GenerateGitHubSecurityReport(GitHubSecurityAssessment assessment)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 🐙 GitHub Security Assessment Report");
        reportBuilder.AppendLine($"**Repository:** {assessment.RepositoryUrl}");
        reportBuilder.AppendLine($"**Scan Time:** {assessment.ScanTime:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Security Score:** {GenerateScoreBar(assessment.SecurityScore)} **{assessment.SecurityScore:F1}%**");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 🔧 Security Features");
        reportBuilder.AppendLine($"- **Dependabot:** {(assessment.SecurityFeatures.DependabotEnabled ? "✅" : "❌")} Enabled");
        reportBuilder.AppendLine($"- **Code Scanning:** {(assessment.SecurityFeatures.CodeScanningEnabled ? "✅" : "❌")} Enabled");
        reportBuilder.AppendLine($"- **Secret Scanning:** {(assessment.SecurityFeatures.SecretScanningEnabled ? "✅" : "❌")} Enabled");
        reportBuilder.AppendLine($"- **Advanced Security:** {(assessment.SecurityFeatures.AdvancedSecurityEnabled ? "✅" : "❌")} Enabled");
        reportBuilder.AppendLine();

        if (assessment.SecurityAlerts.Any())
        {
            reportBuilder.AppendLine("## 🚨 Active Security Alerts");
            foreach (var alert in assessment.SecurityAlerts.Take(5))
            {
                var emoji = alert.Severity == SecurityFindingSeverity.Critical ? "🔴" :
                           alert.Severity == SecurityFindingSeverity.High ? "🟠" : "🟡";
                
                reportBuilder.AppendLine($"### {emoji} {alert.Type}");
                reportBuilder.AppendLine($"- **Description:** {alert.Description}");
                reportBuilder.AppendLine($"- **State:** {alert.State}");
                reportBuilder.AppendLine($"- **Created:** {alert.CreatedAt:yyyy-MM-dd}");
                reportBuilder.AppendLine();
            }
        }

        return reportBuilder.ToString();
    }

    private string GenerateEvidenceReport(SecurityEvidencePackage package)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 📄 Security Evidence Package");
        reportBuilder.AppendLine($"**Project:** `{package.ProjectPath}`");
        reportBuilder.AppendLine($"**Generated:** {package.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Package ID:** {package.PackageId}");
        reportBuilder.AppendLine($"**Frameworks:** {string.Join(", ", package.ComplianceFrameworks)}");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📊 Executive Summary");
        reportBuilder.AppendLine(package.ExecutiveSummary);
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📋 Evidence Collected");
        foreach (var evidence in package.Evidence)
        {
            reportBuilder.AppendLine($"### {evidence.Value.Type}");
            reportBuilder.AppendLine($"- **Title:** {evidence.Value.Title}");
            reportBuilder.AppendLine($"- **Description:** {evidence.Value.Description}");
            reportBuilder.AppendLine($"- **Collected:** {evidence.Value.CollectedAt:yyyy-MM-dd}");
            reportBuilder.AppendLine();
        }

        return reportBuilder.ToString();
    }

    private string GenerateRemediationReport(RemediationExecutionResult result)
    {
        var reportBuilder = new System.Text.StringBuilder();
        reportBuilder.AppendLine("# 🔧 Automated Remediation Report");
        reportBuilder.AppendLine($"**Project:** `{result.ProjectPath}`");
        reportBuilder.AppendLine($"**Executed:** {result.ExecutedAt:yyyy-MM-dd HH:mm:ss UTC}");
        reportBuilder.AppendLine($"**Mode:** {result.ExecutionMode}");
        reportBuilder.AppendLine($"**Success Rate:** {(result.TotalRemediations > 0 ? (result.SuccessfulRemediations * 100.0 / result.TotalRemediations):0):F1}%");
        reportBuilder.AppendLine();

        reportBuilder.AppendLine("## 📊 Summary");
        reportBuilder.AppendLine($"- **Total Remediations:** {result.TotalRemediations}");
        reportBuilder.AppendLine($"- **Successful:** {result.SuccessfulRemediations}");
        reportBuilder.AppendLine($"- **Failed:** {result.FailedRemediations}");
        
        if (!string.IsNullOrEmpty(result.PullRequestUrl))
        {
            reportBuilder.AppendLine($"- **Pull Request:** {result.PullRequestUrl}");
        }
        reportBuilder.AppendLine();

        if (result.Results.Any())
        {
            reportBuilder.AppendLine("## 🔧 Remediation Results");
            foreach (var remediation in result.Results)
            {
                var emoji = remediation.Success ? "✅" : "❌";
                reportBuilder.AppendLine($"### {emoji} {remediation.RemediationType}");
                reportBuilder.AppendLine($"- **Finding:** {remediation.FindingId}");
                reportBuilder.AppendLine($"- **Message:** {remediation.Message}");
                if (remediation.ChangedFiles.Any())
                {
                    reportBuilder.AppendLine($"- **Changed Files:** {string.Join(", ", remediation.ChangedFiles)}");
                }
                reportBuilder.AppendLine();
            }
        }

        return reportBuilder.ToString();
    }

    private string GenerateScoreBar(double score)
    {
        var filledBars = (int)(score / 5); // Each bar represents 5%
        var emptyBars = 20 - filledBars;
        
        var color = score >= 90 ? "🟢" :
                   score >= 80 ? "🟡" :
                   score >= 70 ? "🟠" : "🔴";
        
        return $"{color} {new string('█', filledBars)}{new string('░', emptyBars)}";
    }

    #endregion

    #region Repository Scanning

    /// <summary>
    /// Scan a remote repository from GitHub, Azure DevOps, or GitHub Enterprise for security vulnerabilities.
    /// Automatically clones the repository, performs comprehensive security analysis, and cleans up.
    /// </summary>
    [KernelFunction, Description("Scan a remote Git repository (GitHub, Azure DevOps, GitHub Enterprise) for security vulnerabilities and compliance issues")]
    public async Task<string> ScanRemoteRepositoryAsync(
        [Description("Repository URL (e.g., https://github.com/owner/repo, https://dev.azure.com/org/project/_git/repo)")] string repositoryUrl,
        [Description("Branch to scan (optional, defaults to main/master)")] string? branch = null,
        [Description("File patterns to include in scan")] string? filePatterns = null,
        [Description("Compliance frameworks to check against")] string? complianceFrameworks = null,
        [Description("Scan depth: surface, deep, or comprehensive")] string scanDepth = "deep")
    {
        _logger.LogInformation("Starting remote repository scan for: {RepositoryUrl}", repositoryUrl);

        try
        {
            var progress = new Progress<SecurityScanProgress>(p =>
            {
                _logger.LogInformation("Repository scan progress: {Phase} ({Completed}/{Total}) - {Message}",
                    p.CurrentPhase, p.CompletedPhases, p.TotalPhases, p.Message);
            });

            var assessment = await _codeScanningEngine.ScanRepositoryAsync(
                repositoryUrl,
                branch,
                filePatterns,
                complianceFrameworks,
                scanDepth,
                progress);

            return GenerateFormattedSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote repository scan failed for: {RepositoryUrl}", repositoryUrl);
            return CreateErrorResponse("scan remote repository", ex);
        }
    }

    /// <summary>
    /// Scan a GitHub repository by owner and repository name for security vulnerabilities.
    /// Performs comprehensive SAST, dependency scanning, secret detection, and compliance checking.
    /// </summary>
    [KernelFunction, Description("Scan a GitHub repository by owner and name for security vulnerabilities, exposed secrets, and compliance violations")]
    public async Task<string> ScanGitHubRepositoryAsync(
        [Description("GitHub repository owner (username or organization)")] string owner,
        [Description("GitHub repository name")] string repository,
        [Description("Branch to scan (optional, defaults to main)")] string? branch = null,
        [Description("Compliance frameworks to check against")] string? complianceFrameworks = null)
    {
        _logger.LogInformation("Starting GitHub repository scan for: {Owner}/{Repository}", owner, repository);

        try
        {
            var assessment = await _codeScanningEngine.ScanGitHubRepositoryAsync(
                owner,
                repository,
                branch,
                complianceFrameworks);

            return GenerateFormattedSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub repository scan failed for: {Owner}/{Repository}", owner, repository);
            return CreateErrorResponse("scan GitHub repository", ex);
        }
    }

    /// <summary>
    /// Scan an Azure DevOps repository for security vulnerabilities and compliance issues.
    /// Analyzes code, dependencies, secrets, IaC templates, and container configurations.
    /// </summary>
    [KernelFunction, Description("Scan an Azure DevOps repository for security vulnerabilities, compliance violations, and configuration issues")]
    public async Task<string> ScanAzureDevOpsRepositoryAsync(
        [Description("Azure DevOps organization name")] string organization,
        [Description("Azure DevOps project name")] string project,
        [Description("Azure DevOps repository name")] string repository,
        [Description("Branch to scan (optional, defaults to main)")] string? branch = null,
        [Description("Compliance frameworks to check against")] string? complianceFrameworks = null)
    {
        _logger.LogInformation("Starting Azure DevOps repository scan for: {Organization}/{Project}/{Repository}",
            organization, project, repository);

        try
        {
            var assessment = await _codeScanningEngine.ScanAzureDevOpsRepositoryAsync(
                organization,
                project,
                repository,
                branch,
                complianceFrameworks);

            return GenerateFormattedSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure DevOps repository scan failed for: {Organization}/{Project}/{Repository}",
                organization, project, repository);
            return CreateErrorResponse("scan Azure DevOps repository", ex);
        }
    }

    /// <summary>
    /// Scan a GitHub Enterprise repository for security vulnerabilities and compliance issues.
    /// Supports custom GitHub Enterprise installations with comprehensive security analysis.
    /// </summary>
    [KernelFunction, Description("Scan a GitHub Enterprise repository for security vulnerabilities, exposed secrets, and compliance violations")]
    public async Task<string> ScanGitHubEnterpriseRepositoryAsync(
        [Description("GitHub Enterprise URL (e.g., https://github.company.com)")] string enterpriseUrl,
        [Description("Repository owner (username or organization)")] string owner,
        [Description("Repository name")] string repository,
        [Description("Branch to scan (optional, defaults to main)")] string? branch = null,
        [Description("Compliance frameworks to check against")] string? complianceFrameworks = null)
    {
        _logger.LogInformation("Starting GitHub Enterprise repository scan for: {EnterpriseUrl}/{Owner}/{Repository}",
            enterpriseUrl, owner, repository);

        try
        {
            var assessment = await _codeScanningEngine.ScanGitHubEnterpriseRepositoryAsync(
                enterpriseUrl,
                owner,
                repository,
                branch,
                complianceFrameworks);

            return GenerateFormattedSecurityReport(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub Enterprise repository scan failed for: {EnterpriseUrl}/{Owner}/{Repository}",
                enterpriseUrl, owner, repository);
            return CreateErrorResponse("scan GitHub Enterprise repository", ex);
        }
    }

    #endregion
}
