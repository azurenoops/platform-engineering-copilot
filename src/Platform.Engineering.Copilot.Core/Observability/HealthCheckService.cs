using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using HealthStatus = Platform.Engineering.Copilot.Core.Data.Enumerations.HealthStatus;

namespace Platform.Engineering.Copilot.Core.Observability;

/// <summary>
/// Health check service returning per-agent availability (Healthy/Degraded/Unavailable)
/// within 2 seconds per FR-075 and SC-013.
/// Exposes /health endpoint data and supports agent-specific checks.
/// </summary>
public class HealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly Dictionary<string, AgentHealthStatus> _agentStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public HealthCheckService(ILogger<HealthCheckService> logger)
    {
        _logger = logger;
    }

    /// <summary>Register an agent for health monitoring.</summary>
    public void RegisterAgent(string agentId, string agentName)
    {
        lock (_lock)
        {
            _agentStatuses[agentId] = new AgentHealthStatus
            {
                AgentId = agentId,
                AgentName = agentName,
                Status = HealthStatus.Healthy,
                LastChecked = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>Update an agent's health status.</summary>
    public void UpdateStatus(string agentId, HealthStatus status, string? message = null)
    {
        lock (_lock)
        {
            if (_agentStatuses.TryGetValue(agentId, out var agentStatus))
            {
                agentStatus.Status = status;
                agentStatus.Message = message;
                agentStatus.LastChecked = DateTimeOffset.UtcNow;

                if (status != HealthStatus.Healthy)
                {
                    _logger.LogWarning("Agent '{AgentId}' health changed to {Status}: {Message}",
                        agentId, status, message ?? "No message");
                }
            }
        }
    }

    /// <summary>Get health status for all registered agents.</summary>
    public PlatformHealthReport GetHealthReport()
    {
        lock (_lock)
        {
            var agents = _agentStatuses.Values.ToList();

            var overallStatus = agents.Count == 0
                ? HealthStatus.Healthy
                : agents.All(a => a.Status == HealthStatus.Healthy)
                    ? HealthStatus.Healthy
                    : agents.Any(a => a.Status == HealthStatus.Unavailable)
                        ? HealthStatus.Unavailable
                        : HealthStatus.Degraded;

            return new PlatformHealthReport
            {
                OverallStatus = overallStatus,
                Agents = agents.Select(a => new AgentHealthStatus
                {
                    AgentId = a.AgentId,
                    AgentName = a.AgentName,
                    Status = a.Status,
                    Message = a.Message,
                    LastChecked = a.LastChecked
                }).ToList(),
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>Get health status for a specific agent.</summary>
    public AgentHealthStatus? GetAgentStatus(string agentId)
    {
        lock (_lock)
        {
            return _agentStatuses.TryGetValue(agentId, out var status) ? status : null;
        }
    }
}

/// <summary>Platform-wide health report.</summary>
public class PlatformHealthReport
{
    public HealthStatus OverallStatus { get; set; }
    public List<AgentHealthStatus> Agents { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>Per-agent health status entry.</summary>
public class AgentHealthStatus
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset LastChecked { get; set; }
}

/// <summary>
/// ASP.NET Core IHealthCheck implementation that integrates with the standard
/// /health endpoint middleware. Returns within 2 seconds per SC-013.
/// </summary>
public class PlatformHealthCheck : IHealthCheck
{
    private readonly HealthCheckService _healthService;

    public PlatformHealthCheck(HealthCheckService healthService)
    {
        _healthService = healthService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var report = _healthService.GetHealthReport();

        var data = new Dictionary<string, object>
        {
            ["agents"] = report.Agents.Select(a => new
            {
                a.AgentId,
                a.AgentName,
                Status = a.Status.ToString(),
                a.Message,
                LastChecked = a.LastChecked.ToString("o")
            }).ToList(),
            ["timestamp"] = report.Timestamp.ToString("o")
        };

        return Task.FromResult(report.OverallStatus switch
        {
            HealthStatus.Healthy => HealthCheckResult.Healthy("All agents healthy.", data),
            HealthStatus.Degraded => HealthCheckResult.Degraded("Some agents degraded.", data: data),
            _ => HealthCheckResult.Unhealthy("One or more agents unavailable.", data: data)
        });
    }
}
