# Repository Scanning Guide

This guide explains how to scan remote repositories from GitHub, Azure DevOps (ADO), and GitHub Enterprise (GHE) for security vulnerabilities and compliance issues.

## Overview

The Code Scanning Engine now supports scanning remote repositories without requiring local clones. It automatically:

1. **Clones** the repository to a temporary location
2. **Scans** for security vulnerabilities across multiple domains
3. **Analyzes** compliance against frameworks like NIST 800-53, STIG, SOC2
4. **Cleans up** temporary files automatically

## Supported Repository Types

### 1. GitHub Repositories

Scan public or private GitHub repositories:

```text
Can you scan the GitHub repository microsoft/azure-pipelines-tasks for security vulnerabilities?
```

**Chat Examples:**
- "Scan the GitHub repo owner/repository-name"
- "Analyze security for github.com/owner/repo on the develop branch"
- "Run compliance scan on GitHub repo owner/repo against NIST-800-53"

### 2. Azure DevOps Repositories

Scan Azure DevOps repositories from dev.azure.com:

```text
Can you scan the Azure DevOps repository MyOrg/MyProject/MyRepo for compliance issues?
```

**Chat Examples:**
- "Scan the ADO repository organization/project/repo"
- "Analyze security for dev.azure.com/myorg/myproject/_git/myrepo"
- "Check compliance for Azure DevOps repo MyOrg/MyProject/MyRepo"

### 3. GitHub Enterprise Repositories

Scan repositories from custom GitHub Enterprise installations:

```text
Can you scan the GitHub Enterprise repository at https://github.company.com/team/repo?
```

**Chat Examples:**
- "Scan GHE repository at https://github.company.com/owner/repo"
- "Analyze security for GitHub Enterprise repo on branch feature-branch"
- "Run STIG compliance scan on https://github.internal.com/team/project"

### 4. Generic Git Repositories

Scan any Git repository by URL:

```text
Scan the repository at https://gitlab.com/group/project for security issues
```

## Available Scanning Methods

### Method 1: Scan by URL

Use the full repository URL for any Git provider:

```csharp
var assessment = await codeScanningEngine.ScanRepositoryAsync(
    repositoryUrl: "https://github.com/owner/repository",
    branch: "main",
    filePatterns: "*.cs,*.ts,*.py",
    complianceFrameworks: "NIST-800-53,STIG",
    scanDepth: "deep"
);
```

**Chat Command:**
```text
Scan the repository https://github.com/owner/repo for NIST compliance
```

### Method 2: Scan GitHub by Owner/Repo

Simplified method for GitHub repositories:

```csharp
var assessment = await codeScanningEngine.ScanGitHubRepositoryAsync(
    owner: "microsoft",
    repository: "vscode",
    branch: "main",
    complianceFrameworks: "NIST-800-53"
);
```

**Chat Command:**
```text
Scan GitHub repository microsoft/vscode for security vulnerabilities
```

### Method 3: Scan Azure DevOps Repository

Specific method for ADO repositories:

```csharp
var assessment = await codeScanningEngine.ScanAzureDevOpsRepositoryAsync(
    organization: "MyOrganization",
    project: "MyProject",
    repository: "MyRepository",
    branch: "develop",
    complianceFrameworks: "SOC2"
);
```

**Chat Command:**
```text
Scan Azure DevOps repository MyOrg/MyProject/MyRepo for SOC2 compliance
```

### Method 4: Scan GitHub Enterprise Repository

For custom GitHub Enterprise installations:

```csharp
var assessment = await codeScanningEngine.ScanGitHubEnterpriseRepositoryAsync(
    enterpriseUrl: "https://github.company.com",
    owner: "engineering",
    repository: "platform",
    branch: "production",
    complianceFrameworks: "NIST-800-53,STIG"
);
```

**Chat Command:**
```text
Scan GitHub Enterprise repository at https://github.company.com/team/repo for STIG compliance
```

## Security Analysis Features

All repository scans include:

### 1. **SAST (Static Application Security Testing)**
- SQL injection detection
- XSS vulnerability detection
- Command injection detection
- Path traversal detection
- Insecure deserialization
- Weak cryptography detection
- Hardcoded credentials
- And more...

### 2. **Dependency Scanning**
- Known CVE vulnerabilities
- Outdated packages
- License compliance
- Transitive dependencies

### 3. **Secret Detection**
- API keys and tokens
- Passwords and credentials
- SSH keys and certificates
- AWS/Azure access keys
- Database connection strings

### 4. **Infrastructure as Code (IaC) Security**
- ARM template analysis
- Terraform configuration
- Bicep template scanning
- CloudFormation templates
- Kubernetes manifests
- Docker configurations

### 5. **Container Security**
- Dockerfile analysis
- Base image vulnerabilities
- Security misconfigurations
- Runtime security settings

### 6. **Compliance Frameworks**
- NIST 800-53 controls
- STIG requirements
- SOC2 compliance
- CIS benchmarks
- OWASP Top 10

## Chat Examples

### Basic Repository Scan

