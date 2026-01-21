using System.Net.Http.Json;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

public class ComplianceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComplianceApiService> _logger;

    public ComplianceApiService(HttpClient httpClient, ILogger<ComplianceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ComplianceSummary?> GetComplianceSummaryAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ComplianceSummary>("api/compliance/summary");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get compliance summary");
            throw;
        }
    }

    public async Task RunComplianceScanAsync(string? environmentId = null)
    {
        try
        {
            var url = string.IsNullOrEmpty(environmentId) 
                ? "api/compliance/scan" 
                : $"api/compliance/scan?environmentId={environmentId}";
            
            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run compliance scan");
            throw;
        }
    }

    public async Task RunEnvironmentScanAsync(string environmentId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/compliance/environments/{environmentId}/scan", null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run environment scan for {EnvironmentId}", environmentId);
            throw;
        }
    }

    public async Task<EnvironmentComplianceDetail?> GetEnvironmentComplianceAsync(string environmentId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<EnvironmentComplianceDetail>($"api/compliance/environments/{environmentId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environment compliance for {EnvironmentId}", environmentId);
            throw;
        }
    }

    public async Task ScanEnvironmentAsync(string environmentId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/compliance/scan/{environmentId}", null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan environment {EnvironmentId}", environmentId);
            throw;
        }
    }
}
