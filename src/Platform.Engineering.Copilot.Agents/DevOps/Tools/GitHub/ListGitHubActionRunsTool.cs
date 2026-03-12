using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using System.ComponentModel;
using System.Text.Json;
using System.Web;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for listing GitHub Actions workflow runs with filtering and status information.
/// Uses GitHub REST API v3 GET /repos/{owner}/{repo}/actions/runs endpoint.
/// </summary>
public class ListGitHubActionRunsTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public ListGitHubActionRunsTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("list_github_action_runs")]
    [Description("Lists GitHub Actions workflow runs with filtering by workflow, branch, status, and conclusion")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("OPTIONAL: Filter by workflow ID or filename (e.g., 'deploy.yml')")]
        string? workflowId = null,
        
        [Description("OPTIONAL: Filter by branch name")]
        string? branch = null,
        
        [Description("OPTIONAL: Filter by actor (GitHub username who triggered the run)")]
        string? actor = null,
        
        [Description("OPTIONAL: Filter by status - 'queued', 'in_progress', 'completed'")]
        string? status = null,
        
        [Description("OPTIONAL: Filter by conclusion - 'success', 'failure', 'cancelled', 'skipped', 'neutral'")]
        string? conclusion = null,
        
        [Description("OPTIONAL: Maximum number of runs to return (default: 30, max: 100)")]
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

            // Validate status if provided
            var validStatuses = new[] { "queued", "in_progress", "completed" };
            if (!string.IsNullOrEmpty(status) && !validStatuses.Contains(status.ToLower()))
            {
                return CreateErrorResponse($"Status must be one of: {string.Join(", ", validStatuses)}");
            }

            // Validate conclusion if provided
            var validConclusions = new[] { "success", "failure", "cancelled", "skipped", "neutral", "timed_out", "action_required" };
            if (!string.IsNullOrEmpty(conclusion) && !validConclusions.Contains(conclusion.ToLower()))
            {
                return CreateErrorResponse($"Conclusion must be one of: {string.Join(", ", validConclusions)}");
            }

            // Validate and limit maxResults
            var perPage = Math.Min(maxResults ?? 30, 100);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHub.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build URL - filter by workflow if specified
            string url;
            if (!string.IsNullOrEmpty(workflowId))
            {
                url = $"https://api.github.com/repos/{owner}/{repo}/actions/workflows/{workflowId}/runs";
            }
            else
            {
                url = $"https://api.github.com/repos/{owner}/{repo}/actions/runs";
            }

            // Build query parameters
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["per_page"] = perPage.ToString();

            if (!string.IsNullOrEmpty(branch))
            {
                queryParams["branch"] = branch;
            }

            if (!string.IsNullOrEmpty(actor))
            {
                queryParams["actor"] = actor;
            }

            if (!string.IsNullOrEmpty(status))
            {
                queryParams["status"] = status.ToLower();
            }

            url += $"?{queryParams}";

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to list workflow runs: {response.StatusCode} - {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            var runs = data.GetProperty("workflow_runs").EnumerateArray();

            // Filter by conclusion if specified (API doesn't support this as query param)
            if (!string.IsNullOrEmpty(conclusion))
            {
                runs = runs.Where(r => 
                    r.TryGetProperty("conclusion", out var concl) && 
                    concl.ValueKind != JsonValueKind.Null &&
                    concl.GetString()?.Equals(conclusion, StringComparison.OrdinalIgnoreCase) == true);
            }

            var runList = runs.Select(run => new
            {
                id = run.GetProperty("id").GetInt64(),
                runNumber = run.GetProperty("run_number").GetInt32(),
                workflowId = run.GetProperty("workflow_id").GetInt64(),
                workflowName = run.GetProperty("name").GetString(),
                headBranch = run.GetProperty("head_branch").GetString(),
                status = run.GetProperty("status").GetString(),
                conclusion = run.TryGetProperty("conclusion", out var concl) && concl.ValueKind != JsonValueKind.Null
                    ? concl.GetString()
                    : null,
                actor = run.GetProperty("actor").GetProperty("login").GetString(),
                eventType = run.GetProperty("event").GetString(),
                htmlUrl = run.GetProperty("html_url").GetString(),
                createdAt = run.GetProperty("created_at").GetString(),
                updatedAt = run.GetProperty("updated_at").GetString(),
                runStartedAt = run.TryGetProperty("run_started_at", out var started) && started.ValueKind != JsonValueKind.Null
                    ? started.GetString()
                    : null
            }).ToArray();

            var result = new
            {
                message = "Workflow runs retrieved successfully",
                repository,
                filters = new
                {
                    workflowId,
                    branch,
                    actor,
                    status,
                    conclusion
                },
                totalCount = data.GetProperty("total_count").GetInt32(),
                returnedCount = runList.Length,
                runs = runList
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error listing workflow runs: {ex.Message}");
        }
    }
}
