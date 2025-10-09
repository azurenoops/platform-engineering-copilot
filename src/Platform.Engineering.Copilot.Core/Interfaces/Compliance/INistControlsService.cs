using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Production-ready service for fetching and caching NIST 800-53 controls
/// </summary>
public interface INistControlsService
{
    Task<NistCatalog?> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<NistControl?> GetControlAsync(string controlId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NistControl>> GetControlsByFamilyAsync(string family, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NistControl>> SearchControlsAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);
    Task<ControlEnhancement?> GetControlEnhancementAsync(string controlId, CancellationToken cancellationToken = default);
    Task<bool> ValidateControlIdAsync(string controlId, CancellationToken cancellationToken = default);
}