namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration options for Azure PIM service.
/// </summary>
public class AzurePimServiceOptions
{
    /// <summary>
    /// Whether Azure PIM service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Certificate thumbprint for app registration authentication (alternative to service principal).
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Default duration in minutes for role activations.
    /// </summary>
    public int DefaultActivationDurationMinutes { get; set; } = 480; // 8 hours

    /// <summary>
    /// Maximum allowed duration in minutes for role activations.
    /// </summary>
    public int MaxActivationDurationMinutes { get; set; } = 480;

    /// <summary>
    /// Whether to require a ticket number for activations.
    /// </summary>
    public bool RequireTicketNumber { get; set; } = true;

    /// <summary>
    /// List of approved ticket systems.
    /// </summary>
    public List<string> ApprovedTicketSystems { get; set; } = new()
    {
        "ServiceNow",
        "Jira",
        "Remedy",
        "AzureDevOps"
    };

    /// <summary>
    /// Minimum justification length required.
    /// </summary>
    public int MinJustificationLength { get; set; } = 20;

    /// <summary>
    /// Default ports for JIT VM SSH access.
    /// </summary>
    public int DefaultSshPort { get; set; } = 22;

    /// <summary>
    /// Default ports for JIT VM RDP access.
    /// </summary>
    public int DefaultRdpPort { get; set; } = 3389;

    /// <summary>
    /// Default JIT VM access duration in hours.
    /// </summary>
    public int DefaultVmAccessDurationHours { get; set; } = 3;

    /// <summary>
    /// Maximum JIT VM access duration in hours.
    /// </summary>
    public int MaxVmAccessDurationHours { get; set; } = 24;

    /// <summary>
    /// Whether to enable audit logging.
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Whether to send notifications on activation.
    /// </summary>
    public bool SendNotifications { get; set; } = true;

    /// <summary>
    /// Email addresses to notify on privileged role activations.
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();

    /// <summary>
    /// Roles that should trigger additional scrutiny/notifications.
    /// </summary>
    public List<string> HighPrivilegeRoles { get; set; } = new()
    {
        "Owner",
        "Contributor",
        "User Access Administrator",
        "Global Administrator",
        "Privileged Role Administrator"
    };

    /// <summary>
    /// Computed property to get default max duration as TimeSpan.
    /// </summary>
    public TimeSpan DefaultMaxDuration => TimeSpan.FromMinutes(DefaultActivationDurationMinutes);
}
