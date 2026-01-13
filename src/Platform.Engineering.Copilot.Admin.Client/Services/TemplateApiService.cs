using System.Net.Http.Json;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

/// <summary>
/// API service for template management
/// </summary>
public class TemplateApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "api/templates";

    public TemplateApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TemplateListItem>> GetTemplatesAsync(
        string? category = null,
        string? status = null,
        string? keyword = null,
        int skip = 0,
        int take = 50)
    {
        var query = $"{BaseUrl}?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(category)) query += $"&category={Uri.EscapeDataString(category)}";
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrEmpty(keyword)) query += $"&keyword={Uri.EscapeDataString(keyword)}";

        var result = await _httpClient.GetFromJsonAsync<List<TemplateListItem>>(query);
        return result ?? new List<TemplateListItem>();
    }

    public async Task<TemplateDetail?> GetTemplateAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<TemplateDetail>($"{BaseUrl}/{id}");
    }

    public async Task<TemplateDetail?> GetTemplateByNameAsync(string name)
    {
        return await _httpClient.GetFromJsonAsync<TemplateDetail>($"{BaseUrl}/by-name/{Uri.EscapeDataString(name)}");
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<string>>($"{BaseUrl}/categories");
        return result ?? new List<string>();
    }

    public async Task<TemplateDetail?> CreateTemplateAsync(CreateTemplateModel model)
    {
        var response = await _httpClient.PostAsJsonAsync(BaseUrl, model);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TemplateDetail>();
    }

    public async Task<TemplateDetail?> UpdateTemplateAsync(string id, UpdateTemplateModel model)
    {
        var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", model);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TemplateDetail>();
    }

    public async Task DeleteTemplateAsync(string id, string deletedBy = "admin")
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}?deletedBy={Uri.EscapeDataString(deletedBy)}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<TemplateDetail?> SubmitForApprovalAsync(string id, string submittedBy = "admin")
    {
        var response = await _httpClient.PostAsync(
            $"{BaseUrl}/{id}/submit-for-approval?submittedBy={Uri.EscapeDataString(submittedBy)}", 
            null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TemplateDetail>();
    }

    public async Task<TemplateDetail?> ApproveTemplateAsync(string id, ApprovalModel model)
    {
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{id}/approve", model);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TemplateDetail>();
    }

    public async Task<TemplateDetail?> DeprecateTemplateAsync(string id, string deprecatedBy = "admin", string? reason = null)
    {
        var url = $"{BaseUrl}/{id}/deprecate?deprecatedBy={Uri.EscapeDataString(deprecatedBy)}";
        if (!string.IsNullOrEmpty(reason)) url += $"&reason={Uri.EscapeDataString(reason)}";
        
        var response = await _httpClient.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TemplateDetail>();
    }

    public async Task<ValidationResult> ValidateTemplateAsync(ValidateTemplateModel model)
    {
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/validate", model);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ValidationResult>() ?? new ValidationResult();
    }

    #region Git Sync Operations

    /// <summary>
    /// Import a template from a Git repository
    /// </summary>
    public async Task<GitImportResult?> ImportFromGitAsync(GitImportRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/import-from-git", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitImportResult>();
    }

    /// <summary>
    /// Sync a single template from its Git source
    /// </summary>
    public async Task<GitSyncResult?> SyncTemplateAsync(string templateId, bool force = false)
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}/{templateId}/sync?force={force}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitSyncResult>();
    }

    /// <summary>
    /// Sync all templates with Git sources
    /// </summary>
    public async Task<GitSyncBatchResult?> SyncAllTemplatesAsync(bool force = false)
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}/sync-all?force={force}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitSyncBatchResult>();
    }

    /// <summary>
    /// Check if a template has changes in Git
    /// </summary>
    public async Task<GitDiffResult?> CheckGitStatusAsync(string templateId)
    {
        return await _httpClient.GetFromJsonAsync<GitDiffResult>($"{BaseUrl}/{templateId}/git-status");
    }

    /// <summary>
    /// Match templates using natural language
    /// </summary>
    public async Task<TemplateMatchResult?> MatchTemplatesAsync(NaturalLanguageMatchRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/match", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TemplateMatchResult>();
    }

    #endregion
}

#region Git Sync Models

public class GitImportRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string Path { get; set; } = string.Empty;
}

public class GitImportResult
{
    public bool Success { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CommitSha { get; set; }
}

public class GitSyncResult
{
    public bool Success { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public bool WasUpdated { get; set; }
    public string? CommitSha { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class GitSyncBatchResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Updated { get; set; } = new();
    public List<string> Unchanged { get; set; } = new();
    public List<string> Skipped { get; set; } = new();
    public List<GitSyncFailure> Failed { get; set; } = new();
}

public class GitSyncFailure
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class GitDiffResult
{
    public bool Success { get; set; }
    public bool HasChanges { get; set; }
    public string? CurrentSha { get; set; }
    public string? LatestSha { get; set; }
    public DateTime? LastSynced { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class NaturalLanguageMatchRequest
{
    public string UserRequest { get; set; } = string.Empty;
    public double MinimumScore { get; set; } = 0.3;
    public int MaxResults { get; set; } = 5;
}

public class TemplateMatchResult
{
    public bool Success { get; set; }
    public string UserRequest { get; set; } = string.Empty;
    public List<TemplateMatch> Matches { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class TemplateMatch
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

#endregion
