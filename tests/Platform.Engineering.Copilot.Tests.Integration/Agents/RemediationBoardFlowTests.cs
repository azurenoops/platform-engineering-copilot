using FluentAssertions;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Data.Services;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T127 — Integration test for board creation + task workflow.
/// Create board → move tasks → add comments → complete task with validation scan.
/// </summary>
public class RemediationBoardFlowTests
{
    private readonly RemediationBoardService _service = new();

    private static List<FindingInput> SampleFindings() =>
    [
        new(Guid.NewGuid(), "Enable MFA", Severity.Critical),
        new(Guid.NewGuid(), "Enable encryption", Severity.High),
        new(Guid.NewGuid(), "Configure NSG rules", Severity.Medium)
    ];

    [Fact]
    public void Full_Board_Workflow_CreateToComplete()
    {
        // Step 1: Create board from assessment findings
        var board = _service.CreateBoard(
            Guid.NewGuid(), Guid.NewGuid(), "Assessment Remediation", SampleFindings());
        board.Tasks.Should().HaveCount(3);

        // Step 2: Move critical task through pipeline
        var criticalTask = board.Tasks.First(t => t.Severity == Severity.Critical);
        criticalTask.DisplayId.Should().StartWith("REM-");

        _service.TransitionTask(criticalTask, RemediationTaskStatus.ToDo).Success.Should().BeTrue();
        _service.TransitionTask(criticalTask, RemediationTaskStatus.InProgress).Success.Should().BeTrue();

        // Step 3: Add comment during work
        var comment = _service.AddComment(criticalTask.TaskId, Guid.NewGuid(), "Working on MFA setup");
        comment.Content.Should().Contain("MFA");

        // Step 4: Move to review then done (triggers validation scan)
        _service.TransitionTask(criticalTask, RemediationTaskStatus.InReview).Success.Should().BeTrue();
        var doneResult = _service.TransitionTask(criticalTask, RemediationTaskStatus.Done);
        doneResult.Success.Should().BeTrue();
        doneResult.ValidationScanId.Should().NotBeNull();

        // Step 5: Verify board summary
        var summary = _service.GetBoardSummary(board);
        summary.Columns.First(c => c.Status == RemediationTaskStatus.Done).TaskCount.Should().Be(1);
        summary.Columns.First(c => c.Status == RemediationTaskStatus.Backlog).TaskCount.Should().Be(2);
    }

    [Fact]
    public void Blocked_Task_Requires_Reason_Then_Unblock()
    {
        var board = _service.CreateBoard(
            Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());
        var task = board.Tasks.First();

        _service.TransitionTask(task, RemediationTaskStatus.InProgress);

        // Block without reason should fail
        var blockResult = _service.TransitionTask(task, RemediationTaskStatus.Blocked);
        blockResult.Success.Should().BeFalse();

        // Block with reason should succeed
        var blockWithReason = _service.TransitionTask(task, RemediationTaskStatus.Blocked,
            blockedReason: "Waiting for vendor response");
        blockWithReason.Success.Should().BeTrue();
        task.BlockedReason.Should().Be("Waiting for vendor response");

        // Unblock
        var unblock = _service.TransitionTask(task, RemediationTaskStatus.InProgress);
        unblock.Success.Should().BeTrue();
        task.BlockedReason.Should().BeNull();
    }

    [Fact]
    public void Comment_Management_CRUD_Flow()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        // Create
        var comment = _service.AddComment(taskId, ownerId, "Initial comment");
        comment.Content.Should().Be("Initial comment");

        // Edit by owner — OK
        _service.EditComment(comment, ownerId, "Updated comment").Success.Should().BeTrue();
        comment.Content.Should().Be("Updated comment");

        // Edit by non-owner — FAIL
        _service.EditComment(comment, otherId, "Hacked").Success.Should().BeFalse();
        comment.Content.Should().Be("Updated comment"); // unchanged

        // Delete by non-owner non-officer — FAIL
        _service.DeleteComment(comment, otherId, false).Success.Should().BeFalse();

        // Delete by compliance officer — OK
        _service.DeleteComment(comment, otherId, true).Success.Should().BeTrue();
        comment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Board_Summary_Shows_Overdue_Tasks()
    {
        var board = _service.CreateBoard(
            Guid.NewGuid(), Guid.NewGuid(), "Board", SampleFindings());

        // Force one task to be overdue
        var task = board.Tasks.First();
        task.DueDate = DateTimeOffset.UtcNow.AddHours(-1);
        _service.TransitionTask(task, RemediationTaskStatus.InProgress);

        var summary = _service.GetBoardSummary(board);
        summary.OverdueTasks.Should().BeGreaterThan(0);

        // The overdue task should be flagged in summary
        var overdueColumn = summary.Columns.First(c => c.Status == RemediationTaskStatus.InProgress);
        overdueColumn.Tasks.Should().Contain(t => t.IsOverdue);
    }
}
