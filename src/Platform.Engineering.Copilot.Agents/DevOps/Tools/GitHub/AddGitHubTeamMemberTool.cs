using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for adding members to GitHub organization teams with role assignment.
/// Uses GitHub REST API v3 PUT /orgs/{org}/teams/{team_slug}/memberships/{username} endpoint.
/// </summary>
public class AddGitHubTeamMemberTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public AddGitHubTeamMemberTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("add_github_team_member")]
    [Description("Adds a user to a GitHub organization team with specified role (member or maintainer)")]
    public async Task<string> ExecuteAsync(
        [Description("Organization name (e.g., 'azure', 'microsoft')")]
        string organization,
        
        [Description("Team slug (URL-friendly team name, e.g., 'platform-engineering-team')")]
        string teamSlug,
        
        [Description("GitHub username to add to the team")]
        string username,
        
        [Description("OPTIONAL: Team role - 'member' or 'maintainer' (default: 'member')")]
        string? role = "member")
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(organization))
            {
                return CreateErrorResponse("Organization name is required");
            }

            if (string.IsNullOrWhiteSpace(teamSlug))
            {
                return CreateErrorResponse("Team slug is required");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return CreateErrorResponse("Username is required");
            }

            // Validate role
            var validRoles = new[] { "member", "maintainer" };
            var selectedRole = role?.ToLower() ?? "member";
            if (!validRoles.Contains(selectedRole))
            {
                return CreateErrorResponse($"Role must be one of: {string.Join(", ", validRoles)}");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHub.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Build membership payload
            var membershipPayload = new Dictionary<string, string>
            {
                ["role"] = selectedRole
            };

            // Add user to team
            var url = $"https://api.github.com/orgs/{organization}/teams/{teamSlug}/memberships/{username}";
            var content = new StringContent(
                JsonSerializer.Serialize(membershipPayload),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PutAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to add team member: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var membership = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var state = membership.GetProperty("state").GetString();
            var isNewMember = state == "pending";

            var result = new
            {
                message = isNewMember 
                    ? $"Invitation sent to {username} to join team {teamSlug}"
                    : $"User {username} added to team {teamSlug} successfully",
                organization,
                team = teamSlug,
                username,
                role = membership.GetProperty("role").GetString(),
                state = state,
                url = membership.GetProperty("url").GetString()
            };

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error adding team member: {ex.Message}");
        }
    }
}
