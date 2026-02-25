using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Admin.Client.Services;

/// <summary>Manages application settings with localStorage persistence and theme JS interop.</summary>
public class AppSettingsService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AppSettingsService> _logger;

    public AppSettings CurrentSettings { get; private set; } = AppSettings.CreateDefaults();

    /// <summary>Raised when settings are saved.</summary>
    public event Action? OnSettingsChanged;

    public AppSettingsService(ILocalStorageService localStorage, IJSRuntime jsRuntime, ILogger<AppSettingsService> logger)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>Loads settings from localStorage and applies theme. Call after app initialization.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _localStorage.GetItemAsync<AppSettings>(AppSettings.StorageKey);
            if (stored != null)
            {
                CurrentSettings = stored;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings from localStorage, using defaults");
            CurrentSettings = AppSettings.CreateDefaults();
        }

        await ApplyThemeAsync();
    }

    /// <summary>Saves current settings to localStorage and applies theme.</summary>
    public async Task SaveAsync()
    {
        try
        {
            await _localStorage.SetItemAsync(AppSettings.StorageKey, CurrentSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to localStorage");
        }

        await ApplyThemeAsync();
        OnSettingsChanged?.Invoke();
    }

    /// <summary>Resets settings to factory defaults and saves.</summary>
    public async Task ResetToDefaultsAsync()
    {
        CurrentSettings = AppSettings.CreateDefaults();
        await SaveAsync();
    }

    /// <summary>Applies the current theme setting via JS interop.</summary>
    public async Task ApplyThemeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("themeInterop.setTheme", CurrentSettings.Theme);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply theme via JS interop");
        }
    }

    /// <summary>Starts watching for system theme changes (for Auto mode).</summary>
    public async Task WatchSystemThemeAsync(DotNetObjectReference<AppSettingsService> dotNetRef)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("themeInterop.watchSystemTheme", dotNetRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start system theme watcher");
        }
    }

    /// <summary>Disposes the system theme watcher.</summary>
    public async Task DisposeThemeWatcherAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("themeInterop.disposeThemeWatcher");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispose theme watcher");
        }
    }

    /// <summary>Called from JS when system theme changes (Auto mode).</summary>
    [JSInvokable]
    public async Task OnSystemThemeChanged(string newTheme)
    {
        if (CurrentSettings.Theme == "Auto")
        {
            await ApplyThemeAsync();
        }
    }
}
