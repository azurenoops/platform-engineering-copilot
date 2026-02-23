using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;
using System.Net;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for NistService — dual-source OSCAL catalog service (FR-080).
/// Tests cover: control lookup, family lookup, search, baseline filtering,
/// framework comparison, embedded fallback, dual-source logging.
/// </summary>
public class NistServiceTests
{
    private readonly Mock<ILogger<NistService>> _loggerMock = new();
    private readonly IConfiguration _configuration;

    public NistServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            ["NistData:EnableGitHubFetch"] = "false" // Use embedded resources for unit tests
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private NistService CreateService(HttpClient? httpClient = null)
    {
        return new NistService(
            _loggerMock.Object,
            _configuration,
            httpClient ?? new HttpClient());
    }

    private async Task<NistService> CreateAndInitializeService()
    {
        var service = CreateService();
        await service.InitializeAsync();
        return service;
    }

    // ─── Initialization ─────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WithEmbedded_SetsIsLoadedTrue()
    {
        var service = await CreateAndInitializeService();

        service.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithEmbedded_SetsActiveSourceToFallback()
    {
        var service = await CreateAndInitializeService();

        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
        service.ActiveSource.CatalogVersion.Should().Contain("embedded snapshot");
        service.ActiveSource.LoadedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InitializeAsync_LoadsAllControlFamilies()
    {
        var service = await CreateAndInitializeService();

        // Embedded data has 5 families: AC, AT, AU, SC, SI
        var families = service.GetFamilyCodes();
        families.Should().HaveCount(5);
        families.Should().Contain(new[] { "AC", "AT", "AU", "SC", "SI" });
    }

    [Fact]
    public async Task InitializeAsync_LoadsAllControls()
    {
        var service = await CreateAndInitializeService();

        // AC: AC-1, AC-2, AC-3 = 3
        // AT: AT-1 = 1
        // AU: AU-1, AU-2 = 2
        // SC: SC-1, SC-7, SC-8 = 3
        // SI: SI-1 = 1
        // Total = 10
        var allFamilies = service.GetFamilyCodes();
        var totalControls = allFamilies.Sum(f => service.GetControlsByFamily(f).Count);
        totalControls.Should().Be(10);
    }

    // ─── GetControl ─────────────────────────────────────────────────

    [Theory]
    [InlineData("AC-1", "Policy and Procedures", "AC")]
    [InlineData("AC-2", "Account Management", "AC")]
    [InlineData("SC-7", "Boundary Protection", "SC")]
    [InlineData("SI-1", "Policy and Procedures", "SI")]
    public async Task GetControl_ExistingId_ReturnsCorrectControl(string id, string expectedTitle, string expectedFamily)
    {
        var service = await CreateAndInitializeService();

        var control = service.GetControl(id);

        control.Should().NotBeNull();
        control!.ControlId.Should().Be(id);
        control.Title.Should().Be(expectedTitle);
        control.Family.Should().Be(expectedFamily);
    }

    [Fact]
    public async Task GetControl_NonExistentId_ReturnsNull()
    {
        var service = await CreateAndInitializeService();

        var control = service.GetControl("ZZ-999");

        control.Should().BeNull();
    }

    [Fact]
    public async Task GetControl_CaseInsensitive()
    {
        var service = await CreateAndInitializeService();

        var control = service.GetControl("ac-2");

        control.Should().NotBeNull();
        control!.ControlId.Should().Be("AC-2");
    }

    // ─── GetControlsByFamily ────────────────────────────────────────

    [Fact]
    public async Task GetControlsByFamily_AC_Returns3Controls()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFamily("AC");

        controls.Should().HaveCount(3);
        controls.Select(c => c.ControlId).Should().ContainInOrder("AC-1", "AC-2", "AC-3");
    }

    [Fact]
    public async Task GetControlsByFamily_AT_Returns1Control()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFamily("AT");

        controls.Should().ContainSingle();
        controls[0].ControlId.Should().Be("AT-1");
    }

    [Fact]
    public async Task GetControlsByFamily_NonExistent_ReturnsEmpty()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFamily("ZZ");

