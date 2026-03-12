using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using System.ComponentModel;
using System.Text.Json;
using System.Web;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for listing GitHub organization teams with member counts and permissions.
/// Uses GitHub REST API v3 GET /orgs/{org}/teams endpoint.
/// </summary>
public class ListGitHubTeamsTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public ListGitHubTeamsTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("list_github_teams")]
    [Description("Lists teams in a GitHub organization with member counts, permissions, and repository access")]
    public async Task<string> ExecuteAsync(
        [Description("Organization name (e.g., 'azure', 'microsoft')")]
        string organization,
        
        [Description("OPTIONAL: Maximum number of teams to return (default: 30, max: 100)")]
        int? maxResults = 30)
    {
        try
        {
            // Validate organization
            if (string.IsNullOrWhiteSpace(organization))
            {
                return CreateErrorResponse("Organization name is required");
            }

            // Validate and limit maxResults
            var perPage = Math.Min(maxResults ?? 30, 100);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHub.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build query parameters
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["per_page"] = perPage.ToString();

            var url = $"https://api.github.com/orgs/{organization}/teams?{queryParams}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to list teams: {response.StatusCode} - {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var teams = JsonSerializer.Deserialize<JsonElement>(content);

            var teamList = teams.EnumerateArray()
                .Select(team => new
                {
                    id = team.GetProperty("id").GetInt64(),
                    name = team.GetProperty("name").GetString(),
                    slug = team.GetProperty("slug").GetString(),
                    description = team.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null
                        ? desc.GetString()
                        : null,
                    privacy = team.GetProperty("privacy").GetString(),
                    permission = team.GetProperty("permission").GetString(),
                    membersCount = team.TryGetProperty("members_count", out var mc) 
                        ? mc.GetInt32() 
                        : 0,
                    reposCount = team.TryGetProperty("repos_count", out var rc) 
                        ? rc.GetInt32() 
                        : 0,
                    htmlUrl = team.GetProperty("html_url").GetString(),
                    createdAt = team.TryGetProperty("created_at", out var created)
                        ? created.GetString()
                        : null,
                    updatedAt = team.TryGetProperty("updated_at", out var updated)
                        ? updated.GetString()
                        : null
                })
                .ToArray();

            var result = new
            {
                message = "Teams retrieved successfully",
                organization,
                totalCount = teamList.Length,
                teams = teamList
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error listing teams: {ex.Message}");
        }
    }
}