```text
User: Can you scan the GitHub repository kubernetes/kubernetes for security issues?

Copilot: I'll scan the Kubernetes repository for security vulnerabilities...
[Performs comprehensive security scan]

Here are the results:
- Overall Security Score: 87.5%
- Critical Findings: 2
- High Priority: 8
- Medium Priority: 15
...
```

### Specific Branch Scan

```text
User: Scan the develop branch of github.com/owner/repo for NIST compliance

Copilot: I'll scan the develop branch against NIST 800-53 controls...
[Scans specific branch]

NIST 800-53 Compliance Results:
- Access Control (AC): 92% compliant
- System Integrity (SI): 88% compliant
...
```

### Azure DevOps Scan

```text
User: Check the Azure DevOps repository MyOrg/MyProject/WebApp for security vulnerabilities

Copilot: I'll analyze the WebApp repository in Azure DevOps...
[Clones and scans ADO repository]

Security Assessment:
- SAST Analysis: 15 issues found
- Dependencies: 3 vulnerable packages
- Secrets: 1 exposed API key detected
...
```

### GitHub Enterprise Scan

```text
User: Scan https://github.company.com/platform/core-services for STIG compliance

Copilot: I'll scan the core-services repository from your GitHub Enterprise instance...
[Analyzes against STIG requirements]

STIG Compliance Report:
- V-230221 (Storage Encryption): PASS
- V-230225 (Network Security): FAIL
...
```

## Advanced Options

### File Pattern Filtering

Scan specific file types:

```text
Scan the repository for security issues in TypeScript and Python files only
```

### Compliance Framework Selection

Target specific compliance frameworks:

```text
Scan the repository for NIST-800-53 and SOC2 compliance violations
```

### Scan Depth Control

Control analysis depth:

- **surface**: Quick scan of obvious issues
- **deep**: Comprehensive analysis (default)
- **comprehensive**: Deep analysis with all security checks

```text
Run a comprehensive security scan on the repository
```

## Authentication

### GitHub Repositories

For private repositories, ensure GitHub authentication is configured:

1. Set `GITHUB_TOKEN` environment variable
2. Configure GitHub PAT in application settings
3. Use OAuth authentication

### Azure DevOps Repositories

For private ADO repositories:

1. Set `AZURE_DEVOPS_PAT` environment variable
2. Configure Azure DevOps credentials
3. Use Azure AD authentication

### GitHub Enterprise

For GHE repositories:

1. Configure enterprise URL in settings
2. Set `GHE_TOKEN` environment variable
3. Use enterprise authentication

## Example Output

```markdown
# Security Assessment Report

**Repository:** https://github.com/owner/repository
**Branch:** main
**Provider:** GitHub
**Scan Date:** 2025-11-21 14:30:00 UTC

## Overall Security Score: 84.7% 🟡

## Summary
- **Total Findings:** 47
- **Critical:** 2 🔴
- **High:** 8 🟠
- **Medium:** 18 🟡
- **Low:** 19 🟢

## Security Domains

### SAST Analysis (Score: 82.3%)
- SQL Injection: 3 findings
- XSS Vulnerabilities: 2 findings
- Weak Cryptography: 1 finding

### Dependencies (Score: 85.2%)
- Vulnerable Packages: 8
- Outdated Dependencies: 15

### Secret Detection (Score: 75.0%)
- Exposed API Keys: 2
- Hardcoded Passwords: 1

### Infrastructure as Code (Score: 88.5%)
- Security Misconfigurations: 4
- STIG Violations: 2

## Top Recommendations
1. Update vulnerable dependencies immediately
2. Remove exposed secrets and rotate credentials
3. Fix critical SQL injection vulnerabilities
4. Enable storage encryption at rest
5. Configure minimum TLS version to 1.2
```

## Best Practices

1. **Regular Scans**: Schedule periodic scans for continuous monitoring
2. **Branch Protection**: Scan pull requests before merging
3. **Compliance Tracking**: Track compliance scores over time
4. **Automated Remediation**: Enable auto-remediation for common issues
5. **Evidence Collection**: Store scan results for audit trails

## Troubleshooting

### Clone Failures

If repository cloning fails:

1. Check network connectivity
2. Verify authentication credentials
3. Ensure Git is installed
4. Check repository permissions

### Authentication Errors

For authentication issues:

1. Verify token/PAT is valid
2. Check required scopes/permissions
3. Confirm repository access rights
4. Test credentials manually

### Scan Timeouts

For large repositories:

1. Use shallow clones (default)
2. Reduce scan depth to "surface"
3. Filter by file patterns
4. Exclude build/dependency directories

## API Reference

See the [ICodeScanningEngine interface](../../src/Platform.Engineering.Copilot.Core/Interfaces/Compliance/ICodeScanningEngine.cs) for complete API documentation.

## Related Documentation

- [Code Scanning Guide](./CODE-SCANNING-GUIDE.md)
- [Compliance Framework Guide](./COMPLIANCE-FRAMEWORK-GUIDE.md)
- [Secret Detection Guide](./SECRET-DETECTION-GUIDE.md)
- [STIG Compliance Guide](./STIG-COMPLIANCE-GUIDE.md)
