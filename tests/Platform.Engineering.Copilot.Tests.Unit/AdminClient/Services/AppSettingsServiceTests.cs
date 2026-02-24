using Blazored.LocalStorage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Platform.Engineering.Copilot.Admin.Client.Models;
using Platform.Engineering.Copilot.Admin.Client.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Services;

public class AppSettingsServiceTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();
    private readonly Mock<ILogger<AppSettingsService>> _loggerMock = new();

    private AppSettingsService CreateService()
    {
        return new AppSettingsService(_localStorageMock.Object, _jsRuntimeMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void CurrentSettings_DefaultsToFactoryDefaults()
    {
        var service = CreateService();

        service.CurrentSettings.Should().NotBeNull();
        service.CurrentSettings.OrganizationName.Should().Be("Platform Engineering");
        service.CurrentSettings.Theme.Should().Be("Auto");
        service.CurrentSettings.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task InitializeAsync_LoadsStoredSettings()
    {
        var stored = new AppSettings { OrganizationName = "My Org", Theme = "Dark" };
        _localStorageMock.Setup(x => x.GetItemAsync<AppSettings>(AppSettings.StorageKey, default))
            .ReturnsAsync(stored);

        var service = CreateService();
        await service.InitializeAsync();

        service.CurrentSettings.OrganizationName.Should().Be("My Org");
        service.CurrentSettings.Theme.Should().Be("Dark");
    }

    [Fact]
    public async Task InitializeAsync_WhenNoStoredSettings_UsesDefaults()
    {
        _localStorageMock.Setup(x => x.GetItemAsync<AppSettings>(AppSettings.StorageKey, default))
            .ReturnsAsync((AppSettings?)null);

        var service = CreateService();
        await service.InitializeAsync();

        service.CurrentSettings.OrganizationName.Should().Be("Platform Engineering");
    }

    [Fact]
    public async Task InitializeAsync_OnStorageError_FallsBackToDefaults()
    {
        _localStorageMock.Setup(x => x.GetItemAsync<AppSettings>(AppSettings.StorageKey, default))
            .ThrowsAsync(new InvalidOperationException("localStorage unavailable"));

        var service = CreateService();
        await service.InitializeAsync();

        service.CurrentSettings.Should().NotBeNull();
        service.CurrentSettings.OrganizationName.Should().Be("Platform Engineering");
    }

    [Fact]
    public async Task InitializeAsync_AppliesTheme()
    {
        _localStorageMock.Setup(x => x.GetItemAsync<AppSettings>(AppSettings.StorageKey, default))
            .ReturnsAsync((AppSettings?)null);

        var service = CreateService();
        await service.InitializeAsync();

        _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "themeInterop.setTheme",
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "Auto")), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_PersistsToLocalStorage()
    {
        var service = CreateService();
        service.CurrentSettings.OrganizationName = "Saved Org";

        await service.SaveAsync();

        _localStorageMock.Verify(x => x.SetItemAsync(
            AppSettings.StorageKey,
            It.Is<AppSettings>(s => s.OrganizationName == "Saved Org"),
            default), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_AppliesTheme()
    {
        var service = CreateService();
        service.CurrentSettings.Theme = "Dark";

        await service.SaveAsync();

        _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "themeInterop.setTheme",
            It.Is<object[]>(args => (string)args[0] == "Dark")), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_RaisesOnSettingsChanged()
    {
        var service = CreateService();
        var eventRaised = false;
        service.OnSettingsChanged += () => eventRaised = true;

        await service.SaveAsync();

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_OnStorageError_DoesNotThrow()
    {
        _localStorageMock.Setup(x => x.SetItemAsync(AppSettings.StorageKey, It.IsAny<AppSettings>(), default))
            .ThrowsAsync(new InvalidOperationException("Storage full"));

        var service = CreateService();

        var act = async () => await service.SaveAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresFactoryDefaults()
    {
        var service = CreateService();
        service.CurrentSettings.OrganizationName = "Changed";
        service.CurrentSettings.Theme = "Dark";
        service.CurrentSettings.PageSize = 50;

        await service.ResetToDefaultsAsync();

        service.CurrentSettings.OrganizationName.Should().Be("Platform Engineering");
        service.CurrentSettings.Theme.Should().Be("Auto");
        service.CurrentSettings.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_SavesDefaults()
    {
        var service = CreateService();

        await service.ResetToDefaultsAsync();

        _localStorageMock.Verify(x => x.SetItemAsync(
            AppSettings.StorageKey,
            It.Is<AppSettings>(s => s.OrganizationName == "Platform Engineering"),
            default), Times.Once);
    }

    [Fact]
    public async Task ApplyThemeAsync_InvokesJsInterop()
    {
        var service = CreateService();
        service.CurrentSettings.Theme = "Light";

        await service.ApplyThemeAsync();

        _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "themeInterop.setTheme",
            It.Is<object[]>(args => (string)args[0] == "Light")), Times.Once);
    }

    [Fact]
    public async Task ApplyThemeAsync_OnJsError_DoesNotThrow()
    {
        _jsRuntimeMock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "themeInterop.setTheme", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS error"));

        var service = CreateService();

        var act = async () => await service.ApplyThemeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnSystemThemeChanged_WhenAutoMode_AppliesTheme()
    {
        var service = CreateService();
        service.CurrentSettings.Theme = "Auto";

        await service.OnSystemThemeChanged("Dark");

        _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "themeInterop.setTheme",
            It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task OnSystemThemeChanged_WhenNotAutoMode_DoesNotApplyTheme()
    {
        var service = CreateService();
        service.CurrentSettings.Theme = "Dark";

        await service.OnSystemThemeChanged("Light");

        _jsRuntimeMock.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "themeInterop.setTheme",
            It.IsAny<object[]>()), Times.Never);
    }
}