        controls.Should().BeEmpty();
    }

    [Fact]
    public async Task GetControlsByFamily_CaseInsensitive()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFamily("ac");

        controls.Should().HaveCount(3);
    }

    // ─── SearchControls ─────────────────────────────────────────────

    [Fact]
    public async Task SearchControls_ByTitle_FindsMatches()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("Account Management");

        results.Should().ContainSingle();
        results[0].ControlId.Should().Be("AC-2");
    }

    [Fact]
    public async Task SearchControls_ByControlId_FindsExactMatch()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("AC-3");

        results.Should().HaveCountGreaterOrEqualTo(1);
        results[0].ControlId.Should().Be("AC-3"); // Exact match ranked first
    }

    [Fact]
    public async Task SearchControls_ByDescription_FindsMatches()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("Boundary");

        results.Should().ContainSingle();
        results[0].ControlId.Should().Be("SC-7");
    }

    [Fact]
    public async Task SearchControls_ByFamilyName_FindsMatches()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("Access Control");

        results.Should().HaveCount(3); // AC-1, AC-2, AC-3
    }

    [Fact]
    public async Task SearchControls_EmptyQuery_ReturnsEmpty()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchControls_RespectsMaxResults()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("Policy", maxResults: 2);

        results.Should().HaveCount(2); // There are 5 "Policy and Procedures" controls
    }

    [Fact]
    public async Task SearchControls_CaseInsensitive()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("boundary protection");

        results.Should().ContainSingle();
        results[0].ControlId.Should().Be("SC-7");
    }

    // ─── GetControlsByBaseline ──────────────────────────────────────

    [Fact]
    public async Task GetControlsByBaseline_High_ReturnsAllControls()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByBaseline(BaselineLevel.High);

        // All 10 controls in our embedded data have high=true
        controls.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetControlsByBaseline_Low_ExcludesNonLowControls()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByBaseline(BaselineLevel.Low);

        // SC-7 and SC-8 have low=false, so 8 controls
        controls.Should().HaveCount(8);
        controls.Select(c => c.ControlId).Should().NotContain("SC-7");
        controls.Select(c => c.ControlId).Should().NotContain("SC-8");
    }

    [Fact]
    public async Task GetControlsByBaseline_Moderate_ReturnsCorrectSet()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByBaseline(BaselineLevel.Moderate);

        // All 10 controls have moderate=true in our data
        controls.Should().HaveCount(10);
    }

    // ─── GetControlsByFramework ─────────────────────────────────────

    [Fact]
    public async Task GetControlsByFramework_Nist80053Rev5_ReturnsAll()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFramework(ComplianceFramework.Nist80053Rev5);

        // All controls are inherently NIST 800-53 Rev 5
        controls.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetControlsByFramework_FedRampHigh_ReturnsOverlayControls()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFramework(ComplianceFramework.FedRampHigh);

        // FedRAMP High overlay includes all 10 controls
        controls.Should().HaveCount(10);
        controls.Select(c => c.ControlId).Should().Contain("AC-1");
        controls.Select(c => c.ControlId).Should().Contain("SC-8");
    }

    [Fact]
    public async Task GetControlsByFramework_DoDIL5_ReturnsOverlayControls()
    {
        var service = await CreateAndInitializeService();

        var controls = service.GetControlsByFramework(ComplianceFramework.DoDIL5);

        // DoD IL5 overlay includes all 10 controls
        controls.Should().HaveCount(10);
    }

    // ─── CompareFrameworks ──────────────────────────────────────────

    [Fact]
    public async Task CompareFrameworks_SameFramework_AllCommon()
    {
        var service = await CreateAndInitializeService();

        var result = service.CompareFrameworks(ComplianceFramework.FedRampHigh, ComplianceFramework.FedRampHigh);

        result.FrameworkA.Should().Be(ComplianceFramework.FedRampHigh);
        result.FrameworkB.Should().Be(ComplianceFramework.FedRampHigh);
        result.Common.Should().HaveCount(10);
        result.UniqueToA.Should().BeEmpty();
        result.UniqueToB.Should().BeEmpty();
    }

    [Fact]
    public async Task CompareFrameworks_DifferentFrameworks_CalculatesCorrectly()
    {
        var service = await CreateAndInitializeService();

        // In our test data, FedRAMP High and DoD IL5 have the same 10 controls
        var result = service.CompareFrameworks(ComplianceFramework.FedRampHigh, ComplianceFramework.DoDIL5);

        result.Common.Should().HaveCount(10);
        result.UniqueToA.Should().BeEmpty();
        result.UniqueToB.Should().BeEmpty();
        result.TotalA.Should().Be(10);
        result.TotalB.Should().Be(10);
    }

    [Fact]
    public async Task CompareFrameworks_NistVsFedRampHigh_ShowsAllCommon()
    {
        var service = await CreateAndInitializeService();

        var result = service.CompareFrameworks(ComplianceFramework.Nist80053Rev5, ComplianceFramework.FedRampHigh);

        result.Common.Count.Should().Be(10);
        result.UniqueToA.Should().BeEmpty();
        result.UniqueToB.Should().BeEmpty();
    }

    // ─── GetFamilyCodes ─────────────────────────────────────────────

    [Fact]
    public async Task GetFamilyCodes_ReturnsSortedList()
    {
        var service = await CreateAndInitializeService();

        var codes = service.GetFamilyCodes();

        codes.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetFamilyCodes_ContainsExpectedFamilies()
    {
        var service = await CreateAndInitializeService();

        var codes = service.GetFamilyCodes();

        codes.Should().Contain(new[] { "AC", "AT", "AU", "SC", "SI" });
    }

    // ─── Control Properties ─────────────────────────────────────────

    [Fact]
    public async Task Control_HasGuidance_WhenPresent()
    {
        var service = await CreateAndInitializeService();

        var ac2 = service.GetControl("AC-2");

        ac2.Should().NotBeNull();
        ac2!.ImplementationGuidance.Should().NotBeNullOrEmpty();
        ac2.ImplementationGuidance.Should().ContainEquivalentOf("account management");
    }

    [Fact]
    public async Task Control_HasPriority()
    {
        var service = await CreateAndInitializeService();

        var ac1 = service.GetControl("AC-1");

        ac1.Should().NotBeNull();
        ac1!.Priority.Should().Be("P1");
    }

    [Fact]
    public async Task Control_HasRelatedControls()
    {
        var service = await CreateAndInitializeService();

        var ac2 = service.GetControl("AC-2");

        ac2.Should().NotBeNull();
        ac2!.Related.Should().NotBeEmpty();
        ac2.Related.Should().Contain("AC-3");
    }

    [Fact]
    public async Task Control_HasStigReferences_WhenMapped()
    {
        var service = await CreateAndInitializeService();

        var ac2 = service.GetControl("AC-2");

        ac2.Should().NotBeNull();
        ac2!.StigReferences.Should().NotBeNullOrEmpty();
        ac2.StigReferences![0].StigId.Should().Be("V-252833");
        ac2.StigReferences[0].BenchmarkId.Should().Be("Azure_STIG");
        ac2.StigReferences[0].Severity.Should().Be("CAT II");
    }

    [Fact]
    public async Task Control_HasAzureServiceMappings_WhenMapped()
    {
        var service = await CreateAndInitializeService();

        var ac2 = service.GetControl("AC-2");

        ac2.Should().NotBeNull();
        ac2!.AzureServiceMappings.Should().NotBeEmpty();
        ac2.AzureServiceMappings.Should().Contain("Microsoft.Authorization/roleAssignments");
    }

    [Fact]
    public async Task Control_WithoutStigMapping_HasNullStigReferences()
    {
        var service = await CreateAndInitializeService();

        var ac1 = service.GetControl("AC-1");

        ac1.Should().NotBeNull();
        ac1!.StigReferences.Should().BeNull();
    }

    // ─── Overlay Integration ────────────────────────────────────────

    [Fact]
    public async Task Overlay_FedRampHigh_SetsFrameworkFlag()
    {
        var service = await CreateAndInitializeService();

        var ac1 = service.GetControl("AC-1");

        ac1.Should().NotBeNull();
        ac1!.Frameworks.FedRampHigh.Should().BeTrue();
    }

    [Fact]
    public async Task Overlay_FedRampModerate_SetsFrameworkFlag()
    {
        var service = await CreateAndInitializeService();

        var ac1 = service.GetControl("AC-1");

        ac1.Should().NotBeNull();
        ac1!.Frameworks.FedRampModerate.Should().BeTrue();
    }

    [Fact]
    public async Task Overlay_DoDIL5_SetsFrameworkFlag()
    {
        var service = await CreateAndInitializeService();

        var ac1 = service.GetControl("AC-1");

        ac1.Should().NotBeNull();
        ac1!.Frameworks.DoDIL5.Should().BeTrue();
    }

    [Fact]
    public async Task Overlay_AllFrameworks_SetOnOverlayControls()
    {
        var service = await CreateAndInitializeService();

        var sc8 = service.GetControl("SC-8");

        sc8.Should().NotBeNull();
        sc8!.Frameworks.Nist80053Rev5.Should().BeTrue();
        sc8.Frameworks.FedRampHigh.Should().BeTrue();
        sc8.Frameworks.FedRampModerate.Should().BeTrue();
        sc8.Frameworks.DoDIL5.Should().BeTrue();
    }

    // ─── GitHub Fetch Simulation ────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_GitHubFetchFails_FallsBackToEmbedded()
    {
        var configData = new Dictionary<string, string?>
        {
            ["NistData:EnableGitHubFetch"] = "true",
            ["NistData:GitHubBaseUrl"] = "https://invalid.example.com"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Use a handler that always fails
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handler.Object);
        var service = new NistService(_loggerMock.Object, config, httpClient);

        await service.InitializeAsync();

        // Should still load via embedded fallback
        service.IsLoaded.Should().BeTrue();
        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
    }

    [Fact]
    public async Task InitializeAsync_GitHubDisabled_UsesEmbeddedDirectly()
    {
        var service = await CreateAndInitializeService();

        service.IsLoaded.Should().BeTrue();
        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
    }

    // ─── Edge Cases ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchControls_WhitespaceQuery_ReturnsEmpty()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("   ");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchControls_NoMatch_ReturnsEmpty()
    {
        var service = await CreateAndInitializeService();

        var results = service.SearchControls("xyznonexistent");

        results.Should().BeEmpty();
    }

    [Fact]
    public void BeforeInitialization_IsLoadedIsFalse()
    {
        var service = CreateService();

        service.IsLoaded.Should().BeFalse();
        service.ActiveSource.Source.Should().Be("None");
    }

    [Fact]
    public void BeforeInitialization_GetControl_ReturnsNull()
    {
        var service = CreateService();

        service.GetControl("AC-1").Should().BeNull();
    }

    [Fact]
    public void BeforeInitialization_GetFamilyCodes_ReturnsEmpty()
    {
        var service = CreateService();

        service.GetFamilyCodes().Should().BeEmpty();
    }

    // ─── Baseline Applicability Details ─────────────────────────────

    [Fact]
    public async Task Control_SC7_NotInLowBaseline()
    {
        var service = await CreateAndInitializeService();

        var sc7 = service.GetControl("SC-7");

        sc7.Should().NotBeNull();
        sc7!.Baselines.High.Should().BeTrue();
        sc7.Baselines.Moderate.Should().BeTrue();
        sc7.Baselines.Low.Should().BeFalse();
    }

    [Fact]
    public async Task Control_AC1_InAllBaselines()
    {
        var service = await CreateAndInitializeService();

        var ac1 = service.GetControl("AC-1");

        ac1.Should().NotBeNull();
        ac1!.Baselines.High.Should().BeTrue();
        ac1.Baselines.Moderate.Should().BeTrue();
        ac1.Baselines.Low.Should().BeTrue();
    }

    // ─── STIG Mapping Details ───────────────────────────────────────

    [Fact]
    public async Task StigMapping_AC3_HasCatISeverity()
    {
        var service = await CreateAndInitializeService();

        var ac3 = service.GetControl("AC-3");

        ac3.Should().NotBeNull();
        ac3!.StigReferences.Should().NotBeNullOrEmpty();
        ac3.StigReferences![0].Severity.Should().Be("CAT I");
    }

    [Fact]
    public async Task StigMapping_SC8_HasStigReference()
    {
        var service = await CreateAndInitializeService();

        var sc8 = service.GetControl("SC-8");

        sc8.Should().NotBeNull();
        sc8!.StigReferences.Should().NotBeNullOrEmpty();
        sc8.StigReferences![0].StigId.Should().Be("V-252870");
    }

    // ─── Azure Service Mapping Details ──────────────────────────────

    [Fact]
    public async Task AzureServiceMapping_SC7_HasNetworkServices()
    {
        var service = await CreateAndInitializeService();

        var sc7 = service.GetControl("SC-7");

        sc7.Should().NotBeNull();
        sc7!.AzureServiceMappings.Should().NotBeEmpty();
        sc7.AzureServiceMappings.Should().Contain("Microsoft.Network/networkSecurityGroups");
        sc7.AzureServiceMappings.Should().Contain("Microsoft.Network/azureFirewalls");
    }

    [Fact]
    public async Task AzureServiceMapping_AU2_HasMonitoringServices()
    {
        var service = await CreateAndInitializeService();

        var au2 = service.GetControl("AU-2");

        au2.Should().NotBeNull();
        au2!.AzureServiceMappings.Should().NotBeEmpty();
        au2.AzureServiceMappings.Should().Contain("Microsoft.Insights/diagnosticSettings");
    }

    [Fact]
    public async Task AzureServiceMapping_UnmappedControl_HasEmptyArray()
    {
        var service = await CreateAndInitializeService();

        var at1 = service.GetControl("AT-1");

        at1.Should().NotBeNull();
        at1!.AzureServiceMappings.Should().BeEmpty();
    }
}
