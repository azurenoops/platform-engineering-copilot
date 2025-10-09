using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Workflow;

/// <summary>
/// Generates GitHub Actions workflows for CI/CD pipelines
/// </summary>
public class GitHubActionsWorkflowGenerator
{
    public Dictionary<string, string> GenerateWorkflows(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var app = request.Application ?? new ApplicationSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        // Generate CI workflow (always included)
        files[".github/workflows/ci.yml"] = GenerateCIWorkflow(request);
        
        // Generate platform-specific CD workflows
        var cdWorkflow = GenerateCDWorkflow(request);
        if (!string.IsNullOrEmpty(cdWorkflow))
        {
            var platform = infrastructure.ComputePlatform.ToString().ToLower();
            files[$".github/workflows/cd-{platform}.yml"] = cdWorkflow;
        }
        
        // Generate environment-specific workflows
        files[".github/workflows/cd-dev.yml"] = GenerateEnvironmentWorkflow(request, "dev");
        files[".github/workflows/cd-staging.yml"] = GenerateEnvironmentWorkflow(request, "staging");
        files[".github/workflows/cd-prod.yml"] = GenerateEnvironmentWorkflow(request, "prod");
        
        return files;
    }
    
    private string GenerateCIWorkflow(TemplateGenerationRequest request)
    {
        var app = request.Application ?? new ApplicationSpec();
        
        return app.Language switch
        {
            ProgrammingLanguage.NodeJS => GenerateNodeJSCIWorkflow(request),
            ProgrammingLanguage.Python => GeneratePythonCIWorkflow(request),
            ProgrammingLanguage.DotNet => GenerateDotNetCIWorkflow(request),
            ProgrammingLanguage.Java => GenerateJavaCIWorkflow(request),
            ProgrammingLanguage.Go => GenerateGoCIWorkflow(request),
            ProgrammingLanguage.Rust => GenerateRustCIWorkflow(request),
            _ => GenerateGenericCIWorkflow(request)
        };
    }
    
    private string GenerateNodeJSCIWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "nodejs-service";
        
        sb.AppendLine("name: CI");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine("  pull_request:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-test:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine();
        sb.AppendLine("    strategy:");
        sb.AppendLine("      matrix:");
        sb.AppendLine("        node-version: [18.x, 20.x]");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Setup Node.js ${{ matrix.node-version }}");
        sb.AppendLine("      uses: actions/setup-node@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        node-version: ${{ matrix.node-version }}");
        sb.AppendLine("        cache: 'npm'");
        sb.AppendLine();
        sb.AppendLine("    - name: Install dependencies");
        sb.AppendLine("      run: npm ci");
        sb.AppendLine();
        sb.AppendLine("    - name: Run linter");
        sb.AppendLine("      run: npm run lint");
        sb.AppendLine();
        sb.AppendLine("    - name: Run tests");
        sb.AppendLine("      run: npm test -- --coverage");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload coverage reports");
        sb.AppendLine("      uses: codecov/codecov-action@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        files: ./coverage/lcov.info");
        sb.AppendLine("        flags: unittests");
        sb.AppendLine();
        sb.AppendLine("    - name: Build application");
        sb.AppendLine("      run: npm run build");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload build artifacts");
        sb.AppendLine("      uses: actions/upload-artifact@v4");
        sb.AppendLine("      with:");
        sb.AppendLine($"        name: {serviceName}-${{{{ github.sha }}}}");
        sb.AppendLine("        path: dist/");
        sb.AppendLine("        retention-days: 7");
        
        return sb.ToString();
    }
    
    private string GeneratePythonCIWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "python-service";
        
