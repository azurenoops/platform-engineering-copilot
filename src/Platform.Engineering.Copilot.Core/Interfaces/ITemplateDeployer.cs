namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Deploys infrastructure templates to Azure and manages deployment lifecycle.
/// </summary>
public interface ITemplateDeployer
{
    Task<string> DeployAsync(Guid templateId, string subscriptionId, string resourceGroup, string location,
        string? parameterValuesJson = null, CancellationToken cancellationToken = default);

    Task<string> GetStatusAsync(string deploymentId, CancellationToken cancellationToken = default);

    Task<object> ScaleAsync(string deploymentId, Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);

    Task DeleteResourcesAsync(string deploymentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating ITemplateDeployer instances.
/// </summary>
public interface IDeployerFactory
{
    ITemplateDeployer Create();
}
