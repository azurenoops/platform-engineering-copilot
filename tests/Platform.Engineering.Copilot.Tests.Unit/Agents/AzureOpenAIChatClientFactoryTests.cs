using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T008 — AzureOpenAIChatClientFactory unit tests.
/// T037 — AzureOpenAIOptions validation unit tests.
/// </summary>
public class AzureOpenAIChatClientFactoryTests
{
    private readonly Mock<ILogger<AzureOpenAIChatClientFactory>> _loggerMock = new();

    private AzureOpenAIChatClientFactory CreateFactory(AzureOpenAIOptions options)
    {
        return new AzureOpenAIChatClientFactory(
            Options.Create(options),
            _loggerMock.Object);
    }

    // ─── T008: Factory Tests ────────────────────────────────────────

    [Fact]
    public void CreateChatClient_EmptyEndpoint_ReturnsNull()
    {
        var factory = CreateFactory(new AzureOpenAIOptions { Endpoint = "" });

        var client = factory.CreateChatClient();

        client.Should().BeNull();
    }

    [Fact]
    public void CreateChatClient_WhitespaceEndpoint_ReturnsNull()
    {
        var factory = CreateFactory(new AzureOpenAIOptions { Endpoint = "   " });

        var client = factory.CreateChatClient();

        client.Should().BeNull();
    }

    [Fact]
    public void CreateChatClient_EmptyDeploymentName_ReturnsNull()
    {
        var factory = CreateFactory(new AzureOpenAIOptions
        {
            Endpoint = "https://test.openai.azure.com",
            DeploymentName = ""
        });

        var client = factory.CreateChatClient();

        client.Should().BeNull();
    }

    [Fact]
    public void CreateChatClient_ValidConfigWithApiKey_ReturnsNonNull()
    {
        var factory = CreateFactory(new AzureOpenAIOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "test-key-12345",
            DeploymentName = "gpt-4o",
            ModelId = "gpt-4o"
        });

        var client = factory.CreateChatClient();

        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateChatClient_ValidConfigWithManagedIdentity_ReturnsNonNull()
    {
        // No API key — should use DefaultAzureCredential
        var factory = CreateFactory(new AzureOpenAIOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "",
            DeploymentName = "gpt-4o",
            ModelId = "gpt-4o"
        });

        var client = factory.CreateChatClient();

        client.Should().NotBeNull();
    }

    [Fact]
    public void IsAzureGovernment_GovEndpoint_ReturnsTrue()
    {
        AzureOpenAIChatClientFactory.IsAzureGovernment(
            new Uri("https://test.openai.azure.us")).Should().BeTrue();
    }

    [Fact]
    public void IsAzureGovernment_CommercialEndpoint_ReturnsFalse()
    {
        AzureOpenAIChatClientFactory.IsAzureGovernment(
            new Uri("https://test.openai.azure.com")).Should().BeFalse();
    }

    [Fact]
    public void IsAzureGovernment_UsGovVirginia_ReturnsTrue()
    {
        AzureOpenAIChatClientFactory.IsAzureGovernment(
            new Uri("https://test.cognitiveservices.azure.us")).Should().BeTrue();
    }

    [Fact]
    public void CreateChatClient_GovEndpointWithApiKey_ReturnsNonNull()
    {
        var factory = CreateFactory(new AzureOpenAIOptions
        {
            Endpoint = "https://test.openai.azure.us",
            ApiKey = "test-key-12345",
            DeploymentName = "gpt-4o",
            ModelId = "gpt-4o"
        });

        var client = factory.CreateChatClient();

        client.Should().NotBeNull();
    }

    // ─── T037: AzureOpenAIOptions Validation Tests ──────────────────

    [Fact]
    public void Validate_MaxToolCallRoundsBelow1_Rejects()
    {
        var options = new AzureOpenAIOptions { MaxToolCallRounds = 0 };

        var results = options.Validate().ToList();

        results.Should().Contain(r => r.MemberNames.Contains("MaxToolCallRounds"));
    }

    [Fact]
    public void Validate_MaxToolCallRoundsAbove20_Rejects()
    {
        var options = new AzureOpenAIOptions { MaxToolCallRounds = 21 };

        var results = options.Validate().ToList();

        results.Should().Contain(r => r.MemberNames.Contains("MaxToolCallRounds"));
    }

    [Fact]
    public void Validate_MaxToolCallRoundsInRange_Passes()
    {
        var options = new AzureOpenAIOptions { MaxToolCallRounds = 5 };

        var results = options.Validate().ToList();

        results.Should().NotContain(r => r.MemberNames.Contains("MaxToolCallRounds"));
    }

    [Fact]
    public void Validate_TemperatureBelowZero_Rejects()
    {
        var options = new AzureOpenAIOptions { Temperature = -0.1f };

        var results = options.Validate().ToList();

        results.Should().Contain(r => r.MemberNames.Contains("Temperature"));
    }

    [Fact]
    public void Validate_TemperatureAbove2_Rejects()
    {
        var options = new AzureOpenAIOptions { Temperature = 2.1f };

        var results = options.Validate().ToList();

        results.Should().Contain(r => r.MemberNames.Contains("Temperature"));
    }

    [Fact]
    public void Validate_TemperatureInRange_Passes()
    {
        var options = new AzureOpenAIOptions { Temperature = 0.3f };

        var results = options.Validate().ToList();

        results.Should().NotContain(r => r.MemberNames.Contains("Temperature"));
    }

    [Fact]
    public void Validate_EmptyDeploymentNameWithEndpoint_Rejects()
    {
        var options = new AzureOpenAIOptions
        {
            Endpoint = "https://test.openai.azure.com",
            DeploymentName = ""
        };

        var results = options.Validate().ToList();

        results.Should().Contain(r => r.MemberNames.Contains("DeploymentName"));
    }

    [Fact]
    public void Validate_EmptyDeploymentNameNoEndpoint_Passes()
    {
        var options = new AzureOpenAIOptions
        {
            Endpoint = "",
            DeploymentName = ""
        };

        var results = options.Validate().ToList();

        results.Should().NotContain(r => r.MemberNames.Contains("DeploymentName"));
    }

    [Fact]
    public void Validate_DefaultOptions_Passes()
    {
        var options = new AzureOpenAIOptions();

        var results = options.Validate().ToList();

        results.Should().BeEmpty();
    }
}
