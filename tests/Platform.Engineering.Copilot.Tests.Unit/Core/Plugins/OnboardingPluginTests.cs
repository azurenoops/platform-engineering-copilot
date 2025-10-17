using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Data.Entities;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Plugins
{
    public class OnboardingPluginTests
    {
        private static OnboardingPlugin CreatePlugin(Mock<IOnboardingService> onboardingServiceMock)
        {
            var kernel = Kernel.CreateBuilder().Build();
            var logger = Mock.Of<ILogger<OnboardingPlugin>>();
            return new OnboardingPlugin(logger, kernel, onboardingServiceMock.Object);
        }

        [Fact]
        public async Task CaptureOnboardingRequirementsAsync_CreatesDraft()
        {
            var onboardingServiceMock = new Mock<IOnboardingService>();
            onboardingServiceMock.Setup(s => s.CreateDraftRequestAsync(It.IsAny<CancellationToken>())).ReturnsAsync("req-123");
            var plugin = CreatePlugin(onboardingServiceMock);

            var response = await plugin.CaptureOnboardingRequirementsAsync("Seawolf", "COMNAVWAR", null);

            response.Should().Contain("req-123");
        }

        [Fact]
        public async Task SubmitForApprovalAsync_ValidRequest_Succeeds()
        {
            var requestId = "11111111-1111-1111-1111-111111111111";
            var onboardingServiceMock = new Mock<IOnboardingService>();
            onboardingServiceMock.Setup(s => s.GetRequestAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OnboardingRequest { Id = requestId, Status = OnboardingStatus.Draft, MissionName = "Test" });
            onboardingServiceMock.Setup(s => s.ValidateForSubmissionAsync(requestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<string>());
            onboardingServiceMock.Setup(s => s.SubmitRequestAsync(requestId, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var plugin = CreatePlugin(onboardingServiceMock);

            var response = await plugin.SubmitForApprovalAsync(requestId);

            response.Should().Contain("submitted");
        }

        [Fact]
        public async Task SubmitForApprovalAsync_InvalidRequest_ReturnsError()
        {
            var onboardingServiceMock = new Mock<IOnboardingService>();
            onboardingServiceMock.Setup(s => s.GetRequestAsync("bad-id", It.IsAny<CancellationToken>())).ReturnsAsync((OnboardingRequest?)null);
            var plugin = CreatePlugin(onboardingServiceMock);

            var response = await plugin.SubmitForApprovalAsync("bad-id");

            response.Should().Contain("not found");
        }
    }
}
