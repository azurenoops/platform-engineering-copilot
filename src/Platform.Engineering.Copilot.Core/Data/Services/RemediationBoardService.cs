using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// Service for creating and managing remediation boards.
/// Creates boards from assessment findings, manages task transitions,
/// and enforces SLA/workflow rules per FR-050–FR-056.
/// </summary>
public class RemediationBoardService
{
    /// <summary>6-column board: Backlog, ToDo, InProgress, InReview, Blocked, Done.</summary>
    public static readonly RemediationTaskStatus[] BoardColumns =
    [
        RemediationTaskStatus.Backlog,
        RemediationTaskStatus.ToDo,
        RemediationTaskStatus.InProgress,
        RemediationTaskStatus.InReview,
        RemediationTaskStatus.Blocked,
        RemediationTaskStatus.Done
    ];

    /// <summary>
    /// Create a remediation board from assessment findings.
    /// Auto-generates REM-### display IDs, derives titles, sets SLA-based due dates.
    /// </summary>
    public RemediationBoard CreateBoard(
        Guid assessmentId,
        Guid userId,
        string title,
        IReadOnlyList<FindingInput> findings)
    {
        var now = DateTimeOffset.UtcNow;
        var board = new RemediationBoard
        {
            BoardId = Guid.NewGuid(),
            AssessmentId = assessmentId,
            UserId = userId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (int i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            var slaHours = RemediationTask.GetSlaHours(finding.Severity);
            var task = new RemediationTask
            {
                TaskId = Guid.NewGuid(),
                BoardId = board.BoardId,
                FindingId = finding.FindingId,
                DisplayId = $"REM-{(i + 1):D3}",
                Title = finding.Title,
                Severity = finding.Severity,
                Status = RemediationTaskStatus.Backlog,
                DueDate = now.AddHours(slaHours),
                SlaHours = slaHours,
                CreatedAt = now,
                UpdatedAt = now
            };
            board.Tasks.Add(task);
        }

        return board;
    }

    /// <summary>
    /// Transition a task to a new status. Validates workflow rules:
    /// - Cannot transition from Done.
    /// - Blocked requires a comment (FR-053).
    /// - Done triggers validation scan (sets ValidationScanId).
    /// Returns a TransitionResult with success/failure and optional scan ID.
    /// </summary>
    public TransitionResult TransitionTask(
        RemediationTask task,
        RemediationTaskStatus newStatus,
        string? blockedReason = null,
        Guid? validationScanId = null)
    {
        if (task.Status == RemediationTaskStatus.Done)
            return TransitionResult.Fail("Cannot transition from Done status.");

        if (newStatus == RemediationTaskStatus.Blocked && string.IsNullOrWhiteSpace(blockedReason))
            return TransitionResult.Fail("Blocked status requires a comment explaining the blocker (FR-053).");

        task.Status = newStatus;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        if (newStatus == RemediationTaskStatus.Blocked)
            task.BlockedReason = blockedReason;
        else
            task.BlockedReason = null;

        if (newStatus == RemediationTaskStatus.Done)
        {
            // Done triggers a validation scan (FR-053)
            task.ValidationScanId = validationScanId ?? Guid.NewGuid();
            return TransitionResult.SuccessWithScan(task.ValidationScanId.Value);
        }

        return TransitionResult.Ok();
    }

    /// <summary>
    /// Add a comment to a task. Comments are unlimited (FR-054).
    /// </summary>
    public TaskComment AddComment(Guid taskId, Guid userId, string content)
    {
        var now = DateTimeOffset.UtcNow;
        return new TaskComment
        {
            CommentId = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            Content = content,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Edit a comment. Only the comment owner can edit (FR-054).
    /// </summary>
    public EditResult EditComment(TaskComment comment, Guid requestingUserId, string newContent)
    {
        if (comment.UserId != requestingUserId)
            return EditResult.Fail("Only the comment owner can edit their comment.");

        if (comment.IsDeleted)
            return EditResult.Fail("Cannot edit a deleted comment.");

        comment.Content = newContent;
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        return EditResult.Ok();
    }

    /// <summary>
    /// Delete a comment. Owner can delete own; ComplianceOfficer can delete any (FR-054).
    /// </summary>
    public EditResult DeleteComment(TaskComment comment, Guid requestingUserId, bool isComplianceOfficer)
    {
        if (comment.IsDeleted)
            return EditResult.Fail("Comment is already deleted.");

        if (comment.UserId != requestingUserId && !isComplianceOfficer)
            return EditResult.Fail("Only the comment owner or a Compliance Officer can delete this comment.");

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        return EditResult.Ok();
    }

    /// <summary>
    /// Get board summary grouped by column status.
    /// </summary>
    public BoardSummary GetBoardSummary(RemediationBoard board)
    {
        var tasks = board.Tasks.ToList();
        return new BoardSummary
        {
            BoardId = board.BoardId,
            Title = board.Title,
            TotalTasks = tasks.Count,
            OverdueTasks = tasks.Count(t => t.IsOverdue),
            Columns = BoardColumns.Select(status => new ColumnSummary
            {
                Status = status,
                TaskCount = tasks.Count(t => t.Status == status),
                Tasks = tasks.Where(t => t.Status == status)
                    .Select(t => new TaskSummary
                    {
                        DisplayId = t.DisplayId,
                        Title = t.Title,
                        Severity = t.Severity,
                        IsOverdue = t.IsOverdue,
                        DueDate = t.DueDate,
                        AssigneeUserId = t.AssigneeUserId,
                        BlockedReason = t.BlockedReason
                    }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Assign a user to a task (FR-055).
    /// </summary>
    public void AssignTask(RemediationTask task, Guid userId)
    {
        task.AssigneeUserId = userId;
        task.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>Input DTO for creating remediation tasks from findings.</summary>
public record FindingInput(Guid FindingId, string Title, Severity Severity);

/// <summary>Result of a task state transition.</summary>
public class TransitionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Guid? ValidationScanId { get; init; }

    public static TransitionResult Ok() => new() { Success = true };
    public static TransitionResult Fail(string error) => new() { Success = false, Error = error };
    public static TransitionResult SuccessWithScan(Guid scanId) =>
        new() { Success = true, ValidationScanId = scanId };
}

/// <summary>Result of a comment edit/delete operation.</summary>
public class EditResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static EditResult Ok() => new() { Success = true };
    public static EditResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>Board overview with column groupings.</summary>
public class BoardSummary
{
    public Guid BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int OverdueTasks { get; set; }
    public List<ColumnSummary> Columns { get; set; } = [];
}

/// <summary>Column summary in the board.</summary>
public class ColumnSummary
{
    public RemediationTaskStatus Status { get; set; }
    public int TaskCount { get; set; }
    public List<TaskSummary> Tasks { get; set; } = [];
}

/// <summary>Task card summary for board display.</summary>
public class TaskSummary
{
    public string DisplayId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public string? BlockedReason { get; set; }
}
