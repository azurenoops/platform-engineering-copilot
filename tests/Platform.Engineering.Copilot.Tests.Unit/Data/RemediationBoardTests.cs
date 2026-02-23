using FluentAssertions;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Data.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Data;

/// <summary>
/// T125 — Unit tests for RemediationBoard creation.
/// Board from assessment findings, task card generation (REM-###, severity, SLA dates), 6 columns.
/// </summary>
public class RemediationBoardTests
{
    private readonly RemediationBoardService _service = new();

    private static List<FindingInput> SampleFindings() =>
    [
        new(Guid.NewGuid(), "Enable MFA for all administrator accounts", Severity.Critical),
        new(Guid.NewGuid(), "Encrypt storage at rest", Severity.High),
        new(Guid.NewGuid(), "Configure network segmentation", Severity.Medium),
        new(Guid.NewGuid(), "Update documentation", Severity.Low)
    ];

    [Fact]
    public void CreateBoard_Returns_Board_With_Correct_Properties()
    {
        var assessmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var board = _service.CreateBoard(assessmentId, userId, "Test Board", SampleFindings());

        board.BoardId.Should().NotBeEmpty();
        board.AssessmentId.Should().Be(assessmentId);
        board.UserId.Should().Be(userId);
        board.Title.Should().Be("Test Board");
        board.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateBoard_Generates_Tasks_For_Each_Finding()
    {
        var findings = SampleFindings();
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", findings);

        board.Tasks.Should().HaveCount(findings.Count);
    }

    [Fact]
    public void Tasks_Have_Sequential_REM_Display_Ids()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var displayIds = board.Tasks.Select(t => t.DisplayId).ToList();

        displayIds.Should().Contain("REM-001");
        displayIds.Should().Contain("REM-002");
        displayIds.Should().Contain("REM-003");
        displayIds.Should().Contain("REM-004");
    }

    [Fact]
    public void Tasks_Derive_Titles_From_Findings()
    {
        var findings = SampleFindings();
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", findings);
        var titles = board.Tasks.Select(t => t.Title).ToList();

        titles.Should().Contain("Enable MFA for all administrator accounts");
        titles.Should().Contain("Encrypt storage at rest");
    }

    [Fact]
    public void Tasks_Mirror_Finding_Severity()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var severities = board.Tasks.Select(t => t.Severity).ToList();

        severities.Should().Contain(Severity.Critical);
        severities.Should().Contain(Severity.High);
        severities.Should().Contain(Severity.Medium);
        severities.Should().Contain(Severity.Low);
    }

    [Fact]
    public void Critical_Task_SLA_Is_24_Hours()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var criticalTask = board.Tasks.First(t => t.Severity == Severity.Critical);

        criticalTask.SlaHours.Should().Be(24);
        criticalTask.DueDate.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void High_Task_SLA_Is_7_Days()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var highTask = board.Tasks.First(t => t.Severity == Severity.High);
        highTask.SlaHours.Should().Be(168);
    }

    [Fact]
    public void Medium_Task_SLA_Is_30_Days()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var medTask = board.Tasks.First(t => t.Severity == Severity.Medium);
        medTask.SlaHours.Should().Be(720);
    }

    [Fact]
    public void Low_Task_SLA_Is_90_Days()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var lowTask = board.Tasks.First(t => t.Severity == Severity.Low);
        lowTask.SlaHours.Should().Be(2160);
    }

    [Fact]
    public void All_Tasks_Start_In_Backlog()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        board.Tasks.Should().AllSatisfy(t =>
            t.Status.Should().Be(RemediationTaskStatus.Backlog));
    }

    [Fact]
    public void BoardColumns_Has_Six_Columns()
    {
        RemediationBoardService.BoardColumns.Should().HaveCount(6);
        RemediationBoardService.BoardColumns.Should().Contain(RemediationTaskStatus.Backlog);
        RemediationBoardService.BoardColumns.Should().Contain(RemediationTaskStatus.ToDo);
        RemediationBoardService.BoardColumns.Should().Contain(RemediationTaskStatus.InProgress);
        RemediationBoardService.BoardColumns.Should().Contain(RemediationTaskStatus.InReview);
        RemediationBoardService.BoardColumns.Should().Contain(RemediationTaskStatus.Blocked);
        RemediationBoardService.BoardColumns.Should().Contain(RemediationTaskStatus.Done);
    }

    [Fact]
    public void GetBoardSummary_Groups_Tasks_By_Column()
    {
        var board = _service.CreateBoard(Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var summary = _service.GetBoardSummary(board);

        summary.TotalTasks.Should().Be(4);
        summary.Columns.Should().HaveCount(6);
        summary.Columns.First(c => c.Status == RemediationTaskStatus.Backlog)
            .TaskCount.Should().Be(4);
    }
}
