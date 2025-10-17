using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Xunit;
using Moq;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Verification tests for AzureResourceService refactoring.
/// Ensures the service can be instantiated correctly and maintains proper architecture.
/// </summary>
public class AzureResourceServiceArchitectureTests
{
    [Fact]
    public void AzureResourceService_Implements_IAzureResourceService()
    {
        // Arrange & Act
        var serviceType = typeof(AzureResourceService);
        var interfaceType = typeof(IAzureResourceService);
        
        // Assert
        Assert.True(interfaceType.IsAssignableFrom(serviceType), 
            "AzureResourceService should implement IAzureResourceService");
    }

    [Fact]
    public void AzureResourceService_Is_In_Correct_Namespace()
    {
        // Arrange
        var serviceType = typeof(AzureResourceService);
        
        // Act
        var namespaceName = serviceType.Namespace;
        
        // Assert
        Assert.Equal("Platform.Engineering.Copilot.Core.Services.AzureServices", namespaceName);
    }

    [Fact]
    public void AzureResourceService_Can_Be_Instantiated_With_Disabled_Gateway()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AzureResourceService>>();
        var gatewayOptions = new GatewayOptions
        {
            Azure = new AzureGatewayOptions
            {
                Enabled = false // Disabled - should not throw
            }
        };
        var options = Options.Create(gatewayOptions);
        
        // Act
        var service = new AzureResourceService(mockLogger.Object, options);
        
        // Assert
        Assert.NotNull(service);
        Assert.IsAssignableFrom<IAzureResourceService>(service);
    }

    [Fact]
    public void AzureResourceService_GetArmClient_Returns_Null_When_Disabled()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AzureResourceService>>();
        var gatewayOptions = new GatewayOptions
        {
            Azure = new AzureGatewayOptions
            {
                Enabled = false
            }
        };
        var options = Options.Create(gatewayOptions);
        var service = new AzureResourceService(mockLogger.Object, options);
        
        // Act
        var armClient = service.GetArmClient();
        
        // Assert
        Assert.Null(armClient);
    }

    [Fact]
    public void AzureResourceService_GetSubscriptionId_Returns_Provided_SubscriptionId()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AzureResourceService>>();
        var testSubscriptionId = "00000000-0000-0000-0000-000000000000";
        var gatewayOptions = new GatewayOptions
        {
            Azure = new AzureGatewayOptions
            {
                Enabled = false,
                SubscriptionId = testSubscriptionId
            }
        };
        var options = Options.Create(gatewayOptions);
        var service = new AzureResourceService(mockLogger.Object, options);
        
        // Act
        var result = service.GetSubscriptionId();
        
        // Assert
        Assert.Equal(testSubscriptionId, result);
    }

    [Fact]
    public void AzureResourceService_ValidateCidr_Validates_Correct_CIDR()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AzureResourceService>>();
        var gatewayOptions = new GatewayOptions
        {
            Azure = new AzureGatewayOptions { Enabled = false }
        };
        var options = Options.Create(gatewayOptions);
        var service = new AzureResourceService(mockLogger.Object, options);
        
        // Act & Assert
        Assert.True(service.ValidateCidr("10.0.0.0/16"));
        Assert.True(service.ValidateCidr("192.168.1.0/24"));
        Assert.True(service.ValidateCidr("172.16.0.0/12"));
        
        Assert.False(service.ValidateCidr("invalid"));
        Assert.False(service.ValidateCidr("10.0.0.0"));
        Assert.False(service.ValidateCidr("10.0.0.0/33")); // Invalid prefix
    }

    [Fact]
    public void AzureResourceService_GenerateSubnetConfigurations_Creates_Correct_Subnets()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AzureResourceService>>();
        var gatewayOptions = new GatewayOptions
        {
            Azure = new AzureGatewayOptions { Enabled = false }
        };
        var options = Options.Create(gatewayOptions);
        var service = new AzureResourceService(mockLogger.Object, options);
        
        // Act
        var subnets = service.GenerateSubnetConfigurations(
            vnetCidr: "10.100.0.0/16",
            subnetPrefix: 24,
            subnetCount: 3,
            missionName: "test-mission"
        );
        
        // Assert
        Assert.NotNull(subnets);
        Assert.Equal(3, subnets.Count);
        
        // Verify first subnet
        Assert.Contains("test-mission", subnets[0].Name);
        Assert.Contains("app", subnets[0].Name);
        Assert.StartsWith("10.100.", subnets[0].AddressPrefix);
        Assert.EndsWith("/24", subnets[0].AddressPrefix);
    }

    [Fact]
    public void AzureResourceService_Has_All_Required_Interface_Methods()
    {
        // Arrange
        var serviceType = typeof(AzureResourceService);
        var interfaceType = typeof(IAzureResourceService);
        
        // Act
        var interfaceMethods = interfaceType.GetMethods();
        var serviceMethods = serviceType.GetMethods();
        var serviceMethodNames = serviceMethods.Select(m => m.Name).ToHashSet();
        
        // Assert - verify all interface methods are implemented
        foreach (var interfaceMethod in interfaceMethods)
        {
            Assert.Contains(interfaceMethod.Name, serviceMethodNames);
        }
    }

    [Fact]
    public void No_AzureGatewayService_References_Should_Exist()
    {
        // Arrange
        var assembly = typeof(AzureResourceService).Assembly;
        
        // Act
        var types = assembly.GetTypes();
        var gatewayServiceReferences = types.Where(t => 
            t.Name.Contains("AzureGatewayService") && 
            !t.Name.Contains("Options") // Exclude configuration classes
        );
        
        // Assert
        Assert.Empty(gatewayServiceReferences);
    }

    [Fact]
    public void AzureResourceService_Constructor_Handles_Null_Options_Gracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AzureResourceService>>();
        var emptyGatewayOptions = new GatewayOptions
        {
            Azure = new AzureGatewayOptions() // Empty options
        };
        var options = Options.Create(emptyGatewayOptions);
        
        // Act
        var service = new AzureResourceService(mockLogger.Object, options);
        
        // Assert
        Assert.NotNull(service);
        // Service should handle empty options without throwing
    }
}
