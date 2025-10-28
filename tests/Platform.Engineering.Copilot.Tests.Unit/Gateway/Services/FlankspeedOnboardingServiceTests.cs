using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Services.Onboarding;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Entities;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services
{
    /// <summary>
    /// Unit tests for FlankspeedOnboardingService
    /// Tests 4-step provisioning workflow: request management, approval, template generation, deployment
    /// </summary>
    public class FlankspeedOnboardingServiceTests : IDisposable
    {
        private readonly PlatformEngineeringCopilotContext _context;
        private readonly Mock<ILogger<FlankspeedOnboardingService>> _mockLogger;
        private readonly Mock<IEnvironmentManagementEngine> _mockEnvironmentEngine;
        private readonly Mock<ITemplateStorageService> _mockTemplateStorage;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ISlackService> _mockSlackService;
        private readonly Mock<IDynamicTemplateGenerator> _mockTemplateGenerator;
        private readonly Mock<ITeamsNotificationService> _mockTeamsNotification;
        private readonly FlankspeedOnboardingService _service;

        public FlankspeedOnboardingServiceTests()
        {
            // Use in-memory database for testing
            var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new PlatformEngineeringCopilotContext(options);

            _mockLogger = new Mock<ILogger<FlankspeedOnboardingService>>();
            _mockEnvironmentEngine = new Mock<IEnvironmentManagementEngine>();
            _mockTemplateStorage = new Mock<ITemplateStorageService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockSlackService = new Mock<ISlackService>();
            _mockTemplateGenerator = new Mock<IDynamicTemplateGenerator>();
            _mockTeamsNotification = new Mock<ITeamsNotificationService>();

            _service = new FlankspeedOnboardingService(
                _context,
                _mockLogger.Object,
                _mockEnvironmentEngine.Object,
                _mockTemplateStorage.Object,
                _mockEmailService.Object,
                _mockSlackService.Object,
                _mockTemplateGenerator.Object,
                _mockTeamsNotification.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Draft Request Management Tests

    [Fact]
    public async Task CreateDraftRequestAsync_CreatesNewRequest_WithDraftStatusAsync()
        {
            // Act
            var requestId = await _service.CreateDraftRequestAsync();

            // Assert
            requestId.Should().NotBeNullOrEmpty();
            
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request.Should().NotBeNull();
            request!.Status.Should().Be(OnboardingStatus.Draft);
            request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            request.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

    [Fact]
    public async Task UpdateDraftAsync_WithDictionaryUpdates_UpdatesRequestPropertiesAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Test Mission" },
                { "MissionOwner", "John Doe" },
                { "Command", "COMNAVAIRLANT" }
            };

            // Act
            var result = await _service.UpdateDraftAsync(requestId, updates);

            // Assert
            result.Should().BeTrue();
            
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.MissionName.Should().Be("Test Mission");
            request.MissionOwner.Should().Be("John Doe");
            request.Command.Should().Be("COMNAVAIRLANT");
        }

    [Fact]
    public async Task UpdateDraftAsync_WithNonDraftStatus_ReturnsFalseAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.PendingReview;
            await _context.SaveChangesAsync();

            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Updated Mission" }
            };

            // Act
            var result = await _service.UpdateDraftAsync(requestId, updates);

            // Assert
            result.Should().BeFalse();
        }

    [Fact]
    public async Task UpdateDraftAsync_WithNonExistentRequest_ReturnsFalseAsync()
        {
            // Arrange
            var fakeRequestId = Guid.NewGuid().ToString();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Test Mission" }
            };

            // Act
            var result = await _service.UpdateDraftAsync(fakeRequestId, updates);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Request Submission Tests

        [Fact]
    public async Task SubmitRequestAsync_WithValidDraft_ChangesStatusToPendingReviewAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            
            // Set all required fields for validation
            request!.MissionName = "Test Mission";
            request.MissionOwner = "John Doe";
            request.MissionOwnerEmail = "john.doe@navy.mil";
            request.Command = "COMNAVAIRLANT";
            request.ClassificationLevel = "UNCLASSIFIED";
            request.RequestedSubscriptionName = "test-subscription";
            request.RequestedVNetCidr = "10.0.0.0/16";
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.SubmitRequestAsync(requestId);

            // Assert
            result.Should().BeTrue();
            
            var updatedRequest = await _context.OnboardingRequests.FindAsync(requestId);
            updatedRequest!.Status.Should().Be(OnboardingStatus.PendingReview);
        }

        [Fact]
    public async Task SubmitRequestAsync_WithNonDraftStatus_ReturnsFalseAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.PendingReview;
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.SubmitRequestAsync(requestId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
    public async Task SubmitRequestAsync_WithMissingRequiredFields_ReturnsFalseAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            // Don't set required fields

            // Act
            var result = await _service.SubmitRequestAsync(requestId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Request Retrieval Tests

        [Fact]
    public async Task GetRequestAsync_WithExistingId_ReturnsRequestAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();

            // Act
            var request = await _service.GetRequestAsync(requestId);

            // Assert
            request.Should().NotBeNull();
            request!.Id.Should().Be(requestId);
        }

        [Fact]
    public async Task GetRequestAsync_WithNonExistentId_ReturnsNullAsync()
        {
            // Arrange
            var fakeRequestId = Guid.NewGuid().ToString();

            // Act
            var request = await _service.GetRequestAsync(fakeRequestId);

            // Assert
            request.Should().BeNull();
        }

        [Fact]
    public async Task GetPendingRequestsAsync_ReturnsOnlyPendingAndUnderReviewAsync()
        {
            // Arrange
            var request1 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                Status = OnboardingStatus.PendingReview,
                Priority = 1,
                CreatedAt = DateTime.UtcNow
            };
            var request2 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                Status = OnboardingStatus.UnderReview,
                Priority = 2,
                CreatedAt = DateTime.UtcNow
            };
            var request3 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                Status = OnboardingStatus.Approved,
                Priority = 3,
                CreatedAt = DateTime.UtcNow
            };

            await _context.OnboardingRequests.AddRangeAsync(request1, request2, request3);
            await _context.SaveChangesAsync();

            // Act
            var pendingRequests = await _service.GetPendingRequestsAsync();

            // Assert
            pendingRequests.Should().HaveCount(2);
            pendingRequests.Should().Contain(r => r.Id == request1.Id);
            pendingRequests.Should().Contain(r => r.Id == request2.Id);
            pendingRequests.Should().NotContain(r => r.Id == request3.Id);
        }

        [Fact]
    public async Task GetPendingRequestsAsync_OrdersByPriorityThenCreatedDateAsync()
        {
            // Arrange
            var request1 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                Status = OnboardingStatus.PendingReview,
                Priority = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            var request2 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                Status = OnboardingStatus.PendingReview,
                Priority = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            await _context.OnboardingRequests.AddRangeAsync(request1, request2);
            await _context.SaveChangesAsync();

            // Act
            var pendingRequests = await _service.GetPendingRequestsAsync();

            // Assert
            pendingRequests.Should().HaveCount(2);
            pendingRequests[0].Priority.Should().Be(2); // Higher priority first
            pendingRequests[1].Priority.Should().Be(1);
        }

        [Fact]
    public async Task GetRequestsByOwnerAsync_ReturnsOnlyMatchingOwnerRequestsAsync()
        {
            // Arrange
            var email = "john.doe@navy.mil";
            var request1 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                MissionOwnerEmail = email,
                CreatedAt = DateTime.UtcNow
            };
            var request2 = new OnboardingRequest
            {
                Id = Guid.NewGuid().ToString(),
                MissionOwnerEmail = "jane.smith@navy.mil",
                CreatedAt = DateTime.UtcNow
            };

            await _context.OnboardingRequests.AddRangeAsync(request1, request2);
            await _context.SaveChangesAsync();

            // Act
            var ownerRequests = await _service.GetRequestsByOwnerAsync(email);

            // Assert
            ownerRequests.Should().HaveCount(1);
            ownerRequests[0].Id.Should().Be(request1.Id);
        }

        #endregion

        #region Cancellation Tests

        [Fact]
    public async Task CancelRequestAsync_WithValidRequest_UpdatesStatusToCancelledAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var reason = "Mission requirements changed";

            // Act
            var result = await _service.CancelRequestAsync(requestId, reason);

            // Assert
            result.Should().BeTrue();
            
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status.Should().Be(OnboardingStatus.Cancelled);
            request.RejectionReason.Should().Be(reason);
        }

        [Fact]
    public async Task CancelRequestAsync_WithTerminalState_ReturnsFalseAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.Completed;
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.CancelRequestAsync(requestId, "Test reason");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
    public async Task CancelRequestAsync_WithNonExistentRequest_ReturnsFalseAsync()
        {
            // Arrange
            var fakeRequestId = Guid.NewGuid().ToString();

            // Act
            var result = await _service.CancelRequestAsync(fakeRequestId, "Test reason");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Approval Workflow Tests

        [Fact]
    public async Task ApproveRequestAsync_WithValidRequest_UpdatesStatusAndStartsProvisioningAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.PendingReview;
            request.MissionName = "Test Mission";
            request.MissionOwner = "John Doe";
            await _context.SaveChangesAsync();

            var approvedBy = "admin@navy.mil";
            var comments = "Approved for deployment";

            // Mock Teams notification
            _mockTeamsNotification
                .Setup(t => t.SendOnboardingApprovedNotificationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApproveRequestAsync(requestId, approvedBy, comments);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.JobId.Should().NotBeNullOrEmpty();
            
            var updatedRequest = await _context.OnboardingRequests.FindAsync(requestId);
            updatedRequest!.Status.Should().Be(OnboardingStatus.Provisioning);
            updatedRequest.ApprovedBy.Should().Be(approvedBy);
            updatedRequest.ApprovalComments.Should().Be(comments);
            updatedRequest.ProvisioningJobId.Should().NotBeNullOrEmpty();
        }

        [Fact]
    public async Task ApproveRequestAsync_WithNonExistentRequest_ReturnsFailureAsync()
        {
            // Arrange
            var fakeRequestId = Guid.NewGuid().ToString();

            // Act
            var result = await _service.ApproveRequestAsync(fakeRequestId, "admin@navy.mil");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Request not found");
        }

        [Fact]
    public async Task ApproveRequestAsync_WithInvalidStatus_ReturnsFailureAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.Completed;
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ApproveRequestAsync(requestId, "admin@navy.mil");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("cannot be approved");
        }

        [Fact]
    public async Task RejectRequestAsync_WithValidRequest_UpdatesStatusToRejectedAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.PendingReview;
            await _context.SaveChangesAsync();

            var rejectedBy = "admin@navy.mil";
            var reason = "Insufficient justification";

            // Act
            var result = await _service.RejectRequestAsync(requestId, rejectedBy, reason);

            // Assert
            result.Should().BeTrue();
            
            var updatedRequest = await _context.OnboardingRequests.FindAsync(requestId);
            updatedRequest!.Status.Should().Be(OnboardingStatus.Rejected);
            updatedRequest.RejectedBy.Should().Be(rejectedBy);
            updatedRequest.RejectionReason.Should().Be(reason);
        }

        #endregion

        #region Provisioning Tests (These test the initiation, not the background execution)

        [Fact]
    public async Task ApproveRequestAsync_InitiatesProvisioningWithCorrectJobIdAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.PendingReview;
            request.MissionName = "Test Mission";
            request.MissionOwner = "John Doe";
            await _context.SaveChangesAsync();

            _mockTeamsNotification
                .Setup(t => t.SendOnboardingApprovedNotificationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApproveRequestAsync(requestId, "admin@navy.mil");

            // Assert
            result.JobId.Should().NotBeNullOrEmpty();
            
            var updatedRequest = await _context.OnboardingRequests.FindAsync(requestId);
            updatedRequest!.ProvisioningJobId.Should().Be(result.JobId);
            updatedRequest.Status.Should().Be(OnboardingStatus.Provisioning);
        }

        #endregion

        #region Validation Tests

        [Fact]
    public async Task SubmitRequestAsync_WithMissingMissionName_ReturnsFalseAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            // Set most fields but leave MissionName empty
            request!.MissionOwner = "John Doe";
            request.MissionOwnerEmail = "john.doe@navy.mil";
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.SubmitRequestAsync(requestId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
    public async Task SubmitRequestAsync_WithMissingOwnerEmail_ReturnsFalseAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.MissionName = "Test Mission";
            request.MissionOwner = "John Doe";
            // Leave MissionOwnerEmail empty
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.SubmitRequestAsync(requestId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Property Update Tests

        [Fact]
    public async Task UpdateDraftAsync_UpdatesLastUpdatedTimestampAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var initialRequest = await _context.OnboardingRequests.FindAsync(requestId);
            var initialTimestamp = initialRequest!.LastUpdatedAt;

            await Task.Delay(100); // Ensure timestamp difference

            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Updated Mission" }
            };

            // Act
            await _service.UpdateDraftAsync(requestId, updates);

            // Assert
            var updatedRequest = await _context.OnboardingRequests.FindAsync(requestId);
            updatedRequest!.LastUpdatedAt.Should().BeAfter(initialTimestamp);
        }

        [Fact]
    public async Task UpdateDraftAsync_WithCaseInsensitivePropertyName_UpdatesCorrectlyAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "missionname", "Test Mission" } // lowercase
            };

            // Act
            var result = await _service.UpdateDraftAsync(requestId, updates);

            // Assert
            result.Should().BeTrue();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.MissionName.Should().Be("Test Mission");
        }

        #endregion

        #region Notification Tests (Verify notification service is called)

        [Fact]
    public async Task ApproveRequestAsync_SendsApprovalNotificationAsync()
        {
            // Arrange
            var requestId = await _service.CreateDraftRequestAsync();
            var request = await _context.OnboardingRequests.FindAsync(requestId);
            request!.Status = OnboardingStatus.PendingReview;
            request.MissionName = "Test Mission";
            request.MissionOwner = "John Doe";
            request.Command = "COMNAVAIRLANT";
            await _context.SaveChangesAsync();

            // Act
            await _service.ApproveRequestAsync(requestId, "admin@navy.mil", "Approved");

            // Assert - Allow some time for async notification to be initiated
            await Task.Delay(100);
            
            _mockTeamsNotification.Verify(
                t => t.SendOnboardingApprovedNotificationAsync(
                    "Test Mission",
                    "John Doe",
                    "COMNAVAIRLANT",
                    requestId,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion
    }
}
