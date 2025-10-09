using Xunit;
using Platform.Engineering.Copilot.Core.Services.Generators.Workflow;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Generators;

public class GitHubActionsWorkflowGeneratorTests
{
    [Fact]
    public void GenerateWorkflows_ShouldReturnCIAndCDWorkflows()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);

        // Assert
        Assert.NotEmpty(files);
        Assert.Contains(".github/workflows/ci.yml", files.Keys);
        Assert.Contains(".github/workflows/cd-dev.yml", files.Keys);
        Assert.Contains(".github/workflows/cd-staging.yml", files.Keys);
        Assert.Contains(".github/workflows/cd-prod.yml", files.Keys);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "setup-node")]
    [InlineData(ProgrammingLanguage.Python, "setup-python")]
    [InlineData(ProgrammingLanguage.DotNet, "setup-dotnet")]
    [InlineData(ProgrammingLanguage.Java, "setup-java")]
    [InlineData(ProgrammingLanguage.Go, "setup-go")]
    public void GenerateCIWorkflow_ShouldUseCorrectSetupAction(ProgrammingLanguage language, string expectedAction)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(language, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var ci = files[".github/workflows/ci.yml"];

        // Assert
        Assert.Contains(expectedAction, ci);
        Assert.Contains("actions/checkout@v4", ci);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "npm ci")]
    [InlineData(ProgrammingLanguage.NodeJS, "npm test")]
    [InlineData(ProgrammingLanguage.NodeJS, "npm run build")]
    public void GenerateCIWorkflow_NodeJS_ShouldContainNpmCommands(ProgrammingLanguage language, string expectedCommand)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(language, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var ci = files[".github/workflows/ci.yml"];

        // Assert
        Assert.Contains(expectedCommand, ci);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.Python, "pip install")]
    [InlineData(ProgrammingLanguage.Python, "pytest")]
    [InlineData(ProgrammingLanguage.Python, "flake8")]
    public void GenerateCIWorkflow_Python_ShouldContainPythonCommands(ProgrammingLanguage language, string expectedCommand)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(language, ComputePlatform.Lambda);

        // Act
        var files = generator.GenerateWorkflows(request);
        var ci = files[".github/workflows/ci.yml"];

        // Assert
        Assert.Contains(expectedCommand, ci);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.DotNet, "dotnet restore")]
    [InlineData(ProgrammingLanguage.DotNet, "dotnet build")]
    [InlineData(ProgrammingLanguage.DotNet, "dotnet test")]
    public void GenerateCIWorkflow_DotNet_ShouldContainDotNetCommands(ProgrammingLanguage language, string expectedCommand)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(language, ComputePlatform.AKS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var ci = files[".github/workflows/ci.yml"];

        // Assert
        Assert.Contains(expectedCommand, ci);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS)]
    [InlineData(ProgrammingLanguage.Python)]
    [InlineData(ProgrammingLanguage.DotNet)]
    public void GenerateCIWorkflow_ShouldIncludeCoverageUpload(ProgrammingLanguage language)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(language, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var ci = files[".github/workflows/ci.yml"];

        // Assert
        Assert.Contains("codecov", ci);
        Assert.Contains("coverage", ci, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateCIWorkflow_ShouldRunOnPushAndPullRequest()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var ci = files[".github/workflows/ci.yml"];

        // Assert
        Assert.Contains("on:", ci);
        Assert.Contains("push:", ci);
        Assert.Contains("pull_request:", ci);
        Assert.Contains("branches:", ci);
    }

    [Theory]
    [InlineData(ComputePlatform.ECS, ".github/workflows/cd-ecs.yml")]
    [InlineData(ComputePlatform.Lambda, ".github/workflows/cd-lambda.yml")]
    [InlineData(ComputePlatform.CloudRun, ".github/workflows/cd-cloudrun.yml")]
    [InlineData(ComputePlatform.AKS, ".github/workflows/cd-aks.yml")]
    [InlineData(ComputePlatform.EKS, ".github/workflows/cd-eks.yml")]
    [InlineData(ComputePlatform.GKE, ".github/workflows/cd-gke.yml")]
    public void GenerateCDWorkflow_ShouldGeneratePlatformSpecificWorkflow(ComputePlatform platform, string expectedFile)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, platform);

        // Act
        var files = generator.GenerateWorkflows(request);

        // Assert
        Assert.Contains(expectedFile, files.Keys);
    }

    [Fact]
    public void GenerateCDWorkflow_ECS_ShouldConfigureAWS()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-ecs.yml"];

        // Assert
        Assert.Contains("aws-actions/configure-aws-credentials", cd);
        Assert.Contains("amazon-ecr-login", cd);
        Assert.Contains("amazon-ecs-deploy-task-definition", cd);
        Assert.Contains("docker build", cd);
        Assert.Contains("docker push", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_Lambda_ShouldDeployToLambda()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Python, ComputePlatform.Lambda);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-lambda.yml"];

        // Assert
        Assert.Contains("aws lambda", cd);
        Assert.Contains("update-function-code", cd);
        Assert.Contains("zip", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_CloudRun_ShouldConfigureGCP()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.CloudRun);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-cloudrun.yml"];

        // Assert
        Assert.Contains("google-github-actions/auth", cd);
        Assert.Contains("gcloud run deploy", cd);
        Assert.Contains("gcr.io", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_AKS_ShouldConfigureAzure()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.DotNet, ComputePlatform.AKS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-aks.yml"];

        // Assert
        Assert.Contains("azure/login", cd);
        Assert.Contains("azure/aks-set-context", cd);
        Assert.Contains("kubectl", cd);
        Assert.Contains("azurecr.io", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_EKS_ShouldUpdateKubeconfig()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Go, ComputePlatform.EKS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-eks.yml"];

        // Assert
        Assert.Contains("aws eks update-kubeconfig", cd);
        Assert.Contains("kubectl", cd);
        Assert.Contains("ECR_", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_GKE_ShouldGetCredentials()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Java, ComputePlatform.GKE);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-gke.yml"];

        // Assert
        Assert.Contains("gcloud container clusters get-credentials", cd);
        Assert.Contains("kubectl", cd);
        Assert.Contains("gcr.io", cd);
    }

    [Fact]
    public void GenerateEnvironmentWorkflow_Dev_ShouldTriggerOnPush()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var devWorkflow = files[".github/workflows/cd-dev.yml"];

        // Assert
        Assert.Contains("on:", devWorkflow);
        Assert.Contains("push:", devWorkflow);
        Assert.Contains("develop", devWorkflow);
    }

    [Fact]
    public void GenerateEnvironmentWorkflow_Prod_ShouldRequireManualTrigger()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var prodWorkflow = files[".github/workflows/cd-prod.yml"];

        // Assert
        Assert.Contains("workflow_dispatch", prodWorkflow);
    }

    [Theory]
    [InlineData(".github/workflows/cd-dev.yml", "dev")]
    [InlineData(".github/workflows/cd-staging.yml", "staging")]
    [InlineData(".github/workflows/cd-prod.yml", "prod")]
    public void GenerateEnvironmentWorkflows_ShouldSetCorrectEnvironment(string workflowFile, string environment)
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var workflow = files[workflowFile];

        // Assert
        Assert.Contains($"environment: {environment}", workflow);
    }

    [Fact]
    public void GenerateCDWorkflow_ShouldUseGitHubSHA()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-ecs.yml"];

        // Assert
        Assert.Contains("github.sha", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_ShouldUseSecrets()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-ecs.yml"];

        // Assert
        Assert.Contains("secrets.", cd);
    }

    [Fact]
    public void GenerateCDWorkflow_WithWorkflowDispatch_ShouldAllowEnvironmentSelection()
    {
        // Arrange
        var generator = new GitHubActionsWorkflowGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS, ComputePlatform.ECS);

        // Act
        var files = generator.GenerateWorkflows(request);
        var cd = files[".github/workflows/cd-ecs.yml"];

        // Assert
        Assert.Contains("workflow_dispatch:", cd);
        Assert.Contains("inputs:", cd);
        Assert.Contains("environment:", cd);
    }

    private TemplateGenerationRequest CreateSampleRequest(ProgrammingLanguage language, ComputePlatform platform)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Description = "Test service for unit testing",
            Application = new ApplicationSpec
            {
                Language = language,
                Framework = "Default",
                Type = ApplicationType.WebAPI,
                Port = 8080,
                EnvironmentVariables = new Dictionary<string, string>(),
                IncludeHealthCheck = true
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Terraform,
                Provider = platform switch
                {
                    ComputePlatform.ECS or ComputePlatform.Lambda or ComputePlatform.EKS => CloudProvider.AWS,
                    ComputePlatform.AKS or ComputePlatform.AppService or ComputePlatform.ContainerApps => CloudProvider.Azure,
                    ComputePlatform.GKE or ComputePlatform.CloudRun => CloudProvider.GCP,
                    _ => CloudProvider.AWS
                },
                Region = "us-east-1",
                ComputePlatform = platform
            },
            Deployment = new DeploymentSpec
            {
                Replicas = 3,
                AutoScaling = true,
                MinReplicas = 2,
                MaxReplicas = 10
            },
            Databases = new List<DatabaseSpec>()
        };
    }
}
