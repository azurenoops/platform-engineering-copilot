using Blazored.LocalStorage;
using Microsoft.JSInterop;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

public class AppSettingsService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private const string SETTINGS_KEY = "platform_engineering_settings";
    
    public AppSettings Settings { get; private set; } = new();
    public event Action? OnSettingsChanged;

    public AppSettingsService(ILocalStorageService localStorage, IJSRuntime jsRuntime)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await ApplyThemeAsync();
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            var savedSettings = await _localStorage.GetItemAsync<AppSettings>(SETTINGS_KEY);
            if (savedSettings != null)
            {
                Settings = savedSettings;
                Console.WriteLine($"Settings loaded: Theme={Settings.Theme}, App={Settings.ApplicationName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            Settings = new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            Settings = settings;
            await _localStorage.SetItemAsync(SETTINGS_KEY, Settings);
            await ApplyThemeAsync();
            OnSettingsChanged?.Invoke();
            Console.WriteLine($"Settings saved and applied: Theme={Settings.Theme}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
            throw;
        }
    }

    private async Task ApplyThemeAsync()
    {
        try
        {
            var theme = Settings.Theme.ToLower();
            
            // Apply theme class to body
            if (theme == "dark")
            {
                await _jsRuntime.InvokeVoidAsync("eval", "document.body.classList.add('theme-dark'); document.body.classList.remove('theme-light');");
            }
            else if (theme == "light")
            {
                await _jsRuntime.InvokeVoidAsync("eval", "document.body.classList.add('theme-light'); document.body.classList.remove('theme-dark');");
            }
            else // auto
            {
                await _jsRuntime.InvokeVoidAsync("eval", 
                    @"if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
                        document.body.classList.add('theme-dark');
                        document.body.classList.remove('theme-light');
                    } else {
                        document.body.classList.add('theme-light');
                        document.body.classList.remove('theme-dark');
                    }");
            }
            
            Console.WriteLine($"Theme applied: {theme}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }
}

public class AppSettings
{
    // General
    public string ApplicationName { get; set; } = "Platform Engineering Copilot";
    public string OrganizationName { get; set; } = "My Organization";
    public string DefaultCloud { get; set; } = "AzureUSGovernment";
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool EnableAnalytics { get; set; } = true;
    public bool EnableAutoSave { get; set; } = true;

    // Notifications
    public string NotificationEmail { get; set; } = "";
    public int ToastDurationSeconds { get; set; } = 5;
    public bool NotifyEnvironmentProvision { get; set; } = true;
    public bool NotifyEnvironmentFailure { get; set; } = true;
    public bool NotifyDriftDetection { get; set; } = true;
    public bool NotifyExpiration { get; set; } = true;
    public bool NotifyCostAnomalies { get; set; } = true;
    public bool NotifyComplianceIssues { get; set; } = true;

    // Defaults
    public string DefaultSubscriptionId { get; set; } = "";
    public string DefaultLocation { get; set; } = "usgovvirginia";
    public int DefaultExpirationDays { get; set; } = 30;
    public string DefaultComplianceFramework { get; set; } = "NIST80053";
    public string DefaultTags { get; set; } = "Environment=Development\nManagedBy=PlatformCopilot";
    public bool DefaultAutoDelete { get; set; } = true;

    // Display
    public string Theme { get; set; } = "light";
    public int ItemsPerPage { get; set; } = 25;
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    public string TimeFormat { get; set; } = "12h";
    public string CurrencySymbol { get; set; } = "USD";
    public int DashboardRefreshSeconds { get; set; } = 60;
    public bool ShowCostInLists { get; set; } = true;
    public bool CompactView { get; set; } = false;

    // Agents
    public bool InfrastructureAgentEnabled { get; set; } = true;
    public double InfrastructureTemperature { get; set; } = 0.4;
    public bool ComplianceAgentEnabled { get; set; } = true;
    public double ComplianceTemperature { get; set; } = 0.2;
    public bool EnableAutomatedRemediation { get; set; } = false;
    public bool CostAgentEnabled { get; set; } = true;
    public double CostTemperature { get; set; } = 0.3;
    public int CostAnomalyThreshold { get; set; } = 50;

    // Security
    public string SecurityComplianceFramework { get; set; } = "NIST80053";
    public int MinPasswordLength { get; set; } = 16;
    public bool RequireMFA { get; set; } = true;
    public bool EnforceEncryption { get; set; } = true;
    public bool RequirePrivateEndpoints { get; set; } = false;
    public bool EnableDefenderForCloud { get; set; } = false;
    public string AllowedIpRanges { get; set; } = "10.0.0.0/8\n172.16.0.0/12\n192.168.0.0/16";
}
