using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit;

/// <summary>
/// Helper for constructing test tools with mocked execution behavior.
/// Provides TestTool — a concrete BaseTool implementation for testing.
/// </summary>
public static class BaseToolTestHelper
{
    /// <summary>
    /// Create a TestTool that returns a fixed result.
    /// </summary>
    public static TestTool CreateTool(
        string name = "test_tool",
        string description = "A test tool",
        string result = "{\"status\": \"success\"}",
        bool requiresAuth = false,
        PimTier pimTier = PimTier.None)
    {
        return new TestTool(name, description, result, requiresAuth, pimTier);
    }

    /// <summary>
    /// Create a TestTool that throws when executed.
    /// </summary>
    public static TestTool CreateFailingTool(
        string name = "failing_tool",
        Exception? exception = null)
    {
        return new TestTool(
            name, "A failing tool",
            result: null,
            exception: exception ?? new InvalidOperationException("Tool execution failed"));
    }

    /// <summary>
    /// Create a TestTool that records invocations for verification.
    /// </summary>
    public static RecordingTool CreateRecordingTool(
        string name = "recording_tool",
        string result = "{\"recorded\": true}")
    {
        return new RecordingTool(name, result);
    }
}

/// <summary>
/// Concrete BaseTool implementation for testing.
/// Returns a configured result or throws a configured exception.
/// </summary>
public class TestTool : BaseTool
{
    private readonly string? _result;
    private readonly Exception? _exception;

    public TestTool(
        string name,
        string description,
        string? result,
        bool requiresAuth = false,
        PimTier pimTier = PimTier.None,
        Exception? exception = null)
        : base(new Mock<ILogger<TestTool>>().Object)
    {
        Name = name;
        Description = description;
        RequiresAuthentication = requiresAuth;
        PimTierRequired = pimTier;
        _result = result;
        _exception = exception;
    }

    public override string Name { get; }
    public override string Description { get; }
    public override string Parameters => "{\"type\":\"object\",\"properties\":{}}";
    public override bool RequiresAuthentication { get; }
    public override PimTier PimTierRequired { get; }

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_exception is not null) throw _exception;
        return Task.FromResult(_result ?? "{}");
    }
}

/// <summary>
/// BaseTool implementation that records all invocations.
/// Use for verifying tool was called with expected parameters.
/// </summary>
public class RecordingTool : BaseTool
{
    private readonly string _result;
    private readonly List<Dictionary<string, object?>> _invocations = [];

    public RecordingTool(string name, string result)
        : base(new Mock<ILogger<RecordingTool>>().Object)
    {
        Name = name;
        _result = result;
    }

    public override string Name { get; }
    public override string Description => $"Recording tool: {Name}";
    public override string Parameters => "{\"type\":\"object\",\"properties\":{}}";

    /// <summary>All recorded invocations (parameter dictionaries).</summary>
    public IReadOnlyList<Dictionary<string, object?>> Invocations => _invocations.AsReadOnly();

    /// <summary>Number of times the tool was invoked.</summary>
    public int InvocationCount => _invocations.Count;

    /// <summary>Whether the tool was invoked at least once.</summary>
    public bool WasInvoked => _invocations.Count > 0;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _invocations.Add(new Dictionary<string, object?>(parameters));
        return Task.FromResult(_result);
    }
}
