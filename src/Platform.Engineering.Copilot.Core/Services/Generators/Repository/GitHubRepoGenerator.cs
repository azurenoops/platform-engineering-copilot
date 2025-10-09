using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Repository;

/// <summary>
/// Generates GitHub repository files including .gitignore, README.md, LICENSE, and GitHub-specific templates
/// </summary>
public class GitHubRepoGenerator
{
    public Dictionary<string, string> GenerateRepositoryFiles(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var app = request.Application ?? new ApplicationSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        // Core repository files
        files[".gitignore"] = GenerateGitIgnore(app.Language);
        files["README.md"] = GenerateReadme(request);
        files["LICENSE"] = GenerateLicense("MIT");
        files["CONTRIBUTING.md"] = GenerateContributing(request);
        
        // GitHub-specific files
        files[".github/CODEOWNERS"] = GenerateCodeOwners(request);
        files[".github/PULL_REQUEST_TEMPLATE.md"] = GeneratePullRequestTemplate();
        files[".github/ISSUE_TEMPLATE/bug_report.md"] = GenerateBugReportTemplate();
        files[".github/ISSUE_TEMPLATE/feature_request.md"] = GenerateFeatureRequestTemplate();
        files[".github/dependabot.yml"] = GenerateDependabot(app.Language);
        
        return files;
    }
    
    private string GenerateGitIgnore(ProgrammingLanguage language)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Platform-MCP-Supervisor Generated .gitignore");
        sb.AppendLine();
        
        // Common ignores
        sb.AppendLine("# Environment variables");
        sb.AppendLine(".env");
        sb.AppendLine(".env.local");
        sb.AppendLine(".env.*.local");
        sb.AppendLine();
        sb.AppendLine("# IDE");
        sb.AppendLine(".vscode/");
        sb.AppendLine(".idea/");
        sb.AppendLine("*.swp");
        sb.AppendLine("*.swo");
        sb.AppendLine("*~");
        sb.AppendLine(".DS_Store");
        sb.AppendLine();
        sb.AppendLine("# Logs");
        sb.AppendLine("logs/");
        sb.AppendLine("*.log");
        sb.AppendLine("npm-debug.log*");
        sb.AppendLine("yarn-debug.log*");
        sb.AppendLine("yarn-error.log*");
        sb.AppendLine();
        
