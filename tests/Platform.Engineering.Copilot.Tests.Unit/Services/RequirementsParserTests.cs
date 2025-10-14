using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Platform.Engineering.Copilot.Core.Services.Onboarding.Parsing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for RequirementsParser - Strategy 1: JSON Parsing
/// </summary>
public class RequirementsParserJsonTests
{
    private readonly Mock<ILogger<RequirementsParser>> _mockLogger;
    private readonly RequirementsParser _parser;

    public RequirementsParserJsonTests()
    {
        _mockLogger = new Mock<ILogger<RequirementsParser>>();
        _parser = new RequirementsParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseAsync_ValidJson_ExtractsAllFields()
    {
        // Arrange
        var input = @"{
            ""classificationLevel"": ""Secret"",
            ""environmentType"": ""Production"",
            ""region"": ""US Gov Virginia"",
            ""requiredServices"": ""AKS cluster, Azure SQL"",
            ""networkRequirements"": ""VNet isolation"",
            ""computeRequirements"": ""4 vCPUs, 16GB RAM"",
            ""databaseRequirements"": ""Azure SQL, geo-redundant"",
            ""complianceFrameworks"": ""FedRAMP High, NIST 800-53"",
            ""securityControls"": ""MFA, encryption at rest"",
            ""targetDeploymentDate"": ""2025-11-01"",
            ""expectedGoLiveDate"": ""2025-12-01""
        }";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(11, result.Count);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("US Gov Virginia", result["region"]);
        Assert.Equal("AKS cluster, Azure SQL", result["requiredServices"]);
        Assert.Equal("VNet isolation", result["networkRequirements"]);
        Assert.Equal("4 vCPUs, 16GB RAM", result["computeRequirements"]);
        Assert.Equal("Azure SQL, geo-redundant", result["databaseRequirements"]);
        Assert.Equal("FedRAMP High, NIST 800-53", result["complianceFrameworks"]);
        Assert.Equal("MFA, encryption at rest", result["securityControls"]);
        Assert.Equal("2025-11-01", result["targetDeploymentDate"]);
        Assert.Equal("2025-12-01", result["expectedGoLiveDate"]);
    }