        sb.AppendLine("name: CI");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine("  pull_request:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-test:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine();
        sb.AppendLine("    strategy:");
        sb.AppendLine("      matrix:");
        sb.AppendLine("        python-version: ['3.10', '3.11', '3.12']");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Set up Python ${{ matrix.python-version }}");
        sb.AppendLine("      uses: actions/setup-python@v5");
        sb.AppendLine("      with:");
        sb.AppendLine("        python-version: ${{ matrix.python-version }}");
        sb.AppendLine("        cache: 'pip'");
        sb.AppendLine();
        sb.AppendLine("    - name: Install dependencies");
        sb.AppendLine("      run: |");
        sb.AppendLine("        python -m pip install --upgrade pip");
        sb.AppendLine("        pip install -r requirements.txt");
        sb.AppendLine("        pip install pytest pytest-cov flake8 black");
        sb.AppendLine();
        sb.AppendLine("    - name: Run linter (flake8)");
        sb.AppendLine("      run: flake8 . --count --select=E9,F63,F7,F82 --show-source --statistics");
        sb.AppendLine();
        sb.AppendLine("    - name: Check formatting (black)");
        sb.AppendLine("      run: black --check .");
        sb.AppendLine();
        sb.AppendLine("    - name: Run tests with coverage");
        sb.AppendLine("      run: pytest --cov=. --cov-report=xml --cov-report=html");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload coverage reports");
        sb.AppendLine("      uses: codecov/codecov-action@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        files: ./coverage.xml");
        sb.AppendLine("        flags: unittests");
        
        return sb.ToString();
    }
    
    private string GenerateDotNetCIWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "dotnet-service";
        
        sb.AppendLine("name: CI");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine("  pull_request:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-test:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Setup .NET");
        sb.AppendLine("      uses: actions/setup-dotnet@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        dotnet-version: '8.0.x'");
        sb.AppendLine();
        sb.AppendLine("    - name: Restore dependencies");
        sb.AppendLine("      run: dotnet restore");
        sb.AppendLine();
        sb.AppendLine("    - name: Build");
        sb.AppendLine("      run: dotnet build --no-restore --configuration Release");
        sb.AppendLine();
        sb.AppendLine("    - name: Run tests");
        sb.AppendLine("      run: dotnet test --no-build --configuration Release --verbosity normal /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload coverage reports");
        sb.AppendLine("      uses: codecov/codecov-action@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        files: ./coverage.cobertura.xml");
        sb.AppendLine("        flags: unittests");
        sb.AppendLine();
        sb.AppendLine("    - name: Publish");
        sb.AppendLine("      run: dotnet publish --no-build --configuration Release --output ./publish");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload artifacts");
        sb.AppendLine("      uses: actions/upload-artifact@v4");
        sb.AppendLine("      with:");
        sb.AppendLine($"        name: {serviceName}-${{{{ github.sha }}}}");
        sb.AppendLine("        path: ./publish/");
        sb.AppendLine("        retention-days: 7");
        
