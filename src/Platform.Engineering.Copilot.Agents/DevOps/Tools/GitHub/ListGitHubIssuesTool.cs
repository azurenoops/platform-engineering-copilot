using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Tools;
using System.ComponentModel;
using System.Text.Json;
using System.Web;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for listing and filtering GitHub issues by state, labels, assignees, and other criteria.
/// Uses GitHub REST API v3 GET /repos/{owner}/{repo}/issues endpoint.
/// </summary>
public class ListGitHubIssuesTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public ListGitHubIssuesTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("list_github_issues")]
    [Description("Lists GitHub issues with filtering by state, labels, assignees, creator, and sorting options")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("OPTIONAL: Filter by issue state - 'open', 'closed', or 'all' (default: 'open')")]
        string? state = "open",
        
        [Description("OPTIONAL: Comma-separated list of labels to filter by (e.g., 'bug,high-priority')")]
        string? labels = null,
        
        [Description("OPTIONAL: Filter by assignee username (use 'none' for unassigned, '*' for any assigned)")]
        string? assignee = null,
        
        [Description("OPTIONAL: Filter by creator username")]
        string? creator = null,
        
        [Description("OPTIONAL: Filter by mentioned username")]
        string? mentioned = null,
        
        [Description("OPTIONAL: Sort by 'created', 'updated', or 'comments' (default: 'created')")]
        string? sort = "created",
        
        [Description("OPTIONAL: Sort direction - 'asc' or 'desc' (default: 'desc')")]
        string? direction = "desc",
        
        [Description("OPTIONAL: Maximum number of issues to return (default: 30, max: 100)")]
        int? maxResults = 30)
    {
        try
        {
            // Validate repository format
            var parts = repository.Split('/');
            if (parts.Length != 2)
            {
                return CreateErrorResponse("Repository must be in format 'owner/repo'");
            }

            var owner = parts[0];
            var repo = parts[1];

            // Validate state
            var validStates = new[] { "open", "closed", "all" };
            if (!string.IsNullOrEmpty(state) && !validStates.Contains(state.ToLower()))
            {
                return CreateErrorResponse($"State must be one of: {string.Join(", ", validStates)}");
            }

            // Validate sort
            var validSorts = new[] { "created", "updated", "comments" };
            if (!string.IsNullOrEmpty(sort) && !validSorts.Contains(sort.ToLower()))
            {
                return CreateErrorResponse($"Sort must be one of: {string.Join(", ", validSorts)}");
            }

            // Validate direction
            var validDirections = new[] { "asc", "desc" };
            if (!string.IsNullOrEmpty(direction) && !validDirections.Contains(direction.ToLower()))
            {
                return CreateErrorResponse($"Direction must be one of: {string.Join(", ", validDirections)}");
            }

            // Validate and limit maxResults
            var perPage = Math.Min(maxResults ?? 30, 100);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHubToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build query parameters
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["state"] = state?.ToLower() ?? "open";
            queryParams["sort"] = sort?.ToLower() ?? "created";
            queryParams["direction"] = direction?.ToLower() ?? "desc";
            queryParams["per_page"] = perPage.ToString();

            if (!string.IsNullOrEmpty(labels))
            {
                queryParams["labels"] = labels;
            }

            if (!string.IsNullOrEmpty(assignee))
            {
                queryParams["assignee"] = assignee;
            }

            if (!string.IsNullOrEmpty(creator))
            {
                queryParams["creator"] = creator;
            }

            if (!string.IsNullOrEmpty(mentioned))
            {
                queryParams["mentioned"] = mentioned;
            }

            var url = $"https://api.github.com/repos/{owner}/{repo}/issues?{queryParams}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to list issues: {response.StatusCode} - {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var issues = JsonSerializer.Deserialize<JsonElement>(content);

            var issueList = issues.EnumerateArray()
                .Where(i => !i.TryGetProperty("pull_request", out _)) // Exclude pull requests
                .Select(issue => new
                {
                    number = issue.GetProperty("number").GetInt32(),
                    title = issue.GetProperty("title").GetString(),
                    state = issue.GetProperty("state").GetString(),
                    htmlUrl = issue.GetProperty("html_url").GetString(),
                    user = issue.GetProperty("user").GetProperty("login").GetString(),
                    labels = issue.TryGetProperty("labels", out var lbls)
                        ? lbls.EnumerateArray().Select(l => l.GetProperty("name").GetString()).ToArray()
                        : Array.Empty<string>(),
                    assignees = issue.TryGetProperty("assignees", out var assgn)
                        ? assgn.EnumerateArray().Select(a => a.GetProperty("login").GetString()).ToArray()
                        : Array.Empty<string>(),
                    milestone = issue.TryGetProperty("milestone", out var ms) && ms.ValueKind != JsonValueKind.Null
                        ? ms.GetProperty("title").GetString()
                        : null,
                    comments = issue.GetProperty("comments").GetInt32(),
                    createdAt = issue.GetProperty("created_at").GetString(),
                    updatedAt = issue.GetProperty("updated_at").GetString()
                })
                .ToArray();

            var result = new
            {
                message = "Issues retrieved successfully",
                repository,
                filters = new
                {
                    state = state ?? "open",
                    labels,
                    assignee,
                    creator,
                    mentioned,
                    sort = sort ?? "created",
                    direction = direction ?? "desc"
                },
                totalCount = issueList.Length,
                issues = issueList
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error listing issues: {ex.Message}");
        }
    }
}
