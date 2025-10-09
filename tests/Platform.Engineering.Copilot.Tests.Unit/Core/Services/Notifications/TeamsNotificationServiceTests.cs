using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Notifications;

/// <summary>
/// Unit tests for TeamsNotificationService
/// Tests webhook formatting, adaptive card generation, and error handling
/// </summary>
public class TeamsNotificationServiceTests
{
    private readonly Mock<ILogger<TeamsNotificationService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public TeamsNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<TeamsNotificationService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    private TeamsNotificationService CreateService(string? webhookUrl = "https://test.webhook.office.com")
    {
        _mockConfiguration.Setup(c => c["Teams:WebhookUrl"]).Returns(webhookUrl);

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return new TeamsNotificationService(
            _mockLogger.Object,
            _mockHttpClientFactory.Object,
            _mockConfiguration.Object);
    }

    #region Configuration Tests

    [Fact]
    public void Constructor_WithNoWebhookUrl_LogsWarning()
    {
        // Arrange & Act
        var service = CreateService(webhookUrl: null);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Teams notifications are disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithEmptyWebhookUrl_LogsWarning()
    {
        // Arrange & Act
        var service = CreateService(webhookUrl: "");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Teams notifications are disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendOnboardingApprovedNotificationAsync Tests

    [Fact]
    public async Task SendOnboardingApprovedNotificationAsync_WithValidInput_SendsWebhookRequest()
    {
        // Arrange
        var service = CreateService();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await service.SendOnboardingApprovedNotificationAsync(
            "AEGIS Integration",
            "CDR Sarah Chen",
            "SPAWAR",
            "ONB-001",
            CancellationToken.None);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                (req.RequestUri!.ToString() == "https://test.webhook.office.com" ||
                 req.RequestUri!.ToString() == "https://test.webhook.office.com/")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendOnboardingApprovedNotificationAsync_WithDisabledWebhook_DoesNotSendRequest()
    {
        // Arrange
        var service = CreateService(webhookUrl: null);

        // Act
        await service.SendOnboardingApprovedNotificationAsync(
            "AEGIS Integration",
            "CDR Sarah Chen",
            "SPAWAR",
            "ONB-001",
            CancellationToken.None);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region SendTemplateGenerationCompletedNotificationAsync Tests

    [Fact]
    public async Task SendTemplateGenerationCompletedNotificationAsync_WithValidInput_IncludesFileCount()
    {
        // Arrange
        var service = CreateService();
        HttpContent? capturedContent = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedContent = req.Content)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await service.SendTemplateGenerationCompletedNotificationAsync(
            "AEGIS Integration",
            "ONB-001",
            filesGenerated: 37,
            "Generated 37 files with 9 components",
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedContent);
        var content = await capturedContent!.ReadAsStringAsync();
        Assert.Contains("37", content);  // File count should be in the message
        Assert.Contains("AEGIS Integration", content);
    }

    #endregion

    #region SendDeploymentCompletedNotificationAsync Tests

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "Deployment failed: Resource group creation failed")]
    public async Task SendDeploymentCompletedNotificationAsync_WithSuccessOrFailure_SendsCorrectMessage(
        bool success,
        string? errorMessage)
    {
        // Arrange
        var service = CreateService();
        HttpContent? capturedContent = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedContent = req.Content)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await service.SendDeploymentCompletedNotificationAsync(
            "AEGIS Integration",
            "ONB-001",
            "Production",
            "aegis-integration-rg",
            "sub-12345",
            success,
            errorMessage,
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedContent);
        var content = await capturedContent!.ReadAsStringAsync();
        
        if (success)
        {
            Assert.Contains("Completed Successfully", content);
        }
        else
        {
            Assert.Contains("Failed", content);
            Assert.Contains(errorMessage, content);
        }
    }

    #endregion

    #region SendDeploymentFailedNotificationAsync Tests

    [Fact]
    public async Task SendDeploymentFailedNotificationAsync_WithError_IncludesErrorDetails()
    {
        // Arrange
        var service = CreateService();
        HttpContent? capturedContent = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedContent = req.Content)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await service.SendDeploymentFailedNotificationAsync(
            "AEGIS Integration",
            "ONB-001",
            "Template Generation",
            "NullReferenceException: Object reference not set",
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedContent);
        var content = await capturedContent!.ReadAsStringAsync();
        Assert.Contains("Template Generation", content);
        Assert.Contains("NullReferenceException", content);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SendNotification_WithHttpError_LogsWarning()
    {
        // Arrange
        var service = CreateService();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage 
            { 
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid webhook URL")
            });

        // Act
        await service.SendOnboardingApprovedNotificationAsync(
            "AEGIS Integration",
            "CDR Sarah Chen",
            "SPAWAR",
            "ONB-001",
            CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Teams notification failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotification_WithException_LogsError()
    {
        // Arrange
        var service = CreateService();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await service.SendOnboardingApprovedNotificationAsync(
            "AEGIS Integration",
            "CDR Sarah Chen",
            "SPAWAR",
            "ONB-001",
            CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to send Teams notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Adaptive Card Format Tests

    [Fact]
    public async Task SendCustomNotificationAsync_WithFacts_IncludesFactSetInAdaptiveCard()
    {
        // Arrange
        var service = CreateService();
        HttpContent? capturedContent = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedContent = req.Content)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var facts = new Dictionary<string, string>
        {
            ["Test Key 1"] = "Test Value 1",
            ["Test Key 2"] = "Test Value 2"
        };

        // Act
        await service.SendCustomNotificationAsync(
            "Test Title",
            "Test Message",
            "good",
            facts,
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedContent);
        var content = await capturedContent!.ReadAsStringAsync();
        Assert.Contains("Test Key 1", content);
        Assert.Contains("Test Value 1", content);
        Assert.Contains("Test Key 2", content);
        Assert.Contains("Test Value 2", content);
        Assert.Contains("FactSet", content);  // Adaptive Card FactSet type
    }

    [Fact]
    public async Task SendNotification_Always_IncludesTimestamp()
    {
        // Arrange
        var service = CreateService();
        HttpContent? capturedContent = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedContent = req.Content)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await service.SendCustomNotificationAsync(
            "Test",
            "Test Message",
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(capturedContent);
        var content = await capturedContent!.ReadAsStringAsync();
        Assert.Contains("UTC", content);  // Timestamp should include UTC
        Assert.Matches(@"\d{4}-\d{2}-\d{2}", content);  // Should have date format
    }

    #endregion
}
