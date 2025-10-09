namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Deployment status enumeration
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is in progress
    /// </summary>
    InProgress = 0,
    
    /// <summary>
    /// Deployment completed successfully
    /// </summary>
    Succeeded = 1,
    
    /// <summary>
    /// Deployment failed
    /// </summary>
    Failed = 2,
    
    /// <summary>
    /// Deployment was cancelled
    /// </summary>
    Cancelled = 3,
    
    /// <summary>
    /// Deployment is running normally
    /// </summary>
    Running = 4,
    
    /// <summary>
    /// Deployment is stopped
    /// </summary>
    Stopped = 5,
    
    /// <summary>
    /// Deployment is being deleted
    /// </summary>
    Deleting = 6,
    
    /// <summary>
    /// Deployment has been deleted
    /// </summary>
    Deleted = 7
}