namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// IaC template generation method.
/// </summary>
public enum TemplateMethod
{
    /// <summary>Generated from pre-built template library.</summary>
    TemplateGenerator,
    /// <summary>AI-generated Bicep/Terraform via Semantic Kernel.</summary>
    AiGenerated,
    /// <summary>Pulled from Azure Container Registry (Bicep modules).</summary>
    BicepAcr
}
