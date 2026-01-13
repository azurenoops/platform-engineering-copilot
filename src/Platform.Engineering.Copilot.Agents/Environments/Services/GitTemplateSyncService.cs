using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Service for synchronizing templates between Git repositories and the database cache.
/// Git serves as the source of truth; the database provides fast querying and offline access.
/// </summary>
public class GitTemplateSyncService : IGitTemplateSyncService
{
    private readonly ILogger<GitTemplateSyncService> _logger;
    private readonly IServiceTemplateRepository _repository;
    private readonly GitSyncOptions _options;
    private readonly HttpClient _httpClient;

    public GitTemplateSyncService(
        ILogger<GitTemplateSyncService> logger,
        IServiceTemplateRepository repository,
        IOptions<GitSyncOptions> options,
        HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options?.Value ?? new GitSyncOptions();
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Synchronize a single template from its Git source.
    /// </summary>
    public async Task<GitSyncResult> SyncTemplateAsync(
        Guid templateId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
        {
            return new GitSyncResult
            {
                Success = false,
                TemplateId = templateId.ToString(),
                Message = "Template not found"
            };
        }

        return await SyncTemplateFromGitAsync(entity, force, cancellationToken);
    }

    /// <summary>
    /// Synchronize all templates that need syncing.
    /// </summary>
    public async Task<GitSyncBatchResult> SyncAllTemplatesAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var result = new GitSyncBatchResult();
        
        var templates = force 
            ? await _repository.GetAllAsync(cancellationToken)
            : await _repository.GetTemplatesNeedingSyncAsync(cancellationToken);

        _logger.LogInformation("🔄 Starting Git sync for {Count} templates", templates.Count);

        foreach (var template in templates)
        {
            if (string.IsNullOrEmpty(template.GitRepositoryUrl))
            {
                result.Skipped.Add(template.Id.ToString());
                continue;
            }

            var syncResult = await SyncTemplateFromGitAsync(template, force, cancellationToken);
            
            if (syncResult.Success)
            {
                if (syncResult.WasUpdated)
                    result.Updated.Add(template.Id.ToString());
                else
                    result.Unchanged.Add(template.Id.ToString());
            }
            else
            {
                result.Failed.Add(new GitSyncFailure
                {
                    TemplateId = template.Id.ToString(),
                    TemplateName = template.Name,
                    Error = syncResult.Message
                });
            }
        }

        result.Success = result.Failed.Count == 0;
        result.Message = $"Synced {result.Updated.Count} templates, {result.Unchanged.Count} unchanged, {result.Failed.Count} failed, {result.Skipped.Count} skipped";

        _logger.LogInformation("✅ Git sync complete: {Message}", result.Message);

        return result;
    }

    /// <summary>
    /// Import a template from a Git repository URL.
    /// </summary>
    public async Task<GitImportResult> ImportFromGitAsync(
        string repositoryUrl,
        string branch,
        string path,
        string importedBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📥 Importing template from Git: {Url} ({Branch}:{Path})",
            repositoryUrl, branch, path);

        try
        {
            // Parse the repository URL
            var (owner, repo, provider) = ParseGitUrl(repositoryUrl);
            
            // Fetch the template content
            var content = await FetchGitContentAsync(provider, owner, repo, branch, path, cancellationToken);
            
            if (string.IsNullOrEmpty(content.MainContent))
            {
                return new GitImportResult
                {
                    Success = false,
                    Message = "Could not fetch template content from Git"
                };
            }

            // Parse template metadata
            var metadata = ExtractTemplateMetadata(content, path);

            // Create new template entity
            var entity = new ServiceTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = metadata.Name,
                DisplayName = metadata.DisplayName,
                Description = metadata.Description,
                Version = "1.0.0",
                Category = metadata.Category,
                Format = metadata.Format,
                MainTemplateContent = content.MainContent,
                AdditionalFilesJson = content.AdditionalFiles != null 
                    ? JsonSerializer.Serialize(content.AdditionalFiles) 
                    : null,
                GitRepositoryUrl = repositoryUrl,
                GitBranch = branch,
                GitPath = path,
                GitCommitSha = content.CommitSha,
                LastSyncedFromGit = DateTime.UtcNow,
                GitAutoSync = true,
                GitSyncIntervalMinutes = _options.DefaultSyncIntervalMinutes,
                ParametersJson = metadata.ParametersJson,
                Status = "Draft",
                CreatedBy = importedBy,
                CreatedAt = DateTime.UtcNow,
                Keywords = metadata.Keywords,
                UseCases = metadata.UseCases
            };

            await _repository.CreateAsync(entity, cancellationToken);

            _logger.LogInformation("✅ Imported template {Name} from Git (ID: {Id})",
                entity.Name, entity.Id);

