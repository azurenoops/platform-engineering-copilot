using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Factory for creating ITemplateDeployer instances.
/// Currently returns a stub deployer; will be replaced with real Azure deployment logic.
/// </summary>
public class DeployerFactory : IDeployerFactory
{
    private readonly ILogger<DeployerFactory> _logger;

    public DeployerFactory(ILogger<DeployerFactory> logger)
    {
        _logger = logger;
    }

    public ITemplateDeployer Create()
    {
        _logger.LogDebug("Creating stub template deployer");
        return new StubTemplateDeployer();
    }
}

/// <summary>
/// Stub template deployer for development/testing. Returns simulated deployment results.
/// The simulated deployment status can be configured for testing failure scenarios.
/// </summary>
internal class StubTemplateDeployer : ITemplateDeployer
{
    /// <summary>
    /// Simulated deployment status returned by <see cref="GetStatusAsync"/>.
    /// Defaults to "Succeeded". Set to "Failed" or "InProgress" to simulate other states.
    /// </summary>
    public string SimulatedStatus { get; set; } = "Succeeded";

    public Task<string> DeployAsync(Guid templateId, string subscriptionId, string resourceGroup, string location,
        string? parameterValuesJson = null, CancellationToken cancellationToken = default)
    {
        var deploymentId = $"deploy-{Guid.NewGuid():N}";
        return Task.FromResult(deploymentId);
    }

    public Task<string> GetStatusAsync(string deploymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(SimulatedStatus);

    public Task<object> ScaleAsync(string deploymentId, Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object>(new { deploymentId, status = "Scaling", parameters });
    }

    public Task DeleteResourcesAsync(string deploymentId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
