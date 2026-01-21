using System.Net.Http.Json;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

/// <summary>
/// API service for environment management
/// </summary>
public class EnvironmentApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "api/environments";

    public EnvironmentApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<EnvironmentListItem>> GetEnvironmentsAsync(
        string? subscriptionId = null,
        string? templateId = null,
        string? status = null,
        bool? hasDrift = null,
        int skip = 0,
        int take = 50)
    {
        var query = $"{BaseUrl}?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(subscriptionId)) query += $"&subscriptionId={Uri.EscapeDataString(subscriptionId)}";
        if (!string.IsNullOrEmpty(templateId)) query += $"&templateId={Uri.EscapeDataString(templateId)}";
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (hasDrift.HasValue) query += $"&hasDrift={hasDrift.Value}";

        var result = await _httpClient.GetFromJsonAsync<List<EnvironmentListItem>>(query);
        return result ?? new List<EnvironmentListItem>();
    }

    public async Task<EnvironmentDetail?> GetEnvironmentAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<EnvironmentDetail>($"{BaseUrl}/{id}");
    }

    public async Task<EnvironmentStatusSummary?> GetStatusSummaryAsync()
    {
        return await _httpClient.GetFromJsonAsync<EnvironmentStatusSummary>($"{BaseUrl}/summary");
    }

    public async Task<List<EnvironmentListItem>> GetExpiringEnvironmentsAsync(int withinDays = 7)
    {
        var result = await _httpClient.GetFromJsonAsync<List<EnvironmentListItem>>(
            $"{BaseUrl}/expiring?withinDays={withinDays}");
        return result ?? new List<EnvironmentListItem>();
    }

    public async Task<CreateEnvironmentResult?> CreateEnvironmentAsync(CreateEnvironmentModel model)
    {
        var response = await _httpClient.PostAsJsonAsync(BaseUrl, model);
        var content = await response.Content.ReadAsStringAsync();
        
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<CreateEnvironmentResult>(content, options);
        }
        catch (System.Text.Json.JsonException)
        {
            // If deserialization fails, create a result from the raw response
            return new CreateEnvironmentResult
            {
                Success = response.IsSuccessStatusCode,
                Errors = response.IsSuccessStatusCode 
                    ? null 
                    : new List<string> { $"Server returned: {content}" }
            };
        }
    }

    public async Task<ScaleResult?> ScaleEnvironmentAsync(string id, ScaleEnvironmentModel model)
    {
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{id}/scale", model);
        return await response.Content.ReadFromJsonAsync<ScaleResult>();
    }

    public async Task<CreateEnvironmentResult?> CloneEnvironmentAsync(string id, string newName, string clonedBy = "admin")
    {
        var model = new { NewName = newName, ClonedBy = clonedBy };
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{id}/clone", model);
        return await response.Content.ReadFromJsonAsync<CreateEnvironmentResult>();
    }

    public async Task DeleteEnvironmentAsync(string id, string deletedBy = "admin", bool force = false)
    {
        var response = await _httpClient.DeleteAsync(
            $"{BaseUrl}/{id}?deletedBy={Uri.EscapeDataString(deletedBy)}&force={force}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<DriftDetectionResult?> DetectDriftAsync(string id)
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}/{id}/detect-drift", null);
        return await response.Content.ReadFromJsonAsync<DriftDetectionResult>();
    }

    public async Task<RemediateDriftResult?> RemediateDriftAsync(string id, List<string>? driftItemIds = null, string remediatedBy = "admin")
    {
        var model = new { DriftItemIds = driftItemIds, RemediatedBy = remediatedBy };
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{id}/remediate-drift", model);
        return await response.Content.ReadFromJsonAsync<RemediateDriftResult>();
    }

    public async Task<EnvironmentHealth?> GetHealthAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<EnvironmentHealth>($"{BaseUrl}/{id}/health");
    }

    public async Task<EnvironmentDetail?> ExtendExpirationAsync(string id, DateTime newExpiration, string extendedBy = "admin")
    {
        var model = new { NewExpiration = newExpiration, ExtendedBy = extendedBy };
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{id}/extend", model);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EnvironmentDetail>();
    }

    public async Task<EnvironmentActivityList?> GetActivitiesAsync(
        string id,
        string? activityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 50)
    {
        var query = $"{BaseUrl}/{id}/activities?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(activityType)) query += $"&activityType={Uri.EscapeDataString(activityType)}";
        if (fromDate.HasValue) query += $"&fromDate={fromDate.Value:O}";
        if (toDate.HasValue) query += $"&toDate={toDate.Value:O}";

        return await _httpClient.GetFromJsonAsync<EnvironmentActivityList>(query);
    }

    public async Task<DeployedResourceList?> GetResourcesAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<DeployedResourceList>($"{BaseUrl}/{id}/resources");
    }

    public async Task<SyncResourcesResult?> SyncResourcesAsync(string id)
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}/{id}/sync-resources", null);
        return await response.Content.ReadFromJsonAsync<SyncResourcesResult>();
    }

    public async Task<CreateEnvironmentResult?> ReprovisionEnvironmentAsync(string id, string requestedBy = "admin")
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}/{id}/reprovision?requestedBy={Uri.EscapeDataString(requestedBy)}", null);
        return await response.Content.ReadFromJsonAsync<CreateEnvironmentResult>();
    }

    public async Task<DeleteResourcesResult?> DeleteAzureResourcesAsync(string id, string deletedBy = "admin")
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}/{id}/delete-resources?deletedBy={Uri.EscapeDataString(deletedBy)}", null);
        return await response.Content.ReadFromJsonAsync<DeleteResourcesResult>();
    }

    public async Task<List<EnvironmentListItem>> GetDeletedEnvironmentsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<EnvironmentListItem>>($"{BaseUrl}/deleted");
        return result ?? new List<EnvironmentListItem>();
    }

    public async Task PurgeEnvironmentAsync(string id, string purgedBy = "admin")
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}/purge?purgedBy={Uri.EscapeDataString(purgedBy)}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> PurgeAllDeletedEnvironmentsAsync(string purgedBy = "admin")
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/purge-all?purgedBy={Uri.EscapeDataString(purgedBy)}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PurgeAllResult>();
        return result?.PurgedCount ?? 0;
    }
}
