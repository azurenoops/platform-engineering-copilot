using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

/// <summary>HTTP client service for Template API operations.</summary>
public class TemplateApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TemplateApiService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public TemplateApiService(HttpClient httpClient, ILogger<TemplateApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<TemplateSummaryDto>> GetTemplatesAsync(string? category = null, string? status = null, string? search = null)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(category)) query.Add($"category={Uri.EscapeDataString(category)}");
            if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            var url = query.Count > 0 ? $"api/templates?{string.Join("&", query)}" : "api/templates";

            return await _httpClient.GetFromJsonAsync<List<TemplateSummaryDto>>(url, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get templates");
            return new();
        }
    }

    public async Task<TemplateDetailDto?> GetTemplateByIdAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/templates/{id}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template {TemplateId}", id);
            return null;
        }
    }

    public async Task<TemplateDetailDto?> GetTemplateByNameAsync(string name, string? version = null)
    {
        try
        {
            var url = $"api/templates/by-name/{Uri.EscapeDataString(name)}";
            if (!string.IsNullOrEmpty(version)) url += $"?version={Uri.EscapeDataString(version)}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template by name {Name}", name);
            return null;
        }
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>("api/templates/categories", JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get categories");
            return new();
        }
    }

    public async Task<TemplateDetailDto?> CreateTemplateAsync(CreateTemplateRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/templates", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create template");
            return null;
        }
    }

    public async Task<TemplateDetailDto?> UpdateTemplateAsync(Guid id, UpdateTemplateRequest request, string? etag = null)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"api/templates/{id}")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            if (!string.IsNullOrEmpty(etag))
                httpRequest.Headers.TryAddWithoutValidation("If-Match", etag);

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update template {TemplateId}", id);
            return null;
        }
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, string deletedBy)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/templates/{id}?deletedBy={Uri.EscapeDataString(deletedBy)}");
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete template {TemplateId}", id);
            return false;
        }
    }

    public async Task<TemplateDetailDto?> SubmitForApprovalAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/templates/{id}/submit-for-approval", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit template {TemplateId} for approval", id);
            return null;
        }
    }

    public async Task<TemplateDetailDto?> ApproveTemplateAsync(Guid id, ApprovalRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/templates/{id}/approve", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve template {TemplateId}", id);
            return null;
        }
    }

    public async Task<TemplateDetailDto?> DeprecateTemplateAsync(Guid id, string deprecatedBy, string reason)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"api/templates/{id}/deprecate?deprecatedBy={Uri.EscapeDataString(deprecatedBy)}&reason={Uri.EscapeDataString(reason)}", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deprecate template {TemplateId}", id);
            return null;
        }
    }

    public async Task<TemplateValidationResultDto?> ValidateTemplateAsync(ValidateTemplateRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/templates/validate", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateValidationResultDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate template");
            return null;
        }
    }

    public async Task<List<TemplateParameterDto>> ParseBicepParametersAsync(ParseBicepParametersRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/templates/parse-bicep-parameters", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<TemplateParameterDto>>(JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Bicep parameters");
            return new();
        }
    }

    public async Task<List<TemplateParameterDto>> ParseBicepParametersFromGitAsync(ParseBicepFromGitRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/templates/parse-bicep-parameters-from-git", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<TemplateParameterDto>>(JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Bicep parameters from Git");
            return new();
        }
    }

    public async Task<TemplateDetailDto?> ImportFromGitAsync(ImportFromGitRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/templates/import-from-git", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import template from Git");
            return null;
        }
    }

    public async Task<TemplateDetailDto?> SyncTemplateAsync(Guid id, bool force = false)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/templates/{id}/sync?force={force}", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateDetailDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync template {TemplateId}", id);
            return null;
        }
    }

    public async Task<JsonDocument?> SyncAllTemplatesAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/templates/sync-all", null);
            response.EnsureSuccessStatusCode();
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync all templates");
            return null;
        }
    }

    public async Task<GitStatusDto?> GetGitStatusAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<GitStatusDto>($"api/templates/{id}/git-status", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Git status for template {TemplateId}", id);
            return null;
        }
    }

    public async Task<TemplateMatchResultDto?> MatchTemplatesAsync(TemplateMatchRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/templates/match", request, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TemplateMatchResultDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to match templates");
            return null;
        }
    }
}
