using FluentAssertions;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Data.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Data;

/// <summary>
/// T126 — Unit tests for task transitions.
/// Blocked requires comment (FR-053), Done triggers validation scan, overdue highlighting, SLA calculation.
/// </summary>
public class RemediationTaskTransitionTests
{
    private readonly RemediationBoardService _service = new();

    private static RemediationTask CreateTask(
        RemediationTaskStatus status = RemediationTaskStatus.Backlog,
        Severity severity = Severity.High)
    {
        return new RemediationTask
        {
            TaskId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            FindingId = Guid.NewGuid(),
            DisplayId = "REM-001",
            Title = "Test task",
            Severity = severity,
            Status = status,
            DueDate = DateTimeOffset.UtcNow.AddDays(7),
            SlaHours = RemediationTask.GetSlaHours(severity),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void Transition_Backlog_To_ToDo_Succeeds()
    {
        var task = CreateTask(RemediationTaskStatus.Backlog);
        var result = _service.TransitionTask(task, RemediationTaskStatus.ToDo);
        result.Success.Should().BeTrue();
        task.Status.Should().Be(RemediationTaskStatus.ToDo);
    }

    [Fact]
    public void Transition_ToDo_To_InProgress_Succeeds()
    {
        var task = CreateTask(RemediationTaskStatus.ToDo);
        var result = _service.TransitionTask(task, RemediationTaskStatus.InProgress);
        result.Success.Should().BeTrue();
        task.Status.Should().Be(RemediationTaskStatus.InProgress);
    }

    [Fact]
    public void Transition_From_Done_Fails()
    {
        var task = CreateTask(RemediationTaskStatus.Done);
        var result = _service.TransitionTask(task, RemediationTaskStatus.Backlog);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Done");
    }

    [Fact]
    public void Blocked_Without_Comment_Fails_FR053()
    {
        var task = CreateTask(RemediationTaskStatus.InProgress);
        var result = _service.TransitionTask(task, RemediationTaskStatus.Blocked);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("comment");
    }

    [Fact]
    public void Blocked_With_Comment_Succeeds()
    {
        var task = CreateTask(RemediationTaskStatus.InProgress);
        var result = _service.TransitionTask(task, RemediationTaskStatus.Blocked,
            blockedReason: "Waiting for vendor patch");
        result.Success.Should().BeTrue();
        task.Status.Should().Be(RemediationTaskStatus.Blocked);
        task.BlockedReason.Should().Be("Waiting for vendor patch");
    }

    [Fact]
    public void Done_Triggers_ValidationScan_FR053()
    {
        var task = CreateTask(RemediationTaskStatus.InReview);
        var result = _service.TransitionTask(task, RemediationTaskStatus.Done);
        result.Success.Should().BeTrue();
        result.ValidationScanId.Should().NotBeNull();
        task.ValidationScanId.Should().NotBeNull();
    }

    [Fact]
    public void Done_With_Explicit_ScanId()
    {
        var scanId = Guid.NewGuid();
        var task = CreateTask(RemediationTaskStatus.InReview);
        var result = _service.TransitionTask(task, RemediationTaskStatus.Done,
            validationScanId: scanId);
        result.ValidationScanId.Should().Be(scanId);
        task.ValidationScanId.Should().Be(scanId);
    }

    [Fact]
    public void Unblocking_Clears_BlockedReason()
    {
        var task = CreateTask(RemediationTaskStatus.Blocked);
        task.BlockedReason = "Previous block";
        var result = _service.TransitionTask(task, RemediationTaskStatus.InProgress);
        result.Success.Should().BeTrue();
        task.BlockedReason.Should().BeNull();
    }

    [Fact]
    public void IsOverdue_True_When_PastDue_And_Not_Done()
    {
        var task = CreateTask();
        task.DueDate = DateTimeOffset.UtcNow.AddHours(-1);
        task.Status = RemediationTaskStatus.InProgress;
        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_False_When_Done()
    {
        var task = CreateTask();
        task.DueDate = DateTimeOffset.UtcNow.AddHours(-1);
        task.Status = RemediationTaskStatus.Done;
        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_False_When_FutureDue()
    {
        var task = CreateTask();
        task.DueDate = DateTimeOffset.UtcNow.AddDays(5);
        task.Status = RemediationTaskStatus.InProgress;
        task.IsOverdue.Should().BeFalse();
    }

    [Theory]
    [InlineData(Severity.Critical, 24)]
    [InlineData(Severity.High, 168)]
    [InlineData(Severity.Medium, 720)]
    [InlineData(Severity.Low, 2160)]
    public void GetSlaHours_Returns_Correct_Value(Severity severity, int expectedHours)
    {
        RemediationTask.GetSlaHours(severity).Should().Be(expectedHours);
    }

    [Fact]
    public void AddComment_Creates_Comment()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var comment = _service.AddComment(taskId, userId, "This needs attention");
        comment.TaskId.Should().Be(taskId);
        comment.UserId.Should().Be(userId);
        comment.Content.Should().Be("This needs attention");
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void EditComment_By_Owner_Succeeds()
    {
        var userId = Guid.NewGuid();
        var comment = _service.AddComment(Guid.NewGuid(), userId, "Original");
        var result = _service.EditComment(comment, userId, "Updated");
        result.Success.Should().BeTrue();
        comment.Content.Should().Be("Updated");
        comment.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void EditComment_By_NonOwner_Fails()
    {
        var comment = _service.AddComment(Guid.NewGuid(), Guid.NewGuid(), "Original");
        var result = _service.EditComment(comment, Guid.NewGuid(), "Hijacked");
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("owner");
    }

    [Fact]
    public void DeleteComment_By_Owner_Succeeds()
    {
        var userId = Guid.NewGuid();
        var comment = _service.AddComment(Guid.NewGuid(), userId, "To delete");
        var result = _service.DeleteComment(comment, userId, isComplianceOfficer: false);
        result.Success.Should().BeTrue();
        comment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void DeleteComment_By_ComplianceOfficer_Succeeds()
    {
        var comment = _service.AddComment(Guid.NewGuid(), Guid.NewGuid(), "To delete");
        var officerId = Guid.NewGuid();
        var result = _service.DeleteComment(comment, officerId, isComplianceOfficer: true);
        result.Success.Should().BeTrue();
        comment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void DeleteComment_By_NonOwner_NonOfficer_Fails()
    {
        var comment = _service.AddComment(Guid.NewGuid(), Guid.NewGuid(), "Protected");
        var result = _service.DeleteComment(comment, Guid.NewGuid(), isComplianceOfficer: false);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void AssignTask_Sets_AssigneeUserId()
    {
        var task = CreateTask();
        var userId = Guid.NewGuid();
        _service.AssignTask(task, userId);
        task.AssigneeUserId.Should().Be(userId);
    }
}
