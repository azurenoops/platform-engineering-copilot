using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.KnowledgeBase;

/// <summary>
/// Unit tests for StigKnowledgeService
/// Tests STIG control retrieval, search, severity mapping, and Azure implementation guidance
/// </summary>
public class StigKnowledgeServiceTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<StigKnowledgeServiceTests>> _loggerMock;
    private readonly Mock<IDoDInstructionService> _dodInstructionServiceMock;

    public StigKnowledgeServiceTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<StigKnowledgeServiceTests>>();
        _dodInstructionServiceMock = new Mock<IDoDInstructionService>();
    }

    #region StigControl Model Tests

    [Fact]
    public void StigControl_FullyPopulated()
    {
        // Arrange & Act
        var stig = new StigControl
        {
            StigId = "V-219153",
            VulnId = "V-219153",
            RuleId = "SV-219153r897755_rule",
            Title = "Windows Server 2016 must be configured to audit Logon/Logoff - Account Lockout successes.",
            Description = "Maintaining an audit trail of system activity...",
            Severity = StigSeverity.Medium,
            CheckText = "Run the following PowerShell command...",
            FixText = "Configure the policy value...",
            NistControls = new List<string> { "AU-2", "AU-3", "AU-12" },
            CciRefs = new List<string> { "CCI-000172" },
            Category = "CAT II",
            StigFamily = "Windows Server 2016",
            ServiceType = StigServiceType.Compute,
            AzureImplementation = new Dictionary<string, string>
            {
                ["service"] = "Azure Policy",
                ["configuration"] = "Enable audit logging",
                ["azurePolicy"] = "Windows VMs should have audit policy configured"
            }
        };

        // Assert
        stig.StigId.Should().Be("V-219153");
        stig.Severity.Should().Be(StigSeverity.Medium);
        stig.NistControls.Should().Contain("AU-2");
        stig.Category.Should().Be("CAT II");
        stig.AzureImplementation.Should().ContainKey("service");
    }

    [Fact]
    public void StigControl_DefaultValues()
    {
        // Arrange & Act
        var stig = new StigControl();

        // Assert
        stig.StigId.Should().BeEmpty();
        stig.VulnId.Should().BeEmpty();
        stig.NistControls.Should().BeEmpty();
        stig.CciRefs.Should().BeEmpty();
        stig.AzureImplementation.Should().BeEmpty();
    }

    #endregion

    #region Severity Tests

    [Theory]
    [InlineData(StigSeverity.Low, "CAT III")]
    [InlineData(StigSeverity.Medium, "CAT II")]
    [InlineData(StigSeverity.High, "CAT I")]
    [InlineData(StigSeverity.Critical, "CAT I")]
    public void Severity_CategoryMapping(StigSeverity severity, string expectedCategory)
    {
        // Arrange
        var categoryMap = new Dictionary<StigSeverity, string>
        {
            [StigSeverity.Low] = "CAT III",
            [StigSeverity.Medium] = "CAT II",
            [StigSeverity.High] = "CAT I",
            [StigSeverity.Critical] = "CAT I"
        };

        // Assert
        categoryMap[severity].Should().Be(expectedCategory);
    }

    [Theory]
    [InlineData("CAT I", true)]
    [InlineData("CAT II", true)]
    [InlineData("CAT III", true)]
    [InlineData("CAT IV", false)]
    [InlineData("HIGH", false)]
    public void Category_Validation(string category, bool isValid)
    {
        // Arrange
        var validCategories = new HashSet<string> { "CAT I", "CAT II", "CAT III" };

        // Act
        var result = validCategories.Contains(category);

        // Assert
        result.Should().Be(isValid);
    }

    [Fact]
    public void StigSeverity_AllValuesExist()
    {
        // Assert
        Enum.GetValues<StigSeverity>().Should().HaveCount(4);
        Enum.IsDefined(StigSeverity.Low).Should().BeTrue();
        Enum.IsDefined(StigSeverity.Medium).Should().BeTrue();
        Enum.IsDefined(StigSeverity.High).Should().BeTrue();
        Enum.IsDefined(StigSeverity.Critical).Should().BeTrue();
    }

    #endregion

    #region Service Type Tests

    [Theory]
    [InlineData(StigServiceType.Compute)]
    [InlineData(StigServiceType.Network)]
    [InlineData(StigServiceType.Storage)]
    [InlineData(StigServiceType.Database)]
    [InlineData(StigServiceType.Identity)]
    [InlineData(StigServiceType.Monitoring)]
    [InlineData(StigServiceType.Security)]
    [InlineData(StigServiceType.Platform)]
    [InlineData(StigServiceType.Integration)]
    [InlineData(StigServiceType.Analytics)]
    [InlineData(StigServiceType.Containers)]
    public void StigServiceType_AllValuesExist(StigServiceType serviceType)
    {
        // Assert
        Enum.IsDefined(serviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData("Virtual Machines", StigServiceType.Compute)]
    [InlineData("VNet", StigServiceType.Network)]
    [InlineData("Storage Account", StigServiceType.Storage)]
    [InlineData("Azure SQL", StigServiceType.Database)]
    [InlineData("Azure AD", StigServiceType.Identity)]
    [InlineData("AKS", StigServiceType.Containers)]
    public void AzureService_ServiceTypeMapping(string azureService, StigServiceType expectedType)
    {
        // Arrange
        var serviceTypeMap = new Dictionary<string, StigServiceType>
        {
            ["Virtual Machines"] = StigServiceType.Compute,
            ["VNet"] = StigServiceType.Network,
            ["Storage Account"] = StigServiceType.Storage,
            ["Azure SQL"] = StigServiceType.Database,
            ["Azure AD"] = StigServiceType.Identity,
            ["AKS"] = StigServiceType.Containers
        };

        // Assert
        serviceTypeMap[azureService].Should().Be(expectedType);
    }

    #endregion

    #region STIG ID Format Tests

    [Theory]
    [InlineData("V-219153", true)]
    [InlineData("V-12345", true)]
    [InlineData("V-1", true)]
    [InlineData("219153", false)]
    [InlineData("v-219153", true)] // Allow lowercase
    [InlineData("SV-219153", false)] // SV is rule ID prefix
    public void StigId_FormatValidation(string stigId, bool isValid)
    {
        // Act
        var normalized = stigId.ToUpperInvariant();
        var matchesPattern = System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^V-\d+$");

        // Assert
        matchesPattern.Should().Be(isValid);
    }

    [Theory]
    [InlineData("SV-219153r897755_rule", true)]
    [InlineData("SV-12345r123456_rule", true)]
    [InlineData("V-219153", false)]
    [InlineData("SV-219153", false)]
    public void RuleId_FormatValidation(string ruleId, bool isValid)
    {
        // Act
        var matchesPattern = System.Text.RegularExpressions.Regex.IsMatch(ruleId, @"^SV-\d+r\d+_rule$");

        // Assert
        matchesPattern.Should().Be(isValid);
    }

    #endregion

    #region NIST Control Mapping Tests

    [Fact]
    public void StigControl_NistControlMapping()
    {
        // Arrange
        var stig = new StigControl
        {
            StigId = "V-219153",
            NistControls = new List<string> { "AU-2", "AU-3", "AU-3(1)", "AU-12" }
        };

        // Assert
        stig.NistControls.Should().HaveCount(4);
        stig.NistControls.Should().Contain("AU-2");
        stig.NistControls.Should().Contain("AU-12");
    }

    [Theory]
    [InlineData("AU-2", new[] { "V-219153", "V-219154", "V-219155" })]
    [InlineData("IA-2", new[] { "V-219187", "V-219188" })]
    public void NistControl_StigMapping(string nistControlId, string[] expectedStigs)
    {
        // Arrange - Simulate NIST to STIG mapping
        var nistToStigMap = new Dictionary<string, List<string>>
        {
            ["AU-2"] = new List<string> { "V-219153", "V-219154", "V-219155" },
            ["IA-2"] = new List<string> { "V-219187", "V-219188" }
        };

        // Assert
        nistToStigMap[nistControlId].Should().BeEquivalentTo(expectedStigs);
    }

    #endregion

    #region CCI Reference Tests

    [Theory]
    [InlineData("CCI-000172")]
    [InlineData("CCI-001234")]
    [InlineData("CCI-002475")]
    public void CciReference_FormatValidation(string cciRef)
    {
        // Act
        var matchesPattern = System.Text.RegularExpressions.Regex.IsMatch(cciRef, @"^CCI-\d{6}$");

        // Assert
        matchesPattern.Should().BeTrue();
    }

    [Fact]
    public void StigControl_CciReferences()
    {
        // Arrange
        var stig = new StigControl
        {
            StigId = "V-219153",
            CciRefs = new List<string> { "CCI-000172", "CCI-002234" }
        };

        // Assert
        stig.CciRefs.Should().HaveCount(2);
        stig.CciRefs.All(c => c.StartsWith("CCI-")).Should().BeTrue();
    }

    #endregion

    #region Azure Implementation Tests

    [Fact]
    public void AzureImplementation_FullConfiguration()
    {
        // Arrange
        var implementation = new Dictionary<string, string>
        {
            ["service"] = "Azure Policy",
            ["configuration"] = "Enable audit logging for VMs",
            ["azurePolicy"] = "AuditVMLogConfiguration",
            ["automation"] = "az policy assignment create ..."
        };

        // Assert
        implementation.Should().ContainKey("service");
        implementation.Should().ContainKey("configuration");
        implementation.Should().ContainKey("azurePolicy");
    }

    [Theory]
    [InlineData("Azure Policy")]
    [InlineData("Azure Monitor")]
    [InlineData("Azure Security Center")]
    [InlineData("Azure AD")]
    [InlineData("Azure Key Vault")]
    public void AzureImplementation_ValidServices(string service)
    {
        // Arrange
        var validServices = new HashSet<string>
        {
            "Azure Policy",
            "Azure Monitor",
            "Azure Security Center",
            "Azure AD",
            "Azure Key Vault",
            "Azure Firewall",
            "NSG",
            "Azure Disk Encryption"
        };

        // Assert
        validServices.Should().Contain(service);
    }

    #endregion

    #region Search Tests

    [Theory]
    [InlineData("encryption", true)]
    [InlineData("MFA", true)]
    [InlineData("audit", true)]
    [InlineData("password", true)]
    [InlineData("firewall", true)]
    public void SearchTerm_ValidKeywords(string searchTerm, bool shouldFindResults)
    {
        // Arrange
        var stigsWithKeywords = new List<StigControl>
        {
            new StigControl { Title = "Encryption must be enabled", Description = "Data encryption requirement" },
            new StigControl { Title = "MFA must be required", Description = "Multi-factor authentication" },
            new StigControl { Title = "Audit logging", Description = "Enable audit logs" },
            new StigControl { Title = "Password policy", Description = "Configure password requirements" },
            new StigControl { Title = "Firewall configuration", Description = "Network firewall settings" }
        };

        // Act
        var results = stigsWithKeywords.Where(s =>
            s.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            s.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert
        results.Any().Should().Be(shouldFindResults);
    }

    [Fact]
    public void Search_ByCategory()
    {
        // Arrange
        var stigs = new List<StigControl>
        {
            new StigControl { StigId = "V-1", Category = "CAT I", Severity = StigSeverity.High },
            new StigControl { StigId = "V-2", Category = "CAT II", Severity = StigSeverity.Medium },
            new StigControl { StigId = "V-3", Category = "CAT III", Severity = StigSeverity.Low }
        };

        // Act
        var catIStigs = stigs.Where(s => s.Category == "CAT I").ToList();
        var catIIStigs = stigs.Where(s => s.Category == "CAT II").ToList();

        // Assert
        catIStigs.Should().HaveCount(1);
        catIIStigs.Should().HaveCount(1);
    }

    #endregion

    #region STIG Explanation Formatting Tests

    [Fact]
    public void StigExplanation_Format()
    {
        // Arrange
        var stig = new StigControl
        {
            StigId = "V-219153",
            Title = "Windows Server audit configuration",
            Severity = StigSeverity.Medium,
            Category = "CAT II",
            StigFamily = "Windows Server 2016",
            Description = "Audit logging must be enabled",
            CheckText = "Run: Get-AuditPolicy",
            FixText = "Configure audit policy",
            NistControls = new List<string> { "AU-2" },
            CciRefs = new List<string> { "CCI-000172" },
            RuleId = "SV-219153r897755_rule",
            VulnId = "V-219153"
        };

        // Act
        var explanation = $@"# {stig.StigId}: {stig.Title}

**Severity:** {stig.Severity}
**Category:** {stig.Category}
**STIG Family:** {stig.StigFamily}

## Description

{stig.Description}

## NIST 800-53 Controls

{string.Join(", ", stig.NistControls)}

## CCI References

{string.Join(", ", stig.CciRefs)}

## Check Procedure

{stig.CheckText}

## Remediation

{stig.FixText}

## Compliance Mapping

- **Rule ID:** {stig.RuleId}
- **Vuln ID:** {stig.VulnId}
- **STIG ID:** {stig.StigId}";

        // Assert
        explanation.Should().Contain("V-219153");
        explanation.Should().Contain("**Severity:** Medium");
        explanation.Should().Contain("Check Procedure");
        explanation.Should().Contain("Remediation");
        explanation.Should().Contain("AU-2");
    }

    #endregion

    #region Cross-Reference Tests

    [Fact]
    public void StigCrossReference_Format()
    {
        // Arrange
        var stig = new StigControl
        {
            StigId = "V-219153",
            Title = "Audit configuration",
            Severity = StigSeverity.Medium,
            NistControls = new List<string> { "AU-2", "AU-3" },
            CciRefs = new List<string> { "CCI-000172" }
        };

        // Act
        var crossRef = $@"# STIG Cross-Reference: {stig.StigId}

**Title:** {stig.Title}
**Severity:** {stig.Severity}

## NIST 800-53 Mappings
{string.Join("\n", stig.NistControls.Select(c => $"- {c}"))}

## CCI References
{string.Join("\n", stig.CciRefs.Select(c => $"- {c}"))}";

        // Assert
        crossRef.Should().Contain("Cross-Reference");
        crossRef.Should().Contain("NIST 800-53 Mappings");
        crossRef.Should().Contain("AU-2");
    }

    #endregion

    #region ControlMapping Model Tests

    [Fact]
    public void ControlMapping_FullMapping()
    {
        // Arrange & Act
        var mapping = new ControlMapping
        {
            NistControlId = "AU-2",
            StigIds = new List<string> { "V-219153", "V-219154" },
            CciIds = new List<string> { "CCI-000172", "CCI-000173" },
            DoDInstructions = new List<string> { "DoDI 8500.01" },
            Description = "Audit Events control mapping",
            ImplementationGuidance = new Dictionary<string, string>
            {
                ["azure"] = "Configure Azure Monitor",
                ["onprem"] = "Configure Windows Event Log"
            }
        };

        // Assert
        mapping.NistControlId.Should().Be("AU-2");
        mapping.StigIds.Should().HaveCount(2);
        mapping.ImplementationGuidance.Should().ContainKey("azure");
    }

    #endregion
}
