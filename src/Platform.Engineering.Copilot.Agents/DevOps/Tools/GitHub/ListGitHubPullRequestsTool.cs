using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using System.ComponentModel;
using System.Text.Json;
using System.Web;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for listing GitHub pull requests with filtering by state, author, base branch, and sorting.
/// Uses GitHub REST API v3 GET /repos/{owner}/{repo}/pulls endpoint.
/// </summary>
public class ListGitHubPullRequestsTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public ListGitHubPullRequestsTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("list_github_pull_requests")]
    [Description("Lists GitHub pull requests with filtering by state, author, base branch, and sorting options")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("OPTIONAL: Filter by PR state - 'open', 'closed', or 'all' (default: 'open')")]
        string? state = "open",
        
        [Description("OPTIONAL: Filter by base branch (the branch PRs are targeting)")]
        string? baseBranch = null,
        
        [Description("OPTIONAL: Filter by head branch (the branch with changes)")]
        string? headBranch = null,
        
        [Description("OPTIONAL: Sort by 'created', 'updated', 'popularity', or 'long-running' (default: 'created')")]
        string? sort = "created",
        
        [Description("OPTIONAL: Sort direction - 'asc' or 'desc' (default: 'desc')")]
        string? direction = "desc",
        
        [Description("OPTIONAL: Maximum number of pull requests to return (default: 30, max: 100)")]
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
            var validSorts = new[] { "created", "updated", "popularity", "long-running" };
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
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHub.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build query parameters
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["state"] = state?.ToLower() ?? "open";
            queryParams["sort"] = sort?.ToLower() ?? "created";
            queryParams["direction"] = direction?.ToLower() ?? "desc";
            queryParams["per_page"] = perPage.ToString();

            if (!string.IsNullOrEmpty(baseBranch))
            {
                queryParams["base"] = baseBranch;
            }

            if (!string.IsNullOrEmpty(headBranch))
            {
                queryParams["head"] = headBranch;
            }

            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls?{queryParams}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to list pull requests: {response.StatusCode} - {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var prs = JsonSerializer.Deserialize<JsonElement>(content);

            var prList = prs.EnumerateArray()
                .Select(pr => new
                {
                    number = pr.GetProperty("number").GetInt32(),
                    title = pr.GetProperty("title").GetString(),
                    state = pr.GetProperty("state").GetString(),
                    draft = pr.GetProperty("draft").GetBoolean(),
                    htmlUrl = pr.GetProperty("html_url").GetString(),
                    user = pr.GetProperty("user").GetProperty("login").GetString(),
                    sourceBranch = pr.GetProperty("head").GetProperty("ref").GetString(),
                    targetBranch = pr.GetProperty("base").GetProperty("ref").GetString(),
                    labels = pr.TryGetProperty("labels", out var lbls)
                        ? lbls.EnumerateArray().Select(l => l.GetProperty("name").GetString()).ToArray()
                        : Array.Empty<string>(),
                    requestedReviewers = pr.TryGetProperty("requested_reviewers", out var rvwrs)
                        ? rvwrs.EnumerateArray().Select(r => r.GetProperty("login").GetString()).ToArray()
                        : Array.Empty<string>(),
                    createdAt = pr.GetProperty("created_at").GetString(),
                    updatedAt = pr.GetProperty("updated_at").GetString(),
                    mergedAt = pr.TryGetProperty("merged_at", out var merged) && merged.ValueKind != JsonValueKind.Null
                        ? merged.GetString()
                        : null,
                    mergeable = pr.TryGetProperty("mergeable", out var mgbl) && mgbl.ValueKind != JsonValueKind.Null
                        ? mgbl.GetBoolean().ToString()
                        : "unknown"
                })
                .ToArray();

            var result = new
            {
                message = "Pull requests retrieved successfully",
                repository,
                filters = new
                {
                    state = state ?? "open",
                    baseBranch,
                    headBranch,
                    sort = sort ?? "created",
                    direction = direction ?? "desc"
                },
                totalCount = prList.Length,
                pullRequests = prList
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error listing pull requests: {ex.Message}");
        }
    }
}