        return sb.ToString();
    }
    
    private string GenerateJavaCIWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("name: CI");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine("  pull_request:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-test:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Set up JDK 17");
        sb.AppendLine("      uses: actions/setup-java@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        java-version: '17'");
        sb.AppendLine("        distribution: 'temurin'");
        sb.AppendLine("        cache: maven");
        sb.AppendLine();
        sb.AppendLine("    - name: Build with Maven");
        sb.AppendLine("      run: mvn clean install");
        sb.AppendLine();
        sb.AppendLine("    - name: Run tests");
        sb.AppendLine("      run: mvn test");
        sb.AppendLine();
        sb.AppendLine("    - name: Generate coverage report");
        sb.AppendLine("      run: mvn jacoco:report");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload coverage reports");
        sb.AppendLine("      uses: codecov/codecov-action@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        files: ./target/site/jacoco/jacoco.xml");
        sb.AppendLine("        flags: unittests");
        
        return sb.ToString();
    }
    
    private string GenerateGoCIWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("name: CI");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine("  pull_request:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-test:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Set up Go");
        sb.AppendLine("      uses: actions/setup-go@v5");
        sb.AppendLine("      with:");
        sb.AppendLine("        go-version: '1.21'");
        sb.AppendLine("        cache: true");
        sb.AppendLine();
        sb.AppendLine("    - name: Install dependencies");
        sb.AppendLine("      run: go mod download");
        sb.AppendLine();
        sb.AppendLine("    - name: Run linter");
        sb.AppendLine("      uses: golangci/golangci-lint-action@v3");
        sb.AppendLine("      with:");
        sb.AppendLine("        version: latest");
        sb.AppendLine();
        sb.AppendLine("    - name: Run tests");
        sb.AppendLine("      run: go test -v -cover -coverprofile=coverage.out ./...");
        sb.AppendLine();
        sb.AppendLine("    - name: Upload coverage reports");
        sb.AppendLine("      uses: codecov/codecov-action@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        files: ./coverage.out");
        sb.AppendLine("        flags: unittests");
        sb.AppendLine();
        sb.AppendLine("    - name: Build");
        sb.AppendLine("      run: go build -v ./...");
        
        return sb.ToString();
    }
    
    private string GenerateRustCIWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("name: CI");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine("  pull_request:");
        sb.AppendLine("    branches: [ main, develop ]");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-test:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Install Rust");
        sb.AppendLine("      uses: actions-rs/toolchain@v1");
        sb.AppendLine("      with:");
        sb.AppendLine("        profile: minimal");
        sb.AppendLine("        toolchain: stable");
        sb.AppendLine("        override: true");
        sb.AppendLine("        components: rustfmt, clippy");
        sb.AppendLine();
        sb.AppendLine("    - name: Run formatter check");
        sb.AppendLine("      run: cargo fmt --all -- --check");
        sb.AppendLine();
        sb.AppendLine("    - name: Run clippy");
        sb.AppendLine("      run: cargo clippy -- -D warnings");
        sb.AppendLine();
        sb.AppendLine("    - name: Run tests");
        sb.AppendLine("      run: cargo test --verbose");
        sb.AppendLine();
        sb.AppendLine("    - name: Build");
        sb.AppendLine("      run: cargo build --release");
        
        return sb.ToString();
    }
    
    private string GenerateGenericCIWorkflow(TemplateGenerationRequest request)
    {
        return @"name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Build
      run: echo ""Build step - configure based on your language""
    
    - name: Test
      run: echo ""Test step - configure based on your language""
";
    }
    
    private string GenerateCDWorkflow(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        return infrastructure.ComputePlatform switch
        {
            ComputePlatform.ECS => GenerateECSDeploymentWorkflow(request),
            ComputePlatform.Lambda => GenerateLambdaDeploymentWorkflow(request),
            ComputePlatform.CloudRun => GenerateCloudRunDeploymentWorkflow(request),
            ComputePlatform.AKS => GenerateAKSDeploymentWorkflow(request),
            ComputePlatform.EKS => GenerateEKSDeploymentWorkflow(request),
            ComputePlatform.GKE => GenerateGKEDeploymentWorkflow(request),
            ComputePlatform.AppService => GenerateAppServiceDeploymentWorkflow(request),
            ComputePlatform.ContainerApps => GenerateContainerAppsDeploymentWorkflow(request),
            _ => string.Empty
        };
    }
    
    private string GenerateECSDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("name: Deploy to AWS ECS");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Configure AWS credentials");
        sb.AppendLine("      uses: aws-actions/configure-aws-credentials@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}");
        sb.AppendLine("        aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}");
        sb.AppendLine($"        aws-region: {infrastructure.Region}");
        sb.AppendLine();
        sb.AppendLine("    - name: Login to Amazon ECR");
        sb.AppendLine("      id: login-ecr");
        sb.AppendLine("      uses: aws-actions/amazon-ecr-login@v2");
        sb.AppendLine();
        sb.AppendLine("    - name: Build, tag, and push image to Amazon ECR");
        sb.AppendLine("      id: build-image");
        sb.AppendLine("      env:");
        sb.AppendLine("        ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}");
        sb.AppendLine($"        ECR_REPOSITORY: {serviceName}");
        sb.AppendLine("        IMAGE_TAG: ${{ github.sha }}");
        sb.AppendLine("      run: |");
        sb.AppendLine("        docker build -t $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG .");
        sb.AppendLine("        docker push $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG");
        sb.AppendLine("        echo \"image=$ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG\" >> $GITHUB_OUTPUT");
        sb.AppendLine();
        sb.AppendLine("    - name: Download task definition");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        aws ecs describe-task-definition --task-definition {serviceName} --query taskDefinition > task-definition.json");
        sb.AppendLine();
        sb.AppendLine("    - name: Fill in the new image ID in the Amazon ECS task definition");
        sb.AppendLine("      id: task-def");
        sb.AppendLine("      uses: aws-actions/amazon-ecs-render-task-definition@v1");
        sb.AppendLine("      with:");
        sb.AppendLine("        task-definition: task-definition.json");
        sb.AppendLine($"        container-name: {serviceName}");
        sb.AppendLine("        image: ${{ steps.build-image.outputs.image }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy Amazon ECS task definition");
        sb.AppendLine("      uses: aws-actions/amazon-ecs-deploy-task-definition@v1");
        sb.AppendLine("      with:");
        sb.AppendLine("        task-definition: ${{ steps.task-def.outputs.task-definition }}");
        sb.AppendLine($"        service: {serviceName}");
        sb.AppendLine($"        cluster: {serviceName}-cluster");
        sb.AppendLine("        wait-for-service-stability: true");
        
        return sb.ToString();
    }
    
    private string GenerateLambdaDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "lambda-function";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("name: Deploy to AWS Lambda");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Configure AWS credentials");
        sb.AppendLine("      uses: aws-actions/configure-aws-credentials@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}");
        sb.AppendLine("        aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}");
        sb.AppendLine($"        aws-region: {infrastructure.Region}");
        sb.AppendLine();
        sb.AppendLine("    - name: Install dependencies");
        sb.AppendLine("      run: |");
        sb.AppendLine("        pip install -r requirements.txt -t .");
        sb.AppendLine();
        sb.AppendLine("    - name: Package Lambda function");
        sb.AppendLine("      run: |");
        sb.AppendLine("        zip -r function.zip .");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to Lambda");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        aws lambda update-function-code --function-name {serviceName} --zip-file fileb://function.zip");
        sb.AppendLine($"        aws lambda wait function-updated --function-name {serviceName}");
        sb.AppendLine();
        sb.AppendLine("    - name: Update alias");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        VERSION=$(aws lambda publish-version --function-name {serviceName} --query Version --output text)");
        sb.AppendLine($"        aws lambda update-alias --function-name {serviceName} --name live --function-version $VERSION");
        
        return sb.ToString();
    }
    
    private string GenerateCloudRunDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "cloudrun-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("name: Deploy to Google Cloud Run");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Authenticate to Google Cloud");
        sb.AppendLine("      uses: google-github-actions/auth@v2");
        sb.AppendLine("      with:");
        sb.AppendLine("        credentials_json: ${{ secrets.GCP_SA_KEY }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Set up Cloud SDK");
        sb.AppendLine("      uses: google-github-actions/setup-gcloud@v2");
        sb.AppendLine();
        sb.AppendLine("    - name: Configure Docker for GCR");
        sb.AppendLine("      run: gcloud auth configure-docker");
        sb.AppendLine();
        sb.AppendLine("    - name: Build and push image");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        docker build -t gcr.io/${{{{ secrets.GCP_PROJECT_ID }}}}/{serviceName}:${{{{ github.sha }}}} .");
        sb.AppendLine($"        docker push gcr.io/${{{{ secrets.GCP_PROJECT_ID }}}}/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to Cloud Run");
        sb.AppendLine("      run: |");
        sb.AppendLine("        gcloud run deploy ${{ github.event.inputs.environment }}-" + serviceName + " \\");
        sb.AppendLine($"          --image gcr.io/${{{{ secrets.GCP_PROJECT_ID }}}}/{serviceName}:${{{{ github.sha }}}} \\");
        sb.AppendLine($"          --region {infrastructure.Region} \\");
        sb.AppendLine("          --platform managed \\");
        sb.AppendLine("          --allow-unauthenticated");
        
        return sb.ToString();
    }
    
    private string GenerateAKSDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks-service";
        
        sb.AppendLine("name: Deploy to Azure AKS");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Azure Login");
        sb.AppendLine("      uses: azure/login@v1");
        sb.AppendLine("      with:");
        sb.AppendLine("        creds: ${{ secrets.AZURE_CREDENTIALS }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Login to ACR");
        sb.AppendLine("      run: az acr login --name ${{ secrets.ACR_NAME }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Build and push image");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        docker build -t ${{{{ secrets.ACR_NAME }}}}.azurecr.io/{serviceName}:${{{{ github.sha }}}} .");
        sb.AppendLine($"        docker push ${{{{ secrets.ACR_NAME }}}}.azurecr.io/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine();
        sb.AppendLine("    - name: Set AKS context");
        sb.AppendLine("      uses: azure/aks-set-context@v3");
        sb.AppendLine("      with:");
        sb.AppendLine("        resource-group: ${{ secrets.AZURE_RG }}");
        sb.AppendLine("        cluster-name: ${{ secrets.AKS_CLUSTER_NAME }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to AKS");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        kubectl set image deployment/{serviceName} {serviceName}=${{{{ secrets.ACR_NAME }}}}.azurecr.io/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine($"        kubectl rollout status deployment/{serviceName}");
        
        return sb.ToString();
    }
    
    private string GenerateEKSDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "eks-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("name: Deploy to AWS EKS");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Configure AWS credentials");
        sb.AppendLine("      uses: aws-actions/configure-aws-credentials@v4");
        sb.AppendLine("      with:");
        sb.AppendLine("        aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}");
        sb.AppendLine("        aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}");
        sb.AppendLine($"        aws-region: {infrastructure.Region}");
        sb.AppendLine();
        sb.AppendLine("    - name: Login to Amazon ECR");
        sb.AppendLine("      id: login-ecr");
        sb.AppendLine("      uses: aws-actions/amazon-ecr-login@v2");
        sb.AppendLine();
        sb.AppendLine("    - name: Build and push image");
        sb.AppendLine("      env:");
        sb.AppendLine("        ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}");
        sb.AppendLine($"        ECR_REPOSITORY: {serviceName}");
        sb.AppendLine("      run: |");
        sb.AppendLine("        docker build -t $ECR_REGISTRY/$ECR_REPOSITORY:${{ github.sha }} .");
        sb.AppendLine("        docker push $ECR_REGISTRY/$ECR_REPOSITORY:${{ github.sha }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Update kubeconfig");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        aws eks update-kubeconfig --name {serviceName}-cluster --region {infrastructure.Region}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to EKS");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        kubectl set image deployment/{serviceName} {serviceName}=${{{{ steps.login-ecr.outputs.registry }}}}/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine($"        kubectl rollout status deployment/{serviceName}");
        
        return sb.ToString();
    }
    
    private string GenerateGKEDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "gke-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("name: Deploy to Google GKE");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Authenticate to Google Cloud");
        sb.AppendLine("      uses: google-github-actions/auth@v2");
        sb.AppendLine("      with:");
        sb.AppendLine("        credentials_json: ${{ secrets.GCP_SA_KEY }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Set up Cloud SDK");
        sb.AppendLine("      uses: google-github-actions/setup-gcloud@v2");
        sb.AppendLine();
        sb.AppendLine("    - name: Configure Docker for GCR");
        sb.AppendLine("      run: gcloud auth configure-docker");
        sb.AppendLine();
        sb.AppendLine("    - name: Build and push image");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        docker build -t gcr.io/${{{{ secrets.GCP_PROJECT_ID }}}}/{serviceName}:${{{{ github.sha }}}} .");
        sb.AppendLine($"        docker push gcr.io/${{{{ secrets.GCP_PROJECT_ID }}}}/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine();
        sb.AppendLine("    - name: Get GKE credentials");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        gcloud container clusters get-credentials {serviceName}-cluster --region {infrastructure.Region}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to GKE");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        kubectl set image deployment/{serviceName} {serviceName}=gcr.io/${{{{ secrets.GCP_PROJECT_ID }}}}/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine($"        kubectl rollout status deployment/{serviceName}");
        
        return sb.ToString();
    }
    
    private string GenerateAppServiceDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "appservice";
        
        sb.AppendLine("name: Deploy to Azure App Service");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Azure Login");
        sb.AppendLine("      uses: azure/login@v1");
        sb.AppendLine("      with:");
        sb.AppendLine("        creds: ${{ secrets.AZURE_CREDENTIALS }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to App Service");
        sb.AppendLine("      uses: azure/webapps-deploy@v2");
        sb.AppendLine("      with:");
        sb.AppendLine($"        app-name: {serviceName}");
        sb.AppendLine("        slot-name: ${{ github.event.inputs.environment }}");
        sb.AppendLine("        package: .");
        
        return sb.ToString();
    }
    
    private string GenerateContainerAppsDeploymentWorkflow(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "containerapp";
        
        sb.AppendLine("name: Deploy to Azure Container Apps");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine("    inputs:");
        sb.AppendLine("      environment:");
        sb.AppendLine("        description: 'Environment to deploy to'");
        sb.AppendLine("        required: true");
        sb.AppendLine("        type: choice");
        sb.AppendLine("        options:");
        sb.AppendLine("          - dev");
        sb.AppendLine("          - staging");
        sb.AppendLine("          - prod");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    environment: ${{ github.event.inputs.environment }}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine("    - name: Azure Login");
        sb.AppendLine("      uses: azure/login@v1");
        sb.AppendLine("      with:");
        sb.AppendLine("        creds: ${{ secrets.AZURE_CREDENTIALS }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Login to ACR");
        sb.AppendLine("      run: az acr login --name ${{ secrets.ACR_NAME }}");
        sb.AppendLine();
        sb.AppendLine("    - name: Build and push image");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        docker build -t ${{{{ secrets.ACR_NAME }}}}.azurecr.io/{serviceName}:${{{{ github.sha }}}} .");
        sb.AppendLine($"        docker push ${{{{ secrets.ACR_NAME }}}}.azurecr.io/{serviceName}:${{{{ github.sha }}}}");
        sb.AppendLine();
        sb.AppendLine("    - name: Deploy to Container Apps");
        sb.AppendLine("      run: |");
        sb.AppendLine($"        az containerapp update --name {serviceName} \\");
        sb.AppendLine("          --resource-group ${{ secrets.AZURE_RG }} \\");
        sb.AppendLine($"          --image ${{{{ secrets.ACR_NAME }}}}.azurecr.io/{serviceName}:${{{{ github.sha }}}}");
        
        return sb.ToString();
    }
    
    private string GenerateEnvironmentWorkflow(TemplateGenerationRequest request, string environment)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var platformName = infrastructure.ComputePlatform.ToString().ToLower();
        
        sb.AppendLine($"name: Deploy to {environment.ToUpper()}");
        sb.AppendLine();
        sb.AppendLine("on:");
        
        if (environment == "prod")
        {
            sb.AppendLine("  workflow_dispatch:");
        }
        else
        {
            sb.AppendLine("  push:");
            sb.AppendLine($"    branches: [ {(environment == "dev" ? "develop" : environment)} ]");
        }
        
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine($"    environment: {environment}");
        sb.AppendLine();
        sb.AppendLine("    steps:");
        sb.AppendLine("    - name: Checkout code");
        sb.AppendLine("      uses: actions/checkout@v4");
        sb.AppendLine();
        sb.AppendLine($"    - name: Deploy to {platformName}");
        sb.AppendLine($"      run: echo \"Deploy to {environment} environment on {platformName}\"");
        
        return sb.ToString();
    }
}
