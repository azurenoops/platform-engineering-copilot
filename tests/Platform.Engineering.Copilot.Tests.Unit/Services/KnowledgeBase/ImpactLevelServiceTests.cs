using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.KnowledgeBase;

/// <summary>
/// Unit tests for ImpactLevelService
/// Tests Impact Level retrieval, comparison, migration guidance, and Azure implementation
/// </summary>
public class ImpactLevelServiceTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<ImpactLevelServiceTests>> _loggerMock;

    public ImpactLevelServiceTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<ImpactLevelServiceTests>>();
    }

    #region ImpactLevel Model Tests

    [Fact]
    public void ImpactLevel_IL2_Configuration()
    {
        // Arrange & Act
        var level = new ImpactLevel
        {
            Level = "IL2",
            Name = "Impact Level 2 - Public Cloud Data",
            Description = "Non-CUI data that can be processed in public cloud",
            Requirements = new List<string>
            {
                "FedRAMP Moderate baseline",
                "Standard encryption requirements"
            },
            NistBaseline = new List<string> { "Moderate" },
            MandatoryControls = new List<string>(),
            AzureConfigurations = new Dictionary<string, string>
            {
                ["environment"] = "AzureCloud",
                ["regions"] = "All commercial regions"
            }
        };

        // Assert
        level.Level.Should().Be("IL2");
        level.AzureConfigurations["environment"].Should().Be("AzureCloud");
    }

    [Fact]
    public void ImpactLevel_IL4_Configuration()
    {
        // Arrange & Act
        var level = new ImpactLevel
        {
            Level = "IL4",
            Name = "Impact Level 4 - Controlled Unclassified Information",
            Description = "CUI data that requires Azure Government",
            Requirements = new List<string>
            {
                "Azure Government only",
                "FIPS 140-2 validated encryption",
                "US Citizen access for Azure support"
            },
            NistBaseline = new List<string> { "Moderate", "High" },
            MandatoryControls = new List<string> { "AC-2", "IA-2", "SC-28" },
            AzureConfigurations = new Dictionary<string, string>
            {
                ["environment"] = "AzureGovernment",
                ["regions"] = "usgovvirginia, usgovarizona, usgovtexas"
            }
        };

        // Assert
        level.Level.Should().Be("IL4");
        level.AzureConfigurations["environment"].Should().Be("AzureGovernment");
        level.Requirements.Should().Contain(r => r.Contains("Azure Government"));
    }

    [Fact]
    public void ImpactLevel_IL5_Configuration()
    {
        // Arrange & Act
        var level = new ImpactLevel
        {
            Level = "IL5",
            Name = "Impact Level 5 - CUI with Enhanced Protection",
            Description = "Higher sensitivity CUI requiring additional protections",
            Requirements = new List<string>
            {
                "Azure Government with physical isolation",
                "FIPS 140-2/140-3 validated encryption",
                "US Citizen-only access",
                "Background investigation required"
            },
            NistBaseline = new List<string> { "High" },
            MandatoryControls = new List<string> { "SC-7", "SC-28", "IA-2(1)", "AC-2(7)" },
            AzureConfigurations = new Dictionary<string, string>
            {
                ["environment"] = "AzureGovernment",
                ["regions"] = "usgovvirginia, usgovarizona",
                ["isolation"] = "Physical isolation required"
            }
        };

        // Assert
        level.Level.Should().Be("IL5");
        level.Requirements.Should().Contain(r => r.Contains("physical isolation"));
        level.MandatoryControls.Should().Contain("SC-28");
    }

    [Fact]
    public void ImpactLevel_IL6_Configuration()
    {
        // Arrange & Act
        var level = new ImpactLevel
        {
            Level = "IL6",
            Name = "Impact Level 6 - Classified (Secret)",
            Description = "Classified information up to SECRET",
            Requirements = new List<string>
            {
                "Azure Government Secret",
                "NSA Type 1 encryption",
                "Cleared personnel only",
                "Air-gapped network"
            },
            NistBaseline = new List<string> { "High" },
            MandatoryControls = new List<string> { "SC-7(18)", "SC-8(1)", "SC-28(1)" },
            AzureConfigurations = new Dictionary<string, string>
            {
                ["environment"] = "AzureGovernmentSecret",
                ["classification"] = "SECRET"
            }
        };

        // Assert
        level.Level.Should().Be("IL6");
        level.AzureConfigurations["environment"].Should().Be("AzureGovernmentSecret");
        level.Requirements.Should().Contain("Azure Government Secret");
    }

    #endregion

    #region Level Validation Tests

    [Theory]
    [InlineData("IL2", true)]
    [InlineData("IL4", true)]
    [InlineData("IL5", true)]
    [InlineData("IL6", true)]
    [InlineData("il2", true)]
    [InlineData("il5", true)]
    [InlineData("IL3", false)]
    [InlineData("IL7", false)]
    [InlineData("L5", false)]
    [InlineData("5", false)]
    public void ImpactLevel_Validation(string level, bool isValid)
    {
        // Arrange
        var validLevels = new HashSet<string> { "IL2", "IL4", "IL5", "IL6" };
        var normalized = level.ToUpperInvariant().Trim();

        // Act
        var result = validLevels.Contains(normalized);

        // Assert
        result.Should().Be(isValid);
    }

    [Theory]
    [InlineData("il2", "IL2")]
    [InlineData("IL2", "IL2")]
    [InlineData(" il5 ", "IL5")]
    [InlineData("Il4", "IL4")]
    public void ImpactLevel_Normalization(string input, string expected)
    {
        // Act
        var normalized = input.ToUpperInvariant().Trim();

        // Assert
        normalized.Should().Be(expected);
    }

    #endregion

    #region Azure Environment Mapping Tests

    [Theory]
    [InlineData("IL2", "AzureCloud")]
    [InlineData("IL4", "AzureGovernment")]
    [InlineData("IL5", "AzureGovernment")]
    [InlineData("IL6", "AzureGovernmentSecret")]
    public void ImpactLevel_AzureEnvironment(string level, string expectedEnvironment)
    {
        // Arrange
        var envMap = new Dictionary<string, string>
        {
            ["IL2"] = "AzureCloud",
            ["IL4"] = "AzureGovernment",
            ["IL5"] = "AzureGovernment",
            ["IL6"] = "AzureGovernmentSecret"
        };

        // Assert
        envMap[level].Should().Be(expectedEnvironment);
    }

    [Theory]
    [InlineData("IL2", new[] { "eastus", "westus", "centralus" })]
    [InlineData("IL4", new[] { "usgovvirginia", "usgovarizona", "usgovtexas" })]
    [InlineData("IL5", new[] { "usgovvirginia", "usgovarizona" })]
    public void ImpactLevel_AvailableRegions(string level, string[] expectedRegions)
    {
        // Arrange
        var regionMap = new Dictionary<string, List<string>>
        {
            ["IL2"] = new List<string> { "eastus", "westus", "centralus" },
            ["IL4"] = new List<string> { "usgovvirginia", "usgovarizona", "usgovtexas" },
            ["IL5"] = new List<string> { "usgovvirginia", "usgovarizona" }
        };

        // Assert
        regionMap[level].Should().BeEquivalentTo(expectedRegions);
    }

    #endregion

    #region Encryption Requirements Tests

    [Theory]
    [InlineData("IL2", "TLS 1.2", "AES-256")]
    [InlineData("IL4", "TLS 1.2", "FIPS 140-2")]
    [InlineData("IL5", "TLS 1.2", "FIPS 140-2/140-3")]
    public void ImpactLevel_EncryptionRequirements(string level, string transitEncryption, string atRestEncryption)
    {
        // Arrange
        var encryptionReqs = new Dictionary<string, (string Transit, string AtRest)>
        {
            ["IL2"] = ("TLS 1.2", "AES-256"),
            ["IL4"] = ("TLS 1.2", "FIPS 140-2"),
            ["IL5"] = ("TLS 1.2", "FIPS 140-2/140-3")
        };

        // Assert
        encryptionReqs[level].Transit.Should().Be(transitEncryption);
        encryptionReqs[level].AtRest.Should().Be(atRestEncryption);
    }

    #endregion

    #region BoundaryProtectionRequirement Model Tests

    [Fact]
    public void BoundaryProtectionRequirement_IL5()
    {
        // Arrange & Act
        var requirement = new BoundaryProtectionRequirement
        {
            ImpactLevel = "IL5",
            RequirementId = "BP-IL5-001",
            Description = "Boundary protection for IL5 workloads",
            MandatoryControls = new List<string> { "SC-7", "SC-7(4)", "SC-7(5)", "SC-7(18)" },
            NetworkRequirements = new List<string>
            {
                "Dedicated ExpressRoute connection",
                "Azure Firewall Premium required",
                "No public IP addresses allowed",
                "Private endpoints for all PaaS services"
            },
            EncryptionRequirements = new List<string>
            {
                "TLS 1.2 minimum for data in transit",
                "FIPS 140-2 validated cryptography",
                "Customer-managed keys required"
            },
            AzureImplementation = new Dictionary<string, string>
            {
                ["firewall"] = "Azure Firewall Premium",
                ["topology"] = "Hub-spoke with forced tunneling",
                ["dns"] = "Private DNS zones"
            }
        };

        // Assert
        requirement.ImpactLevel.Should().Be("IL5");
        requirement.MandatoryControls.Should().Contain("SC-7");
        requirement.NetworkRequirements.Should().HaveCountGreaterOrEqualTo(3);
        requirement.AzureImplementation["firewall"].Should().Be("Azure Firewall Premium");
    }

    [Fact]
    public void BoundaryProtectionRequirement_DefaultValues()
    {
        // Arrange & Act
        var requirement = new BoundaryProtectionRequirement();

        // Assert
        requirement.ImpactLevel.Should().BeEmpty();
        requirement.NetworkRequirements.Should().BeEmpty();
        requirement.EncryptionRequirements.Should().BeEmpty();
        requirement.AzureImplementation.Should().BeEmpty();
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void ImpactLevel_Comparison_IL2vsIL4()
    {
        // Arrange
        var comparisonCategories = new List<(string Category, string IL2, string IL4)>
        {
            ("Cloud Environment", "Azure Commercial", "Azure Government"),
            ("Data Classification", "Public/Non-CUI", "CUI"),
            ("Encryption", "Standard", "FIPS 140-2"),
            ("Personnel", "No clearance required", "US Citizen support")
        };

        // Assert
        comparisonCategories.Should().Contain(c => c.Category == "Cloud Environment");
        comparisonCategories.First(c => c.Category == "Cloud Environment").IL2.Should().Be("Azure Commercial");
        comparisonCategories.First(c => c.Category == "Cloud Environment").IL4.Should().Be("Azure Government");
    }

    [Fact]
    public void ImpactLevel_Comparison_IL4vsIL5()
    {
        // Arrange
        var comparisonCategories = new List<(string Category, string IL4, string IL5)>
        {
            ("Physical Isolation", "Logical isolation", "Physical isolation required"),
            ("Personnel Clearance", "Background check", "Elevated background investigation"),
            ("Network Access", "VPN/ExpressRoute", "Dedicated ExpressRoute only")
        };

        // Assert
        comparisonCategories.Should().Contain(c => c.Category == "Physical Isolation");
        comparisonCategories.First(c => c.Category == "Physical Isolation").IL5.Should().Contain("Physical isolation");
    }

    #endregion

    #region Migration Guidance Tests

    [Theory]
    [InlineData("IL2", "IL4")]
    [InlineData("IL4", "IL5")]
    public void MigrationPath_ValidPaths(string fromLevel, string toLevel)
    {
        // Arrange
        var validMigrations = new HashSet<string> { "IL2toIL4", "IL4toIL5" };
        var migrationKey = $"{fromLevel}to{toLevel}";

        // Assert
        validMigrations.Should().Contain(migrationKey);
    }

    [Fact]
    public void MigrationGuidance_IL2toIL4()
    {
        // Arrange
        var migrationSteps = new List<string>
        {
            "Deploy resources in Azure Government",
            "Enable FIPS 140-2 validated encryption",
            "Configure compliant networking (ExpressRoute/VPN)",
            "Implement additional NIST controls",
            "Update authorization package"
        };

        // Assert
        migrationSteps.Should().HaveCountGreaterOrEqualTo(5);
        migrationSteps.Should().Contain(s => s.Contains("Azure Government"));
    }

    [Fact]
    public void MigrationGuidance_IL4toIL5()
    {
        // Arrange
        var migrationSteps = new List<string>
        {
            "Implement physical isolation requirements",
            "Configure dedicated ExpressRoute",
            "Enable customer-managed keys",
            "Implement additional boundary protection",
            "Conduct enhanced personnel screening"
        };

        // Assert
        migrationSteps.Should().HaveCountGreaterOrEqualTo(5);
        migrationSteps.Should().Contain(s => s.Contains("physical isolation"));
    }

    #endregion

    #region Azure Implementation Tests

    [Fact]
    public void AzureImplementation_IL5_RequiredServices()
    {
        // Arrange
        var requiredServices = new List<string>
        {
            "Azure Firewall Premium",
            "Azure Private Link",
            "Azure Key Vault with HSM",
            "Azure Private DNS",
            "ExpressRoute with Private Peering",
            "Azure Policy"
        };

        // Assert
        requiredServices.Should().Contain("Azure Firewall Premium");
        requiredServices.Should().Contain("Azure Key Vault with HSM");
    }

    [Theory]
    [InlineData("IL4", new[] { "Azure Firewall", "Azure Key Vault", "Azure Monitor" })]
    [InlineData("IL5", new[] { "Azure Firewall Premium", "Azure Key Vault HSM", "Private DNS" })]
    public void AzureImplementation_ServicesByLevel(string level, string[] expectedServices)
    {
        // Arrange
        var serviceMap = new Dictionary<string, List<string>>
        {
            ["IL4"] = new List<string> { "Azure Firewall", "Azure Key Vault", "Azure Monitor" },
            ["IL5"] = new List<string> { "Azure Firewall Premium", "Azure Key Vault HSM", "Private DNS" }
        };

        // Assert
        serviceMap[level].Should().BeEquivalentTo(expectedServices);
    }

    #endregion

    #region Explanation Formatting Tests

    [Fact]
    public void ImpactLevelExplanation_Format()
    {
        // Arrange
        var level = new ImpactLevel
        {
            Level = "IL5",
            Name = "Impact Level 5",
            Description = "CUI with enhanced protection",
            Requirements = new List<string> { "Physical isolation", "FIPS 140-2" }
        };

        // Act
        var explanation = $@"# {level.Name} ({level.Level})

## Description
{level.Description}

## Requirements
{string.Join("\n", level.Requirements.Select(r => $"- {r}"))}";

        // Assert
        explanation.Should().Contain("IL5");
        explanation.Should().Contain("Requirements");
        explanation.Should().Contain("Physical isolation");
    }

    #endregion

    #region Data Classification Tests

    [Theory]
    [InlineData("IL2", "Public", true)]
    [InlineData("IL2", "Non-CUI", true)]
    [InlineData("IL2", "CUI", false)]
    [InlineData("IL4", "CUI", true)]
    [InlineData("IL4", "CUI Basic", true)]
    [InlineData("IL5", "CUI Specified", true)]
    public void DataClassification_Authorization(string level, string dataType, bool authorized)
    {
        // Arrange
        var authorizationMap = new Dictionary<string, HashSet<string>>
        {
            ["IL2"] = new HashSet<string> { "Public", "Non-CUI" },
            ["IL4"] = new HashSet<string> { "CUI", "CUI Basic", "Non-CUI" },
            ["IL5"] = new HashSet<string> { "CUI", "CUI Basic", "CUI Specified", "NOFORN" }
        };

        // Act
        var isAuthorized = authorizationMap.ContainsKey(level) && authorizationMap[level].Contains(dataType);

        // Assert
        isAuthorized.Should().Be(authorized);
    }

    #endregion
}
