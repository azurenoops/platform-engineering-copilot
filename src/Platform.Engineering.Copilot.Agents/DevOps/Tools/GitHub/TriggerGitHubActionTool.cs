using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Tools;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;

/// <summary>
/// Tool for triggering GitHub Actions workflow runs manually.
/// Uses GitHub REST API v3 POST /repos/{owner}/{repo}/actions/workflows/{workflow_id}/dispatches endpoint.
/// </summary>
public class TriggerGitHubActionTool : BaseTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayOptions _gatewayOptions;
    private readonly DevOpsAgentOptions _devOpsOptions;

    public TriggerGitHubActionTool(
        IHttpClientFactory httpClientFactory,
        GatewayOptions gatewayOptions,
        DevOpsAgentOptions devOpsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _devOpsOptions = devOpsOptions;
    }

    [KernelFunction("trigger_github_action")]
    [Description("Manually triggers a GitHub Actions workflow run with optional input parameters")]
    public async Task<string> ExecuteAsync(
        [Description("Repository identifier in format 'owner/repo' (e.g., 'azure/azure-sdk')")]
        string repository,
        
        [Description("Workflow ID or filename (e.g., 'deploy.yml' or workflow ID number)")]
        string workflowId,
        
        [Description("Branch or tag ref to run the workflow on (e.g., 'main', 'refs/heads/feature', 'refs/tags/v1.0')")]
        string ref_,
        
        [Description("OPTIONAL: JSON string of input parameters for the workflow (e.g., '{\"environment\":\"production\",\"version\":\"1.0.0\"}')")] 
        string? inputs = null)
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
            if (string.IsNullOrWhiteSpace(workflowId))
            {
                return CreateErrorResponse("Workflow ID or filename is required");
            }

            if (string.IsNullOrWhiteSpace(ref_))
            {
                return CreateErrorResponse("Branch or tag ref is required");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_gatewayOptions.GitHubToken}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // Parse inputs if provided
            Dictionary<string, object>? inputsDict = null;
            if (!string.IsNullOrEmpty(inputs))
            {
                try
                {
                    var jsonDoc = JsonSerializer.Deserialize<JsonElement>(inputs);
                    inputsDict = new Dictionary<string, object>();
                    foreach (var prop in jsonDoc.EnumerateObject())
                    {
                        inputsDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString()!,
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
                catch (JsonException ex)
                {
                    return CreateErrorResponse($"Invalid JSON in inputs parameter: {ex.Message}");
                }
            }

            // Build dispatch payload
            var dispatchPayload = new Dictionary<string, object>
            {
                ["ref"] = ref_
            };

            if (inputsDict != null && inputsDict.Count > 0)
            {
                dispatchPayload["inputs"] = inputsDict;
            }

            // Trigger the workflow
            var dispatchUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches";
            var content = new StringContent(
                JsonSerializer.Serialize(dispatchPayload),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(dispatchUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateErrorResponse($"Failed to trigger workflow: {response.StatusCode} - {errorContent}");
            }

            // Get recent workflow runs to find the triggered run
            await Task.Delay(2000); // Wait a bit for the run to be created

            var runsUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/workflows/{workflowId}/runs?per_page=5";
            var runsResponse = await httpClient.GetAsync(runsUrl);
            
            JsonElement? triggeredRun = null;
            if (runsResponse.IsSuccessStatusCode)
            {
                var runsContent = await runsResponse.Content.ReadAsStringAsync();
                var runs = JsonSerializer.Deserialize<JsonElement>(runsContent);
                
                if (runs.TryGetProperty("workflow_runs", out var workflowRuns) && workflowRuns.GetArrayLength() > 0)
                {
                    // Get the most recent run
                    triggeredRun = workflowRuns.EnumerateArray().FirstOrDefault();
                }
            }

            var result = new Dictionary<string, object>
            {
                ["message"] = "Workflow triggered successfully",
                ["repository"] = repository,
                ["workflowId"] = workflowId,
                ["ref"] = ref_
            };

            if (inputsDict != null)
            {
                result["inputs"] = inputsDict;
            }

            if (triggeredRun.HasValue)
            {
                var run = triggeredRun.Value;
                result["run"] = new
                {
                    id = run.GetProperty("id").GetInt64(),
                    runNumber = run.GetProperty("run_number").GetInt32(),
                    status = run.GetProperty("status").GetString(),
                    conclusion = run.TryGetProperty("conclusion", out var concl) && concl.ValueKind != JsonValueKind.Null 
                        ? concl.GetString() 
                        : null,
                    htmlUrl = run.GetProperty("html_url").GetString(),
                    createdAt = run.GetProperty("created_at").GetString()
                };
            }
            else
            {
                result["note"] = "Workflow triggered but run details not yet available. Check GitHub Actions UI.";
            }

            return CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error triggering workflow: {ex.Message}");
        }
    }
}
