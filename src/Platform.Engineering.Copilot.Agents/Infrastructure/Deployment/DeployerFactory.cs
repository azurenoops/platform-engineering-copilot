using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Deployment;

/// <summary>
/// Factory for selecting the appropriate deployer based on template format
/// </summary>
public interface IDeployerFactory
{
    /// <summary>
    /// Get the deployer for the specified template format
    /// </summary>
    ITemplateDeployer GetDeployer(string format);
    
    /// <summary>
    /// Get all registered deployers
    /// </summary>
    IEnumerable<ITemplateDeployer> GetAllDeployers();
    
    /// <summary>
    /// Check if a deployer exists for the given format
    /// </summary>
    bool HasDeployer(string format);
}

/// <summary>
/// Default implementation of deployer factory
/// </summary>
public class DeployerFactory : IDeployerFactory
{
    private readonly ILogger<DeployerFactory> _logger;
    private readonly IEnumerable<ITemplateDeployer> _deployers;

    public DeployerFactory(
        ILogger<DeployerFactory> logger,
        IEnumerable<ITemplateDeployer> deployers)
    {
        _logger = logger;
        _deployers = deployers;
    }

    public ITemplateDeployer GetDeployer(string format)
    {
        var deployer = _deployers.FirstOrDefault(d => d.CanHandle(format));
        
        if (deployer == null)
        {
            _logger.LogWarning("No deployer found for format: {Format}. Available: {Available}",
                format, string.Join(", ", _deployers.Select(d => d.Format)));
            throw new NotSupportedException($"No deployer available for format: {format}. " +
                $"Supported formats: {string.Join(", ", _deployers.Select(d => d.Format))}");
        }

        _logger.LogDebug("Selected {Deployer} for format {Format}", deployer.GetType().Name, format);
        return deployer;
    }

    public IEnumerable<ITemplateDeployer> GetAllDeployers() => _deployers;

    public bool HasDeployer(string format) => _deployers.Any(d => d.CanHandle(format));
}

/// <summary>
/// Extension methods for deployment services registration
/// </summary>
public static class DeploymentServiceExtensions
{
    /// <summary>
    /// Register all deployment services
    /// </summary>
    public static IServiceCollection AddDeploymentServices(this IServiceCollection services)
    {
        // Register deployers
        services.AddScoped<ITemplateDeployer, BicepDeployer>();
        services.AddScoped<ITemplateDeployer, TerraformDeployer>();
        
        // Register factory
        services.AddScoped<IDeployerFactory, DeployerFactory>();
        
        // Register options
        services.AddOptions<DeployerOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("Deployment").Bind(options);
            });

        return services;
    }
}
