using Azure.Core;
using Azure.Identity;
using Moq;

namespace Platform.Engineering.Copilot.Tests.Integration;

/// <summary>
/// Factory for creating mock Azure SDK clients for testing.
/// Provides mock TokenCredential and other Azure SDK dependencies.
/// </summary>
public static class MockAzureClientFactory
{
    /// <summary>
    /// Create a mock TokenCredential that returns a fixed access token.
    /// </summary>
    public static Mock<TokenCredential> CreateMockCredential(
        string accessToken = "mock-access-token",
        DateTimeOffset? expiresOn = null)
    {
        var mock = new Mock<TokenCredential>();
        var token = new AccessToken(accessToken, expiresOn ?? DateTimeOffset.UtcNow.AddHours(1));

        mock.Setup(c => c.GetTokenAsync(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        mock.Setup(c => c.GetToken(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(token);

        return mock;
    }

    /// <summary>
    /// Create a mock TokenCredential that throws (simulating auth failure).
    /// </summary>
    public static Mock<TokenCredential> CreateFailingCredential()
    {
        var mock = new Mock<TokenCredential>();

        mock.Setup(c => c.GetTokenAsync(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AuthenticationFailedException("Mock authentication failure"));

        return mock;
    }
}
