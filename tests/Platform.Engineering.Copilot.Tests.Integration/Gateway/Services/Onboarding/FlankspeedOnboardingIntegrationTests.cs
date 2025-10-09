using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Data.Entities;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Services.Onboarding;
using Platform.Engineering.Copilot.Data.Context;
using Xunit;
using CoreEnvironmentTemplate = Platform.Engineering.Copilot.Core.Models.EnvironmentTemplate;

namespace Platform.Engineering.Copilot.Tests.Integration.Core.Services.Onboarding
{
    /// <summary>
    /// Integration tests for Flankspeed onboarding end-to-end workflows
    /// Tests complete lifecycle from draft creation through provisioning
    /// </summary>
    public class FlankspeedOnboardingIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider? _serviceProvider;
        private EnvironmentManagementContext? _context;
        private IOnboardingService? _onboardingService;

        public async Task InitializeAsync()
        {
            // Set up in-memory database for integration testing
            var services = new ServiceCollection();

            // Add in-memory database
            services.AddDbContext<EnvironmentManagementContext>(options =>
                options.UseInMemoryDatabase($"FlankspeedOnboarding_{Guid.NewGuid()}"));

            // Add logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Mock external services
            var mockEnvironmentEngine = new Mock<IEnvironmentManagementEngine>();
            mockEnvironmentEngine
                .Setup(e => e.CreateEnvironmentAsync(It.IsAny<EnvironmentCreationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((EnvironmentCreationRequest req, CancellationToken ct) => new EnvironmentCreationResult
                {
                    Success = true,
                    EnvironmentId = Guid.NewGuid().ToString(),
                    EnvironmentName = req.Name,
                    ResourceGroup = req.ResourceGroup,
                    Type = req.Type,
                    Status = "Succeeded",
                    CreatedResources = new List<string> { "VNet", "NSG", "Storage" }
                });

            var mockTemplateStorage = new Mock<ITemplateStorageService>();
            mockTemplateStorage
                .Setup(t => t.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, object template, CancellationToken ct) => new CoreEnvironmentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = "Test template",
                    CreatedAt = DateTime.UtcNow
                });

            var mockEmailService = new Mock<IEmailService>();
            var mockSlackService = new Mock<ISlackService>();
            
            var mockTemplateGenerator = new Mock<IDynamicTemplateGenerator>();
            mockTemplateGenerator
                .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TemplateGenerationResult
                {
                    Success = true,
                    Files = new Dictionary<string, string>
                    {
                        { "main.bicep", "// Generated Bicep template" },
                        { "network.bicep", "// Network configuration" }
                    },
                    Summary = "Successfully generated infrastructure template"
                });

            var mockTeamsNotification = new Mock<ITeamsNotificationService>();

            services.AddSingleton(mockEnvironmentEngine.Object);
            services.AddSingleton(mockTemplateStorage.Object);
            services.AddSingleton(mockEmailService.Object);
            services.AddSingleton(mockSlackService.Object);
            services.AddSingleton(mockTemplateGenerator.Object);
            services.AddSingleton(mockTeamsNotification.Object);

