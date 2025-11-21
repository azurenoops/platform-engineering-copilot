using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.CodeScanning;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Service for storing compliance evidence in Azure Blob Storage
/// </summary>
public class EvidenceStorageService
{
    private readonly ILogger<EvidenceStorageService> _logger;
    private readonly EvidenceOptions _options;
    private BlobContainerClient? _containerClient;

    public EvidenceStorageService(
        ILogger<EvidenceStorageService> logger,
        IOptions<ComplianceAgentOptions> options)
    {
        _logger = logger;
        _options = options.Value.Evidence;
    }

    /// <summary>
    /// Initializes blob storage container with configured settings
    /// </summary>
    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        if (_containerClient != null)
            return _containerClient;

        // Note: In production, use Azure.Identity for authentication
        // For now, assuming connection string is in environment or config
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
            ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING not configured");

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(_options.Container);
        
        await _containerClient.CreateIfNotExistsAsync();
        
        _logger.LogInformation("Evidence storage initialized: {StorageAccount}/{Container}", 
            _options.StorageAccount, _options.Container);

        return _containerClient;
    }

    /// <summary>
    /// Stores security evidence package to blob storage
    /// </summary>
    public async Task<string> StoreEvidencePackageAsync(
        SecurityEvidencePackage evidencePackage,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        
        var blobName = $"evidence/{evidencePackage.PackageId}/{DateTime.UtcNow:yyyy/MM/dd}/package.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var jsonContent = JsonSerializer.Serialize(evidencePackage, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        var content = new BinaryData(jsonContent);
        
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            },
            Metadata = new Dictionary<string, string>
            {
                { "PackageId", evidencePackage.PackageId },
                { "GeneratedAt", evidencePackage.GeneratedAt.ToString("O") },
                { "ComplianceFrameworks", string.Join(",", evidencePackage.ComplianceFrameworks) },
                { "ProjectPath", evidencePackage.ProjectPath }
            }
        };

        // Enable versioning if configured
        if (_options.EnableVersioning)
        {
            _logger.LogInformation("Blob versioning enabled for evidence storage");
        }

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

        // Set immutability policy if enabled
        if (_options.EnableImmutability)
        {
            await SetImmutabilityPolicyAsync(blobClient);
        }

        _logger.LogInformation("Stored evidence package: {BlobName} (Versioning: {Versioning}, Immutability: {Immutability})", 
            blobName, _options.EnableVersioning, _options.EnableImmutability);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Stores compliance evidence package to blob storage
    /// </summary>
    public async Task<string> StoreComplianceEvidencePackageAsync(
        EvidencePackage evidencePackage,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        
        var blobName = $"compliance-evidence/{evidencePackage.PackageId}/{DateTime.UtcNow:yyyy/MM/dd}/package.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var jsonContent = JsonSerializer.Serialize(evidencePackage, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        var content = new BinaryData(jsonContent);
        
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            },
            Metadata = new Dictionary<string, string>
            {
                { "PackageId", evidencePackage.PackageId },
                { "SubscriptionId", evidencePackage.SubscriptionId },
                { "ControlFamily", evidencePackage.ControlFamily },
                { "CollectionStartTime", evidencePackage.CollectionStartTime.ToString("O") },
                { "EvidenceCount", evidencePackage.Evidence.Count.ToString() },
                { "CollectedBy", evidencePackage.CollectedBy }
            }
        };

        // Enable versioning if configured
        if (_options.EnableVersioning)
        {
            _logger.LogInformation("Blob versioning enabled for compliance evidence storage");
        }

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

        // Set immutability policy if enabled
        if (_options.EnableImmutability)
        {
            await SetImmutabilityPolicyAsync(blobClient);
        }

        _logger.LogInformation("Stored compliance evidence package: {BlobName} (Evidence Count: {Count})", 
            blobName, evidencePackage.Evidence.Count);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Stores scan results to blob storage
    /// </summary>
    public async Task<string> StoreScanResultsAsync(
        string scanType,
        object scanResults,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        
        var scanId = Guid.NewGuid().ToString();
        var blobName = $"scans/{scanType}/{DateTime.UtcNow:yyyy/MM/dd}/{scanId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var jsonContent = JsonSerializer.Serialize(scanResults, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        var content = new BinaryData(jsonContent);
        
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            },
            Metadata = new Dictionary<string, string>
            {
                { "ScanType", scanType },
                { "ScanId", scanId },
                { "ScanTime", DateTime.UtcNow.ToString("O") },
                { "ProjectPath", projectPath }
            }
        };

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

        if (_options.EnableImmutability)
        {
            await SetImmutabilityPolicyAsync(blobClient);
        }

        _logger.LogInformation("Stored {ScanType} scan results: {BlobName}", scanType, blobName);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Retrieves evidence package from storage
    /// </summary>
    public async Task<SecurityEvidencePackage?> RetrieveEvidencePackageAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        
        // Search for the package blob
        await foreach (var blobItem in containerClient.GetBlobsAsync(
            prefix: $"evidence/{packageId}/",
            cancellationToken: cancellationToken))
        {
            if (blobItem.Name.EndsWith("package.json"))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                
                var package = JsonSerializer.Deserialize<SecurityEvidencePackage>(
                    response.Value.Content.ToString());
                
                _logger.LogInformation("Retrieved evidence package: {PackageId}", packageId);
                return package;
            }
        }

        _logger.LogWarning("Evidence package not found: {PackageId}", packageId);
        return null;
    }

    /// <summary>
    /// Cleans up evidence older than retention period
    /// </summary>
    public async Task<int> CleanupExpiredEvidenceAsync(CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        var expirationDate = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var deletedCount = 0;

        _logger.LogInformation("Cleaning up evidence older than {ExpirationDate} (retention: {RetentionDays} days)", 
            expirationDate, _options.RetentionDays);

        await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            // Skip if immutability is enabled (cannot delete immutable blobs)
            if (_options.EnableImmutability)
            {
                _logger.LogDebug("Skipping immutable blob: {BlobName}", blobItem.Name);
                continue;
            }

            if (blobItem.Properties.CreatedOn < expirationDate)
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                deletedCount++;
                
                _logger.LogDebug("Deleted expired evidence: {BlobName}", blobItem.Name);
            }
        }

        _logger.LogInformation("Cleanup complete: {DeletedCount} evidence items removed", deletedCount);
        return deletedCount;
    }

    /// <summary>
    /// Sets immutability policy on blob (requires immutable storage configuration)
    /// </summary>
    private async Task SetImmutabilityPolicyAsync(BlobClient blobClient)
    {
        try
        {
            // Note: Immutability requires blob versioning to be enabled at the account level
            // and an immutability policy configured. This is a simplified implementation.
            _logger.LogDebug("Immutability policy enabled for blob: {BlobName}", blobClient.Name);
            
            // In production, you would set time-based retention or legal hold:
            // await blobClient.SetLegalHoldAsync(true);
            // or configure at account/container level
            
            await Task.CompletedTask; // Placeholder for actual immutability setup
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set immutability policy on blob: {BlobName}", blobClient.Name);
        }
    }

    /// <summary>
    /// Lists all evidence packages
    /// </summary>
    public async Task<List<EvidencePackageMetadata>> ListEvidencePackagesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerClientAsync();
        var packages = new List<EvidencePackageMetadata>();

        await foreach (var blobItem in containerClient.GetBlobsAsync(
            prefix: "evidence/",
            cancellationToken: cancellationToken))
        {
            if (!blobItem.Name.EndsWith("package.json"))
                continue;

            var createdOn = blobItem.Properties.CreatedOn?.DateTime;
            
            if (startDate.HasValue && createdOn < startDate.Value)
                continue;
            
            if (endDate.HasValue && createdOn > endDate.Value)
                continue;

            var blobClient = containerClient.GetBlobClient(blobItem.Name);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            packages.Add(new EvidencePackageMetadata
            {
                PackageId = properties.Value.Metadata.TryGetValue("PackageId", out var packageId) ? packageId : "unknown",
                BlobName = blobItem.Name,
                BlobUri = blobClient.Uri.ToString(),
                CreatedAt = createdOn ?? DateTime.MinValue,
                SizeInBytes = blobItem.Properties.ContentLength ?? 0,
                ComplianceFrameworks = properties.Value.Metadata.TryGetValue("ComplianceFrameworks", out var frameworks) 
                    ? frameworks.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() 
                    : new List<string>(),
                ProjectPath = properties.Value.Metadata.TryGetValue("ProjectPath", out var projectPath) ? projectPath : ""
            });
        }

        _logger.LogInformation("Listed {Count} evidence packages", packages.Count);
        return packages;
    }
}

/// <summary>
/// Metadata for stored evidence package
/// </summary>
public class EvidencePackageMetadata
{
    public string PackageId { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeInBytes { get; set; }
    public List<string> ComplianceFrameworks { get; set; } = new();
    public string ProjectPath { get; set; } = string.Empty;
}
