using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Tools;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for updating GitHub repository settings including description, visibility, and features.
/// Uses GitHub REST API v3 PATCH /repos/{owner}/{repo} endpoint.
/// </summary>
public class UpdateGitHubRepositoryTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public UpdateGitHubRepositoryTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("update_github_repository")]
    [Description("Updates GitHub repository settings including description, visibility, homepage, topics, and features")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("OPTIONAL: New repository description")]
        string? description = null,
        
        [Description("OPTIONAL: New repository visibility - 'public' or 'private'")]
        string? visibility = null,
        
        [Description("OPTIONAL: Repository homepage URL")]
        string? homepage = null,
        
        [Description("OPTIONAL: Comma-separated list of topics/tags (e.g., 'azure,devops,iac')")]
        string? topics = null,
        
        [Description("OPTIONAL: Enable/disable wiki - 'true' or 'false'")]
        string? hasWiki = null,
        
        [Description("OPTIONAL: Enable/disable issues - 'true' or 'false'")]
        string? hasIssues = null,
        
        [Description("OPTIONAL: Enable/disable projects - 'true' or 'false'")]
        string? hasProjects = null,
        
        [Description("OPTIONAL: Enable/disable vulnerability alerts - 'true' or 'false'")]
        string? enableVulnerabilityAlerts = null,
        
        [Description("OPTIONAL: Archive the repository - 'true' or 'false'")]
        string? archived = null)
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

            // Validate at least one update parameter is provided
            if (string.IsNullOrEmpty(description) && 
                string.IsNullOrEmpty(visibility) && 
                string.IsNullOrEmpty(homepage) &&
                string.IsNullOrEmpty(topics) &&
                string.IsNullOrEmpty(hasWiki) &&
                string.IsNullOrEmpty(hasIssues) &&
                string.IsNullOrEmpty(hasProjects) &&
                string.IsNullOrEmpty(enableVulnerabilityAlerts) &&
                string.IsNullOrEmpty(archived))
            {
                return CreateErrorResponse("At least one field must be specified for update");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHubToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build update payload
            var updatePayload = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(description))
                updatePayload["description"] = description;

            if (!string.IsNullOrEmpty(visibility))
            {
                if (visibility.ToLower() != "public" && visibility.ToLower() != "private")
                {
                    return CreateErrorResponse("Visibility must be 'public' or 'private'");
                }
                updatePayload["visibility"] = visibility.ToLower() == "public" ? "public" : "private";
            }

            if (!string.IsNullOrEmpty(homepage))
                updatePayload["homepage"] = homepage;

            if (!string.IsNullOrEmpty(hasWiki) && bool.TryParse(hasWiki, out var wiki))
                updatePayload["has_wiki"] = wiki;

            if (!string.IsNullOrEmpty(hasIssues) && bool.TryParse(hasIssues, out var issues))
                updatePayload["has_issues"] = issues;

            if (!string.IsNullOrEmpty(hasProjects) && bool.TryParse(hasProjects, out var projects))
                updatePayload["has_projects"] = projects;

            if (!string.IsNullOrEmpty(enableVulnerabilityAlerts) && bool.TryParse(enableVulnerabilityAlerts, out var alerts))
                updatePayload["security_and_analysis"] = new
                {
                    advanced_security = new { status = alerts ? "enabled" : "disabled" },
                    secret_scanning = new { status = alerts ? "enabled" : "disabled" },
                    secret_scanning_push_protection = new { status = alerts ? "enabled" : "disabled" }
                };

            if (!string.IsNullOrEmpty(archived) && bool.TryParse(archived, out var archiveStatus))
                updatePayload["archived"] = archiveStatus;

            // Update repository
            var updateUrl = $"https://api.github.com/repos/{owner}/{repo}";
            var updateContent = new StringContent(
                JsonSerializer.Serialize(updatePayload),
                Encoding.UTF8,
                "application/json");

            var updateResponse = await httpClient.PatchAsync(updateUrl, updateContent);
            if (!updateResponse.IsSuccessStatusCode)
            {
                var errorContent = await updateResponse.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to update repository: {updateResponse.StatusCode} - {errorContent}");
            }

            // Update topics if specified (separate API endpoint)
            if (!string.IsNullOrEmpty(topics))
            {
                var topicList = topics.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();

                var topicsUrl = $"https://api.github.com/repos/{owner}/{repo}/topics";
                var topicsPayload = new { names = topicList };
                var topicsContent = new StringContent(
                    JsonSerializer.Serialize(topicsPayload),
                    Encoding.UTF8,
                    "application/json");

                httpClient.DefaultRequestHeaders.Remove("Accept");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.mercy-preview+json");

                var topicsResponse = await httpClient.PutAsync(topicsUrl, topicsContent);
                if (!topicsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await topicsResponse.Content.ReadAsStringAsync();
                    return CreateErrorResponse($"Repository updated but topics failed: {topicsResponse.StatusCode} - {errorContent}");
                }
            }

            var finalResponse = await httpClient.GetAsync(updateUrl);
            var finalContent = await finalResponse.Content.ReadAsStringAsync();
            var finalRepo = JsonSerializer.Deserialize<JsonElement>(finalContent);

            return CreateSuccessResponse(new
            {
                message = "Repository updated successfully",
                repository = new
                {
                    name = finalRepo.GetProperty("name").GetString(),
                    fullName = finalRepo.GetProperty("full_name").GetString(),
                    description = finalRepo.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    visibility = finalRepo.GetProperty("private").GetBoolean() ? "private" : "public",
                    homepage = finalRepo.TryGetProperty("homepage", out var home) ? home.GetString() : null,
                    hasWiki = finalRepo.GetProperty("has_wiki").GetBoolean(),
                    hasIssues = finalRepo.GetProperty("has_issues").GetBoolean(),
                    hasProjects = finalRepo.GetProperty("has_projects").GetBoolean(),
                    archived = finalRepo.GetProperty("archived").GetBoolean(),
                    htmlUrl = finalRepo.GetProperty("html_url").GetString(),
                    updatedAt = finalRepo.GetProperty("updated_at").GetString()
                }
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error updating repository: {ex.Message}");
        }
    }
}
