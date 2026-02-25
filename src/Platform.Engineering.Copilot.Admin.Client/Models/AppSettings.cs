using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Application settings persisted in browser localStorage.</summary>
public class AppSettings
{
    // ===== General =====
    public string OrganizationName { get; set; } = "Platform Engineering";
    public string DefaultSubscriptionId { get; set; } = string.Empty;
    public string DefaultLocation { get; set; } = "usgovvirginia";
    public int AutoRefreshInterval { get; set; } = 30;
    public int PageSize { get; set; } = 10;

    // ===== Notifications =====
    public bool EnableToastNotifications { get; set; } = true;
    public int ToastDuration { get; set; } = 5;
    public bool ShowSuccessToasts { get; set; } = true;
    public bool ShowErrorToasts { get; set; } = true;
    public bool ShowWarningToasts { get; set; } = true;

    // ===== Defaults =====
    public int DefaultExpirationDays { get; set; } = 90;
    public bool DefaultAutoDelete { get; set; }
    public bool DefaultRequiresApproval { get; set; } = true;
    public string DefaultDeploymentScope { get; set; } = "ResourceGroup";

    // ===== Display =====
    public string Theme { get; set; } = "Auto";
    public bool SidebarCollapsed { get; set; }
    public bool ShowCostEstimates { get; set; } = true;
    public string DateFormat { get; set; } = "relative";
    public bool CompactMode { get; set; }

    // ===== Agents =====
    public bool EnableAiMatching { get; set; } = true;
    public double AiMatchMinScore { get; set; } = 0.3;
    public int AiMatchMaxResults { get; set; } = 5;

    // ===== Security =====
    public int SessionTimeout { get; set; } = 30;
    public bool RequireConfirmation { get; set; } = true;
    public bool AuditLogEnabled { get; set; } = true;

    /// <summary>Creates a new AppSettings with factory defaults.</summary>
    public static AppSettings CreateDefaults() => new();

    /// <summary>Storage key for localStorage persistence.</summary>
    [JsonIgnore]
    public const string StorageKey = "platform_engineering_settings";
}
