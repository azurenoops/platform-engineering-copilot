using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.Application;
using Platform.Engineering.Copilot.Core.Services.Generators.Database;
using Platform.Engineering.Copilot.Core.Services.Generators.Security;
using Platform.Engineering.Copilot.Core.Services.Generators.Observability;
using Platform.Engineering.Copilot.Core.Services.Generators.Repository;
using Platform.Engineering.Copilot.Core.Services.Generators.Workflow;
using Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Generators.ARM;
using Platform.Engineering.Copilot.Core.Services.Generators.CloudFormation;

namespace Platform.Engineering.Copilot.Core.Services
{
    /// <summary>
    /// Universal dynamic template generator supporting all programming languages, databases, and infrastructure formats.
    /// Architecture: Language-agnostic approach where templates are generated based on specifications, not assumptions.
    /// </summary>
    public interface IDynamicTemplateGenerator
    {
        Task<TemplateGenerationResult> GenerateTemplateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken = default);
    }

    public class DynamicTemplateGeneratorService : IDynamicTemplateGenerator
    {
        private readonly ILogger<DynamicTemplateGeneratorService> _logger;
        private readonly Dictionary<ProgrammingLanguage, IApplicationCodeGenerator> _codeGenerators;
        private readonly Dictionary<DatabaseType, IDatabaseTemplateGenerator> _databaseGenerators;
        private readonly Dictionary<InfrastructureFormat, IInfrastructureGenerator> _infraGenerators;

        public DynamicTemplateGeneratorService(ILogger<DynamicTemplateGeneratorService> logger)
        {
            _logger = logger;
            
            // Initialize code generators
            _codeGenerators = new Dictionary<ProgrammingLanguage, IApplicationCodeGenerator>
            {
                [ProgrammingLanguage.DotNet] = new DotNetCodeGenerator(),
                [ProgrammingLanguage.NodeJS] = new NodeJSCodeGenerator(),
                [ProgrammingLanguage.Python] = new PythonCodeGenerator(),
                [ProgrammingLanguage.Java] = new JavaCodeGenerator(),
                [ProgrammingLanguage.Go] = new GoCodeGenerator(),
                [ProgrammingLanguage.Rust] = new RustCodeGenerator(),
                [ProgrammingLanguage.Ruby] = new RubyCodeGenerator(),
                [ProgrammingLanguage.PHP] = new PHPCodeGenerator()
            };

            // Initialize database generators
            _databaseGenerators = new Dictionary<DatabaseType, IDatabaseTemplateGenerator>
            {
                [DatabaseType.PostgreSQL] = new PostgreSQLTemplateGenerator(),
                [DatabaseType.MySQL] = new MySQLTemplateGenerator(),
                [DatabaseType.SQLServer] = new SQLServerTemplateGenerator(),
                [DatabaseType.AzureSQL] = new AzureSQLTemplateGenerator(),
                [DatabaseType.MongoDB] = new MongoDBTemplateGenerator(),
                [DatabaseType.CosmosDB] = new CosmosDBTemplateGenerator(),
                [DatabaseType.Redis] = new RedisTemplateGenerator(),
                [DatabaseType.DynamoDB] = new DynamoDBTemplateGenerator()
            };

            // Initialize infrastructure generators
            // Use UnifiedInfrastructureOrchestrator for Terraform and Bicep (modular, platform-specific)
            // The orchestrator handles Terraform, Bicep, and Kubernetes cluster provisioning
            // For Kubernetes manifests, use Terraform/Bicep which include K8s resources
            var unifiedOrchestrator = new UnifiedInfrastructureOrchestrator(_logger as ILogger<UnifiedInfrastructureOrchestrator> ?? 
                LoggerFactory.Create(builder => {}).CreateLogger<UnifiedInfrastructureOrchestrator>());
            
            _infraGenerators = new Dictionary<InfrastructureFormat, IInfrastructureGenerator>
            {
                [InfrastructureFormat.Kubernetes] = unifiedOrchestrator,  // K8s clusters via Terraform/Bicep
                [InfrastructureFormat.Bicep] = unifiedOrchestrator,  // Unified orchestrator handles Bicep
                [InfrastructureFormat.Terraform] = unifiedOrchestrator,  // Unified orchestrator handles Terraform
                [InfrastructureFormat.ARM] = new ARMGenerator(),
                [InfrastructureFormat.CloudFormation] = new CloudFormationGenerator()
            };
        }

        public async Task<TemplateGenerationResult> GenerateTemplateAsync(
            TemplateGenerationRequest request, 
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var result = new TemplateGenerationResult { Success = true };
            var components = new List<string>();

            try
            {
                _logger.LogInformation("Generating template for service: {ServiceName} (Type: {TemplateType})", 
                    request.ServiceName, request.TemplateType ?? "unspecified");

                // Check if this is an infrastructure-only template
                bool isInfrastructureTemplate = IsInfrastructureTemplate(request);

                if (isInfrastructureTemplate)
                {
                    _logger.LogInformation("Infrastructure-only template detected. Generating IaC files only.");
                    
                    // For infrastructure templates: ONLY generate the IaC files (Bicep/Terraform)
                    await GenerateInfrastructureAsync(request, result, components, cancellationToken);
                    
                    result.GeneratedComponents = components;
                    result.Summary = $"Generated {result.Files.Count} infrastructure files for {request.ServiceName}";
                    _logger.LogInformation("Infrastructure template generation complete: {Summary}", result.Summary);
                    
                    return result;
                }

                // For application/service templates: Generate full set of files

                // 1. Generate Application Code (if specified)
                if (request.Application != null)
                {
                    await GenerateApplicationCodeAsync(request, result, components, cancellationToken);
                }

                // 2. Generate Database Templates (if specified)
                if (request.Databases.Any())
                {
                    await GenerateDatabaseTemplatesAsync(request, result, components, cancellationToken);
                }

                // 3. Generate Infrastructure (always - can be infrastructure-only template)
                await GenerateInfrastructureAsync(request, result, components, cancellationToken);

                // 4. Generate Repository Files (GitHub templates, .gitignore, etc.)
                await GenerateRepositoryFilesAsync(request, result, components, cancellationToken);

                // 5. Generate Docker Files (Dockerfile, docker-compose, etc.)
                await GenerateDockerFilesAsync(request, result, components, cancellationToken);

                // 6. Generate CI/CD Workflows
                await GenerateWorkflowsAsync(request, result, components, cancellationToken);

                // 7. Generate Security Components
                if (request.Security.RBAC || request.Security.NetworkPolicies)
                {
                    await GenerateSecurityComponentsAsync(request, result, components, cancellationToken);
                }

                // 8. Generate Observability Components
                if (request.Observability.Prometheus || request.Observability.Grafana)
                {
                    await GenerateObservabilityComponentsAsync(request, result, components, cancellationToken);
                }

                // 9. Generate Documentation (note: this may overlap with GitHubRepoGenerator README)
                // Skip generating README.md here since GitHubRepoGenerator now generates it
                // result.Files["README.md"] = GenerateReadme(request, components);

                result.GeneratedComponents = components;
                result.Summary = $"Generated {result.Files.Count} files with {components.Count} components for {request.ServiceName}";
                
                _logger.LogInformation("Template generation complete: {Summary}", result.Summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Template generation failed for {ServiceName}", request.ServiceName);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Determines if the template is an infrastructure-only template.
        /// Infrastructure templates should only generate Bicep/Terraform files, not application code.
        /// </summary>
        private bool IsInfrastructureTemplate(TemplateGenerationRequest request)
        {
            // Check if TemplateType is explicitly set to "infrastructure"
            if (!string.IsNullOrEmpty(request.TemplateType) && 
                request.TemplateType.Equals("infrastructure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Also consider it infrastructure-only if no application spec is provided
            // AND the format is clearly IaC (Bicep/Terraform/ARM/CloudFormation)
            if (request.Application == null && 
                (request.Infrastructure.Format == InfrastructureFormat.Bicep ||
                 request.Infrastructure.Format == InfrastructureFormat.Terraform ||
                 request.Infrastructure.Format == InfrastructureFormat.ARM ||
                 request.Infrastructure.Format == InfrastructureFormat.CloudFormation))
            {
                return true;
            }

            return false;
        }

        private async Task GenerateApplicationCodeAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            if (request.Application == null) return;

            var language = request.Application.Language;
            if (!_codeGenerators.ContainsKey(language))
            {
                _logger.LogWarning("No code generator for language: {Language}", language);
                return;
            }

            var generator = _codeGenerators[language];
            var appFiles = await generator.GenerateAsync(request, cancellationToken);
            
            foreach (var file in appFiles)
            {
                result.Files[file.Key] = file.Value;
            }

            components.Add($"Application ({language})");
            _logger.LogDebug("Generated {Count} application files for {Language}", appFiles.Count, language);
        }

        private async Task GenerateDatabaseTemplatesAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            foreach (var dbSpec in request.Databases)
            {
                if (!_databaseGenerators.ContainsKey(dbSpec.Type))
                {
                    _logger.LogWarning("No database generator for type: {Type}", dbSpec.Type);
                    continue;
                }

                var generator = _databaseGenerators[dbSpec.Type];
                var dbFiles = await generator.GenerateAsync(request, dbSpec, cancellationToken);
                
                foreach (var file in dbFiles)
                {
                    result.Files[file.Key] = file.Value;
                }

                components.Add($"Database ({dbSpec.Type})");
                _logger.LogDebug("Generated {Count} database files for {Type}", dbFiles.Count, dbSpec.Type);
            }
        }

        private async Task GenerateInfrastructureAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            var format = request.Infrastructure.Format;
            if (!_infraGenerators.ContainsKey(format))
            {
                _logger.LogWarning("No infrastructure generator for format: {Format}", format);
                return;
            }

            var generator = _infraGenerators[format];
            var infraFiles = await generator.GenerateAsync(request, cancellationToken);
            
            foreach (var file in infraFiles)
            {
                result.Files[file.Key] = file.Value;
            }

            components.Add($"Infrastructure ({format})");
            _logger.LogDebug("Generated {Count} infrastructure files for {Format}", infraFiles.Count, format);
        }

        private async Task GenerateRepositoryFilesAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var repoGenerator = new GitHubRepoGenerator();
                var repoFiles = repoGenerator.GenerateRepositoryFiles(request);
                
                foreach (var file in repoFiles)
                {
                    result.Files[file.Key] = file.Value;
                }

                components.Add("Repository Files");
                _logger.LogDebug("Generated {Count} repository files", repoFiles.Count);
            }, cancellationToken);
        }

        private async Task GenerateDockerFilesAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var dockerGenerator = new DockerfileGenerator();
                var dockerFiles = dockerGenerator.GenerateDockerFiles(request);
                
                foreach (var file in dockerFiles)
                {
                    result.Files[file.Key] = file.Value;
                }

                components.Add("Docker Files");
                _logger.LogDebug("Generated {Count} Docker files", dockerFiles.Count);
            }, cancellationToken);
        }

        private async Task GenerateWorkflowsAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var workflowGenerator = new GitHubActionsWorkflowGenerator();
                var workflowFiles = workflowGenerator.GenerateWorkflows(request);
                
                foreach (var file in workflowFiles)
                {
                    result.Files[file.Key] = file.Value;
                }

                components.Add("CI/CD Workflows");
                _logger.LogDebug("Generated {Count} workflow files", workflowFiles.Count);
            }, cancellationToken);
        }

        private async Task GenerateSecurityComponentsAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            var securityGen = new SecurityComponentGenerator();
            var securityFiles = await securityGen.GenerateAsync(request, cancellationToken);
            
            foreach (var file in securityFiles)
            {
                result.Files[file.Key] = file.Value;
            }

            if (securityFiles.Any())
            {
                components.Add("Security");
            }
        }

        private async Task GenerateObservabilityComponentsAsync(
            TemplateGenerationRequest request,
            TemplateGenerationResult result,
            List<string> components,
            CancellationToken cancellationToken)
        {
            var observabilityGen = new ObservabilityComponentGenerator();
            var observabilityFiles = await observabilityGen.GenerateAsync(request, cancellationToken);
            
            foreach (var file in observabilityFiles)
            {
                result.Files[file.Key] = file.Value;
            }

            if (observabilityFiles.Any())
            {
                components.Add("Observability");
            }
        }

        private string GenerateReadme(TemplateGenerationRequest request, List<string> components)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {request.ServiceName}");
            sb.AppendLine();
            sb.AppendLine($"## Description");
            sb.AppendLine(request.Description);
            sb.AppendLine();
            
            sb.AppendLine($"## Architecture");
            sb.AppendLine();
            
            if (request.Application != null)
            {
                sb.AppendLine($"### Application");
                sb.AppendLine($"- **Language**: {request.Application.Language}");
                sb.AppendLine($"- **Framework**: {request.Application.Framework}");
                sb.AppendLine($"- **Type**: {request.Application.Type}");
                sb.AppendLine($"- **Port**: {request.Application.Port}");
                sb.AppendLine();
            }

            if (request.Databases.Any())
            {
                sb.AppendLine($"### Databases");
                foreach (var db in request.Databases)
                {
                    sb.AppendLine($"- **{db.Name}**: {db.Type} ({db.Version}) - {db.Tier} tier");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"### Infrastructure");
            sb.AppendLine($"- **Format**: {request.Infrastructure.Format}");
            sb.AppendLine($"- **Provider**: {request.Infrastructure.Provider}");
            sb.AppendLine($"- **Platform**: {request.Infrastructure.ComputePlatform}");
            sb.AppendLine($"- **Region**: {request.Infrastructure.Region}");
            sb.AppendLine();

            sb.AppendLine($"## Generated Components");
            foreach (var component in components)
            {
                sb.AppendLine($"- {component}");
            }
            sb.AppendLine();

            sb.AppendLine($"## Deployment");
            sb.AppendLine($"- **Replicas**: {request.Deployment.Replicas}");
            sb.AppendLine($"- **Auto-scaling**: {request.Deployment.AutoScaling}");
            if (request.Deployment.AutoScaling)
            {
                sb.AppendLine($"  - Min: {request.Deployment.MinReplicas}, Max: {request.Deployment.MaxReplicas}");
                sb.AppendLine($"  - Target CPU: {request.Deployment.TargetCpuPercent}%");
            }
            sb.AppendLine();

            sb.AppendLine($"## Getting Started");
            sb.AppendLine();
            sb.AppendLine($"1. Review the generated templates");
            sb.AppendLine($"2. Customize parameters as needed");
            sb.AppendLine($"3. Deploy using your CI/CD pipeline");

            return sb.ToString();
        }
    }
}
