using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Tools;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for creating GitHub issues with labels, assignees, and milestones.
/// Uses GitHub REST API v3 POST /repos/{owner}/{repo}/issues endpoint.
/// </summary>
public class CreateGitHubIssueTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public CreateGitHubIssueTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("create_github_issue")]
    [Description("Creates a new GitHub issue with title, body, labels, assignees, and optional milestone")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("Issue title")]
        string title,
        
        [Description("Issue body/description in Markdown format")]
        string body,
        
        [Description("OPTIONAL: Comma-separated list of labels (e.g., 'bug,high-priority,security')")]
        string? labels = null,
        
        [Description("OPTIONAL: Comma-separated list of GitHub usernames to assign (e.g., 'octocat,hubot')")]
        string? assignees = null,
        
        [Description("OPTIONAL: Milestone number to associate with this issue")]
        int? milestone = null)
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

            // Validate required fields
            if (string.IsNullOrWhiteSpace(title))
            {
                return CreateErrorResponse("Issue title is required");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHubToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build issue payload
            var issuePayload = new Dictionary<string, object>
            {
                ["title"] = title,
                ["body"] = body ?? string.Empty
            };

            // Add labels if provided
            if (!string.IsNullOrEmpty(labels))
            {
                var labelList = labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToArray();

                if (labelList.Length > 0)
                {
                    issuePayload["labels"] = labelList;
                }
            }

            // Add assignees if provided
            if (!string.IsNullOrEmpty(assignees))
            {
                var assigneeList = assignees.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToArray();

                if (assigneeList.Length > 0)
                {
                    issuePayload["assignees"] = assigneeList;
                }
            }

            // Add milestone if provided
            if (milestone.HasValue)
            {
                issuePayload["milestone"] = milestone.Value;
            }

            // Create the issue
            var createUrl = $"https://api.github.com/repos/{owner}/{repo}/issues";
            var content = new StringContent(
                JsonSerializer.Serialize(issuePayload),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(createUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to create issue: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var issue = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var result = new
            {
                message = "GitHub issue created successfully",
                issue = new
                {
                    number = issue.GetProperty("number").GetInt32(),
                    title = issue.GetProperty("title").GetString(),
                    state = issue.GetProperty("state").GetString(),
                    htmlUrl = issue.GetProperty("html_url").GetString(),
                    createdAt = issue.GetProperty("created_at").GetString(),
                    user = issue.GetProperty("user").GetProperty("login").GetString(),
                    labels = issue.TryGetProperty("labels", out var lbls) 
                        ? lbls.EnumerateArray().Select(l => l.GetProperty("name").GetString()).ToArray() 
                        : Array.Empty<string>(),
                    assignees = issue.TryGetProperty("assignees", out var assgn) 
                        ? assgn.EnumerateArray().Select(a => a.GetProperty("login").GetString()).ToArray() 
                        : Array.Empty<string>(),
                    milestone = issue.TryGetProperty("milestone", out var ms) && ms.ValueKind != JsonValueKind.Null
                        ? ms.GetProperty("title").GetString()
                        : null
                }
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error creating issue: {ex.Message}");
        }
    }
}