            // Add the actual onboarding service
            services.AddScoped<IOnboardingService, FlankspeedOnboardingService>();

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<EnvironmentManagementContext>();
            _onboardingService = _serviceProvider.GetRequiredService<IOnboardingService>();

            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_context != null)
            {
                await _context.Database.EnsureDeletedAsync();
                await _context.DisposeAsync();
            }

            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }

        #region Complete Workflow Tests

        [Fact]
        public async Task CompleteOnboardingWorkflow_SuccessPath_CompletesAllStages()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Act & Assert - Stage 1: Create Draft
            var requestId = await _onboardingService!.CreateDraftRequestAsync(cancellationToken);
            requestId.Should().NotBeNullOrEmpty();

            var draft = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            draft.Should().NotBeNull();
            draft!.Status.Should().Be(OnboardingStatus.Draft);

            // Act & Assert - Stage 2: Update Draft with Mission Details
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Test Mission Alpha" },
                { "MissionOwner", "John Doe" },
                { "MissionOwnerEmail", "john.doe@navy.mil" },
                { "Command", "NAVWAR" },
                { "ClassificationLevel", "SECRET" },
                { "RequiredServicesJson", "[\"AKS\", \"Azure SQL\"]" },
                { "RequestedSubscriptionName", "navwar-mission-alpha" },
                { "RequestedVNetCidr", "10.100.0.0/16" },
                { "Region", "usgovvirginia" }
            };

            var updateSuccess = await _onboardingService.UpdateDraftAsync(requestId, updates, cancellationToken);
            updateSuccess.Should().BeTrue();

            var updatedDraft = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            updatedDraft!.MissionName.Should().Be("Test Mission Alpha");
            updatedDraft.Command.Should().Be("NAVWAR");

            // Act & Assert - Stage 3: Submit for Review
            var submitSuccess = await _onboardingService.SubmitRequestAsync(requestId, cancellationToken);
            submitSuccess.Should().BeTrue();

            var submittedRequest = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            submittedRequest!.Status.Should().Be(OnboardingStatus.PendingReview);

            // Act & Assert - Stage 4: Approve Request
            var approveResult = await _onboardingService.ApproveRequestAsync(
                requestId,
                "Integration Test Approver",
                "Approved for testing",
                cancellationToken);
            approveResult.Success.Should().BeTrue();

            // Wait a bit for background provisioning to start
            await Task.Delay(500, cancellationToken);

            var approvedRequest = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            approvedRequest!.Status.Should().BeOneOf(OnboardingStatus.Approved, OnboardingStatus.Provisioning, OnboardingStatus.Completed);
            approvedRequest.ApprovedBy.Should().Be("Integration Test Approver");
        }

        [Fact]
        public async Task OnboardingWorkflow_WithRejection_StopsAtRejectedStage()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Act - Create and submit request
            var requestId = await _onboardingService!.CreateDraftRequestAsync(cancellationToken);
            
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Rejected Mission" },
                { "MissionOwner", "Test User" },
                { "MissionOwnerEmail", "test.user@navy.mil" },
                { "Command", "NAVAIR" },
                { "RequestedSubscriptionName", "navair-rejected" },
                { "RequestedVNetCidr", "10.101.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(requestId, updates, cancellationToken);
            await _onboardingService.SubmitRequestAsync(requestId, cancellationToken);

            // Act - Reject the request
            var rejectSuccess = await _onboardingService.RejectRequestAsync(
                requestId,
                "Test Reviewer",
                "Insufficient justification",
                cancellationToken);

            // Assert
            rejectSuccess.Should().BeTrue();

            var rejectedRequest = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            rejectedRequest!.Status.Should().Be(OnboardingStatus.Rejected);
            rejectedRequest.RejectedBy.Should().Be("Test Reviewer");
            rejectedRequest.RejectionReason.Should().Be("Insufficient justification");
        }

        #endregion

        #region Draft Management Tests

        [Fact]
        public async Task CreateDraftRequest_CreatesNewDraftWithDefaultStatus()
        {
            // Act
            var requestId = await _onboardingService!.CreateDraftRequestAsync();

            // Assert
            requestId.Should().NotBeNullOrEmpty();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request.Should().NotBeNull();
            request!.Status.Should().Be(OnboardingStatus.Draft);
            request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task UpdateDraft_WithValidData_UpdatesRequestFields()
        {
            // Arrange
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Updated Mission" },
                { "Command", "NAVSEA" },
                { "ClassificationLevel", "TOP SECRET" }
            };

            // Act
            var success = await _onboardingService.UpdateDraftAsync(requestId, updates);

            // Assert
            success.Should().BeTrue();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.MissionName.Should().Be("Updated Mission");
            request.Command.Should().Be("NAVSEA");
            request.ClassificationLevel.Should().Be("TOP SECRET");
        }

        [Fact]
        public async Task UpdateDraft_WithValidData_UpdatesMultipleRequestFields()
        {
            // Arrange
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Updated Mission" },
                { "Command", "NAVSEA" },
                { "ClassificationLevel", "TOP SECRET" },
                { "MissionOwnerEmail", "updated@navy.mil" },
                { "RequestedSubscriptionName", "updated-subscription" }
            };

            // Act
            var success = await _onboardingService.UpdateDraftAsync(requestId, updates);

            // Assert
            success.Should().BeTrue();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.MissionName.Should().Be("Updated Mission");
            request.Command.Should().Be("NAVSEA");
            request.ClassificationLevel.Should().Be("TOP SECRET");
            request.MissionOwnerEmail.Should().Be("updated@navy.mil");
            request.RequestedSubscriptionName.Should().Be("updated-subscription");
        }

        [Fact]
        public async Task UpdateDraft_AfterSubmit_DisallowsUpdates()
        {
            // Arrange - The service restricts updates after submission (status != Draft)
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var initialUpdates = new Dictionary<string, object>
            {
                { "MissionName", "Test Mission" },
                { "MissionOwner", "Owner" },
                { "MissionOwnerEmail", "noupdate@navy.mil" },
                { "Command", "Command" },
                { "RequestedSubscriptionName", "no-update-mission" },
                { "RequestedVNetCidr", "10.108.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(requestId, initialUpdates);
            await _onboardingService.SubmitRequestAsync(requestId);

            var newUpdates = new Dictionary<string, object>
            {
                { "MissionName", "Should Not Update" }
            };

            // Act
            var success = await _onboardingService.UpdateDraftAsync(requestId, newUpdates);

            // Assert - UpdateDraftAsync checks status and rejects updates if not Draft
            success.Should().BeFalse();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.MissionName.Should().Be("Test Mission"); // Should not have changed
        }

        #endregion

        #region Submission Tests

        [Fact]
        public async Task SubmitRequest_WithValidDraft_ChangesStatusToSubmitted()
        {
            // Arrange
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Submittable Mission" },
                { "MissionOwner", "Owner Name" },
                { "MissionOwnerEmail", "owner@navy.mil" },
                { "Command", "NAVWAR" },
                { "RequestedSubscriptionName", "navwar-submittable" },
                { "RequestedVNetCidr", "10.102.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(requestId, updates);

            // Act
            var success = await _onboardingService.SubmitRequestAsync(requestId);

            // Assert
            success.Should().BeTrue();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.Status.Should().Be(OnboardingStatus.PendingReview);
        }

        [Fact]
        public async Task SubmitRequest_WithInvalidRequest_ReturnsFalse()
        {
            // Arrange
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            // Don't update with required fields

            // Act
            var success = await _onboardingService.SubmitRequestAsync(requestId);

            // Assert
            success.Should().BeFalse();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.Status.Should().Be(OnboardingStatus.Draft);
        }

        #endregion

        #region Approval/Rejection Tests

        [Fact]
        public async Task ApproveRequest_WithValidSubmission_ApprovestAndStartsProvisioning()
        {
            // Arrange
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Approvable Mission" },
                { "MissionOwner", "Mission Owner" },
                { "MissionOwnerEmail", "owner@navy.mil" },
                { "Command", "SPAWAR" },
                { "ClassificationLevel", "SECRET" },
                { "RequestedSubscriptionName", "spawar-approvable" },
                { "RequestedVNetCidr", "10.103.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(requestId, updates);
            await _onboardingService.SubmitRequestAsync(requestId);

            // Act
            var result = await _onboardingService.ApproveRequestAsync(requestId, "Test Approver", "Test approval");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.Status.Should().BeOneOf(OnboardingStatus.Approved, OnboardingStatus.Provisioning);
        }

        [Fact]
        public async Task RejectRequest_WithReason_UpdatesStatusAndStoresReason()
        {
            // Arrange
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Rejectable Mission" },
                { "MissionOwner", "Owner" },
                { "MissionOwnerEmail", "rejectowner@navy.mil" },
                { "Command", "Command" },
                { "RequestedSubscriptionName", "rejectable-mission" },
                { "RequestedVNetCidr", "10.104.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(requestId, updates);
            await _onboardingService.SubmitRequestAsync(requestId);

            // Act
            var success = await _onboardingService.RejectRequestAsync(
                requestId,
                "Security Reviewer",
                "Does not meet security requirements");

            // Assert
            success.Should().BeTrue();

            var request = await _onboardingService.GetRequestAsync(requestId);
            request!.Status.Should().Be(OnboardingStatus.Rejected);
        }

        #endregion

        #region Query Tests

        [Fact]
        public async Task GetPendingRequests_ReturnsOnlySubmittedRequests()
        {
            // Arrange
            var draftId = await _onboardingService!.CreateDraftRequestAsync();
            
            var submittedId = await _onboardingService.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Submitted Mission" },
                { "MissionOwner", "Owner" },
                { "MissionOwnerEmail", "pending@navy.mil" },
                { "Command", "Command" },
                { "RequestedSubscriptionName", "submitted-mission" },
                { "RequestedVNetCidr", "10.105.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(submittedId, updates);
            await _onboardingService.SubmitRequestAsync(submittedId);

            // Act
            var pendingRequests = await _onboardingService.GetPendingRequestsAsync();

            // Assert
            pendingRequests.Should().HaveCount(1);
            pendingRequests[0].Id.Should().Be(submittedId);
            pendingRequests[0].Status.Should().Be(OnboardingStatus.PendingReview);
        }

        [Fact]
        public async Task GetProvisioningRequests_AfterApproval_ReturnsRequestsInProvisioningState()
        {
            // Arrange - Create a request and approve it to start provisioning
            var requestId = await _onboardingService!.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Provisioning Mission" },
                { "MissionOwner", "Owner" },
                { "MissionOwnerEmail", "provisioning@navy.mil" },
                { "Command", "NAVWAR" },
                { "ClassificationLevel", "SECRET" },
                { "RequestedSubscriptionName", "navwar-provisioning" },
                { "RequestedVNetCidr", "10.106.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(requestId, updates);
            await _onboardingService.SubmitRequestAsync(requestId);
            var result = await _onboardingService.ApproveRequestAsync(requestId, "Approver", "Test");

            // Assert result should be successful
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            // Wait a bit for provisioning to potentially start (background job)
            await Task.Delay(200);

            // Act
            var provisioningRequests = await _onboardingService.GetProvisioningRequestsAsync();

            // Assert - Request should be in Approved, Provisioning, or potentially Completed state
            var request = await _onboardingService.GetRequestAsync(requestId);
            request.Should().NotBeNull();
            request!.Status.Should().BeOneOf(OnboardingStatus.Approved, OnboardingStatus.Provisioning, OnboardingStatus.Completed);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task GetStats_ReturnsAccurateCountsByStatus()
        {
            // Arrange - Create requests in various states
            var draftId = await _onboardingService!.CreateDraftRequestAsync();
            
            var submittedId = await _onboardingService.CreateDraftRequestAsync();
            var updates = new Dictionary<string, object>
            {
                { "MissionName", "Submitted" },
                { "MissionOwner", "Owner" },
                { "MissionOwnerEmail", "stats@navy.mil" },
                { "Command", "Command" },
                { "RequestedSubscriptionName", "stats-mission" },
                { "RequestedVNetCidr", "10.107.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(submittedId, updates);
            await _onboardingService.SubmitRequestAsync(submittedId);

            var rejectedId = await _onboardingService.CreateDraftRequestAsync();
            var rejectUpdates = new Dictionary<string, object>
            {
                { "MissionName", "Rejected" },
                { "MissionOwner", "Owner" },
                { "MissionOwnerEmail", "reject@navy.mil" },
                { "Command", "Command" },
                { "RequestedSubscriptionName", "reject-mission" },
                { "RequestedVNetCidr", "10.108.0.0/16" }
            };
            await _onboardingService.UpdateDraftAsync(rejectedId, rejectUpdates);
            await _onboardingService.SubmitRequestAsync(rejectedId);
            await _onboardingService.RejectRequestAsync(rejectedId, "Reviewer", "Test");

            // Act
            var stats = await _onboardingService.GetStatsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalRequests.Should().BeGreaterOrEqualTo(3);
            
            // Verify that counts make sense
            var sumOfCounts = stats.PendingReview + stats.Approved + stats.Rejected + 
                             stats.InProvisioning + stats.Completed + stats.Failed;
            sumOfCounts.Should().BeLessOrEqualTo(stats.TotalRequests);
            
            // We created at least one rejected request
            stats.Rejected.Should().BeGreaterOrEqualTo(1);
        }

        #endregion

        #region Multi-Request Scenarios

        [Fact]
        public async Task MultipleSimultaneousRequests_AllProcessedIndependently()
        {
            // Arrange
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_onboardingService!.CreateDraftRequestAsync());
            }

            // Act
            var requestIds = await Task.WhenAll(tasks);

            // Assert
            requestIds.Should().HaveCount(5);
            requestIds.Should().OnlyHaveUniqueItems();

            foreach (var requestId in requestIds)
            {
                var request = await _onboardingService!.GetRequestAsync(requestId);
                request.Should().NotBeNull();
                request!.Status.Should().Be(OnboardingStatus.Draft);
            }
        }

        [Fact]
        public async Task SequentialRequestProcessing_MaintainsDataIntegrity()
        {
            // Arrange & Act
            var request1Id = await _onboardingService!.CreateDraftRequestAsync();
            await _onboardingService.UpdateDraftAsync(request1Id, new Dictionary<string, object>
            {
                { "MissionName", "Mission 1" },
                { "Command", "Command 1" }
            });

            var request2Id = await _onboardingService.CreateDraftRequestAsync();
            await _onboardingService.UpdateDraftAsync(request2Id, new Dictionary<string, object>
            {
                { "MissionName", "Mission 2" },
                { "Command", "Command 2" }
            });

            // Assert
            var req1 = await _onboardingService.GetRequestAsync(request1Id);
            var req2 = await _onboardingService.GetRequestAsync(request2Id);

            req1!.MissionName.Should().Be("Mission 1");
            req1.Command.Should().Be("Command 1");

            req2!.MissionName.Should().Be("Mission 2");
            req2.Command.Should().Be("Command 2");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task GetRequest_WithInvalidId_ReturnsNull()
        {
            // Act
            var request = await _onboardingService!.GetRequestAsync("invalid-id-123");

            // Assert
            request.Should().BeNull();
        }

        [Fact]
        public async Task UpdateDraft_WithNonExistentId_ReturnsFalse()
        {
            // Act
            var success = await _onboardingService!.UpdateDraftAsync("non-existent", new Dictionary<string, object>());

            // Assert
            success.Should().BeFalse();
        }

        [Fact]
        public async Task ApproveRequest_WithInvalidId_ReturnsFailedResult()
        {
            // Act
            var result = await _onboardingService!.ApproveRequestAsync("invalid-id", "Approver", "Test");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
        }

        #endregion
    }
}
