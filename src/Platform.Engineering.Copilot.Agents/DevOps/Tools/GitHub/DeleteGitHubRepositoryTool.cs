using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using System.ComponentModel;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for deleting GitHub repositories with safety confirmation.
/// Uses GitHub REST API v3 DELETE /repos/{owner}/{repo} endpoint.
/// WARNING: This action is permanent and cannot be undone.
/// </summary>
public class DeleteGitHubRepositoryTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public DeleteGitHubRepositoryTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("delete_github_repository")]
    [Description("Permanently deletes a GitHub repository. WARNING: This action cannot be undone! Use with extreme caution.")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/temp-repo')")]
        string repository,
        
        [Description("Confirmation - must exactly match the full repository name 'owner/repo' to proceed")]
        string confirmation)
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

            // Safety check: require exact confirmation
            if (confirmation != repository)
            {
                return CreateErrorResponse(
                    $"Confirmation does not match. To delete '{repository}', " +
                    $"you must provide exact confirmation value matching 'owner/repo'. " +
                    $"Received: '{confirmation}'");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHub.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // First, verify the repository exists and get its details
            var getUrl = $"https://api.github.com/repos/{owner}/{repo}";
            var getResponse = await httpClient.GetAsync(getUrl);
            
            if (!getResponse.IsSuccessStatusCode)
            {
                if (getResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return CreateErrorResponse($"Repository '{repository}' not found");
                }
                
                var errorContent = await getResponse.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to verify repository: {getResponse.StatusCode} - {errorContent}");
            }

            var repoContent = await getResponse.Content.ReadAsStringAsync();
            var repoData = JsonSerializer.Deserialize<JsonElement>(repoContent);
            
            var repoInfo = new
            {
                name = repoData.GetProperty("name").GetString(),
                fullName = repoData.GetProperty("full_name").GetString(),
                description = repoData.TryGetProperty("description", out var desc) ? desc.GetString() : "No description",
                visibility = repoData.GetProperty("private").GetBoolean() ? "private" : "public",
                createdAt = repoData.GetProperty("created_at").GetString(),
                size = repoData.GetProperty("size").GetInt32()
            };

            // Perform the deletion
            var deleteUrl = $"https://api.github.com/repos/{owner}/{repo}";
            var deleteResponse = await httpClient.DeleteAsync(deleteUrl);

            if (!deleteResponse.IsSuccessStatusCode)
            {
                var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to delete repository: {deleteResponse.StatusCode} - {errorContent}");
            }

            // Verify deletion
            var verifyResponse = await httpClient.GetAsync(getUrl);
            var deletionConfirmed = verifyResponse.StatusCode == System.Net.HttpStatusCode.NotFound;

            return CreateSuccessResponse(new
            {
                message = "Repository deleted successfully",
                deletionConfirmed,
                deletedRepository = repoInfo,
                warning = "This action is permanent and cannot be undone",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error deleting repository: {ex.Message}");
        }
    }
}
