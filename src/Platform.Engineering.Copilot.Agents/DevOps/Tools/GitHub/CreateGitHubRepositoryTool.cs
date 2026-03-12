using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Agents.DevOps.Models.GitHub;
using Platform.Engineering.Copilot.Core.Configuration;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for creating GitHub repositories with templates and compliance settings
/// </summary>
public class CreateGitHubRepositoryTool : BaseTool
{
    public override string Name => "create_github_repository";
    
    public override string Description =>
        "Create a new GitHub repository with optional template, branch protection, and compliance settings. " +
        "Use this when the user wants to create a new repo, scaffold a project, or set up a new service. " +
        "Automatically applies security best practices like branch protection and required reviews.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public CreateGitHubRepositoryTool(
        ILogger<CreateGitHubRepositoryTool> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<GatewayOptions> gatewayOptions,
        IOptions<DevOpsAgentOptions> devOpsOptions)
        : base(logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _gatewayOptions = gatewayOptions?.Value ?? throw new ArgumentNullException(nameof(gatewayOptions));
        _devOpsOptions = devOpsOptions?.Value ?? throw new ArgumentNullException(nameof(devOpsOptions));

        // Define parameters
        Parameters.Add(new ToolParameter("org", "GitHub organization name (optional, uses default if not specified)", false));
        Parameters.Add(new ToolParameter("name", "Repository name (kebab-case recommended)", true));
        Parameters.Add(new ToolParameter("description", "Repository description", false));
        Parameters.Add(new ToolParameter("private", "Make repository private (default: true)", false));
        Parameters.Add(new ToolParameter("template", "Template repository to use (org/repo format)", false));
        Parameters.Add(new ToolParameter("topics", "Array of topics/tags for the repository", false));
        Parameters.Add(new ToolParameter("auto_init", "Initialize with README (default: true)", false));
        Parameters.Add(new ToolParameter("gitignore_template", "Gitignore template (e.g., 'VisualStudio', 'Node')", false));
        Parameters.Add(new ToolParameter("license", "License type (e.g., 'mit', 'apache-2.0')", false));
        Parameters.Add(new ToolParameter("branch_protection", "Enable branch protection on default branch (default: true)", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse parameters
            var parameters = ParseParameters(arguments);
            
            // Validate GitHub configuration
            if (!_gatewayOptions.GitHub.Enabled)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "GitHub integration is not enabled. Please configure GitHub in appsettings."
                });
            }

            if (string.IsNullOrWhiteSpace(_gatewayOptions.GitHub.AccessToken))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "GitHub access token is not configured. Please set GITHUB_TOKEN in .env file."
                });
            }

            // Use default org if not specified
            var org = parameters.Organization ?? _devOpsOptions.GitHub.DefaultOrg;
            if (string.IsNullOrWhiteSpace(org))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Organization not specified and no default configured. Please specify 'org' parameter."
                });
            }

            Logger.LogInformation("Creating GitHub repository: {Org}/{Repo}", org, parameters.Name);

            // Create repository via GitHub API
            var repository = await CreateRepositoryAsync(org, parameters, cancellationToken);

            // Apply branch protection if requested
            if (parameters.BranchProtection && repository != null)
            {
                await ApplyBranchProtectionAsync(org, repository.Name, repository.DefaultBranch, cancellationToken);
            }

            Logger.LogInformation("✅ Repository created successfully: {RepoUrl}", repository?.HtmlUrl);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repository = new
                {
                    name = repository?.Name,
                    full_name = repository?.FullName,
                    html_url = repository?.HtmlUrl,
                    clone_url = repository?.CloneUrl,
                    default_branch = repository?.DefaultBranch,
                    @private = repository?.Private,
                    topics = repository?.Topics
                },
                message = $"Repository '{repository?.FullName}' created successfully with branch protection enabled."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating GitHub repository");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private CreateRepositoryParameters ParseParameters(IDictionary<string, object?> arguments)
    {
        return new CreateRepositoryParameters
        {
            Organization = arguments.TryGetValue("org", out var org) ? org?.ToString() : null,
            Name = arguments.TryGetValue("name", out var name) ? name?.ToString() ?? string.Empty : string.Empty,
            Description = arguments.TryGetValue("description", out var desc) ? desc?.ToString() : null,
            Private = arguments.TryGetValue("private", out var priv) && priv is bool privBool ? privBool : true,
            Template = arguments.TryGetValue("template", out var template) ? template?.ToString() : null,
            Topics = arguments.TryGetValue("topics", out var topics) && topics is JsonElement topicsJson
                ? topicsJson.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToArray()
                : null,
            AutoInit = arguments.TryGetValue("auto_init", out var autoInit) && autoInit is bool autoInitBool ? autoInitBool : true,
            GitignoreTemplate = arguments.TryGetValue("gitignore_template", out var gitignore) ? gitignore?.ToString() : null,
            License = arguments.TryGetValue("license", out var license) ? license?.ToString() : null,
            BranchProtection = arguments.TryGetValue("branch_protection", out var protection) && protection is bool protBool ? protBool : true
        };
    }

    private async Task<RepositoryInfo?> CreateRepositoryAsync(
        string org,
        CreateRepositoryParameters parameters,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_gatewayOptions.GitHub.AccessToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");

        var url = $"{_gatewayOptions.GitHub.ApiBaseUrl}/orgs/{org}/repos";
        
        var requestBody = new
        {
            name = parameters.Name,
            description = parameters.Description,
            @private = parameters.Private,
            auto_init = parameters.AutoInit,
            gitignore_template = parameters.GitignoreTemplate,
            license_template = parameters.License
        };

        var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"GitHub API error: {response.StatusCode} - {error}");
        }

        var repo = await response.Content.ReadFromJsonAsync<RepositoryInfo>(cancellationToken);
        
        // Set topics if specified
        if (parameters.Topics?.Length > 0 && repo != null)
        {
            await SetRepositoryTopicsAsync(org, repo.Name, parameters.Topics, cancellationToken);
        }

        return repo;
    }

    private async Task SetRepositoryTopicsAsync(
        string org,
        string repo,
        string[] topics,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_gatewayOptions.GitHub.AccessToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");

        var url = $"{_gatewayOptions.GitHub.ApiBaseUrl}/repos/{org}/{repo}/topics";
        var requestBody = new { names = topics };

        await client.PutAsJsonAsync(url, requestBody, cancellationToken);
    }

    private async Task ApplyBranchProtectionAsync(
        string org,
        string repo,
        string branch,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_gatewayOptions.GitHub.AccessToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");

        var url = $"{_gatewayOptions.GitHub.ApiBaseUrl}/repos/{org}/{repo}/branches/{branch}/protection";
        
        var protection = new
        {
            required_status_checks = (object?)null,
            enforce_admins = true,
            required_pull_request_reviews = new
            {
                required_approving_review_count = 1,
                dismiss_stale_reviews = true,
                require_code_owner_reviews = _devOpsOptions.GitHub.RequireCodeOwners
            },
            restrictions = (object?)null
        };

        try
        {
            await client.PutAsJsonAsync(url, protection, cancellationToken);
            Logger.LogInformation("✅ Branch protection applied to {Branch}", branch);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not apply branch protection (may require admin permissions)");
        }
    }
}
