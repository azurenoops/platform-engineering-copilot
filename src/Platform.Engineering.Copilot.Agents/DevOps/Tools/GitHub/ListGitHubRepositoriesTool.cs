using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Agents.DevOps.Models.GitHub;
using Platform.Engineering.Copilot.Core.Configuration;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for listing GitHub repositories with filtering
/// </summary>
public class ListGitHubRepositoriesTool : BaseTool
{
    public override string Name => "list_github_repositories";
    
    public override string Description =>
        "List GitHub repositories with optional filtering by organization, topic, visibility, or archived status. " +
        "Use this to discover existing repositories, check naming conventions, or find repos by characteristics.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public ListGitHubRepositoriesTool(
        ILogger<ListGitHubRepositoriesTool> logger,
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
        Parameters.Add(new ToolParameter("topic", "Filter by topic/tag", false));
        Parameters.Add(new ToolParameter("archived", "Include archived repositories (default: false)", false));
        Parameters.Add(new ToolParameter("visibility", "Filter by visibility: public, private, or all (default: all)", false));
        Parameters.Add(new ToolParameter("limit", "Maximum number of repositories to return (default: 50)", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate GitHub configuration
            if (!_gatewayOptions.GitHub.Enabled)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "GitHub integration is not enabled."
                });
            }

            if (string.IsNullOrWhiteSpace(_gatewayOptions.GitHub.AccessToken))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "GitHub access token is not configured."
                });
            }

            // Parse parameters
            var org = arguments.TryGetValue("org", out var orgValue) ? orgValue?.ToString() : null;
            org ??= _devOpsOptions.GitHub.DefaultOrg;

            if (string.IsNullOrWhiteSpace(org))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Organization not specified and no default configured."
                });
            }

            var topic = arguments.TryGetValue("topic", out var topicValue) ? topicValue?.ToString() : null;
            var includeArchived = arguments.TryGetValue("archived", out var archivedValue) && archivedValue is bool archived && archived;
            var visibility = arguments.TryGetValue("visibility", out var visValue) ? visValue?.ToString() : "all";
            var limit = arguments.TryGetValue("limit", out var limitValue) && limitValue is int limitInt ? limitInt : 50;

            Logger.LogInformation("Listing GitHub repositories for org: {Org}", org);

            // Fetch repositories from GitHub API
            var repositories = await FetchRepositoriesAsync(org, visibility, limit, cancellationToken);

            // Apply filters
            if (!includeArchived)
            {
                repositories = repositories.Where(r => !r.Name.Contains("[archived]")).ToList(); // Simplified check
            }

            if (!string.IsNullOrWhiteSpace(topic))
            {
                repositories = repositories.Where(r => 
                    r.Topics?.Any(t => t.Equals(topic, StringComparison.OrdinalIgnoreCase)) == true).ToList();
            }

            Logger.LogInformation("✅ Found {Count} repositories", repositories.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = repositories.Count,
                repositories = repositories.Select(r => new
                {
                    name = r.Name,
                    full_name = r.FullName,
                    description = r.Description,
                    html_url = r.HtmlUrl,
                    @private = r.Private,
                    default_branch = r.DefaultBranch,
                    topics = r.Topics,
                    created_at = r.CreatedAt,
                    updated_at = r.UpdatedAt
                })
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing GitHub repositories");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private async Task<List<RepositoryInfo>> FetchRepositoriesAsync(
        string org,
        string visibility,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_gatewayOptions.GitHub.AccessToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");

        var url = $"{_gatewayOptions.GitHub.ApiBaseUrl}/orgs/{org}/repos?type={visibility}&per_page={Math.Min(limit, 100)}";
        
        var response = await client.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"GitHub API error: {response.StatusCode} - {error}");
        }

        var repositories = await response.Content.ReadFromJsonAsync<List<RepositoryInfo>>(cancellationToken);
        return repositories ?? new List<RepositoryInfo>();
    }
}
