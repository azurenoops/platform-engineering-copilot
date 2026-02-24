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
/// </summary>
internal class StubTemplateDeployer : ITemplateDeployer
{
    public Task<string> DeployAsync(Guid templateId, string subscriptionId, string resourceGroup, string location,
        string? parameterValuesJson = null, CancellationToken cancellationToken = default)
    {
        var deploymentId = $"deploy-{Guid.NewGuid():N}";
        return Task.FromResult(deploymentId);
    }

    public Task<string> GetStatusAsync(string deploymentId, CancellationToken cancellationToken = default)
    {
        // Stub always returns Succeeded
        return Task.FromResult("Succeeded");
    }

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
