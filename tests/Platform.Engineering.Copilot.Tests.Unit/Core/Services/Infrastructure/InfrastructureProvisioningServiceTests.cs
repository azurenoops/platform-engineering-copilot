using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services.Infrastructure;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Infrastructure;

public class InfrastructureProvisioningServiceTests
{
    private readonly Mock<ILogger<InfrastructureProvisioningService>> _mockLogger;
    private readonly Mock<IAzureResourceService> _mockAzureResourceService;
    private readonly Mock<IChatCompletionService> _mockChatCompletionService;
    private readonly Kernel _kernel;
    private readonly InfrastructureProvisioningService _service;

    public InfrastructureProvisioningServiceTests()
    {
        _mockLogger = new Mock<ILogger<InfrastructureProvisioningService>>();
        _mockAzureResourceService = new Mock<IAzureResourceService>();
        _mockChatCompletionService = new Mock<IChatCompletionService>();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(_mockChatCompletionService.Object);
        builder.Services.AddLogging();
        _kernel = builder.Build();

        _service = new InfrastructureProvisioningService(
            _mockLogger.Object,
            _mockAzureResourceService.Object,
            _kernel);
    }

    [Fact]
    public async Task ProvisionInfrastructureAsync_WithValidAiResponse_ReturnsSuccessAsync()
    {
        var responseContent = "{\"resourceType\":\"storage-account\",\"resourceGroupName\":\"rg-test\",\"resourceName\":\"teststorage\",\"location\":\"eastus\",\"parameters\":{\"sku\":\"Standard_LRS\",\"enableHttpsOnly\":true}}";

        _mockChatCompletionService
            .Setup(service => service.GetChatMessageContentAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMessageContent(AuthorRole.Assistant, responseContent));

        var result = await _service.ProvisionInfrastructureAsync("Create a storage account", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResourceName.Should().Be("teststorage");
        result.ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
        result.Properties.Should().ContainKey("sku");
        result.Properties!["sku"].Should().Be("Standard_LRS");
        result.Message.Should().NotBeNull();
        result.Message!.Should().ContainEquivalentOf("storage account");
    }

    [Fact]
    public async Task ProvisionInfrastructureAsync_WithMarkdownWrappedJson_StripsCodeBlockAsync()
    {
        var responseContent = "```json\n{\n  \"resourceType\": \"keyvault\",\n  \"resourceGroupName\": \"rg-security\",\n  \"resourceName\": \"secrets-vault\",\n  \"location\": \"eastus\",\n  \"parameters\": { \"enableSoftDelete\": true }\n}\n```";

        _mockChatCompletionService
            .Setup(service => service.GetChatMessageContentAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMessageContent(AuthorRole.Assistant, responseContent));

        var result = await _service.ProvisionInfrastructureAsync("Provision a key vault", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResourceType.Should().Be("Microsoft.KeyVault/vaults");
        result.Properties.Should().ContainKey("enableSoftDelete");
        result.Properties!["enableSoftDelete"].Should().Be("True");
    }

    [Fact]
    public async Task ProvisionInfrastructureAsync_WhenAiReturnsInvalidJson_ReturnsFailureAsync()
    {
        _mockChatCompletionService
            .Setup(service => service.GetChatMessageContentAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMessageContent(AuthorRole.Assistant, "{ invalid json"));

        var result = await _service.ProvisionInfrastructureAsync("Create a VNet", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
        result.ErrorDetails.Should().NotBeNull();
        result.ErrorDetails!.Should().ContainEquivalentOf("AI parsing error");
    }

    [Fact]
    public async Task EstimateCostAsync_WhenAiParsesQuery_ReturnsCostEstimateAsync()
    {
        var responseContent = "{\"resourceType\":\"storage-account\",\"resourceGroupName\":\"rg-test\",\"resourceName\":\"teststorage\",\"location\":\"eastus\"}";

        _mockChatCompletionService
            .Setup(service => service.GetChatMessageContentAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMessageContent(AuthorRole.Assistant, responseContent));

        var estimate = await _service.EstimateCostAsync("Estimate cost for a storage account", CancellationToken.None);

        estimate.ResourceType.Should().Be("storage-account");
        estimate.MonthlyEstimate.Should().Be(20.00m);
        estimate.AnnualEstimate.Should().Be(240.00m);
        estimate.Notes.Should().NotBeNull();
        estimate.Notes!.Should().ContainEquivalentOf("Estimated cost");
    }

    [Fact]
    public async Task DeleteResourceGroupAsync_WhenCalled_ReturnsTrueAsync()
    {
        var result = await _service.DeleteResourceGroupAsync("rg-test", CancellationToken.None);
        result.Should().BeTrue();
    }
}