        // Language-specific ignores
        switch (language)
        {
            case ProgrammingLanguage.NodeJS:
                sb.AppendLine("# Node.js");
                sb.AppendLine("node_modules/");
                sb.AppendLine("npm-debug.log");
                sb.AppendLine("yarn-error.log");
                sb.AppendLine(".npm");
                sb.AppendLine(".yarn");
                sb.AppendLine("dist/");
                sb.AppendLine("build/");
                sb.AppendLine("coverage/");
                sb.AppendLine(".nyc_output/");
                sb.AppendLine(".next/");
                sb.AppendLine("out/");
                sb.AppendLine(".nuxt/");
                sb.AppendLine(".cache/");
                sb.AppendLine(".parcel-cache/");
                break;
                
            case ProgrammingLanguage.Python:
                sb.AppendLine("# Python");
                sb.AppendLine("__pycache__/");
                sb.AppendLine("*.py[cod]");
                sb.AppendLine("*$py.class");
                sb.AppendLine("*.so");
                sb.AppendLine(".Python");
                sb.AppendLine("build/");
                sb.AppendLine("develop-eggs/");
                sb.AppendLine("dist/");
                sb.AppendLine("downloads/");
                sb.AppendLine("eggs/");
                sb.AppendLine(".eggs/");
                sb.AppendLine("lib/");
                sb.AppendLine("lib64/");
                sb.AppendLine("parts/");
                sb.AppendLine("sdist/");
                sb.AppendLine("var/");
                sb.AppendLine("wheels/");
                sb.AppendLine("*.egg-info/");
                sb.AppendLine(".installed.cfg");
                sb.AppendLine("*.egg");
                sb.AppendLine("venv/");
                sb.AppendLine("env/");
                sb.AppendLine("ENV/");
                sb.AppendLine(".venv/");
                sb.AppendLine("pip-log.txt");
                sb.AppendLine("pip-delete-this-directory.txt");
                sb.AppendLine(".pytest_cache/");
                sb.AppendLine(".coverage");
                sb.AppendLine("htmlcov/");
                sb.AppendLine(".mypy_cache/");
                sb.AppendLine(".dmypy.json");
                sb.AppendLine("dmypy.json");
                break;
                
            case ProgrammingLanguage.DotNet:
                sb.AppendLine("# .NET");
                sb.AppendLine("bin/");
                sb.AppendLine("obj/");
                sb.AppendLine("*.user");
                sb.AppendLine("*.suo");
                sb.AppendLine("*.userosscache");
                sb.AppendLine("*.sln.docstates");
                sb.AppendLine("[Dd]ebug/");
                sb.AppendLine("[Dd]ebugPublic/");
                sb.AppendLine("[Rr]elease/");
                sb.AppendLine("[Rr]eleases/");
                sb.AppendLine("x64/");
                sb.AppendLine("x86/");
                sb.AppendLine("[Aa][Rr][Mm]/");
                sb.AppendLine("[Aa][Rr][Mm]64/");
                sb.AppendLine("bld/");
                sb.AppendLine("[Bb]in/");
                sb.AppendLine("[Oo]bj/");
                sb.AppendLine("[Ll]og/");
                sb.AppendLine("*.csproj.user");
                sb.AppendLine("*.dbmdl");
                sb.AppendLine("*.jfm");
                sb.AppendLine("*.pfx");
                sb.AppendLine("*.publishsettings");
                sb.AppendLine("project.lock.json");
                sb.AppendLine("project.fragment.lock.json");
                sb.AppendLine("artifacts/");
                break;
                
            case ProgrammingLanguage.Java:
                sb.AppendLine("# Java");
                sb.AppendLine("target/");
                sb.AppendLine("*.class");
                sb.AppendLine("*.jar");
                sb.AppendLine("*.war");
                sb.AppendLine("*.ear");
                sb.AppendLine("*.nar");
                sb.AppendLine(".gradle/");
                sb.AppendLine("build/");
                sb.AppendLine("gradle-app.setting");
                sb.AppendLine(".gradletasknamecache");
                sb.AppendLine(".mvn/");
                sb.AppendLine("mvnw");
                sb.AppendLine("mvnw.cmd");
                sb.AppendLine(".classpath");
                sb.AppendLine(".project");
                sb.AppendLine(".settings/");
                break;
                
            case ProgrammingLanguage.Go:
                sb.AppendLine("# Go");
                sb.AppendLine("*.exe");
                sb.AppendLine("*.exe~");
                sb.AppendLine("*.dll");
                sb.AppendLine("*.so");
                sb.AppendLine("*.dylib");
                sb.AppendLine("*.test");
                sb.AppendLine("*.out");
                sb.AppendLine("vendor/");
                sb.AppendLine("go.work");
                break;
                
            case ProgrammingLanguage.Rust:
                sb.AppendLine("# Rust");
                sb.AppendLine("target/");
                sb.AppendLine("Cargo.lock");
                sb.AppendLine("**/*.rs.bk");
                break;
                
            case ProgrammingLanguage.Ruby:
                sb.AppendLine("# Ruby");
                sb.AppendLine("*.gem");
                sb.AppendLine("*.rbc");
                sb.AppendLine("/.config");
                sb.AppendLine("/coverage/");
                sb.AppendLine("/InstalledFiles");
                sb.AppendLine("/pkg/");
                sb.AppendLine("/spec/reports/");
                sb.AppendLine("/tmp/");
                sb.AppendLine(".bundle/");
                sb.AppendLine("vendor/bundle");
                sb.AppendLine("lib/bundler/man/");
                break;
                
            case ProgrammingLanguage.PHP:
                sb.AppendLine("# PHP");
                sb.AppendLine("vendor/");
                sb.AppendLine("composer.lock");
                sb.AppendLine(".phpunit.result.cache");
                sb.AppendLine("/phpunit.xml");
                break;
        }
        