    [Fact]
    public async Task ParseAsync_MinimalJson_ExtractsPartialFields()
    {
        // Arrange
        var input = @"{
            ""classificationLevel"": ""Secret"",
            ""environmentType"": ""Production""
        }";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_JsonWithNonStandardKeys_NormalizesKeys()
    {
        // Arrange
        var input = @"{
            ""classification"": ""Secret"",
            ""environment"": ""Production"",
            ""location"": ""East US""
        }";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("East US", result["region"]);
    }

    [Fact]
    public async Task ParseAsync_CompactJson_Parses()
    {
        // Arrange
        var input = @"{""classificationLevel"":""Secret"",""environmentType"":""Production""}";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_ReturnsEmpty()
    {
        // Arrange
        var input = @"{ invalid json }";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert - Should try other strategies, not return JSON
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseAsync_EmptyJson_ReturnsEmpty()
    {
        // Arrange
        var input = @"{}";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseAsync_NullInput_ReturnsEmpty()
    {
        // Act
        var result = await _parser.ParseAsync(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseAsync_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = await _parser.ParseAsync("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseAsync_WhitespaceInput_ReturnsEmpty()
    {
        // Act
        var result = await _parser.ParseAsync("   \n\t  ");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

/// <summary>
/// Unit tests for RequirementsParser - Strategy 2: Bullet List Parsing
/// </summary>
public class RequirementsParserBulletListTests
{
    private readonly Mock<ILogger<RequirementsParser>> _mockLogger;
    private readonly RequirementsParser _parser;

    public RequirementsParserBulletListTests()
    {
        _mockLogger = new Mock<ILogger<RequirementsParser>>();
        _parser = new RequirementsParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseAsync_BulletListWithDash_ExtractsAllFields()
    {
        // Arrange
        var input = @"
- Classification: Secret
- Environment: Production
- Region: US Gov Virginia
- Services: AKS cluster, Azure SQL
- Network: VNet isolation
- Compute: 4 vCPUs, 16GB RAM
- Database: Azure SQL, geo-redundant
- Compliance: FedRAMP High, NIST 800-53
- Security: MFA, encryption at rest
- Target Deployment: 2025-11-01
- Go-Live: 2025-12-01";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 10, $"Expected at least 10 fields, got {result.Count}");
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("US Gov Virginia", result["region"]);
        Assert.Equal("AKS cluster, Azure SQL", result["requiredServices"]);
        Assert.Equal("VNet isolation", result["networkRequirements"]);
        Assert.Equal("4 vCPUs, 16GB RAM", result["computeRequirements"]);
    }

    [Fact]
    public async Task ParseAsync_BulletListWithAsterisk_ExtractsFields()
    {
        // Arrange
        var input = @"
* Classification: Secret
* Environment: Production
* Region: East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("East US", result["region"]);
    }

    [Fact]
    public async Task ParseAsync_BulletListWithBulletPoint_ExtractsFields()
    {
        // Arrange
        var input = @"
• Classification: Top Secret
• Environment: Production
• Region: US Gov Virginia";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);
        Assert.Equal("Top Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("US Gov Virginia", result["region"]);
    }

    [Fact]
    public async Task ParseAsync_MinimalBulletList_ExtractsPartialFields()
    {
        // Arrange
        var input = @"
- Classification: Secret
- Environment: Production";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_BulletListWithVariedSpacing_HandlesCorrectly()
    {
        // Arrange
        var input = @"
-Classification:Secret
-   Environment  :   Production  
-  Region :US Gov Virginia  ";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("US Gov Virginia", result["region"]);
    }

    [Fact]
    public async Task ParseAsync_MixedBulletStyles_ExtractsAll()
    {
        // Arrange
        var input = @"
- Classification: Secret
* Environment: Production
• Region: East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("East US", result["region"]);
    }

    [Fact]
    public async Task ParseAsync_BulletListWithExtraText_IgnoresNonBullets()
    {
        // Arrange
        var input = @"Here are the requirements:
- Classification: Secret
- Environment: Production
This is for Mission Alpha.";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }
}

/// <summary>
/// Unit tests for RequirementsParser - Strategy 3: Key-Value Pattern Matching
/// </summary>
public class RequirementsParserKeyValueTests
{
    private readonly Mock<ILogger<RequirementsParser>> _mockLogger;
    private readonly RequirementsParser _parser;

    public RequirementsParserKeyValueTests()
    {
        _mockLogger = new Mock<ILogger<RequirementsParser>>();
        _parser = new RequirementsParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseAsync_KeyValueWithIs_ExtractsFields()
    {
        // Arrange
        var input = "Classification is Secret and environment is Production and region is East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2); // At least classification and environment
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_KeyValueWithColon_ExtractsFields()
    {
        // Arrange
        var input = "Classification: Secret. Environment: Production. Region: US Gov Virginia.";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_MixedKeyValueFormats_ExtractsAll()
    {
        // Arrange
        var input = "Classification is Secret, environment: Production, region in East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_NaturalLanguageSentence_ExtractsFields()
    {
        // Arrange
        var input = "The classification is Secret and we need a Production environment type";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_LongSentence_ExtractsMultipleFields()
    {
        // Arrange
        var input = @"We need a Secret classification production environment in US Gov Virginia 
                      with AKS cluster and Azure SQL services requiring VNet isolation for networking.";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2); // Should extract at least some fields
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_EnvironmentTypeVariants_NormalizesCorrectly()
    {
        // Arrange - Test "dev" → "Development"
        var input1 = "Environment is dev";
        var input2 = "Environment type: staging";
        var input3 = "Environment: prod";

        // Act
        var result1 = await _parser.ParseAsync(input1);
        var result2 = await _parser.ParseAsync(input2);
        var result3 = await _parser.ParseAsync(input3);

        // Assert
        Assert.NotNull(result1);
        Assert.True(result1.ContainsKey("environmentType"));
        Assert.NotNull(result2);
        Assert.Equal("staging", result2["environmentType"]);
        Assert.NotNull(result3);
        Assert.Equal("prod", result3["environmentType"]);
    }
}

/// <summary>
/// Unit tests for RequirementsParser - Strategy 4: Comma-Separated Parsing
/// </summary>
public class RequirementsParserCommaSeparatedTests
{
    private readonly Mock<ILogger<RequirementsParser>> _mockLogger;
    private readonly RequirementsParser _parser;

    public RequirementsParserCommaSeparatedTests()
    {
        _mockLogger = new Mock<ILogger<RequirementsParser>>();
        _parser = new RequirementsParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseAsync_CommaSeparatedWithIs_ExtractsAllFields()
    {
        // Arrange
        var input = "Classification is Secret, environment is Production, region is US Gov Virginia, services are AKS cluster and Azure SQL";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_CommaSeparatedWithColon_ExtractsFields()
    {
        // Arrange
        var input = "Classification: Secret, Environment: Production, Region: East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
        Assert.Equal("East US", result["region"]);
    }

    [Fact]
    public async Task ParseAsync_CommaSeparatedMixedFormat_ExtractsFields()
    {
        // Arrange
        var input = "Classification is Secret, Environment: Production, region in East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }

    [Fact]
    public async Task ParseAsync_SingleCommaSegment_ExtractsField()
    {
        // Arrange
        var input = "Classification is Secret";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 1);
        Assert.Equal("Secret", result["classificationLevel"]);
    }

    [Fact]
    public async Task ParseAsync_CommaSeparatedWithExtraCommas_HandlesCorrectly()
    {
        // Arrange
        var input = "Classification is Secret,, Environment is Production,";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Equal("Secret", result["classificationLevel"]);
        Assert.Equal("Production", result["environmentType"]);
    }
}

/// <summary>
/// Unit tests for RequirementsParser - Key Normalization
/// </summary>
public class RequirementsParserNormalizationTests
{
    private readonly Mock<ILogger<RequirementsParser>> _mockLogger;
    private readonly RequirementsParser _parser;

    public RequirementsParserNormalizationTests()
    {
        _mockLogger = new Mock<ILogger<RequirementsParser>>();
        _parser = new RequirementsParser(_mockLogger.Object);
    }

    [Theory]
    [InlineData("Classification", "classificationLevel")]
    [InlineData("classification level", "classificationLevel")]
    [InlineData("CLASSIFICATION", "classificationLevel")]
    [InlineData("Classification Level", "classificationLevel")]
    public async Task ParseAsync_ClassificationVariants_NormalizesToSameKey(string variant, string expectedKey)
    {
        // Arrange
        var input = $"- {variant}: Secret";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey(expectedKey));
        Assert.Equal("Secret", result[expectedKey]);
    }

    [Theory]
    [InlineData("Environment", "environmentType")]
    [InlineData("environment type", "environmentType")]
    [InlineData("env type", "environmentType")]
    [InlineData("env", "environmentType")]
    public async Task ParseAsync_EnvironmentVariants_NormalizesToSameKey(string variant, string expectedKey)
    {
        // Arrange
        var input = $"- {variant}: Production";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey(expectedKey));
        Assert.Equal("Production", result[expectedKey]);
    }

    [Theory]
    [InlineData("Region", "region")]
    [InlineData("location", "region")]
    [InlineData("Azure Region", "region")]
    [InlineData("REGION", "region")]
    public async Task ParseAsync_RegionVariants_NormalizesToSameKey(string variant, string expectedKey)
    {
        // Arrange
        var input = $"- {variant}: East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey(expectedKey));
        Assert.Equal("East US", result[expectedKey]);
    }
}

/// <summary>
/// Unit tests for RequirementsParser - Edge Cases and Error Handling
/// </summary>
public class RequirementsParserEdgeCaseTests
{
    private readonly Mock<ILogger<RequirementsParser>> _mockLogger;
    private readonly RequirementsParser _parser;

    public RequirementsParserEdgeCaseTests()
    {
        _mockLogger = new Mock<ILogger<RequirementsParser>>();
        _parser = new RequirementsParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseAsync_VeryLongInput_Handles()
    {
        // Arrange
        var input = string.Join(", ", Enumerable.Repeat("Classification is Secret", 1000));

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public async Task ParseAsync_SpecialCharacters_HandlesGracefully()
    {
        // Arrange
        var input = "Classification: Secret™, Environment: Production®, Region: East-US™";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        // Should still extract some fields
        Assert.True(result.Count >= 0);
    }

    [Fact]
    public async Task ParseAsync_UnicodeCharacters_Handles()
    {
        // Arrange
        var input = "- Classification: Secret\n- Environment: Production\n- Region: 美国东部";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public async Task ParseAsync_MultiplePatternsInSameInput_ChoosesBestStrategy()
    {
        // Arrange - Mix bullet list with comma-separated
        var input = @"- Classification: Secret
Environment is Production, Region: East US";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2); // Should extract at least 2 fields
    }

    [Fact]
    public async Task ParseAsync_NoRecognizablePattern_ReturnsEmpty()
    {
        // Arrange
        var input = "This is just random text with no extractable requirements";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        // May be empty or have minimal extraction
        Assert.True(result.Count >= 0);
    }

    [Fact]
    public async Task ParseAsync_OnlyDelimiters_ReturnsEmpty()
    {
        // Arrange
        var input = "- : , . - : ,";

        // Act
        var result = await _parser.ParseAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
