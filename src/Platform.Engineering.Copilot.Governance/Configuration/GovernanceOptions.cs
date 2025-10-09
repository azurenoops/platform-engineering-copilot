// NOTE: This file has been moved to Platform.Engineering.Copilot.Core.Configuration.GovernanceOptions
// to avoid circular dependencies. This is a forwarding type alias for backwards compatibility.

using PlatformGovernanceOptions = Platform.Engineering.Copilot.Core.Configuration.GovernanceOptions;

namespace Platform.Engineering.Copilot.Governance.Configuration;

/// <summary>
/// Configuration options for governance (forwarding to Core.Configuration.GovernanceOptions)
/// </summary>
public class GovernanceOptions : PlatformGovernanceOptions
{
}