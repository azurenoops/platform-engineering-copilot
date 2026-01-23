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
    private readonly IHttpClientFactory _httpClientFactory;

    public GitTemplateSyncService(
        ILogger<GitTemplateSyncService> logger,
        IServiceTemplateRepository repository,
        IOptions<GitSyncOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options?.Value ?? new GitSyncOptions();
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
                AdditionalFilesJson = SerializeAdditionalFiles(content.AdditionalFiles),
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
            entity.AdditionalFilesJson = SerializeAdditionalFiles(content.AdditionalFiles);
            entity.GitCommitSha = content.CommitSha;
            entity.LastSyncedFromGit = DateTime.UtcNow;
            entity.UpdatedBy = "GitSync";
            entity.UpdatedAt = DateTime.UtcNow;

            // Re-extract parameters if template changed, BUT only if parameters haven't been manually overridden
            if (!entity.ParametersOverridden)
            {
                var metadata = ExtractTemplateMetadata(content, entity.GitPath);
                if (!string.IsNullOrEmpty(metadata.ParametersJson))
                {
                    entity.ParametersJson = metadata.ParametersJson;
                }
            }
            else
            {
                _logger.LogInformation("⏭️ Skipping parameter update for template {Name} - parameters were manually overridden", entity.Name);
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
        
        // Try GitHub API first (provides commit SHA for version tracking)
        GitContent result;
        try
        {
            result = await FetchFromGitHubApiAsync(owner, repo, branch, filePath, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("401"))
        {
            // Fallback to raw content URL for public repos (bypasses SAML SSO requirements)
            _logger.LogWarning("GitHub API returned {Error}, falling back to raw content URL", ex.Message);
            result = await FetchFromGitHubRawAsync(owner, repo, branch, filePath, cancellationToken);
        }

        // If this is a Bicep file, scan for module references and fetch them
        if (filePath.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(result.MainContent))
        {
            var moduleFiles = await FetchBicepModulesAsync(owner, repo, branch, filePath, result.MainContent, cancellationToken);
            if (moduleFiles.Count > 0)
            {
                result.AdditionalFiles = moduleFiles;
                _logger.LogInformation("📦 Fetched {Count} module files for template: {Files}", 
                    moduleFiles.Count, string.Join(", ", moduleFiles.Keys));
            }
        }

        return result;
    }

    /// <summary>
    /// Extract module file paths from Bicep content and fetch them from GitHub
    /// </summary>
    private async Task<Dictionary<string, string>> FetchBicepModulesAsync(
        string owner,
        string repo,
        string branch,
        string mainFilePath,
        string bicepContent,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>();
        
        // Find module declarations: module name 'path/to/module.bicep' = { ... }
        var modulePattern = new Regex(@"module\s+\w+\s+'([^']+\.bicep)'", RegexOptions.IgnoreCase);
        
        // Find loadJsonContent, loadTextContent, loadFileAsBase64 references
        var loadContentPattern = new Regex(@"load(?:Json|Text|FileAsBase64)Content\s*\(\s*'([^']+)'", RegexOptions.IgnoreCase);
        
        var moduleMatches = modulePattern.Matches(bicepContent);
        var loadContentMatches = loadContentPattern.Matches(bicepContent);

        // Get the base directory of the main file
        var baseDir = Path.GetDirectoryName(mainFilePath)?.Replace('\\', '/') ?? "";
        if (!string.IsNullOrEmpty(baseDir) && !baseDir.EndsWith("/"))
        {
            baseDir += "/";
        }

        var processedPaths = new HashSet<string>();

        // Process module references
        // Process module references
        foreach (Match match in moduleMatches)
        {
            var relativePath = match.Groups[1].Value;
            
            // Skip registry modules (br:, ts:, etc.)
            if (relativePath.Contains(":"))
            {
                _logger.LogDebug("Skipping registry module reference: {Path}", relativePath);
                continue;
            }

            // Resolve the full path relative to the main file
            var fullPath = ResolveBicepModulePath(baseDir, relativePath);
            
            // Use the relative path as the key (what the main.bicep references)
            if (processedPaths.Contains(relativePath))
            {
                continue;
            }
            processedPaths.Add(relativePath);

            try
            {
                // Fetch the module content
                _logger.LogInformation("📥 Fetching Bicep module: {Path}", fullPath);
                var moduleContent = await FetchSingleFileFromGitHubAsync(owner, repo, branch, fullPath, cancellationToken);
                
                if (!string.IsNullOrEmpty(moduleContent))
                {
                    files[relativePath] = moduleContent;
                    
                    // Recursively check for nested modules and data files
                    var nestedFiles = await FetchBicepModulesAsync(
                        owner, repo, branch, fullPath, moduleContent, cancellationToken);
                    
                    foreach (var nested in nestedFiles)
                    {
                        // Adjust nested file path to be relative to the main file's directory
                        var nestedRelativePath = Path.Combine(Path.GetDirectoryName(relativePath) ?? "", nested.Key)
                            .Replace('\\', '/');
                        
                        if (!files.ContainsKey(nestedRelativePath))
                        {
                            files[nestedRelativePath] = nested.Value;
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Failed to fetch module {Path}: {Error}", fullPath, ex.Message);
            }
        }

        // Process loadJsonContent, loadTextContent, loadFileAsBase64 references
        foreach (Match match in loadContentMatches)
        {
            var relativePath = match.Groups[1].Value;
            
            if (processedPaths.Contains(relativePath))
            {
                continue;
            }
            processedPaths.Add(relativePath);

            // Resolve the full path relative to the current file
            var fullPath = ResolveBicepModulePath(baseDir, relativePath);

            try
            {
                _logger.LogInformation("📥 Fetching data file: {Path}", fullPath);
                var fileContent = await FetchSingleFileFromGitHubAsync(owner, repo, branch, fullPath, cancellationToken);
                
                if (!string.IsNullOrEmpty(fileContent))
                {
                    files[relativePath] = fileContent;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Failed to fetch data file {Path}: {Error}", fullPath, ex.Message);
            }
        }

        return files;
    }

    /// <summary>
    /// Resolve a relative module path against a base directory
    /// </summary>
    private static string ResolveBicepModulePath(string baseDir, string relativePath)
    {
        // Handle relative paths like "../modules/foo.bicep" or "./modules/foo.bicep"
        var combined = Path.Combine(baseDir, relativePath).Replace('\\', '/');
        
        // Normalize the path (resolve .. and .)
        var segments = combined.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var result = new List<string>();
        
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                if (result.Count > 0)
                {
                    result.RemoveAt(result.Count - 1);
                }
            }
            else if (segment != ".")
            {
                result.Add(segment);
            }
        }

        return string.Join("/", result);
    }

    /// <summary>
    /// Fetch a single file from GitHub (raw content)
    /// </summary>
    private async Task<string> FetchSingleFileFromGitHubAsync(
        string owner,
        string repo,
        string branch,
        string filePath,
        CancellationToken cancellationToken)
    {
        var url = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{filePath}";
        
        using var httpClient = _httpClientFactory.CreateClient("GitHubRaw");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Platform-Engineering-Copilot");

        var response = await httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GitHub returned {response.StatusCode} for {filePath}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<GitContent> FetchFromGitHubApiAsync(
        string owner,
        string repo,
        string branch,
        string filePath,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}?ref={branch}";

        using var httpClient = _httpClientFactory.CreateClient("GitHubApi");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github.v3+json");
        request.Headers.Add("User-Agent", "Platform-Engineering-Copilot");
        
        if (!string.IsNullOrEmpty(_options.GitHubToken))
        {
            request.Headers.Add("Authorization", $"token {_options.GitHubToken}");
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        
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

    /// <summary>
    /// Fetch content from GitHub raw URL (for public repos, bypasses API auth requirements)
    /// </summary>
    private async Task<GitContent> FetchFromGitHubRawAsync(
        string owner,
        string repo,
        string branch,
        string filePath,
        CancellationToken cancellationToken)
    {
        var url = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{filePath}";
        
        using var httpClient = _httpClientFactory.CreateClient("GitHubRaw");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Platform-Engineering-Copilot");

        var response = await httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GitHub raw content returned {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Generate a pseudo-SHA from content hash (raw URL doesn't provide commit SHA)
        var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
            Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        _logger.LogInformation("Fetched template from raw GitHub URL: {Url}", url);

        return new GitContent
        {
            MainContent = content,
            CommitSha = contentHash, // Content-based hash as pseudo-SHA
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

            using var httpClient = _httpClientFactory.CreateClient("GitHubApi");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "Platform-Engineering-Copilot");
            
            if (!string.IsNullOrEmpty(_options.GitHubToken))
            {
                request.Headers.Add("Authorization", $"token {_options.GitHubToken}");
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
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

        // Extract parameters - handle decorators on separate lines
        // Pattern matches: @description('...') followed by optional other decorators, then param name type [= default]
        var parameters = new List<object>();
        
        // First, find all param declarations with their preceding decorators
        var paramBlockPattern = @"(?:@\w+\([^)]*\)\s*)+param\s+(\w+)\s+(\w+)(?:\s*=\s*(.+?))?(?=\r?\n|$)";
        var paramBlocks = Regex.Matches(content, paramBlockPattern, RegexOptions.Multiline);
        
        foreach (Match block in paramBlocks)
        {
            var paramName = block.Groups[1].Value;
            var paramType = block.Groups[2].Value;
            var defaultValue = block.Groups[3].Success ? block.Groups[3].Value.Trim().Trim('\'', '"') : null;
            
            // Look for @description decorator before this param
            var descPattern = $@"@description\('([^']+)'\)[\s\S]*?param\s+{Regex.Escape(paramName)}\s+";
            var descMatch = Regex.Match(content, descPattern);
            var description = descMatch.Success ? descMatch.Groups[1].Value : "";
            
            // Check for @minLength, @maxLength, @minValue, @maxValue
            int? minLength = null, maxLength = null, minValue = null, maxValue = null;
            var decoratorSection = block.Value.Substring(0, block.Value.IndexOf("param"));
            
            var minLenMatch = Regex.Match(decoratorSection, @"@minLength\((\d+)\)");
            if (minLenMatch.Success) minLength = int.Parse(minLenMatch.Groups[1].Value);
            
            var maxLenMatch = Regex.Match(decoratorSection, @"@maxLength\((\d+)\)");
            if (maxLenMatch.Success) maxLength = int.Parse(maxLenMatch.Groups[1].Value);
            
            var minValMatch = Regex.Match(decoratorSection, @"@minValue\((\d+)\)");
            if (minValMatch.Success) minValue = int.Parse(minValMatch.Groups[1].Value);
            
            var maxValMatch = Regex.Match(decoratorSection, @"@maxValue\((\d+)\)");
            if (maxValMatch.Success) maxValue = int.Parse(maxValMatch.Groups[1].Value);
            
            // Check if required (no default value)
            var required = !block.Groups[3].Success || string.IsNullOrEmpty(block.Groups[3].Value);
            
            parameters.Add(new
            {
                name = paramName,
                type = MapBicepType(paramType),
                description = description,
                defaultValue = defaultValue,
                required = required,
                minLength = minLength,
                maxLength = maxLength,
                minValue = minValue,
                maxValue = maxValue
            });
        }
        
        if (parameters.Any())
        {
            metadata.ParametersJson = JsonSerializer.Serialize(parameters);
        }
    }

    private string MapBicepType(string bicepType)
    {
        return bicepType.ToLowerInvariant() switch
        {
            "string" => "String",
            "int" => "Number",
            "bool" => "Boolean",
            "array" => "Array",
            "object" => "Object",
            _ => "String"
        };
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

    /// <summary>
    /// Fetch raw file content from a Git repository without importing.
    /// </summary>
    public async Task<string> FetchFileContentAsync(
        string repositoryUrl,
        string branch,
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching file content from Git: {Url}/{Path}", repositoryUrl, path);

            // Parse repository info
            var (owner, repo, provider) = ParseGitUrl(repositoryUrl);
            
            // Fetch content using existing logic
            var gitContent = await FetchGitContentAsync(provider, owner, repo, branch, path, cancellationToken);

            _logger.LogInformation("Successfully fetched {Length} bytes from Git", gitContent.MainContent?.Length ?? 0);
            return gitContent.MainContent ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching file content from Git: {Url}/{Path}", repositoryUrl, path);
            throw;
        }
    }

    /// <summary>
    /// Convert the Dictionary of additional files to the proper TemplateFile format for serialization
    /// </summary>
    private static string? SerializeAdditionalFiles(Dictionary<string, string>? additionalFiles)
    {
        if (additionalFiles == null || additionalFiles.Count == 0)
        {
            return null;
        }

        var templateFiles = additionalFiles.Select((kvp, index) => new Platform.Engineering.Copilot.Core.Models.ServiceTemplates.TemplateFile
        {
            FileName = Path.GetFileName(kvp.Key),
            RelativePath = kvp.Key,
            Content = kvp.Value,
            FileType = Path.GetExtension(kvp.Key).TrimStart('.').ToLowerInvariant(),
            IsEntryPoint = false,
            Order = index + 1
        }).ToList();

        return JsonSerializer.Serialize(templateFiles);
    }
}
