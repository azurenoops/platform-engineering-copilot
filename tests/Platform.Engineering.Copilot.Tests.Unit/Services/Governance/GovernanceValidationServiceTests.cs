using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services.Governance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Governance;

/// <summary>
/// Unit tests for the GovernanceValidationService.
/// Tests runtime enforcement of governance policies including:
/// - Approved regions (NIST CM-7, AC-3)
/// - Naming conventions (NIST CM-2)
/// - Required tags (NIST CM-2, AU-2)
/// </summary>
public class GovernanceValidationServiceTests
{
    private readonly Mock<ILogger<GovernanceValidationService>> _loggerMock;
    private readonly GovernanceOptions _defaultOptions;

    public GovernanceValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<GovernanceValidationService>>();
        _defaultOptions = new GovernanceOptions
        {
            EnforcePolicies = true,
            ApprovedRegions = new List<string> { "usgovvirginia", "usgovarizona", "usgovtexas" },
            EnforceNamingConventions = true,
            EnforceTagging = true,
            RequiredTags = new List<string> { "Environment", "Owner", "CostCenter" },
            EnableAuditLogging = true
        };
    }

    private GovernanceValidationService CreateService(GovernanceOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        return new GovernanceValidationService(_loggerMock.Object, opts);
    }

    #region Region Validation Tests

    [Theory]
    [InlineData("usgovvirginia", true)]
    [InlineData("usgovarizona", true)]
    [InlineData("usgovtexas", true)]
    [InlineData("USGovVirginia", true)]  // Case insensitive
    [InlineData("usgov virginia", true)]  // Space normalized
    [InlineData("eastus", false)]
    [InlineData("westeurope", false)]
    [InlineData("", false)]
    public void IsRegionApproved_ValidatesCorrectly(string region, bool expected)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.IsRegionApproved(region);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsRegionApproved_WhenNoApprovedRegionsConfigured_AllowsAllRegions()
    {
        // Arrange
        var options = new GovernanceOptions { ApprovedRegions = new List<string>() };
        var service = CreateService(options);

        // Act & Assert
        service.IsRegionApproved("eastus").Should().BeTrue();
        service.IsRegionApproved("westeurope").Should().BeTrue();
        service.IsRegionApproved("any-region").Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_BlocksUnapprovedRegion()
    {
        // Arrange
        var service = CreateService();
        var request = new GovernanceValidationRequest
        {
            Location = "eastus",
            EnvironmentName = "test-env",
            Tags = new Dictionary<string, string>
            {
                { "Environment", "dev" },
                { "Owner", "test@example.com" },
                { "CostCenter", "12345" }
            }
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.PolicyType == GovernancePolicyType.ApprovedRegion);
        result.Violations.First().NistControls.Should().Contain("CM-7");
    }

    #endregion

    #region Naming Convention Tests

    [Theory]
    [InlineData("my-environment", true)]
    [InlineData("prod-aks-cluster", true)]
    [InlineData("dev-001", true)]
    [InlineData("a", false)]  // Too short
    [InlineData("123-invalid", false)]  // Starts with number
    [InlineData("my_environment", false)]  // Underscore not allowed
    [InlineData("my--env", false)]  // Double hyphen forbidden
    [InlineData("admin-env", false)]  // Contains 'admin' forbidden
    public void ValidateResourceName_ValidatesCorrectly(string name, bool expectedValid)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateResourceName(name);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void ValidateResourceName_SuggestsValidName_WhenInvalid()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateResourceName("My Invalid Name!");

        // Assert
        result.IsValid.Should().BeFalse();
        result.SuggestedName.Should().NotBeNullOrEmpty();
        result.SuggestedName.Should().MatchRegex(@"^[a-z][a-z0-9-]*[a-z0-9]$");
    }

    [Fact]
    public async Task ValidateAsync_BlocksInvalidEnvironmentName()
    {
        // Arrange
        var service = CreateService();
        var request = new GovernanceValidationRequest
        {
            Location = "usgovvirginia",
            EnvironmentName = "123-bad-name",  // Starts with number
            Tags = new Dictionary<string, string>
            {
                { "Environment", "dev" },
                { "Owner", "test@example.com" },
                { "CostCenter", "12345" }
            }
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.PolicyType == GovernancePolicyType.NamingConvention);
    }

    #endregion

    #region Tag Validation Tests

    [Fact]
    public void ValidateRequiredTags_PassesWithAllRequiredTags()
    {
        // Arrange
        var service = CreateService();
        var tags = new Dictionary<string, string>
        {
            { "Environment", "production" },
            { "Owner", "platform-team@example.com" },
            { "CostCenter", "CC-12345" }
        };

        // Act
        var result = service.ValidateRequiredTags(tags);

        // Assert
        result.IsValid.Should().BeTrue();
        result.MissingTags.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRequiredTags_FailsWithMissingTags()
    {
        // Arrange
        var service = CreateService();
        var tags = new Dictionary<string, string>
        {
            { "Environment", "production" }
            // Missing: Owner, CostCenter
        };

        // Act
        var result = service.ValidateRequiredTags(tags);

        // Assert
        result.IsValid.Should().BeFalse();
        result.MissingTags.Should().Contain("Owner");
        result.MissingTags.Should().Contain("CostCenter");
    }

    [Fact]
    public void ValidateRequiredTags_IsCaseInsensitive()
    {
        // Arrange
        var service = CreateService();
        var tags = new Dictionary<string, string>
        {
            { "ENVIRONMENT", "production" },
            { "owner", "team@example.com" },
            { "costcenter", "12345" }
        };

        // Act
        var result = service.ValidateRequiredTags(tags);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_BlocksMissingRequiredTags()
    {
        // Arrange
        var service = CreateService();
        var request = new GovernanceValidationRequest
        {
            Location = "usgovvirginia",
            EnvironmentName = "my-env",
            Tags = new Dictionary<string, string>()  // Empty tags
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.PolicyType == GovernancePolicyType.RequiredTags);
    }

    #endregion

    #region Policy Enforcement Toggle Tests

    [Fact]
    public async Task ValidateAsync_SkipsValidation_WhenPoliciesDisabled()
    {
        // Arrange
        var options = new GovernanceOptions { EnforcePolicies = false };
        var service = CreateService(options);
        var request = new GovernanceValidationRequest
        {
            Location = "any-region",  // Would normally fail
            EnvironmentName = "123-bad-name",  // Would normally fail
            Tags = null  // Would normally fail
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_SkipsNamingValidation_WhenDisabled()
    {
        // Arrange
        var options = new GovernanceOptions
        {
            EnforcePolicies = true,
            EnforceNamingConventions = false,
            EnforceTagging = false,
            ApprovedRegions = new List<string>()  // Allow all
        };
        var service = CreateService(options);
        var request = new GovernanceValidationRequest
        {
            EnvironmentName = "123-bad-name"  // Would normally fail
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Complete Validation Tests

    [Fact]
    public async Task ValidateAsync_PassesCompleteValidRequest()
    {
        // Arrange
        var service = CreateService();
        var request = new GovernanceValidationRequest
        {
            Location = "usgovvirginia",
            EnvironmentName = "prod-aks-cluster",
            ResourceGroupName = "rg-prod-aks",
            Tags = new Dictionary<string, string>
            {
                { "Environment", "production" },
                { "Owner", "platform-team@example.com" },
                { "CostCenter", "CC-12345" }
            },
            RequestedBy = "platform-engineer@example.com"
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsMultipleViolations()
    {
        // Arrange
        var service = CreateService();
        var request = new GovernanceValidationRequest
        {
            Location = "eastus",  // Unapproved region
            EnvironmentName = "123-bad",  // Invalid name
            Tags = new Dictionary<string, string>()  // Missing required tags
        };

        // Act
        var result = await service.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Count.Should().Be(3);
        result.Violations.Should().Contain(v => v.PolicyType == GovernancePolicyType.ApprovedRegion);
        result.Violations.Should().Contain(v => v.PolicyType == GovernancePolicyType.NamingConvention);
        result.Violations.Should().Contain(v => v.PolicyType == GovernancePolicyType.RequiredTags);
    }

    #endregion
}
