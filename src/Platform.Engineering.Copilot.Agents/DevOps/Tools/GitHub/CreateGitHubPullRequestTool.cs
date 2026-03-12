using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for creating GitHub pull requests with title, body, reviewers, and labels.
/// Uses GitHub REST API v3 POST /repos/{owner}/{repo}/pulls endpoint.
/// </summary>
public class CreateGitHubPullRequestTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public CreateGitHubPullRequestTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("create_github_pull_request")]
    [Description("Creates a new GitHub pull request from a source branch to a target branch with reviewers and labels")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("Pull request title")]
        string title,
        
        [Description("Pull request body/description in Markdown format")]
        string body,
        
        [Description("Source branch name (the branch with your changes)")]
        string sourceBranch,
        
        [Description("Target branch name (the branch you want to merge into, typically 'main' or 'develop')")]
        string targetBranch,
        
        [Description("OPTIONAL: Comma-separated list of GitHub usernames to request as reviewers")]
        string? reviewers = null,
        
        [Description("OPTIONAL: Comma-separated list of labels to add (e.g., 'enhancement,ready-for-review')")]
        string? labels = null,
        
        [Description("OPTIONAL: Set to 'true' to create as draft pull request")]
        string? draft = "false")
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
                return CreateErrorResponse("Pull request title is required");
            }

            if (string.IsNullOrWhiteSpace(sourceBranch))
            {
                return CreateErrorResponse("Source branch is required");
            }

            if (string.IsNullOrWhiteSpace(targetBranch))
            {
                return CreateErrorResponse("Target branch is required");
            }

            if (sourceBranch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase))
            {
                return CreateErrorResponse("Source and target branches must be different");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHub.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build PR payload
            var prPayload = new Dictionary<string, object>
            {
                ["title"] = title,
                ["body"] = body ?? string.Empty,
                ["head"] = sourceBranch,
                ["base"] = targetBranch,
                ["draft"] = bool.TryParse(draft, out var isDraft) && isDraft
            };

            // Create the pull request
            var createUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls";
            var content = new StringContent(
                JsonSerializer.Serialize(prPayload),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(createUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to create pull request: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var pr = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var prNumber = pr.GetProperty("number").GetInt32();

            // Add reviewers if provided
            if (!string.IsNullOrEmpty(reviewers))
            {
                var reviewerList = reviewers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToArray();

                if (reviewerList.Length > 0)
                {
                    var reviewersUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/requested_reviewers";
                    var reviewersPayload = new { reviewers = reviewerList };
                    var reviewersContent = new StringContent(
                        JsonSerializer.Serialize(reviewersPayload),
                        Encoding.UTF8,
                        "application/json");

                    await httpClient.PostAsync(reviewersUrl, reviewersContent);
                }
            }

            // Add labels if provided
            if (!string.IsNullOrEmpty(labels))
            {
                var labelList = labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToArray();

                if (labelList.Length > 0)
                {
                    var labelsUrl = $"https://api.github.com/repos/{owner}/{repo}/issues/{prNumber}/labels";
                    var labelsPayload = new { labels = labelList };
                    var labelsContent = new StringContent(
                        JsonSerializer.Serialize(labelsPayload),
                        Encoding.UTF8,
                        "application/json");

                    await httpClient.PostAsync(labelsUrl, labelsContent);
                }
            }

            // Get updated PR details
            var prUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";
            var finalResponse = await httpClient.GetAsync(prUrl);
            var finalContent = await finalResponse.Content.ReadAsStringAsync();
            var finalPr = JsonSerializer.Deserialize<JsonElement>(finalContent);

            var result = new
            {
                message = "Pull request created successfully",
                pullRequest = new
                {
                    number = finalPr.GetProperty("number").GetInt32(),
                    title = finalPr.GetProperty("title").GetString(),
                    state = finalPr.GetProperty("state").GetString(),
                    draft = finalPr.GetProperty("draft").GetBoolean(),
                    htmlUrl = finalPr.GetProperty("html_url").GetString(),
                    sourceBranch = finalPr.GetProperty("head").GetProperty("ref").GetString(),
                    targetBranch = finalPr.GetProperty("base").GetProperty("ref").GetString(),
                    createdAt = finalPr.GetProperty("created_at").GetString(),
                    user = finalPr.GetProperty("user").GetProperty("login").GetString(),
                    requestedReviewers = finalPr.TryGetProperty("requested_reviewers", out var rvwrs)
                        ? rvwrs.EnumerateArray().Select(r => r.GetProperty("login").GetString()).ToArray()
                        : Array.Empty<string>()
                }
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error creating pull request: {ex.Message}");
        }
    }
}