        sb.AppendLine();
        sb.AppendLine("# Docker");
        sb.AppendLine("docker-compose.override.yml");
        sb.AppendLine();
        sb.AppendLine("# Terraform");
        sb.AppendLine("*.tfstate");
        sb.AppendLine("*.tfstate.*");
        sb.AppendLine(".terraform/");
        sb.AppendLine(".terraform.lock.hcl");
        sb.AppendLine("terraform.tfvars");
        sb.AppendLine();
        sb.AppendLine("# Secrets");
        sb.AppendLine("secrets/");
        sb.AppendLine("*.key");
        sb.AppendLine("*.pem");
        sb.AppendLine("*.p12");
        
        return sb.ToString();
    }
    
    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "my-service";
        var app = request.Application ?? new ApplicationSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine($"# {serviceName}");
        sb.AppendLine();
        sb.AppendLine($"> Generated by Platform-MCP-Supervisor on {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"A {app.Type} service built with {app.Language} ({app.Framework}) and deployed to {infrastructure.Provider} {infrastructure.ComputePlatform}.");
        sb.AppendLine();
        sb.AppendLine("## Tech Stack");
        sb.AppendLine();
        sb.AppendLine($"- **Language**: {app.Language}");
        sb.AppendLine($"- **Framework**: {app.Framework}");
        sb.AppendLine($"- **Cloud Provider**: {infrastructure.Provider}");
        sb.AppendLine($"- **Compute Platform**: {infrastructure.ComputePlatform}");
        sb.AppendLine($"- **Infrastructure as Code**: {infrastructure.Format}");
        sb.AppendLine();
        
        // Prerequisites
        sb.AppendLine("## Prerequisites");
        sb.AppendLine();
        sb.AppendLine(GetLanguagePrerequisites(app.Language));
        sb.AppendLine("- Docker (for containerization)");
        sb.AppendLine($"- {infrastructure.Format} (for infrastructure deployment)");
        sb.AppendLine($"- {GetCloudCLI(infrastructure.Provider)} (for cloud deployment)");
        sb.AppendLine();
        
        // Getting Started
        sb.AppendLine("## Getting Started");
        sb.AppendLine();
        sb.AppendLine("### Local Development");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Clone the repository");
        sb.AppendLine($"git clone <repository-url>");
        sb.AppendLine($"cd {serviceName}");
        sb.AppendLine();
        sb.AppendLine("# Install dependencies");
        sb.AppendLine(GetInstallCommand(app.Language));
        sb.AppendLine();
        sb.AppendLine("# Run locally");
        sb.AppendLine(GetRunCommand(app.Language));
        sb.AppendLine("```");
        sb.AppendLine();
        
        // Docker
        sb.AppendLine("### Docker");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Build image");
        sb.AppendLine($"docker build -t {serviceName}:latest .");
        sb.AppendLine();
        sb.AppendLine("# Run container");
        sb.AppendLine($"docker run -p {app.Port}:{app.Port} {serviceName}:latest");
        sb.AppendLine();
        sb.AppendLine("# Or use docker-compose");
        sb.AppendLine("docker-compose up");
        sb.AppendLine("```");
        sb.AppendLine();
        
        // Testing
        sb.AppendLine("## Testing");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Run tests");
        sb.AppendLine(GetTestCommand(app.Language));
        sb.AppendLine();
        sb.AppendLine("# Run with coverage");
        sb.AppendLine(GetCoverageCommand(app.Language));
        sb.AppendLine("```");
        sb.AppendLine();
        
        // Deployment
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine($"### Deploy to {infrastructure.Provider}");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"# Navigate to infrastructure directory");
        sb.AppendLine("cd infra");
        sb.AppendLine();
        
        if (infrastructure.Format == InfrastructureFormat.Terraform)
        {
            sb.AppendLine("# Initialize Terraform");
            sb.AppendLine("terraform init");
            sb.AppendLine();
            sb.AppendLine("# Plan deployment");
            sb.AppendLine("terraform plan");
            sb.AppendLine();
            sb.AppendLine("# Apply infrastructure");
            sb.AppendLine("terraform apply");
        }
        else if (infrastructure.Format == InfrastructureFormat.Bicep)
        {
            sb.AppendLine("# Deploy with Azure CLI");
            sb.AppendLine("az deployment sub create \\");
            sb.AppendLine("  --location eastus \\");
            sb.AppendLine("  --template-file main.bicep \\");
            sb.AppendLine("  --parameters parameters.json");
        }
        
        sb.AppendLine("```");
        sb.AppendLine();
        
        // CI/CD
        sb.AppendLine("## CI/CD");
        sb.AppendLine();
        sb.AppendLine("This project includes GitHub Actions workflows for:");
        sb.AppendLine();
        sb.AppendLine("- **CI**: Build, test, and lint on every push/PR");
        sb.AppendLine("- **CD**: Deploy to dev/staging/prod environments");
        sb.AppendLine();
        sb.AppendLine("Workflows are located in `.github/workflows/`.");
        sb.AppendLine();
        
        // Project Structure
        sb.AppendLine("## Project Structure");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(".");
        sb.AppendLine("├── .github/              # GitHub Actions workflows and templates");
        sb.AppendLine("├── infra/                # Infrastructure as Code");
        sb.AppendLine("├── src/                  # Application source code");
        sb.AppendLine("├── tests/                # Test files");
        sb.AppendLine("├── Dockerfile            # Container image definition");
        sb.AppendLine("├── docker-compose.yml    # Local development setup");
        sb.AppendLine("└── README.md             # This file");
        sb.AppendLine("```");
        sb.AppendLine();
        
        // Environment Variables
        if (app.EnvironmentVariables != null && app.EnvironmentVariables.Any())
        {
            sb.AppendLine("## Environment Variables");
            sb.AppendLine();
            sb.AppendLine("| Variable | Description | Default |");
            sb.AppendLine("|----------|-------------|---------|");
            foreach (var env in app.EnvironmentVariables)
            {
                sb.AppendLine($"| `{env.Key}` | | `{env.Value}` |");
            }
            sb.AppendLine();
        }
        
        // Contributing
        sb.AppendLine("## Contributing");
        sb.AppendLine();
        sb.AppendLine("Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.");
        sb.AppendLine();
        
        // License
        sb.AppendLine("## License");
        sb.AppendLine();
        sb.AppendLine("This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.");
        
        return sb.ToString();
    }
    
    private string GenerateLicense(string licenseType)
    {
        if (licenseType == "MIT")
        {
            return @$"MIT License

Copyright (c) {DateTime.UtcNow.Year}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";
        }
        
        return "# License - To be specified";
    }
    
    private string GenerateContributing(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Contributing");
        sb.AppendLine();
        sb.AppendLine("Thank you for considering contributing to this project!");
        sb.AppendLine();
        sb.AppendLine("## Code of Conduct");
        sb.AppendLine();
        sb.AppendLine("This project adheres to a code of conduct. By participating, you are expected to uphold this code.");
        sb.AppendLine();
        sb.AppendLine("## How to Contribute");
        sb.AppendLine();
        sb.AppendLine("### Reporting Bugs");
        sb.AppendLine();
        sb.AppendLine("- Use the GitHub issue tracker");
        sb.AppendLine("- Include detailed steps to reproduce");
        sb.AppendLine("- Provide system information");
        sb.AppendLine();
        sb.AppendLine("### Suggesting Enhancements");
        sb.AppendLine();
        sb.AppendLine("- Use the feature request template");
        sb.AppendLine("- Explain the use case");
        sb.AppendLine("- Consider backward compatibility");
        sb.AppendLine();
        sb.AppendLine("### Pull Requests");
        sb.AppendLine();
        sb.AppendLine("1. Fork the repository");
        sb.AppendLine("2. Create a feature branch (`git checkout -b feature/amazing-feature`)");
        sb.AppendLine("3. Commit your changes (`git commit -m 'Add amazing feature'`)");
        sb.AppendLine("4. Push to the branch (`git push origin feature/amazing-feature`)");
        sb.AppendLine("5. Open a Pull Request");
        sb.AppendLine();
        sb.AppendLine("### Development Setup");
        sb.AppendLine();
        sb.AppendLine("See README.md for development setup instructions.");
        sb.AppendLine();
        sb.AppendLine("### Coding Standards");
        sb.AppendLine();
        sb.AppendLine("- Follow the existing code style");
        sb.AppendLine("- Write tests for new features");
        sb.AppendLine("- Update documentation as needed");
        sb.AppendLine("- Ensure CI passes before requesting review");
        
        return sb.ToString();
    }
    
    private string GenerateCodeOwners(TemplateGenerationRequest request)
    {
        return @"# CODEOWNERS
# https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners

# Default owners for everything
* @team-leads

# Infrastructure
/infra/ @platform-team @devops-team

# CI/CD
/.github/ @devops-team

# Documentation
/docs/ @tech-writers @team-leads
";
    }
    
    private string GeneratePullRequestTemplate()
    {
        return @"## Description
<!-- Describe your changes in detail -->

## Type of Change
<!-- Mark with an 'x' -->
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## How Has This Been Tested?
<!-- Describe the tests you ran -->

## Checklist
- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my own code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
- [ ] Any dependent changes have been merged and published

## Screenshots (if applicable)

## Additional Context
";
    }
    
    private string GenerateBugReportTemplate()
    {
        return @"---
name: Bug Report
about: Create a report to help us improve
title: '[BUG] '
labels: bug
assignees: ''
---

## Describe the Bug
A clear and concise description of what the bug is.

## To Reproduce
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

## Expected Behavior
A clear and concise description of what you expected to happen.

## Screenshots
If applicable, add screenshots to help explain your problem.

## Environment
- OS: [e.g. Ubuntu 22.04]
- Version: [e.g. 1.2.3]
- Browser (if applicable): [e.g. Chrome 120]

## Additional Context
Add any other context about the problem here.
";
    }
    
    private string GenerateFeatureRequestTemplate()
    {
        return @"---
name: Feature Request
about: Suggest an idea for this project
title: '[FEATURE] '
labels: enhancement
assignees: ''
---

## Is your feature request related to a problem?
A clear and concise description of what the problem is. Ex. I'm always frustrated when [...]

## Describe the solution you'd like
A clear and concise description of what you want to happen.

## Describe alternatives you've considered
A clear and concise description of any alternative solutions or features you've considered.

## Additional Context
Add any other context or screenshots about the feature request here.
";
    }
    
    private string GenerateDependabot(ProgrammingLanguage language)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("version: 2");
        sb.AppendLine("updates:");
        
        // GitHub Actions updates
        sb.AppendLine("  - package-ecosystem: \"github-actions\"");
        sb.AppendLine("    directory: \"/\"");
        sb.AppendLine("    schedule:");
        sb.AppendLine("      interval: \"weekly\"");
        sb.AppendLine();
        
        // Language-specific updates
        switch (language)
        {
            case ProgrammingLanguage.NodeJS:
                sb.AppendLine("  - package-ecosystem: \"npm\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
                
            case ProgrammingLanguage.Python:
                sb.AppendLine("  - package-ecosystem: \"pip\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
                
            case ProgrammingLanguage.DotNet:
                sb.AppendLine("  - package-ecosystem: \"nuget\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
                
            case ProgrammingLanguage.Java:
                sb.AppendLine("  - package-ecosystem: \"maven\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
                
            case ProgrammingLanguage.Go:
                sb.AppendLine("  - package-ecosystem: \"gomod\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
                
            case ProgrammingLanguage.Rust:
                sb.AppendLine("  - package-ecosystem: \"cargo\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
                
            case ProgrammingLanguage.Ruby:
                sb.AppendLine("  - package-ecosystem: \"bundler\"");
                sb.AppendLine("    directory: \"/\"");
                sb.AppendLine("    schedule:");
                sb.AppendLine("      interval: \"weekly\"");
                sb.AppendLine("    open-pull-requests-limit: 10");
                break;
        }
        
        // Docker updates
        sb.AppendLine();
        sb.AppendLine("  - package-ecosystem: \"docker\"");
        sb.AppendLine("    directory: \"/\"");
        sb.AppendLine("    schedule:");
        sb.AppendLine("      interval: \"weekly\"");
        
        return sb.ToString();
    }
    
    // Helper methods
    private string GetLanguagePrerequisites(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.NodeJS => "- Node.js 20.x or higher\n- npm or yarn",
            ProgrammingLanguage.Python => "- Python 3.11 or higher\n- pip",
            ProgrammingLanguage.DotNet => "- .NET 8.0 SDK or higher",
            ProgrammingLanguage.Java => "- Java 17 or higher\n- Maven or Gradle",
            ProgrammingLanguage.Go => "- Go 1.21 or higher",
            ProgrammingLanguage.Rust => "- Rust 1.70 or higher\n- Cargo",
            ProgrammingLanguage.Ruby => "- Ruby 3.2 or higher\n- Bundler",
            ProgrammingLanguage.PHP => "- PHP 8.2 or higher\n- Composer",
            _ => "- Runtime for chosen language"
        };
    }
    
    private string GetCloudCLI(CloudProvider provider)
    {
        return provider switch
        {
            CloudProvider.AWS => "AWS CLI",
            CloudProvider.Azure => "Azure CLI",
            CloudProvider.GCP => "Google Cloud SDK (gcloud)",
            _ => "Cloud provider CLI"
        };
    }
    
    private string GetInstallCommand(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.NodeJS => "npm install",
            ProgrammingLanguage.Python => "pip install -r requirements.txt",
            ProgrammingLanguage.DotNet => "dotnet restore",
            ProgrammingLanguage.Java => "mvn install",
            ProgrammingLanguage.Go => "go mod download",
            ProgrammingLanguage.Rust => "cargo build",
            ProgrammingLanguage.Ruby => "bundle install",
            ProgrammingLanguage.PHP => "composer install",
            _ => "# Install dependencies"
        };
    }
    
    private string GetRunCommand(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.NodeJS => "npm start",
            ProgrammingLanguage.Python => "python app.py",
            ProgrammingLanguage.DotNet => "dotnet run",
            ProgrammingLanguage.Java => "mvn spring-boot:run",
            ProgrammingLanguage.Go => "go run main.go",
            ProgrammingLanguage.Rust => "cargo run",
            ProgrammingLanguage.Ruby => "ruby app.rb",
            ProgrammingLanguage.PHP => "php -S localhost:8000",
            _ => "# Run the application"
        };
    }
    
    private string GetTestCommand(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.NodeJS => "npm test",
            ProgrammingLanguage.Python => "pytest",
            ProgrammingLanguage.DotNet => "dotnet test",
            ProgrammingLanguage.Java => "mvn test",
            ProgrammingLanguage.Go => "go test ./...",
            ProgrammingLanguage.Rust => "cargo test",
            ProgrammingLanguage.Ruby => "rspec",
            ProgrammingLanguage.PHP => "phpunit",
            _ => "# Run tests"
        };
    }
    
    private string GetCoverageCommand(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.NodeJS => "npm test -- --coverage",
            ProgrammingLanguage.Python => "pytest --cov",
            ProgrammingLanguage.DotNet => "dotnet test /p:CollectCoverage=true",
            ProgrammingLanguage.Java => "mvn test jacoco:report",
            ProgrammingLanguage.Go => "go test -cover ./...",
            ProgrammingLanguage.Rust => "cargo tarpaulin",
            ProgrammingLanguage.Ruby => "rspec --format documentation",
            ProgrammingLanguage.PHP => "phpunit --coverage-html coverage",
            _ => "# Run tests with coverage"
        };
    }
}
