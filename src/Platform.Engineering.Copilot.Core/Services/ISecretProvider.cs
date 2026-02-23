namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Abstraction for secret retrieval. Implementors must support
/// FIPS 140-2 Level 2 key storage (FR-082).
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Retrieve a secret value by name.
    /// </summary>
    /// <param name="secretName">The secret identifier (e.g. "CacSigningCert", "PimClientSecret").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check whether the provider is available and ready.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
