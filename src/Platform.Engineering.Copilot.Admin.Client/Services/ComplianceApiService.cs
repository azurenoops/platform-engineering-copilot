using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

/// <summary>HTTP client service for Compliance API operations.</summary>
public class ComplianceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComplianceApiService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ComplianceApiService(HttpClient httpClient, ILogger<ComplianceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ComplianceSummaryDto?> GetSummaryAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ComplianceSummaryDto>("api/compliance/summary", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get compliance summary");
            return null;
        }
    }

    public async Task<bool> TriggerScanAsync(Guid? environmentId = null)
    {
        try
        {
            var url = environmentId.HasValue
                ? $"api/compliance/scan?environmentId={environmentId}"
                : "api/compliance/scan";
            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger compliance scan");
            return false;
        }
    }

    public async Task<EnvironmentComplianceDto?> GetEnvironmentComplianceAsync(Guid environmentId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<EnvironmentComplianceDto>(
                $"api/compliance/environments/{environmentId}", JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get compliance for environment {EnvironmentId}", environmentId);
            return null;
        }
    }
}
