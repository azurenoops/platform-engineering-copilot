using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Services.Onboarding;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Entities;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Onboarding;

/// <summary>
/// Unit tests for FlankspeedOnboardingService helper methods
/// Tests the 13 data-driven infrastructure generation methods
/// </summary>
public class FlankspeedOnboardingServiceHelperMethodsTests : IDisposable
{
    private readonly EnvironmentManagementContext _context;
    private readonly FlankspeedOnboardingService _service;
    private readonly Mock<ILogger<FlankspeedOnboardingService>> _mockLogger;
    private readonly Mock<IEnvironmentManagementEngine> _mockEnvironmentEngine;
    private readonly Mock<ITemplateStorageService> _mockTemplateStorage;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ISlackService> _mockSlackService;
    private readonly Mock<IDynamicTemplateGenerator> _mockTemplateGenerator;
    private readonly Mock<ITeamsNotificationService> _mockTeamsNotificationService;

    public FlankspeedOnboardingServiceHelperMethodsTests()
    {
        var options = new DbContextOptionsBuilder<EnvironmentManagementContext>()
            .UseInMemoryDatabase($"FlankspeedHelperTests-{Guid.NewGuid()}")
            .Options;

        _context = new EnvironmentManagementContext(options);
        _mockLogger = new Mock<ILogger<FlankspeedOnboardingService>>();
        _mockEnvironmentEngine = new Mock<IEnvironmentManagementEngine>();
        _mockTemplateStorage = new Mock<ITemplateStorageService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockSlackService = new Mock<ISlackService>();
        _mockTemplateGenerator = new Mock<IDynamicTemplateGenerator>();
        _mockTeamsNotificationService = new Mock<ITeamsNotificationService>();

        _service = new FlankspeedOnboardingService(
            _context,
            _mockLogger.Object,
            _mockEnvironmentEngine.Object,
            _mockTemplateStorage.Object,
            _mockEmailService.Object,
            _mockSlackService.Object,
            _mockTemplateGenerator.Object,
            _mockTeamsNotificationService.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private T InvokePrivate<T>(string methodName, params object?[] parameters)
    {
        var method = typeof(FlankspeedOnboardingService)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull($"Expected private helper {methodName} to exist on FlankspeedOnboardingService");

        var result = method!.Invoke(_service, parameters);
        return result is null ? default! : (T)result;
    }

    private OnboardingRequest CreateRequest(
        string classification,
        string requiredServices,
        int? estimatedUsers = null,
        string? vnetCidr = null)
    {
        return new OnboardingRequest
        {
            Id = Guid.NewGuid().ToString(),
            MissionName = "fleet-ops",
            MissionOwner = "Test Owner",
            MissionOwnerEmail = "owner@test.mil",
            Command = "NNWC",
            RequestedSubscriptionName = "sub-test",
            ClassificationLevel = classification,
            RequiredServicesJson = requiredServices,
            RequestedVNetCidr = vnetCidr ?? "10.0.0.0/16",
            Region = "usgovvirginia",
            BusinessJustification = "Test scenario",
            EstimatedUserCount = estimatedUsers ?? 0
        };
    }

    #region DetermineComputePlatform Tests

    [Theory]
    [InlineData("We need AKS with PostgreSQL", ComputePlatform.AKS)]
    [InlineData("Azure Kubernetes Service for containers", ComputePlatform.AKS)]
    [InlineData("App Service with SQL Server", ComputePlatform.AppService)]
    [InlineData("Azure App Service hosting", ComputePlatform.AppService)]
    [InlineData("Container Apps for microservices", ComputePlatform.ContainerApps)]
    [InlineData("Network only connectivity", ComputePlatform.Network)]
    [InlineData("", ComputePlatform.AKS)]
    public void DetermineComputePlatform_WithVariousInputs_ReturnsCorrectPlatform(string services, ComputePlatform expected)
    {
        var platform = InvokePrivate<ComputePlatform>("DetermineComputePlatform", services);
        platform.Should().Be(expected);
    }

    #endregion

    #region ParseDatabaseRequirements Tests

    [Fact]
    public void ParseDatabaseRequirements_WithSqlServerAndRedis_ReturnsBothSpecifications()
    {
        var specs = InvokePrivate<List<DatabaseSpec>>("ParseDatabaseRequirements", "mission-zero", "AKS with SQL Server and Redis");

        specs.Should().NotBeNull();
        specs.Select(s => s.Type).Should().BeEquivalentTo(new[] { DatabaseType.AzureSQL, DatabaseType.Redis });
    }

    [Theory]
    [InlineData("PostgreSQL database needed", DatabaseType.PostgreSQL)]
    [InlineData("We need postgres for data storage", DatabaseType.PostgreSQL)]
    [InlineData("Redis cache required", DatabaseType.Redis)]
    [InlineData("MongoDB document store", DatabaseType.MongoDB)]
    public void ParseDatabaseRequirements_WithSingleDatabase_ReturnsExpectedSpec(string services, DatabaseType expected)
    {
        var specs = InvokePrivate<List<DatabaseSpec>>("ParseDatabaseRequirements", "mission-zero", services);

        specs.Should().ContainSingle();
        specs.Single().Type.Should().Be(expected);
    }

    #endregion

    #region EstimateReplicasFromUserCount Tests

    [Theory]
    [InlineData(null, 3)]
    [InlineData(100, 2)]
    [InlineData(500, 2)]
    [InlineData(750, 2)]
    [InlineData(1000, 3)]
    [InlineData(2500, 6)]
    [InlineData(5000, 11)] // Capped later
    public void EstimateReplicasFromUserCount_WithVariousUserCounts_ReturnsCorrectReplicas(int? userCount, int expectedReplicas)
    {
        var actualReplicas = InvokePrivate<int>("EstimateReplicasFromUserCount", userCount);
        var cappedExpected = Math.Min(expectedReplicas, 10);
        actualReplicas.Should().Be(cappedExpected);
    }

    #endregion

    #region CalculateSubnetCidr Tests

    [Theory]
    [InlineData("10.150.0.0/16", 0, "10.150.0.0/24")]
    [InlineData("10.150.0.0/16", 1, "10.150.1.0/24")]
    [InlineData("10.150.0.0/16", 2, "10.150.2.0/24")]
    [InlineData("10.150.0.0/16", 3, "10.150.3.0/24")]
    [InlineData("192.168.0.0/16", 0, "192.168.0.0/24")]
    [InlineData("192.168.0.0/16", 5, "192.168.5.0/24")]
    public void CalculateSubnetCidr_WithVariousInputs_ReturnsCorrectSubnetCidr(string vnetCidr, int subnetIndex, string expectedSubnetCidr)
    {
        var actualCidr = InvokePrivate<string>("CalculateSubnetCidr", vnetCidr, subnetIndex);
        actualCidr.Should().Be(expectedSubnetCidr);
    }

    #endregion

    #region BuildSubnetsFromRequest Tests

    [Fact]
    public void BuildSubnetsFromRequest_WithUnclassAndNoDatabase_Creates1Subnet()
    {
        var request = CreateRequest("UNCLASS", "Network only");
        var subnets = InvokePrivate<List<SubnetConfiguration>>("BuildSubnetsFromRequest", request);

        subnets.Should().HaveCount(1);
        subnets.Single().Purpose.Should().Be(SubnetPurpose.Application);
    }

    [Fact]
    public void BuildSubnetsFromRequest_WithSecretAndPostgreSQL_Creates4Subnets()
    {
        var request = CreateRequest("SECRET", "AKS and Kubernetes with PostgreSQL", vnetCidr: "10.150.0.0/16");
        var subnets = InvokePrivate<List<SubnetConfiguration>>("BuildSubnetsFromRequest", request);

        subnets.Should().HaveCount(4);
        subnets.Select(s => s.Purpose).Should().BeEquivalentTo(new[]
        {
            SubnetPurpose.Application,
            SubnetPurpose.PrivateEndpoints,
            SubnetPurpose.Database,
            SubnetPurpose.ApplicationGateway
        });
    }

    [Fact]
    public void BuildSubnetsFromRequest_WithSecretNoDatabase_Creates3Subnets()
    {
        var request = CreateRequest("SECRET", "AKS and Kubernetes", vnetCidr: "10.0.0.0/16");
        var subnets = InvokePrivate<List<SubnetConfiguration>>("BuildSubnetsFromRequest", request);

        subnets.Should().HaveCount(3);
        subnets.Select(s => s.Purpose).Should().BeEquivalentTo(new[]
        {
            SubnetPurpose.Application,
            SubnetPurpose.PrivateEndpoints,
            SubnetPurpose.ApplicationGateway
        });
    }

    [Fact]
    public void BuildSubnetsFromRequest_WithUnclassAndPostgreSQL_Creates2Subnets()
    {
        var request = CreateRequest("UNCLASS", "PostgreSQL database requested", vnetCidr: "10.0.0.0/16");
        var subnets = InvokePrivate<List<SubnetConfiguration>>("BuildSubnetsFromRequest", request);

        subnets.Should().HaveCount(2);
        subnets.Select(s => s.Purpose).Should().BeEquivalentTo(new[]
        {
            SubnetPurpose.Application,
            SubnetPurpose.Database
        });
    }

    #endregion

    #region ShouldEnablePrivateCluster Tests

    [Theory]
    [InlineData("AKS and Kubernetes", "UNCLASS", false)]
    [InlineData("AKS and Kubernetes", "SECRET", true)]
    [InlineData("AKS and Kubernetes", "TOP SECRET", true)]
    [InlineData("AKS and Kubernetes", "IL5", true)]
    [InlineData("AKS and Kubernetes", "IL6", true)]
    [InlineData("Azure App Service", "SECRET", false)]
    [InlineData("Azure Container Apps", "SECRET", false)]
    [InlineData("Network only", "SECRET", false)]
    public void ShouldEnablePrivateCluster_WithVariousInputs_ReturnsCorrectValue(string services, string classification, bool expected)
    {
        var request = CreateRequest(classification, services);
        var actual = InvokePrivate<bool>("ShouldEnablePrivateCluster", request);
        actual.Should().Be(expected);
    }

    #endregion

    #region DetermineAuthorizedIPRanges Tests

    [Theory]
    [InlineData("AKS and Kubernetes", "UNCLASS", null)]
    [InlineData("AKS and Kubernetes", "SECRET", null)]
    [InlineData("AKS and Kubernetes", "TOP SECRET", null)]
    [InlineData("Azure App Service", "UNCLASS", null)]
    public void DetermineAuthorizedIPRanges_WithVariousInputs_ReturnsCorrectValue(string services, string classification, string? expected)
    {
        var request = CreateRequest(classification, services);
        var actual = InvokePrivate<string?>("DetermineAuthorizedIPRanges", request);
        actual.Should().Be(expected);
    }

    #endregion

    #region ShouldEnableWorkloadIdentity Tests

    [Theory]
    [InlineData("AKS and Kubernetes", true)]
    [InlineData("Azure App Service", true)]
    [InlineData("Azure Container Apps", false)]
    [InlineData("Network only", false)]
    public void ShouldEnableWorkloadIdentity_WithVariousPlatforms_ReturnsCorrectValue(string services, bool expected)
    {
        var request = CreateRequest("UNCLASS", services);
        var actual = InvokePrivate<bool>("ShouldEnableWorkloadIdentity", request);
        actual.Should().Be(expected);
    }

    #endregion

    #region ShouldEnableImageCleaner Tests

    [Theory]
    [InlineData("AKS and Kubernetes", true)]
    [InlineData("Azure App Service", false)]
    [InlineData("Azure Container Apps", false)]
    [InlineData("Network only", false)]
    public void ShouldEnableImageCleaner_WithVariousPlatforms_ReturnsCorrectValue(string services, bool expected)
    {
        var request = CreateRequest("SECRET", services);
        var actual = InvokePrivate<bool>("ShouldEnableImageCleaner", request);
        actual.Should().Be(expected);
    }

    #endregion

    #region DetermineTlsVersion Tests

    [Theory]
    [InlineData("UNCLASS", "1.2")]
    [InlineData("SECRET", "1.3")]
    [InlineData("TOP SECRET", "1.3")]
    [InlineData("IL5", "1.2")]
    [InlineData("IL6", "1.2")]
    public void DetermineTlsVersion_WithVariousClassifications_ReturnsCorrectVersion(string classification, string expectedVersion)
    {
        var request = CreateRequest(classification, "AKS and Kubernetes");
        var actual = InvokePrivate<string>("DetermineTlsVersion", request);
        actual.Should().Be(expectedVersion);
    }

    #endregion

    #region ShouldEnableDefender Tests

    [Theory]
    [InlineData("UNCLASS", true)]      // Defender recommended for all
    [InlineData("SECRET", true)]
    [InlineData("TOP SECRET", true)]
    [InlineData("IL5", true)]
    [InlineData("IL6", true)]
    public void ShouldEnableDefender_WithVariousClassifications_AlwaysReturnsTrue(string classification, bool expected)
    {
        var request = CreateRequest(classification, "AKS and Kubernetes");
        var actual = InvokePrivate<bool>("ShouldEnableDefender", request);
        actual.Should().Be(expected);
    }

    #endregion

    #region DetermineNetworkPolicy Tests

    [Theory]
    [InlineData("AKS and Kubernetes", "UNCLASS", "azure")]
    [InlineData("AKS and Kubernetes", "SECRET", "azure")]
    [InlineData("AKS and Kubernetes", "TOP SECRET", "azure")]
    [InlineData("AKS and Kubernetes", "IL5", "azure")]
    [InlineData("Azure App Service", "SECRET", null)]
    [InlineData("Azure Container Apps", "SECRET", null)]
    public void DetermineNetworkPolicy_WithVariousInputs_ReturnsCorrectPolicy(string services, string classification, string? expectedPolicy)
    {
        var request = CreateRequest(classification, services);
        var actual = InvokePrivate<string?>("DetermineNetworkPolicy", request);
        actual.Should().Be(expectedPolicy);
    }

    #endregion

    #region Full Integration - BuildTemplateRequestFromOnboarding Tests

    [Fact]
    public void BuildTemplateRequestFromOnboarding_WithSecretAksPostgreSQL_CreatesCompleteRequest()
    {
        var request = CreateRequest("SECRET", "AKS and Kubernetes with PostgreSQL", 1000, "10.150.0.0/16");
        request.MissionName = "mission-zero";

        var template = InvokePrivate<TemplateGenerationRequest>("BuildTemplateRequestFromOnboarding", request);

    template.Infrastructure.ComputePlatform.Should().Be(ComputePlatform.AKS);
    template.Deployment.Replicas.Should().Be(3);
    template.Infrastructure.NetworkConfig.Should().NotBeNull();
    template.Infrastructure.NetworkConfig!.Subnets.Should().HaveCount(4);
    template.Infrastructure.EnablePrivateCluster.Should().BeTrue();
        template.Databases.Should().ContainSingle(db => db.Type == DatabaseType.PostgreSQL);
    }

    [Fact]
    public void BuildTemplateRequestFromOnboarding_WithUnclassAppService_CreatesBasicRequest()
    {
        var request = CreateRequest("UNCLASS", "Azure App Service with SQL Server", 200, "10.20.0.0/16");
        request.MissionName = "mission-bravo";

        var template = InvokePrivate<TemplateGenerationRequest>("BuildTemplateRequestFromOnboarding", request);

    template.Infrastructure.ComputePlatform.Should().Be(ComputePlatform.AppService);
    template.Databases.Should().ContainSingle(db => db.Type == DatabaseType.AzureSQL);
    template.Infrastructure.NetworkConfig.Should().NotBeNull();
    template.Infrastructure.NetworkConfig!.Subnets.Should().HaveCount(2);
    template.Infrastructure.EnablePrivateCluster.Should().BeFalse();
        template.Deployment.Replicas.Should().Be(2);
    }

    #endregion
}
