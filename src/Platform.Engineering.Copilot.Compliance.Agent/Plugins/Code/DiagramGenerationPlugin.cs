using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces.Services.DiagramGeneration;
using Platform.Engineering.Copilot.Core.Plugins;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins.Code;

/// <summary>
/// Kernel plugin for generating Mermaid diagrams from various sources
/// Phase 1 compliant: Generates diagram markdown for review, does not render automatically
/// </summary>
public class DiagramGenerationPlugin : BaseSupervisorPlugin
{
    private readonly IMermaidDiagramService _diagramService;

    public DiagramGenerationPlugin(
        IMermaidDiagramService diagramService,
        ILogger<DiagramGenerationPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _diagramService = diagramService ?? throw new ArgumentNullException(nameof(diagramService));
    }

    [KernelFunction("generate_architecture_diagram")]
    [Description("Generate Mermaid C4 architecture diagram from Azure resource group. " +
                 "Creates visual representation of containers, databases, and relationships. " +
                 "Use when users ask 'Show me architecture for rg-prod' or 'Generate diagram of my resources'. " +
                 "Phase 1: Returns Mermaid markdown for review, user can render manually.")]
    public async Task<string> GenerateArchitectureDiagramAsync(
        [Description("Azure resource group name")] string resourceGroup,
        [Description("Diagram type: c4-context, c4-container, flowchart (default: c4-container)")] string diagramType = "c4-container",
        [Description("Optional: Custom diagram title")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating architecture diagram for resource group {ResourceGroup}, type {DiagramType}", 
            resourceGroup, diagramType);

        try
        {
            string mermaidCode;

            if (diagramType.Contains("context", StringComparison.OrdinalIgnoreCase))
            {
                mermaidCode = await _diagramService.GenerateC4ContextDiagramAsync(
                    resourceGroup, title, cancellationToken);
            }
            else
            {
                mermaidCode = await _diagramService.GenerateC4ContainerDiagramAsync(
                    resourceGroup, null, title, cancellationToken);
            }

            return $@"# Architecture Diagram: {title ?? resourceGroup}

**Diagram Type:** {diagramType}
**Resource Group:** {resourceGroup}

## Mermaid Diagram

{mermaidCode}

## How to Use (Phase 1 - Manual Review)

**Option 1: VS Code Preview**
1. Copy the diagram code above
2. Create a new `.md` file in your workspace
3. Paste the code
4. Use VS Code Mermaid preview extension

**Option 2: GitHub Rendering**
1. Save this markdown to your repository
2. GitHub will automatically render the Mermaid diagram
3. View in pull request or README

**Option 3: Documentation Site**
1. Include in documentation (MkDocs, Docusaurus, etc.)
2. Most doc generators support Mermaid rendering

**Phase 1 Compliant:** ✅ Diagram generated for manual review. No automatic rendering.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating architecture diagram for {ResourceGroup}", resourceGroup);
            return $"Error generating architecture diagram: {ex.Message}";
        }
    }

    [KernelFunction("generate_sequence_diagram")]
    [Description("Generate Mermaid sequence diagram for API interactions or workflows. " +
                 "Shows interactions between systems, actors, and components over time. " +
                 "Use when users ask 'Show PR review flow' or 'Diagram the deployment process'. " +
                 "Phase 1: Returns Mermaid markdown for review.")]
    public async Task<string> GenerateSequenceDiagramAsync(
        [Description("Workflow description or process to diagram")] string workflowDescription,
        [Description("Optional: Custom diagram title")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating sequence diagram for workflow");

        try
        {
            var mermaidCode = await _diagramService.GenerateSequenceDiagramAsync(
                workflowDescription, title, cancellationToken);

            return $@"# Sequence Diagram: {title ?? "Workflow"}

**Process:** {workflowDescription}

## Mermaid Diagram

{mermaidCode}

## Phase 1 Instructions

This sequence diagram shows the interaction flow over time.

**Save to workspace:**
- Create `.md` file with diagram code
- Preview in VS Code or GitHub
- Include in documentation

**Phase 1 Compliant:** ✅ Diagram markdown generated for manual use.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sequence diagram");
            return $"Error generating sequence diagram: {ex.Message}";
        }
    }

    [KernelFunction("generate_erd_diagram")]
    [Description("Generate Mermaid entity-relationship diagram from database schema. " +
                 "Shows database tables, columns, relationships, and foreign keys. " +
                 "Use when users ask 'Diagram my database schema' or 'Show table relationships'. " +
                 "Phase 1: Returns Mermaid markdown for review.")]
    public async Task<string> GenerateErdDiagramAsync(
        [Description("Database schema description or JSON")] string schemaDescription,
        [Description("Optional: Custom diagram title")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating ERD diagram");

        try
        {
            var mermaidCode = await _diagramService.GenerateEntityRelationshipDiagramAsync(
                schemaDescription, title, cancellationToken);

            return $@"# Entity-Relationship Diagram: {title ?? "Database Schema"}

**Schema:** {schemaDescription}

## Mermaid Diagram

{mermaidCode}

## Phase 1 Instructions

This ERD shows database structure and relationships.

**Save to workspace:**
- Add to database documentation
- Include in architecture docs
- Review with DBA team

**Phase 1 Compliant:** ✅ Schema diagram generated for manual review.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ERD diagram");
            return $"Error generating ERD diagram: {ex.Message}";
        }
    }

    [KernelFunction("generate_flowchart")]
    [Description("Generate Mermaid flowchart for processes and decision trees. " +
                 "Shows process steps, decisions, and flow logic. " +
                 "Use when users ask 'Diagram the approval workflow' or 'Show deployment process'. " +
                 "Phase 1: Returns Mermaid markdown for review.")]
    public async Task<string> GenerateFlowchartAsync(
        [Description("Process description with steps and decisions")] string processDescription,
        [Description("Optional: Custom diagram title")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating flowchart");

        try
        {
            var mermaidCode = await _diagramService.GenerateFlowchartAsync(
                processDescription, title, cancellationToken);

            return $@"# Flowchart: {title ?? "Process Flow"}

**Process:** {processDescription}

## Mermaid Diagram

{mermaidCode}

## Phase 1 Instructions

This flowchart shows the process flow and decision points.

**Save to workspace:**
- Add to process documentation
- Include in runbooks
- Review with stakeholders

**Phase 1 Compliant:** ✅ Flowchart generated for manual review and deployment.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating flowchart");
            return $"Error generating flowchart: {ex.Message}";
        }
    }
}
