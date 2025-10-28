using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services.Azure;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual;

/// <summary>
/// Manual integration tests for Azure MCP Server
/// These tests spawn the actual Azure MCP Server process and verify integration
/// 
/// Prerequisites:
/// - Node.js 18+ installed (npx available)
/// - Azure CLI authenticated (az login)
/// - Internet connection (first run downloads @azure/mcp package)
/// </summary>
public class AzureMcpIntegrationTest
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AzureMcpClient> _logger;

    public AzureMcpIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger<AzureMcpClient>(output);
    }

    [Fact(Skip = "Manual test - requires Node.js, Azure CLI, and network access")]
    public async Task Should_Initialize_Azure_MCP_Server()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = true,
            Namespaces = new[] { "storage", "keyvault" }
        };
        
        using var client = new AzureMcpClient(_logger, config);

        // Act
        await client.InitializeAsync();

        // Assert
        _output.WriteLine("✅ Azure MCP Server initialized successfully");
    }

    [Fact(Skip = "Manual test - requires Node.js, Azure CLI, and network access")]
    public async Task Should_List_Available_Azure_Tools()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = false
        };
        
        using var client = new AzureMcpClient(_logger, config);
        await client.InitializeAsync();

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        Assert.NotEmpty(tools);
        _output.WriteLine($"✅ Found {tools.Count} Azure MCP tools:");
        
        foreach (var tool in tools.Take(10))
        {
            _output.WriteLine($"   - {tool.Name}: {tool.Description}");
        }
        
        if (tools.Count > 10)
        {
            _output.WriteLine($"   ... and {tools.Count - 10} more tools");
        }

        // Verify common tools exist
        Assert.Contains(tools, t => t.Name.Contains("subscription", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, t => t.Name.Contains("resource", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Skip = "Manual test - requires Azure subscription access")]
    public async Task Should_List_Azure_Subscriptions()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = true
        };
        
        using var client = new AzureMcpClient(_logger, config);
        await client.InitializeAsync();

        // Act
        var result = await client.ListSubscriptionsAsync();

        // Assert
        Assert.True(result.Success, $"Failed: {result.ErrorMessage}");
        _output.WriteLine($"✅ Successfully called Azure MCP subscription tool");
        
        if (result.Result != null)
        {
            var resultJson = JsonSerializer.Serialize(result.Result, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine($"Result:\n{resultJson}");
        }
    }

    [Fact(Skip = "Manual test - requires Azure subscription access")]
    public async Task Should_List_Resource_Groups()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = true
        };
        
        using var client = new AzureMcpClient(_logger, config);
        await client.InitializeAsync();

        // Get subscription ID from Azure CLI
        var subscriptionId = "453c2549-4cc5-464f-ba66-acad920823e8"; // From az account show

        // Act
        var result = await client.ListResourceGroupsAsync(subscriptionId);

        // Assert
        Assert.True(result.Success, $"Failed: {result.ErrorMessage}");
        _output.WriteLine($"✅ Successfully listed resource groups");
        
        if (result.Result != null)
        {
            var resultJson = JsonSerializer.Serialize(result.Result, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine($"Result:\n{resultJson}");
        }
    }

    [Fact(Skip = "Manual test - requires Azure storage accounts")]
    public async Task Should_List_Storage_Accounts()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = true,
            Namespaces = new[] { "storage" }
        };
        
        using var client = new AzureMcpClient(_logger, config);
        await client.InitializeAsync();

        var subscriptionId = "453c2549-4cc5-464f-ba66-acad920823e8";

        // Act
        var result = await client.ListStorageAccountsAsync(subscriptionId);

        // Assert
        Assert.True(result.Success, $"Failed: {result.ErrorMessage}");
        _output.WriteLine($"✅ Successfully listed storage accounts");
        
        if (result.Result != null)
        {
            var resultJson = JsonSerializer.Serialize(result.Result, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine($"Result:\n{resultJson}");
        }
    }

    [Fact(Skip = "Manual test - comprehensive integration test")]
    public async Task Should_Perform_Full_Discovery_Workflow()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = true
        };
        
        using var client = new AzureMcpClient(_logger, config);
        
        _output.WriteLine("🔧 Starting Azure MCP integration test workflow...\n");

        // Act & Assert - Step by step workflow
        
        // 1. Initialize
        _output.WriteLine("Step 1: Initializing Azure MCP Server...");
        await client.InitializeAsync();
        _output.WriteLine("✅ Initialized\n");

        // 2. List tools
        _output.WriteLine("Step 2: Listing available tools...");
        var tools = await client.ListToolsAsync();
        _output.WriteLine($"✅ Found {tools.Count} tools\n");

        // 3. List subscriptions
        _output.WriteLine("Step 3: Listing subscriptions...");
        var subsResult = await client.ListSubscriptionsAsync();
        Assert.True(subsResult.Success);
        _output.WriteLine($"✅ Subscriptions: {(subsResult.Result != null ? "Retrieved" : "None")}\n");

        // 4. List resource groups
        _output.WriteLine("Step 4: Listing resource groups...");
        var rgResult = await client.ListResourceGroupsAsync("453c2549-4cc5-464f-ba66-acad920823e8");
        Assert.True(rgResult.Success);
        _output.WriteLine($"✅ Resource groups: {(rgResult.Result != null ? "Retrieved" : "None")}\n");

        // 5. List storage accounts
        _output.WriteLine("Step 5: Listing storage accounts...");
        var storageResult = await client.ListStorageAccountsAsync("453c2549-4cc5-464f-ba66-acad920823e8");
        Assert.True(storageResult.Success);
        _output.WriteLine($"✅ Storage accounts: {(storageResult.Result != null ? "Retrieved" : "None")}\n");

        _output.WriteLine("🎉 Full Azure MCP integration workflow completed successfully!");
    }

    [Fact(Skip = "Manual test - test with custom tool call")]
    public async Task Should_Call_Custom_Azure_Tool()
    {
        // Arrange
        var config = new AzureMcpConfiguration
        {
            ReadOnly = true,
            Debug = true
        };
        
        using var client = new AzureMcpClient(_logger, config);
        await client.InitializeAsync();

        // Act - Call a specific Azure MCP tool directly
        var result = await client.CallToolAsync(
            "azure_resource_groups_list",
            new Dictionary<string, object?>
            {
                ["subscriptionId"] = "453c2549-4cc5-464f-ba66-acad920823e8"
            });

        // Assert
        Assert.True(result.Success, $"Failed: {result.ErrorMessage}");
        _output.WriteLine($"✅ Tool executed successfully");
        
        if (result.Result != null)
        {
            var resultJson = JsonSerializer.Serialize(result.Result, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine($"Result:\n{resultJson}");
        }
    }
}

/// <summary>
/// Simple test logger that writes to xUnit output
/// </summary>
internal class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {message}");
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }
}
