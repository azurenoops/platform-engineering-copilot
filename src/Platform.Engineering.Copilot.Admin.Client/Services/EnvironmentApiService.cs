using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

/// <summary>HTTP client service for Environment API operations.</summary>
public class EnvironmentApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EnvironmentApiService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EnvironmentApiService(HttpClient httpClient, ILogger<EnvironmentApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(List<EnvironmentDetailDto> Items, int TotalCount)> GetEnvironmentsAsync(
        string? status = null, bool? hasDrift = null, int skip = 0, int take = 10)
    {
        try
        {
            var query = new List<string> { $"skip={skip}", $"take={take}" };
            if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (hasDrift.HasValue) query.Add($"hasDrift={hasDrift.Value}");
            var url = $"api/environments?{string.Join("&", query)}";

            var doc = await _httpClient.GetFromJsonAsync<JsonDocument>(url, JsonOptions);
            if (doc == null) return (new(), 0);

            var items = doc.RootElement.GetProperty("items").Deserialize<List<EnvironmentDetailDto>>(JsonOptions) ?? new();
            var totalCount = doc.RootElement.GetProperty("totalCount").GetInt32();
            return (items, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environments");
            return (new(), 0);
        }
    }

    public async Task<EnvironmentDetailDto?> GetEnvironmentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/environments/{id}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EnvironmentDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<EnvironmentSummaryDto?> GetSummaryAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<EnvironmentSummaryDto>("api/environments/summary", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environment summary");
            return null;
        }
    }

    public async Task<EnvironmentDetailDto?> CreateEnvironmentAsync(CreateEnvironmentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/environments", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EnvironmentDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create environment");
            return null;
        }
    }

    public async Task<ScaleResultDto?> ScaleEnvironmentAsync(Guid id, ScaleEnvironmentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/environments/{id}/scale", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ScaleResultDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scale environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<EnvironmentDetailDto?> CloneEnvironmentAsync(Guid id, CloneEnvironmentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/environments/{id}/clone", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EnvironmentDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clone environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<EnvironmentDetailDto?> ReprovisionEnvironmentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/environments/{id}/reprovision", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EnvironmentDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reprovision environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<bool> DeleteEnvironmentAsync(Guid id, string deletedBy, bool force = false)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"api/environments/{id}?deletedBy={Uri.EscapeDataString(deletedBy)}&force={force}");
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete environment {EnvironmentId}", id);
            return false;
        }
    }

    public async Task<DeleteResourcesResultDto?> DeleteAzureResourcesAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/environments/{id}/delete-resources", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DeleteResourcesResultDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Azure resources for environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<(List<EnvironmentDetailDto> Items, int TotalCount)> GetDeletedEnvironmentsAsync()
    {
        try
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonDocument>("api/environments/deleted", JsonOptions);
            if (doc == null) return (new(), 0);

            var items = doc.RootElement.GetProperty("items").Deserialize<List<EnvironmentDetailDto>>(JsonOptions) ?? new();
            var totalCount = doc.RootElement.GetProperty("totalCount").GetInt32();
            return (items, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deleted environments");
            return (new(), 0);
        }
    }

    public async Task<bool> PurgeEnvironmentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/environments/{id}/purge");
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge environment {EnvironmentId}", id);
            return false;
        }
    }

    public async Task<int> PurgeAllDeletedAsync()
    {
        try
        {
            var response = await _httpClient.DeleteAsync("api/environments/purge-all");
            response.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return doc.RootElement.GetProperty("purgedCount").GetInt32();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge all deleted environments");
            return 0;
        }
    }

    public async Task<(List<ResourceDto> Resources, int TotalCount)> GetResourcesAsync(Guid id)
    {
        try
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonDocument>($"api/environments/{id}/resources", JsonOptions);
            if (doc == null) return (new(), 0);

            var resources = doc.RootElement.GetProperty("resources").Deserialize<List<ResourceDto>>(JsonOptions) ?? new();
            var totalCount = doc.RootElement.GetProperty("totalCount").GetInt32();
            return (resources, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resources for environment {EnvironmentId}", id);
            return (new(), 0);
        }
    }

    public async Task<JsonDocument?> SyncResourcesAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/environments/{id}/sync-resources", null);
            response.EnsureSuccessStatusCode();
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync resources for environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<EnvironmentHealthDto?> GetHealthAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<EnvironmentHealthDto>($"api/environments/{id}/health", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health for environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<ActivityListDto?> GetActivitiesAsync(Guid id, int skip = 0, int take = 20)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ActivityListDto>(
                $"api/environments/{id}/activities?skip={skip}&take={take}", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get activities for environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<(List<EnvironmentDetailDto> Items, int TotalCount, int WithinDays)> GetExpiringEnvironmentsAsync(int withinDays = 7)
    {
        try
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonDocument>(
                $"api/environments/expiring?withinDays={withinDays}", JsonOptions);
            if (doc == null) return (new(), 0, withinDays);

            var items = doc.RootElement.GetProperty("items").Deserialize<List<EnvironmentDetailDto>>(JsonOptions) ?? new();
            var totalCount = doc.RootElement.GetProperty("totalCount").GetInt32();
            var days = doc.RootElement.GetProperty("withinDays").GetInt32();
            return (items, totalCount, days);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get expiring environments");
            return (new(), 0, withinDays);
        }
    }

    public async Task<EnvironmentDetailDto?> ExtendExpirationAsync(Guid id, ExtendExpirationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/environments/{id}/extend", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EnvironmentDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend expiration for environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<DriftDetectionResultDto?> DetectDriftAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/environments/{id}/detect-drift", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DriftDetectionResultDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect drift for environment {EnvironmentId}", id);
            return null;
        }
    }

    public async Task<RemediateDriftResultDto?> RemediateDriftAsync(Guid id, RemediateDriftRequest? request = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/environments/{id}/remediate-drift", request ?? new(), JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RemediateDriftResultDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remediate drift for environment {EnvironmentId}", id);
            return null;
        }
    }
}
