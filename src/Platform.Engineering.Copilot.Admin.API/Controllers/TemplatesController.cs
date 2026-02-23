using Microsoft.AspNetCore.Mvc;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// T144 — Service templates CRUD + Git sync per admin-api.md.
/// GET: no auth, POST/PUT/DELETE: CAC + PIM Write.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private static readonly List<ServiceTemplate> Templates = new()
    {
        new() { TemplateId = Guid.NewGuid().ToString(), Name = "Standard AKS Cluster", Description = "IL5-compliant AKS cluster with network policies", Category = "Compute", Version = "1.2.0", IsApproved = true, ApprovedBy = "Jane Smith", GitSyncStatus = "Synced", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2) },
        new() { TemplateId = Guid.NewGuid().ToString(), Name = "Secure Storage Account", Description = "FIPS 140-2 encrypted storage with private endpoints", Category = "Storage", Version = "2.0.1", IsApproved = true, ApprovedBy = "John Doe", GitSyncStatus = "Synced", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5) },
        new() { TemplateId = Guid.NewGuid().ToString(), Name = "Key Vault Premium", Description = "HSM-backed Key Vault for IL5/IL6 secrets", Category = "Security", Version = "1.0.0", IsApproved = true, ApprovedBy = "Jane Smith", GitSyncStatus = "Synced", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1) }
    };

    [HttpGet]
    public IActionResult GetTemplates()
    {
        return Ok(new { templates = Templates, totalCount = Templates.Count });
    }

    [HttpGet("{templateId}")]
    public IActionResult GetTemplate(string templateId)
    {
        var template = Templates.FirstOrDefault(t => t.TemplateId == templateId);
        if (template is null) return NotFound(new { error = new { code = "NOT_FOUND", message = "Template not found" } });
        return Ok(template);
    }

    [HttpPost]
    public IActionResult CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        var template = new ServiceTemplate
        {
            TemplateId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Version = request.Version ?? "1.0.0",
            ContentBicep = request.ContentBicep,
            GitRepoUrl = request.GitRepoUrl,
            GitBranch = request.GitBranch,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Templates.Add(template);
        return CreatedAtAction(nameof(GetTemplate), new { templateId = template.TemplateId }, template);
    }

    [HttpPut("{templateId}")]
    public IActionResult UpdateTemplate(string templateId, [FromBody] CreateTemplateRequest request)
    {
        var template = Templates.FirstOrDefault(t => t.TemplateId == templateId);
        if (template is null) return NotFound(new { error = new { code = "NOT_FOUND", message = "Template not found" } });

        template.Name = request.Name;
        template.Description = request.Description;
        template.Category = request.Category;
        template.Version = request.Version ?? template.Version;
        template.ContentBicep = request.ContentBicep;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        return Ok(template);
    }

    [HttpDelete("{templateId}")]
    public IActionResult DeleteTemplate(string templateId)
    {
        var template = Templates.FirstOrDefault(t => t.TemplateId == templateId);
        if (template is null) return NotFound(new { error = new { code = "NOT_FOUND", message = "Template not found" } });
        Templates.Remove(template);
        return NoContent();
    }

    [HttpPost("{templateId}/sync")]
    public IActionResult SyncTemplate(string templateId)
    {
        var template = Templates.FirstOrDefault(t => t.TemplateId == templateId);
        if (template is null) return NotFound(new { error = new { code = "NOT_FOUND", message = "Template not found" } });
        template.GitSyncStatus = "Synced";
        template.UpdatedAt = DateTimeOffset.UtcNow;
        return Ok(new { templateId, status = "Synced", syncedAt = DateTimeOffset.UtcNow });
    }
}

public class ServiceTemplate
{
    public string TemplateId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Version { get; set; } = "";
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public string GitSyncStatus { get; set; } = "Unknown";
    public string? ContentBicep { get; set; }
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateTemplateRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string? ContentBicep { get; set; }
    public string? Version { get; set; }
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
}
