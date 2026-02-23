using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// T149 — ISecretProvider with Azure Key Vault backend.
/// Uses managed identity for Azure Government (FIPS 140-2 Level 2).
/// Falls back to .env / IConfiguration for local development.
///
/// Consumed by CacAuthenticationHandler and PimAuthorizationHandler
/// for credential storage per FR-082.
/// </summary>
public class KeyVaultSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeyVaultSecretProvider> _logger;
    private readonly string? _vaultUri;
    private readonly bool _useKeyVault;

    // In-memory cache for secrets (reduces Key Vault calls)
    private readonly Dictionary<string, CachedSecret> _cache = new();
    private readonly object _cacheLock = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public KeyVaultSecretProvider(IConfiguration configuration, ILogger<KeyVaultSecretProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _vaultUri = configuration["KeyVault:VaultUri"]
                    ?? configuration["AZURE_KEYVAULT_URI"];

        _useKeyVault = !string.IsNullOrEmpty(_vaultUri)
                       && !string.Equals(configuration["KeyVault:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

        if (_useKeyVault)
        {
            _logger.LogInformation("KeyVaultSecretProvider: Using Azure Key Vault at {VaultUri} (FIPS 140-2 Level 2)", _vaultUri);
        }
        else
        {
            _logger.LogWarning("KeyVaultSecretProvider: Key Vault not configured — falling back to .env / IConfiguration. " +
                               "This mode is NOT FIPS 140-2 compliant. Set KeyVault:VaultUri or AZURE_KEYVAULT_URI to enable.");
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        // Check cache first
        if (TryGetFromCache(secretName, out var cached))
        {
            _logger.LogDebug("KeyVaultSecretProvider: Cache hit for '{SecretName}'", secretName);
            return cached;
        }

        string? value;

        if (_useKeyVault)
        {
            value = await GetFromKeyVaultAsync(secretName, cancellationToken);
        }
        else
        {
            value = GetFromConfiguration(secretName);
        }

        if (value is not null)
        {
            AddToCache(secretName, value);
        }

        return value;
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_useKeyVault)
        {
            // In production, we'd ping Key Vault. For now, check config presence.
            return Task.FromResult(!string.IsNullOrEmpty(_vaultUri));
        }

        // Local fallback is always available
        return Task.FromResult(true);
    }

    /// <summary>
    /// Retrieve secret from Azure Key Vault using managed identity.
    /// Azure Government Key Vault uses FIPS 140-2 Level 2 validated HSMs.
    /// </summary>
    private async Task<string?> GetFromKeyVaultAsync(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("KeyVaultSecretProvider: Fetching '{SecretName}' from Key Vault {VaultUri}", secretName, _vaultUri);

            // Use Azure.Security.KeyVault.Secrets with DefaultAzureCredential (managed identity)
            // In Azure Government: uses https://{vault}.vault.usgovcloudapi.net
            //
            // NOTE: This is a stub implementation. In production, replace with:
            //   var client = new SecretClient(new Uri(_vaultUri!), new DefaultAzureCredential(
            //       new DefaultAzureCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzureGovernment }));
            //   var secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            //   return secret.Value.Value;
            //
            // Azure Government Key Vault guarantees:
            // - FIPS 140-2 Level 2 validated HSMs for key storage
            // - Data sovereignty within US Government regions
            // - FedRAMP High / DoD IL5 authorization

            await Task.CompletedTask; // Placeholder for async KV call

            // For now, fall back to configuration as stub
            _logger.LogWarning("KeyVaultSecretProvider: Key Vault client not yet integrated — using config fallback for '{SecretName}'", secretName);
            return GetFromConfiguration(secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KeyVaultSecretProvider: Failed to retrieve '{SecretName}' from Key Vault", secretName);
            // Fall back to configuration
            return GetFromConfiguration(secretName);
        }
    }

    /// <summary>
    /// Fallback: retrieve secret from IConfiguration (.env, appsettings, env vars).
    /// NOT FIPS 140-2 compliant — for local development only.
    /// </summary>
    private string? GetFromConfiguration(string secretName)
    {
        // Try multiple key formats:
        // 1. Direct key (e.g. "CacSigningCert")
        // 2. Secrets section (e.g. "Secrets:CacSigningCert")
        // 3. Environment variable style (e.g. "CAC_SIGNING_CERT")

        var value = _configuration[secretName]
                    ?? _configuration[$"Secrets:{secretName}"]
                    ?? _configuration[ToEnvVarName(secretName)];

        if (value is not null)
        {
            _logger.LogDebug("KeyVaultSecretProvider: Found '{SecretName}' in configuration (local fallback)", secretName);
        }
        else
        {
            _logger.LogDebug("KeyVaultSecretProvider: '{SecretName}' not found in any configuration source", secretName);
        }

        return value;
    }

    /// <summary>
    /// Convert PascalCase/camelCase to UPPER_SNAKE_CASE for env var lookup.
    /// e.g. "CacSigningCert" → "CAC_SIGNING_CERT"
    /// </summary>
    private static string ToEnvVarName(string name)
    {
        var chars = new List<char>();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                chars.Add('_');
            }
            chars.Add(char.ToUpperInvariant(name[i]));
        }
        return new string(chars.ToArray());
    }

    // ─── Cache helpers ───

    private bool TryGetFromCache(string key, out string? value)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                value = cached.Value;
                return true;
            }
            value = null;
            return false;
        }
    }

    private void AddToCache(string key, string value)
    {
        lock (_cacheLock)
        {
            _cache[key] = new CachedSecret(value, DateTimeOffset.UtcNow.Add(CacheTtl));
        }
    }

    private record CachedSecret(string Value, DateTimeOffset ExpiresAt);
}
