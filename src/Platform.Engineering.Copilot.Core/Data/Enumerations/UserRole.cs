namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Roles a platform user may hold. Users may have multiple roles;
/// effective permissions are the union of all assigned roles (FR-017).
/// </summary>
public enum UserRole
{
    ComplianceOfficer,
    PlatformEngineer,
    SecurityLead,
    Auditor
}
