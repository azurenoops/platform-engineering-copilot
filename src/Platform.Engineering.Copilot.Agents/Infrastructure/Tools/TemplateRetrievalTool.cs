using System.Text;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

/// <summary>
/// Tool for retrieving generated infrastructure templates from storage.
/// Supports retrieving templates by ID, name, or listing recent templates.
/// </summary>
public class TemplateRetrievalTool : BaseTool
{
    private readonly ITemplateStorageService _templateStorage;

    public override string Name => "get_template_files";

    public override string Description =>
        "Retrieves generated infrastructure template files from storage. " +
        "Can get a specific template by ID or name, list recent templates, " +
        "or show file contents for a specific file within a template. " +
        "Use this when the user asks to 'review', 'show', 'display', or 'get' template details.";

    public TemplateRetrievalTool(
        ILogger<TemplateRetrievalTool> logger,
        ITemplateStorageService templateStorage) : base(logger)
    {
        _templateStorage = templateStorage ?? throw new ArgumentNullException(nameof(templateStorage));

        Parameters.Add(new ToolParameter("template_id", "Template ID (GUID) to retrieve", false));
        Parameters.Add(new ToolParameter("template_name", "Template name to search for (e.g., 'myakscluster-aks-prod')", false));
        Parameters.Add(new ToolParameter("file_name", "Specific file to retrieve from template (e.g., 'main.bicep')", false));
        Parameters.Add(new ToolParameter("action", "Action: 'get' (retrieve files), 'list' (list all templates), 'latest' (get most recent). Default: latest", false));
        Parameters.Add(new ToolParameter("include_content", "Include file contents in response. Default: true", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var templateId = GetOptionalString(arguments, "template_id");
            var templateName = GetOptionalString(arguments, "template_name");
            var fileName = GetOptionalString(arguments, "file_name");
            var action = GetOptionalString(arguments, "action") ?? "latest";
            var includeContent = GetOptionalBool(arguments, "include_content", true);

            Logger.LogInformation("Retrieving template: action={Action}, id={Id}, name={Name}", 
                action, templateId, templateName);

            return action.ToLowerInvariant() switch
            {
                "list" => await ListAllTemplatesAsync(cancellationToken),
                "latest" when string.IsNullOrEmpty(templateId) && string.IsNullOrEmpty(templateName) 
                    => await GetLatestTemplateAsync(fileName, includeContent, cancellationToken),
                _ => await GetTemplateAsync(templateId, templateName, fileName, includeContent, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving template");
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private async Task<string> GetLatestTemplateAsync(
        string? fileName,
        bool includeContent,
        CancellationToken cancellationToken)
    {
        var template = await _templateStorage.GetLatestTemplateAsync(cancellationToken);
        
        if (template == null)
        {
            return ToJson(new
            {
                success = false,
                error = "No templates found. Generate a template first using generate_infrastructure_template."
            });
        }

        return FormatTemplateResponse(template, fileName, includeContent);
    }

    private async Task<string> GetTemplateAsync(
        string? templateId,
        string? templateName,
        string? fileName,
        bool includeContent,
        CancellationToken cancellationToken)
    {
        InfrastructureTemplate? template = null;

        // Try to get by ID first
        if (!string.IsNullOrEmpty(templateId))
        {
            template = await _templateStorage.GetTemplateByIdAsync(templateId, cancellationToken);
        }
        
        // Try by name if no ID or not found
        if (template == null && !string.IsNullOrEmpty(templateName))
        {
            template = await _templateStorage.GetTemplateByNameAsync(templateName, cancellationToken);
        }

        // Fallback to latest
        if (template == null)
        {
            return await GetLatestTemplateAsync(fileName, includeContent, cancellationToken);
        }

        return FormatTemplateResponse(template, fileName, includeContent);
    }

    private string FormatTemplateResponse(InfrastructureTemplate template, string? fileName, bool includeContent)
    {
        // If specific file requested
        if (!string.IsNullOrEmpty(fileName) && template.Files != null)
        {
            var file = template.Files.FirstOrDefault(f =>
                f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                f.FileName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (file == null)
            {
                return ToJson(new
                {
                    success = false,
                    error = $"File '{fileName}' not found in template",
                    availableFiles = template.Files?.Select(f => f.FileName).ToList() ?? new List<string>()
                });
            }

            return ToJson(new
            {
                success = true,
                templateId = template.Id,
                templateName = template.Name,
                file = new
                {
                    fileName = file.FileName,
                    fileType = file.FileType,
                    content = file.Content
                }
            });
        }

        // Return full template details
        var result = new StringBuilder();
        result.AppendLine($"## 📦 Template: {template.Name}");
        result.AppendLine();
        result.AppendLine($"- **ID:** `{template.Id}`");
        result.AppendLine($"- **Description:** {template.Description}");
        result.AppendLine($"- **Type:** {template.TemplateType}");
        result.AppendLine($"- **Format:** {template.Format}");
        result.AppendLine($"- **Azure Service:** {template.AzureService}");
        result.AppendLine($"- **Created:** {template.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        result.AppendLine($"- **File Count:** {template.FilesCount}");
        result.AppendLine();

        if (template.Files != null && template.Files.Any())
        {
            result.AppendLine("### 📁 Files");
            result.AppendLine();

            foreach (var file in template.Files)
            {
                result.AppendLine($"#### {file.FileName}");
                if (includeContent && !string.IsNullOrEmpty(file.Content))
                {
                    var lang = InferLanguageFromFileName(file.FileName, file.FileType);
                    result.AppendLine($"```{lang}");
                    result.AppendLine(file.Content);
                    result.AppendLine("```");
                }
                else
                {
                    result.AppendLine($"- Type: `{file.FileType}`");
                    result.AppendLine($"- Size: {file.Content?.Length ?? 0} bytes");
                }
                result.AppendLine();
            }
        }
        else if (!string.IsNullOrEmpty(template.Content))
        {
            result.AppendLine("### 📄 Template Content");
            result.AppendLine();
            result.AppendLine($"```{template.MainFileType ?? "json"}");
            result.AppendLine(template.Content);
            result.AppendLine("```");
        }

        return ToJson(new
        {
            success = true,
            markdown = result.ToString(),
            template = new
            {
                id = template.Id,
                name = template.Name,
                description = template.Description,
                templateType = template.TemplateType,
                format = template.Format,
                azureService = template.AzureService,
                createdAt = template.CreatedAt,
                filesCount = template.FilesCount,
                files = template.Files?.Select(f => new
                {
                    fileName = f.FileName,
                    fileType = f.FileType,
                    content = includeContent ? f.Content : null
                }).ToList()
            }
        });
    }

    private static string InferLanguageFromFileName(string fileName, string? fileType)
    {
        if (fileName.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase)) return "bicep";
        if (fileName.EndsWith(".tf", StringComparison.OrdinalIgnoreCase)) return "hcl";
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return "json";
        if (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || 
            fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) return "yaml";
        if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return "markdown";
        return fileType?.ToLowerInvariant() ?? "plaintext";
    }

    private async Task<string> ListAllTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await _templateStorage.ListAllTemplatesAsync(cancellationToken);

        if (!templates.Any())
        {
            return ToJson(new
            {
                success = true,
                message = "No templates found in storage.",
                templates = Array.Empty<object>()
            });
        }

        var result = new StringBuilder();
        result.AppendLine("## 📦 Available Templates");
        result.AppendLine();

        foreach (var template in templates.OrderByDescending(t => t.CreatedAt).Take(20))
        {
            result.AppendLine($"- **{template.Name}** (ID: `{template.Id}`)");
            result.AppendLine($"  - Type: {template.TemplateType}, Format: {template.Format}");
            result.AppendLine($"  - Service: {template.AzureService}, Files: {template.FilesCount}");
            result.AppendLine($"  - Created: {template.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        return ToJson(new
        {
            success = true,
            markdown = result.ToString(),
            count = templates.Count,
            templates = templates.OrderByDescending(t => t.CreatedAt).Take(20).Select(t => new
            {
                id = t.Id,
                name = t.Name,
                templateType = t.TemplateType,
                format = t.Format,
                azureService = t.AzureService,
                filesCount = t.FilesCount,
                createdAt = t.CreatedAt
            }).ToList()
        });
    }
}
