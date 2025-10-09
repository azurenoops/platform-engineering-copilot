using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Governance.Configuration;
using Platform.Engineering.Copilot.Governance.Models;
using Platform.Engineering.Copilot.Governance.Services;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Governance.Services.Governance;

/// <summary>
/// Comprehensive unit tests for GovernanceService
/// Tests policy enforcement, approval workflows, ATO rules, and Teams notifications
/// </summary>
public class GovernanceServiceTests
{
    private readonly Mock<ILogger<GovernanceService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly GovernanceOptions _options;
    private readonly string _testRulesPath;

    public GovernanceServiceTests()
    {
        _mockLogger = new Mock<ILogger<GovernanceService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        
        _testRulesPath = Path.Combine(Path.GetTempPath(), $"ato-rules-test-{Guid.NewGuid()}.json");
        
        _options = new GovernanceOptions
        {
            EnforcePolicies = true,
            RequireApprovals = true,
            ApprovalTimeoutMinutes = 5,
            AtoRulesPath = _testRulesPath,
            AzureSubscriptionId = "test-subscription-id",
            TeamsWebhookUrl = "https://example.com/webhook"
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var service = CreateGovernanceService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsNullReferenceException()
    {
        // Arrange, Act & Assert
        var act = () => new GovernanceService(
            _mockLogger.Object,
            null!,
            _httpClient);

        act.Should().Throw<NullReferenceException>();
    }

    #endregion

    #region CheckPolicyAsync Tests - Disabled Policies

    [Fact]
    public async Task CheckPolicyAsync_WithPoliciesDisabled_AllowsAllCalls()
    {
        // Arrange
        var options = new GovernanceOptions { EnforcePolicies = false };
        var service = new GovernanceService(_mockLogger.Object, Options.Create(options), _httpClient);
        var toolCall = CreateTestToolCall("provision_infrastructure");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RequiresApproval.Should().BeFalse();
        result.Violations.Should().BeEmpty();
    }

    #endregion

    #region CheckPolicyAsync Tests - ATO Rules

    [Fact]
    public async Task CheckPolicyAsync_WithBlockRule_DeniesAccess()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "block-test",
                Control = "SC-7",
                Description = "Block this tool",
                Action = "block",
                Match = new AtoRuleMatch { ToolName = "dangerous_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var service = CreateGovernanceService();
        await Task.Delay(100); // Give time for rules to load
        
        var toolCall = CreateTestToolCall("dangerous_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("block-test"));
    }

    [Fact]
    public async Task CheckPolicyAsync_WithDenyRule_DeniesAccess()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "deny-test",
                Control = "AC-3",
                Description = "Deny this action",
                Action = "deny",
                Match = new AtoRuleMatch { ToolName = "restricted_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var service = CreateGovernanceService();
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("restricted_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("deny-test"));
    }

    [Fact]
    public async Task CheckPolicyAsync_WithApprovalRule_RequiresApproval()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "approval-test",
                Control = "IA-5",
                Description = "Requires approval",
                Action = "require-approval",
                Match = new AtoRuleMatch { ToolName = "sensitive_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var service = CreateGovernanceService();
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("sensitive_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RequiresApproval.Should().BeTrue();
        result.Reason.Should().Contain("approval-test");
    }

    [Fact]
    public async Task CheckPolicyAsync_WithApprovalRule_WhenApprovalsDisabled_DoesNotRequireApproval()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "approval-test",
                Control = "IA-5",
                Description = "Requires approval",
                Action = "require-approval",
                Match = new AtoRuleMatch { ToolName = "sensitive_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var options = new GovernanceOptions
        {
            EnforcePolicies = true,
            RequireApprovals = false,
            AtoRulesPath = _testRulesPath
        };
        var service = new GovernanceService(_mockLogger.Object, Options.Create(options), _httpClient);
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("sensitive_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPolicyAsync_WithWarnRule_LogsWarningButAllows()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "warn-test",
                Control = "AU-2",
                Description = "Warning for this action",
                Action = "warn",
                Match = new AtoRuleMatch { ToolName = "monitored_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var service = CreateGovernanceService();
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("monitored_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RequiresApproval.Should().BeFalse();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckPolicyAsync_WithNoMatchingRules_AllowsAccess()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "other-rule",
                Control = "SI-4",
                Description = "Other tool rule",
                Action = "block",
                Match = new AtoRuleMatch { ToolName = "other_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var service = CreateGovernanceService();
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("allowed_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RequiresApproval.Should().BeFalse();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckPolicyAsync_WithMultipleMatchingRules_CombinesViolations()
    {
        // Arrange
        var rules = new[]
        {
            new AtoRule
            {
                RuleId = "rule-1",
                Control = "CP-9",
                Description = "First block",
                Action = "block",
                Match = new AtoRuleMatch { ToolName = "blocked_tool" }
            },
            new AtoRule
            {
                RuleId = "rule-2",
                Control = "CP-10",
                Description = "Second block",
                Action = "deny",
                Match = new AtoRuleMatch { ToolName = "blocked_tool" }
            }
        };
        await CreateRulesFile(rules);
        
        var service = CreateGovernanceService();
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("blocked_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeFalse();
        result.Violations.Should().HaveCount(2);
        result.Violations.Should().Contain(v => v.Contains("rule-1"));
        result.Violations.Should().Contain(v => v.Contains("rule-2"));
    }

    [Fact]
    public async Task CheckPolicyAsync_WithMissingRulesFile_AllowsAccess()
    {
        // Arrange
        var options = new GovernanceOptions
        {
            EnforcePolicies = true,
            RequireApprovals = true,
            AtoRulesPath = "/nonexistent/rules.json"
        };
        var service = new GovernanceService(_mockLogger.Object, Options.Create(options), _httpClient);
        await Task.Delay(100);
        
        var toolCall = CreateTestToolCall("any_tool");

        // Act
        var result = await service.CheckPolicyAsync(toolCall);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    #endregion

    #region RequestApprovalAsync Tests

    [Fact]
    public async Task RequestApprovalAsync_WithValidRequest_ReturnsApprovalResult()
    {
        // Arrange
        var service = CreateGovernanceService();
        var toolCall = CreateTestToolCall("test_tool");

        // Act
        var result = await service.RequestApprovalAsync(toolCall, "Test reason");

        // Assert
        result.Should().NotBeNull();
        result.ApprovalId.Should().NotBeNullOrEmpty();
        result.IsApproved.Should().BeTrue(); // Auto-approved in demo mode
        result.ApprovedBy.Should().NotBeNullOrEmpty();
        result.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestApprovalAsync_WithTeamsWebhook_SendsNotification()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == _options.TeamsWebhookUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var service = CreateGovernanceService();
        var toolCall = CreateTestToolCall("test_tool");

        // Act
        var result = await service.RequestApprovalAsync(toolCall, "Test reason");

        // Assert
        result.Should().NotBeNull();
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == _options.TeamsWebhookUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task RequestApprovalAsync_WithNoTeamsWebhook_DoesNotSendNotification()
    {
        // Arrange
        var options = new GovernanceOptions
        {
            EnforcePolicies = true,
            RequireApprovals = true,
            ApprovalTimeoutMinutes = 5,
            AtoRulesPath = _testRulesPath,
            TeamsWebhookUrl = null
        };
        var service = new GovernanceService(_mockLogger.Object, Options.Create(options), _httpClient);
        var toolCall = CreateTestToolCall("test_tool");

        // Act
        var result = await service.RequestApprovalAsync(toolCall, "Test reason");

        // Assert
        result.Should().NotBeNull();
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task RequestApprovalAsync_WithTeamsWebhookFailure_StillReturnsResult()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var service = CreateGovernanceService();
        var toolCall = CreateTestToolCall("test_tool");

        // Act
        var result = await service.RequestApprovalAsync(toolCall, "Test reason");

        // Assert
        result.Should().NotBeNull();
        result.ApprovalId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestApprovalAsync_WithHttpException_ReturnsErrorResult()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateGovernanceService();
        var toolCall = CreateTestToolCall("test_tool");

        // Act
        var result = await service.RequestApprovalAsync(toolCall, "Test reason");

        // Assert
        result.Should().NotBeNull();
        // Should still return a result even with HTTP error
        result.ApprovalId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    private GovernanceService CreateGovernanceService()
    {
        return new GovernanceService(
            _mockLogger.Object,
            Options.Create(_options),
            _httpClient);
    }

    private McpToolCall CreateTestToolCall(string toolName)
    {
        return new McpToolCall
        {
            Name = toolName,
            Arguments = new Dictionary<string, object>
            {
                { "param1", "value1" },
                { "param2", "value2" }
            }
        };
    }

    private async Task CreateRulesFile(AtoRule[] rules)
    {
        var json = JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_testRulesPath, json);
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (File.Exists(_testRulesPath))
        {
            File.Delete(_testRulesPath);
        }
    }

    #endregion
}
