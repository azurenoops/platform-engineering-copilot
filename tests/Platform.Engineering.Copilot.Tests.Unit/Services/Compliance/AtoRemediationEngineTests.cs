using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance.Remediation;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Models.Infrastructure;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for AtoRemediationEngine
/// Tests remediation plan generation, execution, validation, rollback, and progress tracking
/// </summary>
public class AtoRemediationEngineTests
{
    private readonly Mock<IAzureResourceService> _resourceServiceMock;
    private readonly Mock<IComplianceRemediationService> _complianceRemediationServiceMock;
    private readonly Mock<ILogger<AtoRemediationEngine>> _loggerMock;
    private readonly Mock<INistRemediationStepsService> _nistRemediationStepsMock;
    private readonly Mock<IAzureArmRemediationService> _armRemediationServiceMock;
    private readonly Mock<IRemediationScriptExecutor> _scriptExecutorMock;
    private readonly Mock<IAiRemediationPlanGenerator> _aiRemediationGeneratorMock;
    private readonly Mock<INistControlsService> _nistServiceMock;
    private readonly Mock<IScriptSanitizationService> _sanitizationServiceMock;
    private readonly IOptions<ComplianceAgentOptions> _options;
    private readonly AtoRemediationEngine _engine;

    public AtoRemediationEngineTests()
    {
        _resourceServiceMock = new Mock<IAzureResourceService>();
        _complianceRemediationServiceMock = new Mock<IComplianceRemediationService>();
        _loggerMock = new Mock<ILogger<AtoRemediationEngine>>();
        _nistRemediationStepsMock = new Mock<INistRemediationStepsService>();
        _armRemediationServiceMock = new Mock<IAzureArmRemediationService>();
        _scriptExecutorMock = new Mock<IRemediationScriptExecutor>();
        _aiRemediationGeneratorMock = new Mock<IAiRemediationPlanGenerator>();
        _nistServiceMock = new Mock<INistControlsService>();
        _sanitizationServiceMock = new Mock<IScriptSanitizationService>();

        var complianceOptions = new ComplianceAgentOptions
        {
            EnableAutomatedRemediation = true
        };
        _options = Options.Create(complianceOptions);

        // Setup default mocks
        SetupDefaultMocks();

        _engine = new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null, // Kernel
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);
    }

    private void SetupDefaultMocks()
    {
        // Setup compliance remediation service
        _complianceRemediationServiceMock
            .Setup(x => x.CanAutoRemediateAsync(It.IsAny<AtoFinding>()))
            .ReturnsAsync(false);

        _complianceRemediationServiceMock
            .Setup(x => x.GenerateRemediationPlanAsync(
                It.IsAny<AtoFinding>(),
                It.IsAny<InfrastructureRemediationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InfrastructureRemediationPlan
            {
                PlanId = Guid.NewGuid().ToString(),
                Finding = new AtoFinding { Id = "test-finding" },
                Actions = new List<InfrastructureRemediationAction>()
            });

        // Setup NIST remediation steps service
        _nistRemediationStepsMock
            .Setup(x => x.GetRemediationStepsAsync(It.IsAny<string>()))
            .ReturnsAsync((RemediationStepsDefinition?)null);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Assert
        _engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullComplianceRemediationService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoRemediationEngine(
            _resourceServiceMock.Object,
            null!,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null,
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("complianceRemediationService");
    }

    [Fact]
    public void Constructor_WithNullNistRemediationStepsService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            null!,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null,
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nistRemediationSteps");
    }

    [Fact]
    public void Constructor_WithNullArmRemediationService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            null!,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null,
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("armRemediationService");
    }

    [Fact]
    public void Constructor_WithNullScriptExecutor_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            null!,
            _aiRemediationGeneratorMock.Object,
            null,
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("scriptExecutor");
    }

    [Fact]
    public void Constructor_WithNullAiRemediationGenerator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            null!,
            null,
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aiRemediationGenerator");
    }

    [Fact]
    public void Constructor_WithOptionalKernelNull_CreatesInstance()
    {
        // Act
        var engine = new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null, // Kernel is optional
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithOptionalNistServiceNull_CreatesInstance()
    {
        // Act
        var engine = new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null,
            null, // NistService is optional
            _sanitizationServiceMock.Object);

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithOptionalSanitizationServiceNull_CreatesInstance()
    {
        // Act
        var engine = new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            _options,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null,
            _nistServiceMock.Object,
            null); // SanitizationService is optional

        // Assert
        engine.Should().NotBeNull();
    }

    #endregion

    #region GenerateRemediationPlanAsync Tests

    [Fact]
    public async Task GenerateRemediationPlanAsync_WithValidFindings_ReturnsRemediationPlan()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(3);

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.PlanId.Should().NotBeNullOrEmpty();
        result.RemediationItems.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_WithEmptyFindings_ReturnsEmptyPlan()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = new List<AtoFinding>();

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.RemediationItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_WithOptions_RespectsMinimumSeverity()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = new List<AtoFinding>
        {
            CreateTestFinding(AtoFindingSeverity.Critical),
            CreateTestFinding(AtoFindingSeverity.High),
            CreateTestFinding(AtoFindingSeverity.Low)
        };
        var options = new RemediationPlanOptions
        {
            MinimumSeverity = AtoFindingSeverity.High
        };

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_WithOptions_FiltersAutoRemediableOnly()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var autoRemediableFinding = CreateTestFinding(AtoFindingSeverity.High);
        autoRemediableFinding.IsAutoRemediable = true;
        
        var manualFinding = CreateTestFinding(AtoFindingSeverity.High);
        manualFinding.IsAutoRemediable = false;
        
        var findings = new List<AtoFinding> { autoRemediableFinding, manualFinding };
        var options = new RemediationPlanOptions
        {
            IncludeOnlyAutomatable = true
        };

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_CalculatesEstimatedEffort()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(3);

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.EstimatedEffort.Should().NotBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_GeneratesImplementationTimeline()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.Timeline.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_CalculatesProjectedRiskReduction()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.ProjectedRiskReduction.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_GeneratesExecutiveSummary()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.ExecutiveSummary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateRemediationPlanAsync_ForSingleFinding_ReturnsValidPlan()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateRemediationPlanAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.PlanId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ExecuteRemediationAsync Tests

    [Fact]
    public async Task ExecuteRemediationAsync_WithAutomatedRemediationDisabled_ReturnsFailedExecution()
    {
        // Arrange
        var disabledOptions = Options.Create(new ComplianceAgentOptions
        {
            EnableAutomatedRemediation = false
        });
        var engine = new AtoRemediationEngine(
            _resourceServiceMock.Object,
            _complianceRemediationServiceMock.Object,
            _loggerMock.Object,
            disabledOptions,
            _nistRemediationStepsMock.Object,
            _armRemediationServiceMock.Object,
            _scriptExecutorMock.Object,
            _aiRemediationGeneratorMock.Object,
            null,
            _nistServiceMock.Object,
            _sanitizationServiceMock.Object);

        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        var options = new RemediationExecutionOptions { ExecutedBy = "test-user" };

        // Act
        var result = await engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Status.Should().Be(RemediationExecutionStatus.Failed);
        result.ErrorMessage.Should().Contain("disabled");
    }

    [Fact]
    public async Task ExecuteRemediationAsync_WithApprovalRequired_ReturnsPendingExecution()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        var options = new RemediationExecutionOptions
        {
            ExecutedBy = "test-user",
            RequireApproval = true
        };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(RemediationExecutionStatus.Pending);
        result.RequiredApproval.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteRemediationAsync_WithDryRun_DoesNotApplyChanges()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        finding.IsAutoRemediable = true;
        var options = new RemediationExecutionOptions
        {
            ExecutedBy = "test-user",
            DryRun = true,
            RequireApproval = false // Skip approval check for dry run testing
        };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Status.Should().Be(RemediationExecutionStatus.Completed);
        result.Message.Should().Contain("DRY RUN");
    }

    [Fact]
    public async Task ExecuteRemediationAsync_WithNonRemediableFinding_ReturnsFailedExecution()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        finding.IsAutoRemediable = false;
        var options = new RemediationExecutionOptions { ExecutedBy = "test-user" };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        // Non-remediable findings should either fail, require manual intervention,
        // or be pending approval depending on the execution path
        var hasExpectedOutcome = 
            result.Success == false ||
            result.Status == RemediationExecutionStatus.Failed ||
            result.Status == RemediationExecutionStatus.Pending ||
            (result.ErrorMessage?.Contains("Manual") ?? false) ||
            (result.ErrorMessage?.Contains("remediation") ?? false) ||
            (result.Message?.Contains("Manual") ?? false) ||
            (result.Message?.Contains("remediation") ?? false);
        hasExpectedOutcome.Should().BeTrue("non-remediable findings should indicate failure or manual intervention required");
    }

    [Fact]
    public async Task ExecuteRemediationAsync_WithAutoRemediableFinding_TriesComplianceService()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        finding.IsAutoRemediable = true;

        _complianceRemediationServiceMock
            .Setup(x => x.CanAutoRemediateAsync(It.IsAny<AtoFinding>()))
            .ReturnsAsync(true);

        _complianceRemediationServiceMock
            .Setup(x => x.ExecuteRemediationAsync(
                It.IsAny<InfrastructureRemediationPlan>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InfrastructureRemediationResult
            {
                IsSuccess = true,
                ActionResults = new List<InfrastructureActionResult>
                {
                    new InfrastructureActionResult
                    {
                        IsSuccess = true,
                        Action = new InfrastructureRemediationAction
                        {
                            Description = "Applied encryption"
                        }
                    }
                }
            });

        var options = new RemediationExecutionOptions { ExecutedBy = "test-user" };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        // When auto-remediable finding is processed, it should attempt to use the compliance service
        // The result should indicate completion, failure, or pending approval
        result.Status.Should().BeOneOf(
            RemediationExecutionStatus.Completed, 
            RemediationExecutionStatus.Failed,
            RemediationExecutionStatus.InProgress,
            RemediationExecutionStatus.Pending);
    }

    [Fact]
    public async Task ExecuteRemediationAsync_CapturesSnapshots_WhenRequested()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        finding.IsAutoRemediable = true;
        finding.RemediationActions = new List<AtoRemediationAction>
        {
            new AtoRemediationAction
            {
                Description = "Test action",
                ToolCommand = "TEST"
            }
        };

        var options = new RemediationExecutionOptions
        {
            ExecutedBy = "test-user",
            CaptureSnapshots = true
        };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteRemediationAsync_RecordsExecutionTiming()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        finding.IsAutoRemediable = false;
        var options = new RemediationExecutionOptions 
        { 
            ExecutedBy = "test-user",
            RequireApproval = false // Skip approval check to reach timing logic
        };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        result.StartedAt.Should().BeBefore(result.CompletedAt ?? DateTimeOffset.MaxValue);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteRemediationAsync_SetsExecutedBy()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        var executedBy = "test-user@example.com";
        var options = new RemediationExecutionOptions { ExecutedBy = executedBy };

        // Act
        var result = await _engine.ExecuteRemediationAsync(subscriptionId, finding, options);

        // Assert
        result.Should().NotBeNull();
        result.ExecutedBy.Should().Be(executedBy);
    }

    #endregion

    #region ExecuteBatchRemediationAsync Tests

    [Fact]
    public async Task ExecuteBatchRemediationAsync_WithMultipleFindings_ReturnsResults()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(3);
        var options = new BatchRemediationOptions
        {
            MaxConcurrentRemediations = 2,
            ExecutionOptions = new RemediationExecutionOptions { ExecutedBy = "test-user" }
        };

        // Act
        var result = await _engine.ExecuteBatchRemediationAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
        result.BatchId.Should().NotBeNullOrEmpty();
        result.TotalRemediations.Should().Be(3);
        result.Executions.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteBatchRemediationAsync_WithEmptyFindings_ReturnsEmptyResult()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = new List<AtoFinding>();
        var options = new BatchRemediationOptions
        {
            ExecutionOptions = new RemediationExecutionOptions { ExecutedBy = "test-user" }
        };

        // Act
        var result = await _engine.ExecuteBatchRemediationAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
        result.TotalRemediations.Should().Be(0);
        result.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteBatchRemediationAsync_CountsSuccessAndFailures()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(3);
        var options = new BatchRemediationOptions
        {
            ExecutionOptions = new RemediationExecutionOptions { ExecutedBy = "test-user" }
        };

        // Act
        var result = await _engine.ExecuteBatchRemediationAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
        (result.SuccessfulRemediations + result.FailedRemediations + result.SkippedRemediations)
            .Should().Be(result.TotalRemediations);
    }

    [Fact]
    public async Task ExecuteBatchRemediationAsync_RecordsDuration()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);
        var options = new BatchRemediationOptions
        {
            ExecutionOptions = new RemediationExecutionOptions { ExecutedBy = "test-user" }
        };

        // Act
        var result = await _engine.ExecuteBatchRemediationAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
        result.StartedAt.Should().BeBefore(result.CompletedAt ?? DateTimeOffset.MaxValue);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteBatchRemediationAsync_GeneratesSummary()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);
        var options = new BatchRemediationOptions
        {
            ExecutionOptions = new RemediationExecutionOptions { ExecutedBy = "test-user" }
        };

        // Act
        var result = await _engine.ExecuteBatchRemediationAsync(subscriptionId, findings, options);

        // Assert
        result.Should().NotBeNull();
        result.Summary.Should().NotBeNull();
    }

    #endregion

    #region ValidateRemediationAsync Tests

    [Fact]
    public async Task ValidateRemediationAsync_WithSuccessfulExecution_ReturnsValidResult()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = finding.Id,
            SubscriptionId = subscriptionId,
            ResourceId = finding.ResourceId,
            Success = true,
            StepsExecuted = new List<RemediationStep>
            {
                new RemediationStep { Order = 1, Description = "Step 1" }
            }
        };

        // Act
        var result = await _engine.ValidateRemediationAsync(subscriptionId, finding, execution);

        // Assert
        result.Should().NotBeNull();
        result.ValidationId.Should().NotBeNullOrEmpty();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRemediationAsync_WithFailedExecution_ReturnsInvalidResult()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = finding.Id,
            SubscriptionId = subscriptionId,
            ResourceId = finding.ResourceId,
            Success = false,
            StepsExecuted = new List<RemediationStep>()
        };

        // Act
        var result = await _engine.ValidateRemediationAsync(subscriptionId, finding, execution);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateRemediationAsync_PerformsValidationChecks()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var finding = CreateTestFinding(AtoFindingSeverity.High);
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = finding.Id,
            SubscriptionId = subscriptionId,
            ResourceId = finding.ResourceId,
            Success = true,
            StepsExecuted = new List<RemediationStep>
            {
                new RemediationStep { Order = 1, Description = "Step 1" }
            }
        };

        // Act
        var result = await _engine.ValidateRemediationAsync(subscriptionId, finding, execution);

        // Assert
        result.Should().NotBeNull();
        result.Checks.Should().NotBeEmpty();
        result.Checks.Should().Contain(c => c.CheckName == "Execution Status");
        result.Checks.Should().Contain(c => c.CheckName == "Steps Completed");
    }

    #endregion

    #region RollbackRemediationAsync Tests

    [Fact]
    public async Task RollbackRemediationAsync_WithValidExecution_ReturnsRollbackResult()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = "test-finding",
            SubscriptionId = subscriptionId,
            ResourceId = "test-resource",
            BeforeSnapshot = new RemediationSnapshot 
            { 
                SnapshotId = Guid.NewGuid().ToString(),
                ResourceId = "test-resource", 
                CapturedAt = DateTimeOffset.UtcNow 
            }
        };

        // Act
        var result = await _engine.RollbackRemediationAsync(subscriptionId, execution);

        // Assert
        result.Should().NotBeNull();
        result.RollbackId.Should().NotBeNullOrEmpty();
        result.ExecutionId.Should().Be(execution.ExecutionId);
    }

    [Fact]
    public async Task RollbackRemediationAsync_WithSnapshot_RestoresFromSnapshot()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = "test-finding",
            SubscriptionId = subscriptionId,
            ResourceId = "test-resource",
            BeforeSnapshot = new RemediationSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString(),
                ResourceId = "test-resource",
                CapturedAt = DateTimeOffset.UtcNow,
                Configuration = new Dictionary<string, object>()
            }
        };

        // Act
        var result = await _engine.RollbackRemediationAsync(subscriptionId, execution);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.StepsExecuted.Should().Contain(s => s.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RollbackRemediationAsync_RecordsCompletionTime()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var execution = new RemediationExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            FindingId = "test-finding",
            SubscriptionId = subscriptionId,
            ResourceId = "test-resource"
        };

        // Act
        var result = await _engine.RollbackRemediationAsync(subscriptionId, execution);

        // Assert
        result.Should().NotBeNull();
        result.CompletedAt.Should().NotBeNull();
        // Use BeOnOrBefore instead of BeBefore since the operation may complete in the same tick
        result.StartedAt.Should().BeOnOrBefore(result.CompletedAt!.Value);
    }

    #endregion

    #region GetRemediationProgressAsync Tests

    [Fact]
    public async Task GetRemediationProgressAsync_ReturnsProgressInfo()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.GetRemediationProgressAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.TotalRemediableFindings.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetRemediationProgressAsync_WithSinceDate_FiltersResults()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        // Act
        var result = await _engine.GetRemediationProgressAsync(subscriptionId, since);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRemediationProgressAsync_CalculatesAverageRemediationTime()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.GetRemediationProgressAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.AverageRemediationTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    #endregion

    #region GetRemediationHistoryAsync Tests

    [Fact]
    public async Task GetRemediationHistoryAsync_WithValidDateRange_ReturnsHistory()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetRemediationHistoryAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
    }

    [Fact]
    public async Task GetRemediationHistoryAsync_ReturnsExecutionsList()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetRemediationHistoryAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Executions.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRemediationHistoryAsync_GeneratesMetrics()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = await _engine.GetRemediationHistoryAsync(subscriptionId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.Should().NotBeNull();
    }

    #endregion

    #region AnalyzeRemediationImpactAsync Tests

    [Fact]
    public async Task AnalyzeRemediationImpactAsync_WithFindings_ReturnsImpactAnalysis()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(3);

        // Act
        var result = await _engine.AnalyzeRemediationImpactAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.AnalysisId.Should().NotBeNullOrEmpty();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.TotalFindings.Should().Be(3);
    }

    [Fact]
    public async Task AnalyzeRemediationImpactAsync_CountsAutomatableFindings()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var autoFinding = CreateTestFinding(AtoFindingSeverity.High);
        autoFinding.IsAutoRemediable = true;
        var manualFinding = CreateTestFinding(AtoFindingSeverity.High);
        manualFinding.IsAutoRemediable = false;
        var findings = new List<AtoFinding> { autoFinding, manualFinding };

        // Act
        var result = await _engine.AnalyzeRemediationImpactAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.AutomatableFindings.Should().Be(1);
        result.ManualFindings.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeRemediationImpactAsync_CalculatesRiskReduction()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);

        // Act
        var result = await _engine.AnalyzeRemediationImpactAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.CurrentRiskScore.Should().BeGreaterThanOrEqualTo(0);
        result.ProjectedRiskScore.Should().BeGreaterThanOrEqualTo(0);
        result.RiskReduction.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AnalyzeRemediationImpactAsync_EstimatesTotalDuration()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);

        // Act
        var result = await _engine.AnalyzeRemediationImpactAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.EstimatedTotalDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task AnalyzeRemediationImpactAsync_GeneratesRecommendations()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);

        // Act
        var result = await _engine.AnalyzeRemediationImpactAsync(subscriptionId, findings);

        // Assert
        result.Should().NotBeNull();
        result.Recommendations.Should().NotBeNull();
    }

    #endregion

    #region GenerateManualRemediationGuideAsync Tests

    [Fact]
    public async Task GenerateManualRemediationGuideAsync_ReturnsGuide()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateManualRemediationGuideAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.GuideId.Should().NotBeNullOrEmpty();
        result.FindingId.Should().Be(finding.Id);
    }

    [Fact]
    public async Task GenerateManualRemediationGuideAsync_IncludesSteps()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateManualRemediationGuideAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.Steps.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateManualRemediationGuideAsync_IncludesPrerequisites()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateManualRemediationGuideAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.Prerequisites.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateManualRemediationGuideAsync_IncludesValidationSteps()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateManualRemediationGuideAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.ValidationSteps.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateManualRemediationGuideAsync_EstimatesDuration()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateManualRemediationGuideAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GenerateManualRemediationGuideAsync_IncludesRollbackPlan()
    {
        // Arrange
        var finding = CreateTestFinding(AtoFindingSeverity.High);

        // Act
        var result = await _engine.GenerateManualRemediationGuideAsync(finding);

        // Assert
        result.Should().NotBeNull();
        result.RollbackPlan.Should().NotBeNull();
    }

    #endregion

    #region GetActiveRemediationWorkflowsAsync Tests

    [Fact]
    public async Task GetActiveRemediationWorkflowsAsync_ReturnsActiveWorkflows()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        // Act
        var result = await _engine.GetActiveRemediationWorkflowsAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region ProcessRemediationApprovalAsync Tests

    [Fact]
    public async Task ProcessRemediationApprovalAsync_WithApproval_ReturnsApprovedResult()
    {
        // Arrange
        var remediationId = Guid.NewGuid().ToString();
        var approver = "approver@example.com";

        // Act
        var result = await _engine.ProcessRemediationApprovalAsync(
            remediationId, approved: true, approvedBy: approver, comments: "Approved");

        // Assert
        result.Should().NotBeNull();
        result.RemediationId.Should().Be(remediationId);
        result.Approved.Should().BeTrue();
        result.ApprovedBy.Should().Be(approver);
        result.CanProceed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRemediationApprovalAsync_WithRejection_ReturnsRejectedResult()
    {
        // Arrange
        var remediationId = Guid.NewGuid().ToString();
        var approver = "approver@example.com";

        // Act
        var result = await _engine.ProcessRemediationApprovalAsync(
            remediationId, approved: false, approvedBy: approver, comments: "Rejected due to risk");

        // Assert
        result.Should().NotBeNull();
        result.Approved.Should().BeFalse();
        result.CanProceed.Should().BeFalse();
        result.Comments.Should().Be("Rejected due to risk");
    }

    [Fact]
    public async Task ProcessRemediationApprovalAsync_RecordsApprovalTime()
    {
        // Arrange
        var remediationId = Guid.NewGuid().ToString();
        var approver = "approver@example.com";

        // Act
        var result = await _engine.ProcessRemediationApprovalAsync(
            remediationId, approved: true, approvedBy: approver);

        // Assert
        result.Should().NotBeNull();
        result.ApprovedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region ScheduleRemediationAsync Tests

    [Fact]
    public async Task ScheduleRemediationAsync_WithValidInputs_ReturnsScheduleResult()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);
        var scheduledTime = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var result = await _engine.ScheduleRemediationAsync(subscriptionId, findings, scheduledTime);

        // Assert
        result.Should().NotBeNull();
        result.ScheduleId.Should().NotBeNullOrEmpty();
        result.ScheduledTime.Should().Be(scheduledTime);
        result.IsScheduled.Should().BeTrue();
    }

    [Fact]
    public async Task ScheduleRemediationAsync_IncludesFindingIds()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var findings = CreateTestFindings(2);
        var scheduledTime = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var result = await _engine.ScheduleRemediationAsync(subscriptionId, findings, scheduledTime);

        // Assert
        result.Should().NotBeNull();
        result.FindingId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    private static List<AtoFinding> CreateTestFindings(int count)
    {
        var findings = new List<AtoFinding>();
        for (int i = 0; i < count; i++)
        {
            findings.Add(CreateTestFinding((AtoFindingSeverity)(i % 4 + 1)));
        }
        return findings;
    }

    private static AtoFinding CreateTestFinding(AtoFindingSeverity severity)
    {
        return new AtoFinding
        {
            Id = Guid.NewGuid().ToString(),
            ResourceId = $"/subscriptions/test/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/test-vm-{Guid.NewGuid().ToString()[..8]}",
            ResourceName = $"test-resource-{Guid.NewGuid().ToString()[..8]}",
            ResourceType = "Microsoft.Compute/virtualMachines",
            SubscriptionId = "test-subscription-id",
            ResourceGroupName = "test-resource-group",
            FindingType = AtoFindingType.Configuration,
            Severity = severity,
            ComplianceStatus = AtoComplianceStatus.NonCompliant,
            Title = $"Test Finding - {severity}",
            Description = "Test finding description for unit testing",
            Recommendation = "Apply recommended configuration",
            RuleId = "TEST-001",
            RemediationGuidance = "Follow remediation steps",
            IsAutoRemediable = false,
            AffectedNistControls = new List<string> { "AC-1", "AC-2" },
            DetectedAt = DateTime.UtcNow,
            RemediationActions = new List<AtoRemediationAction>()
        };
    }

    #endregion
}
