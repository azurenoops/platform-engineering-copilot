using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

// ========== GENERATOR INTERFACES ==========

public interface IApplicationCodeGenerator
{
    Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken);
}

public interface IDatabaseTemplateGenerator
{
    Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, DatabaseSpec dbSpec, CancellationToken cancellationToken);
}

public interface IInfrastructureGenerator
{
    Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken);
}