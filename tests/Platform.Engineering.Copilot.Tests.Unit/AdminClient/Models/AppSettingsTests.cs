using System.Text.Json;
using FluentAssertions;
using Platform.Engineering.Copilot.Admin.Client.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Models;

public class AppSettingsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void CreateDefaults_ReturnsInstanceWithFactoryDefaults()
    {
        var settings = AppSettings.CreateDefaults();

        // General
        settings.OrganizationName.Should().Be("Platform Engineering");
        settings.DefaultSubscriptionId.Should().BeEmpty();
        settings.DefaultLocation.Should().Be("usgovvirginia");
        settings.AutoRefreshInterval.Should().Be(30);
        settings.PageSize.Should().Be(10);

        // Notifications
        settings.EnableToastNotifications.Should().BeTrue();
        settings.ToastDuration.Should().Be(5);
        settings.ShowSuccessToasts.Should().BeTrue();
        settings.ShowErrorToasts.Should().BeTrue();
        settings.ShowWarningToasts.Should().BeTrue();

        // Defaults
        settings.DefaultExpirationDays.Should().Be(90);
        settings.DefaultAutoDelete.Should().BeFalse();
        settings.DefaultRequiresApproval.Should().BeTrue();
        settings.DefaultDeploymentScope.Should().Be("ResourceGroup");

        // Display
        settings.Theme.Should().Be("Auto");
        settings.SidebarCollapsed.Should().BeFalse();
        settings.ShowCostEstimates.Should().BeTrue();
        settings.DateFormat.Should().Be("relative");
        settings.CompactMode.Should().BeFalse();

        // Agents
        settings.EnableAiMatching.Should().BeTrue();
        settings.AiMatchMinScore.Should().Be(0.3);
        settings.AiMatchMaxResults.Should().Be(5);

        // Security
        settings.SessionTimeout.Should().Be(30);
        settings.RequireConfirmation.Should().BeTrue();
        settings.AuditLogEnabled.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_Has25Properties()
    {
        var properties = typeof(AppSettings).GetProperties()
            .Where(p => p.Name != "StorageKey")
            .ToList();

        properties.Should().HaveCount(25);
    }

    [Fact]
    public void AppSettings_Serialization_RoundTrip()
    {
        var settings = AppSettings.CreateDefaults();
        settings.OrganizationName = "My Org";
        settings.Theme = "Dark";
        settings.PageSize = 25;
        settings.AiMatchMinScore = 0.5;

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.OrganizationName.Should().Be("My Org");
        deserialized.Theme.Should().Be("Dark");
        deserialized.PageSize.Should().Be(25);
        deserialized.AiMatchMinScore.Should().Be(0.5);
    }

    [Fact]
    public void AppSettings_Serialization_ExcludesStorageKey()
    {
        var settings = AppSettings.CreateDefaults();
        var json = JsonSerializer.Serialize(settings, JsonOptions);

        json.Should().NotContain("storageKey");
        json.Should().NotContain("StorageKey");
    }

    [Fact]
    public void AppSettings_StorageKey_HasCorrectValue()
    {
        AppSettings.StorageKey.Should().Be("platform_engineering_settings");
    }

    [Fact]
    public void AppSettings_Deserialization_WithMissingFields_UsesDefaults()
    {
        var json = """{"organizationName":"Custom Org","theme":"Light"}""";
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        settings.Should().NotBeNull();
        settings!.OrganizationName.Should().Be("Custom Org");
        settings.Theme.Should().Be("Light");
        // Missing fields use defaults
        settings.PageSize.Should().Be(10);
        settings.DefaultLocation.Should().Be("usgovvirginia");
        settings.EnableToastNotifications.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_Deserialization_EmptyJson_UsesAllDefaults()
    {
        var json = "{}";
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        var defaults = AppSettings.CreateDefaults();
        settings!.OrganizationName.Should().Be(defaults.OrganizationName);
        settings.DefaultLocation.Should().Be(defaults.DefaultLocation);
        settings.Theme.Should().Be(defaults.Theme);
        settings.PageSize.Should().Be(defaults.PageSize);
    }
}