            return new GitImportResult
            {
                Success = true,
                TemplateId = entity.Id.ToString(),
                TemplateName = entity.Name,
                Message = $"Successfully imported template '{entity.DisplayName}' from Git",
                CommitSha = content.CommitSha
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import template from Git: {Url}", repositoryUrl);
            return new GitImportResult
            {
                Success = false,
                Message = $"Import failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Check if a template has changes in Git compared to the database.
    /// </summary>
    public async Task<GitDiffResult> CheckForChangesAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
        {
            return new GitDiffResult
            {
                HasChanges = false,
                Message = "Template not found"
            };
        }

        if (string.IsNullOrEmpty(entity.GitRepositoryUrl))
        {
            return new GitDiffResult
            {
                HasChanges = false,
                Message = "Template has no Git source configured"
            };
        }

        try
        {
            var (owner, repo, provider) = ParseGitUrl(entity.GitRepositoryUrl);
            var latestSha = await GetLatestCommitShaAsync(provider, owner, repo, 
                entity.GitBranch ?? "main", entity.GitPath, cancellationToken);

            var hasChanges = latestSha != entity.GitCommitSha;

            return new GitDiffResult
            {
                HasChanges = hasChanges,
                CurrentSha = entity.GitCommitSha,
                LatestSha = latestSha,
                LastSynced = entity.LastSyncedFromGit,
                Message = hasChanges 
                    ? $"Template has changes (current: {entity.GitCommitSha?[..7]}, latest: {latestSha?[..7]})"
                    : "Template is up to date"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for Git changes: {TemplateId}", templateId);
            return new GitDiffResult
            {
                HasChanges = false,
                Message = $"Failed to check: {ex.Message}"
            };
        }
    }

    #region Private Methods

    private async Task<GitSyncResult> SyncTemplateFromGitAsync(
        ServiceTemplateEntity entity,
        bool force,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(entity.GitRepositoryUrl))
        {
            return new GitSyncResult
            {
                Success = true,
                TemplateId = entity.Id.ToString(),
                WasUpdated = false,
                Message = "No Git source configured"
            };
        }

        // Check if sync is needed
        if (!force && !ShouldSync(entity))
        {
            return new GitSyncResult
            {
                Success = true,
                TemplateId = entity.Id.ToString(),
                WasUpdated = false,
                Message = "Sync not needed yet"
            };
        }

        try
        {
            var (owner, repo, provider) = ParseGitUrl(entity.GitRepositoryUrl);
            var content = await FetchGitContentAsync(provider, owner, repo,
                entity.GitBranch ?? "main", entity.GitPath, cancellationToken);

            // Check if content actually changed
            if (content.CommitSha == entity.GitCommitSha && !force)
            {
                await _repository.UpdateGitSyncTimestampAsync(entity.Id, entity.GitCommitSha, cancellationToken);
                
                return new GitSyncResult
                {
                    Success = true,
                    TemplateId = entity.Id.ToString(),
                    WasUpdated = false,
                    CommitSha = content.CommitSha,
                    Message = "No changes detected"
                };
            }

            // Update template content
            entity.MainTemplateContent = content.MainContent;
            entity.AdditionalFilesJson = content.AdditionalFiles != null
                ? JsonSerializer.Serialize(content.AdditionalFiles)
                : null;
            entity.GitCommitSha = content.CommitSha;
            entity.LastSyncedFromGit = DateTime.UtcNow;
            entity.UpdatedBy = "GitSync";
            entity.UpdatedAt = DateTime.UtcNow;

            // Re-extract parameters if template changed
            var metadata = ExtractTemplateMetadata(content, entity.GitPath);
            if (!string.IsNullOrEmpty(metadata.ParametersJson))
            {
                entity.ParametersJson = metadata.ParametersJson;
            }

            await _repository.UpdateAsync(entity, cancellationToken);

            _logger.LogInformation("🔄 Synced template {Name} from Git (commit: {Sha})",
                entity.Name, content.CommitSha?[..7]);

            return new GitSyncResult
            {
                Success = true,
                TemplateId = entity.Id.ToString(),
                WasUpdated = true,
                CommitSha = content.CommitSha,
                Message = "Template updated from Git"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git sync failed for template {Id}", entity.Id);
            return new GitSyncResult
            {
                Success = false,
                TemplateId = entity.Id.ToString(),
                Message = $"Sync failed: {ex.Message}"
            };
        }
    }

    private bool ShouldSync(ServiceTemplateEntity entity)
    {
        if (!entity.GitAutoSync) return false;
        if (!entity.LastSyncedFromGit.HasValue) return true;

        var intervalMinutes = entity.GitSyncIntervalMinutes > 0 
            ? entity.GitSyncIntervalMinutes 
            : _options.DefaultSyncIntervalMinutes;

        return DateTime.UtcNow - entity.LastSyncedFromGit.Value > TimeSpan.FromMinutes(intervalMinutes);
    }

    private (string owner, string repo, string provider) ParseGitUrl(string url)
    {
        // GitHub: https://github.com/owner/repo or git@github.com:owner/repo.git
        var githubMatch = Regex.Match(url, @"github\.com[:/]([^/]+)/([^/\.]+)");
        if (githubMatch.Success)
        {
            return (githubMatch.Groups[1].Value, githubMatch.Groups[2].Value, "github");
        }

        // Azure DevOps: https://dev.azure.com/org/project/_git/repo
        var adoMatch = Regex.Match(url, @"dev\.azure\.com/([^/]+)/([^/]+)/_git/([^/]+)");
        if (adoMatch.Success)
        {
            return ($"{adoMatch.Groups[1].Value}/{adoMatch.Groups[2].Value}", adoMatch.Groups[3].Value, "azuredevops");
        }

        throw new ArgumentException($"Unsupported Git URL format: {url}");
    }

    private async Task<GitContent> FetchGitContentAsync(
        string provider,
        string owner,
        string repo,
        string branch,
        string? path,
        CancellationToken cancellationToken)
    {
        if (provider == "github")
        {
            return await FetchFromGitHubAsync(owner, repo, branch, path, cancellationToken);
        }
        else if (provider == "azuredevops")
        {
            return await FetchFromAzureDevOpsAsync(owner, repo, branch, path, cancellationToken);
        }

        throw new NotSupportedException($"Git provider '{provider}' is not supported");
    }

    private async Task<GitContent> FetchFromGitHubAsync(
        string owner,
        string repo,
        string branch,
        string? path,
        CancellationToken cancellationToken)
    {
        var filePath = path ?? "main.bicep";
        var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}?ref={branch}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github.v3+json");
        request.Headers.Add("User-Agent", "Platform-Engineering-Copilot");
        
        if (!string.IsNullOrEmpty(_options.GitHubToken))
        {
            request.Headers.Add("Authorization", $"token {_options.GitHubToken}");
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GitHub API returned {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var content = root.GetProperty("content").GetString();
        var sha = root.GetProperty("sha").GetString();

        // Decode base64 content
        var decodedContent = Encoding.UTF8.GetString(Convert.FromBase64String(content?.Replace("\n", "") ?? ""));

        return new GitContent
        {
            MainContent = decodedContent,
            CommitSha = sha,
            FilePath = filePath
        };
    }

    private Task<GitContent> FetchFromAzureDevOpsAsync(
        string ownerProject,
        string repo,
        string branch,
        string? path,
        CancellationToken cancellationToken)
    {
        // Azure DevOps implementation would go here
        // For now, return placeholder
        throw new NotImplementedException("Azure DevOps Git integration is not yet implemented");
    }

    private async Task<string?> GetLatestCommitShaAsync(
        string provider,
        string owner,
        string repo,
        string branch,
        string? path,
        CancellationToken cancellationToken)
    {
        if (provider == "github")
        {
            var filePath = path ?? "main.bicep";
            var url = $"https://api.github.com/repos/{owner}/{repo}/commits?sha={branch}&path={filePath}&per_page=1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "Platform-Engineering-Copilot");
            
            if (!string.IsNullOrEmpty(_options.GitHubToken))
            {
                request.Headers.Add("Authorization", $"token {_options.GitHubToken}");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var commits = doc.RootElement;

            if (commits.GetArrayLength() > 0)
            {
                return commits[0].GetProperty("sha").GetString();
            }
        }

        return null;
    }

    private TemplateMetadata ExtractTemplateMetadata(GitContent content, string? path)
    {
        var metadata = new TemplateMetadata
        {
            Name = Path.GetFileNameWithoutExtension(path ?? "template"),
            DisplayName = Path.GetFileNameWithoutExtension(path ?? "Template"),
            Format = DetectTemplateFormat(path, content.MainContent)
        };

        // Extract metadata from Bicep/ARM comments or metadata block
        if (metadata.Format == "Bicep")
        {
            ExtractBicepMetadata(content.MainContent, metadata);
        }
        else if (metadata.Format == "ARM")
        {
            ExtractArmMetadata(content.MainContent, metadata);
        }
        else if (metadata.Format == "Terraform")
        {
            ExtractTerraformMetadata(content.MainContent, metadata);
        }

        return metadata;
    }

    private string DetectTemplateFormat(string? path, string content)
    {
        if (path?.EndsWith(".bicep") == true) return "Bicep";
        if (path?.EndsWith(".tf") == true) return "Terraform";
        if (path?.EndsWith(".json") == true)
        {
            if (content.Contains("\"$schema\"") && content.Contains("deploymentTemplate"))
                return "ARM";
        }
        
        // Try to detect from content
        if (content.Contains("param ") && content.Contains("resource ")) return "Bicep";
        if (content.Contains("\"$schema\"")) return "ARM";
        if (content.Contains("resource \"") || content.Contains("variable \"")) return "Terraform";

        return "Bicep"; // Default
    }

    private void ExtractBicepMetadata(string content, TemplateMetadata metadata)
    {
        // Extract metadata from Bicep metadata block
        var metadataMatch = Regex.Match(content, @"metadata\s+(\w+)\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        if (metadataMatch.Success)
        {
            var block = metadataMatch.Groups[2].Value;
            
            var nameMatch = Regex.Match(block, @"name:\s*'([^']+)'");
            if (nameMatch.Success) metadata.DisplayName = nameMatch.Groups[1].Value;

            var descMatch = Regex.Match(block, @"description:\s*'([^']+)'");
            if (descMatch.Success) metadata.Description = descMatch.Groups[1].Value;
        }

        // Extract parameters
        var parameters = new List<object>();
        var paramMatches = Regex.Matches(content, @"@description\('([^']+)'\)\s*param\s+(\w+)\s+(\w+)(?:\s*=\s*(.+))?");
        foreach (Match match in paramMatches)
        {
            parameters.Add(new
            {
                name = match.Groups[2].Value,
                type = match.Groups[3].Value,
                description = match.Groups[1].Value,
                defaultValue = match.Groups[4].Success ? match.Groups[4].Value.Trim() : null
            });
        }
        
        if (parameters.Any())
        {
            metadata.ParametersJson = JsonSerializer.Serialize(parameters);
        }
    }

    private void ExtractArmMetadata(string content, TemplateMetadata metadata)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("metadata", out var metadataElement))
            {
                if (metadataElement.TryGetProperty("name", out var name))
                    metadata.DisplayName = name.GetString() ?? metadata.DisplayName;
                if (metadataElement.TryGetProperty("description", out var desc))
                    metadata.Description = desc.GetString() ?? metadata.Description;
            }

            if (root.TryGetProperty("parameters", out var paramsElement))
            {
                var parameters = new List<object>();
                foreach (var prop in paramsElement.EnumerateObject())
                {
                    var param = new Dictionary<string, object?>
                    {
                        ["name"] = prop.Name,
                        ["type"] = prop.Value.TryGetProperty("type", out var t) ? t.GetString() : "string"
                    };
                    
                    if (prop.Value.TryGetProperty("metadata", out var m) && m.TryGetProperty("description", out var d))
                        param["description"] = d.GetString();
                    if (prop.Value.TryGetProperty("defaultValue", out var dv))
                        param["defaultValue"] = dv.ToString();

                    parameters.Add(param);
                }
                metadata.ParametersJson = JsonSerializer.Serialize(parameters);
            }
        }
        catch
        {
            // Ignore JSON parse errors
        }
    }

    private void ExtractTerraformMetadata(string content, TemplateMetadata metadata)
    {
        // Extract variables as parameters
        var parameters = new List<object>();
        var varMatches = Regex.Matches(content, @"variable\s+""(\w+)""\s*\{([^}]+)\}", RegexOptions.Singleline);
        
        foreach (Match match in varMatches)
        {
            var varName = match.Groups[1].Value;
            var varBlock = match.Groups[2].Value;

            var param = new Dictionary<string, object?>
            {
                ["name"] = varName,
                ["type"] = "string"
            };

            var descMatch = Regex.Match(varBlock, @"description\s*=\s*""([^""]+)""");
            if (descMatch.Success) param["description"] = descMatch.Groups[1].Value;

            var defaultMatch = Regex.Match(varBlock, @"default\s*=\s*(.+)");
            if (defaultMatch.Success) param["defaultValue"] = defaultMatch.Groups[1].Value.Trim();

            var typeMatch = Regex.Match(varBlock, @"type\s*=\s*(\w+)");
            if (typeMatch.Success) param["type"] = typeMatch.Groups[1].Value;

            parameters.Add(param);
        }

        if (parameters.Any())
        {
            metadata.ParametersJson = JsonSerializer.Serialize(parameters);
        }
    }

    #endregion

    #region Private Classes

    private class GitContent
    {
        public string MainContent { get; set; } = string.Empty;
        public string? CommitSha { get; set; }
        public string? FilePath { get; set; }
        public Dictionary<string, string>? AdditionalFiles { get; set; }
    }

    private class TemplateMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = "Imported from Git";
        public string Category { get; set; } = "Imported";
        public string Format { get; set; } = "Bicep";
        public string? ParametersJson { get; set; }
        public string? Keywords { get; set; }
        public string? UseCases { get; set; }
    }

    #endregion
}
