namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Git synchronization status for service templates.
/// </summary>
public enum GitSyncStatus
{
    NotConfigured,
    Synced,
    OutOfSync,
    SyncFailed
}
