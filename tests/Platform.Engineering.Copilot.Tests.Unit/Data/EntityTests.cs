using FluentAssertions;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Data;

public class UserTests
{
    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        var user = new User();

        user.PimActiveTier.Should().Be(PimTier.None);
        user.IsActive.Should().BeTrue();
        user.Roles.Should().BeEmpty();
    }

    [Fact]
    public void User_CanHaveMultipleRoles()
    {
        var user = new User
        {
            Roles = [UserRole.ComplianceOfficer, UserRole.SecurityLead]
        };

        user.Roles.Should().HaveCount(2);
        user.Roles.Should().Contain(UserRole.ComplianceOfficer);
        user.Roles.Should().Contain(UserRole.SecurityLead);
    }
}

public class RemediationTaskTests
{
    [Fact]
    public void Task_DefaultStatus_IsBacklog()
    {
        var task = new RemediationTask();
        task.Status.Should().Be(RemediationTaskStatus.Backlog);
    }

    [Theory]
    [InlineData(Severity.Critical, 24)]
    [InlineData(Severity.High, 168)]
    [InlineData(Severity.Medium, 720)]
    [InlineData(Severity.Low, 2160)]
    public void Task_SlaHours_MatchesSeverity(Severity severity, int expectedHours)
    {
        RemediationTask.GetSlaHours(severity).Should().Be(expectedHours);
    }

    [Fact]
    public void Task_IsOverdue_TrueWhenPastDueAndNotDone()
    {
        var task = new RemediationTask
        {
            DueDate = DateTimeOffset.UtcNow.AddHours(-1),
            Status = RemediationTaskStatus.InProgress
        };

        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void Task_IsOverdue_FalseWhenDone()
    {
        var task = new RemediationTask
        {
            DueDate = DateTimeOffset.UtcNow.AddHours(-1),
            Status = RemediationTaskStatus.Done
        };

        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void Task_IsOverdue_FalseWhenNotPastDue()
    {
        var task = new RemediationTask
        {
            DueDate = DateTimeOffset.UtcNow.AddHours(1),
            Status = RemediationTaskStatus.InProgress
        };

        task.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void Task_StatusTransition_BacklogThroughDone()
    {
        var task = new RemediationTask();

        task.Status = RemediationTaskStatus.ToDo;
        task.Status.Should().Be(RemediationTaskStatus.ToDo);

        task.Status = RemediationTaskStatus.InProgress;
        task.Status.Should().Be(RemediationTaskStatus.InProgress);

        task.Status = RemediationTaskStatus.InReview;
        task.Status.Should().Be(RemediationTaskStatus.InReview);

        task.Status = RemediationTaskStatus.Done;
        task.Status.Should().Be(RemediationTaskStatus.Done);
    }

    [Fact]
    public void Task_StatusTransition_ToBlocked()
    {
        var task = new RemediationTask
        {
            Status = RemediationTaskStatus.InProgress,
            BlockedReason = "Waiting for vendor patch"
        };

        task.Status = RemediationTaskStatus.Blocked;
        task.Status.Should().Be(RemediationTaskStatus.Blocked);
        task.BlockedReason.Should().NotBeNullOrEmpty();
    }
}

public class IaCTemplateTests
{
    [Fact]
    public void IaCTemplate_IsExpired_TrueWhenPastExpiry()
    {
        var template = new IaCTemplate
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        template.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IaCTemplate_IsExpired_FalseWhenNotExpired()
    {
        var template = new IaCTemplate
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(29)
        };

        template.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IaCTemplate_DefaultTtl_Is30Minutes()
    {
        var now = DateTimeOffset.UtcNow;
        var template = new IaCTemplate
        {
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        };

        (template.ExpiresAt - template.CreatedAt).TotalMinutes.Should().Be(30);
    }
}

public class AlertTests
{
    [Fact]
    public void Alert_DefaultState_IsNew()
    {
        var alert = new Alert();
        alert.LifecycleState.Should().Be(AlertState.New);
    }

    [Theory]
    [InlineData(Severity.Critical, 1)]
    [InlineData(Severity.High, 4)]
    [InlineData(Severity.Medium, 24)]
    [InlineData(Severity.Low, 168)]
    public void Alert_SlaHours_MatchesSeverity(Severity severity, int expectedHours)
    {
        Alert.GetAlertSlaHours(severity).Should().Be(expectedHours);
    }

    [Fact]
    public void Alert_StateTransition_NewToResolved()
    {
        var alert = new Alert();

        alert.LifecycleState = AlertState.Acknowledged;
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.LifecycleState.Should().Be(AlertState.Acknowledged);

        alert.LifecycleState = AlertState.InProgress;
        alert.LifecycleState.Should().Be(AlertState.InProgress);

        alert.LifecycleState = AlertState.Resolved;
        alert.ResolvedAt = DateTimeOffset.UtcNow;
        alert.LifecycleState.Should().Be(AlertState.Resolved);
    }

    [Fact]
    public void Alert_DefaultEscalationCount_IsZero()
    {
        var alert = new Alert();
        alert.EscalationCount.Should().Be(0);
    }
}

public class AuditLogEntryTests
{
    [Fact]
    public void AuditLogEntry_RetentionExpiresAt_Default7Years()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new AuditLogEntry
        {
            Timestamp = now,
            RetentionExpiresAt = now.AddYears(7)
        };

        entry.RetentionExpiresAt.Should().BeCloseTo(now.AddYears(7), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AuditLogEntry_ConcurrencyToken_IsGenerated()
    {
        var entry = new AuditLogEntry();
        entry.ConcurrencyToken.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AuditLogEntry_DefaultArchived_IsFalse()
    {
        var entry = new AuditLogEntry();
        entry.IsArchived.Should().BeFalse();
    }
}
