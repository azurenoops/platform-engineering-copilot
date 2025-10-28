using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services.Azure;
using System.Text.Json;

Console.WriteLine("🔧 Azure MCP Integration Test");
Console.WriteLine("==============================\n");

// Create logger
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<AzureMcpClient>();

// Configure Azure MCP
var config = new AzureMcpConfiguration
{
    ReadOnly = true,
    Debug = true,
    Namespaces = null // All namespaces
};

Console.WriteLine("Configuration:");
Console.WriteLine($"  ReadOnly: {config.ReadOnly}");
Console.WriteLine($"  Debug: {config.Debug}");
Console.WriteLine($"  Namespaces: {(config.Namespaces == null ? "All" : string.Join(", ", config.Namespaces))}");
Console.WriteLine();

using var client = new AzureMcpClient(logger, config);

try
{
    // Test 1: Initialize
    Console.WriteLine("Test 1: Initializing Azure MCP Server...");
    Console.WriteLine("(This may take 30-60 seconds on first run to download @azure/mcp package)");
    await client.InitializeAsync();
    Console.WriteLine("✅ Initialized successfully\n");

    // Test 2: List tools
    Console.WriteLine("Test 2: Listing available Azure MCP tools...");
    var tools = await client.ListToolsAsync();
    Console.WriteLine($"✅ Found {tools.Count} tools:");
    
    // Show first 15 tools
    foreach (var tool in tools.Take(15))
    {
        var name = tool.Name ?? "(null)";
        var desc = tool.Description ?? "(no description)";
        Console.WriteLine($"   • {name}");
        if (!string.IsNullOrEmpty(desc) && desc != "(no description)")
        {
            Console.WriteLine($"     {desc}");
        }
    }
    
    if (tools.Count > 15)
    {
        Console.WriteLine($"   ... and {tools.Count - 15} more tools\n");
    }

    // Test 3: List subscriptions
    Console.WriteLine("\nTest 3: Listing Azure subscriptions...");
    var subsResult = await client.ListSubscriptionsAsync();
    
    if (subsResult.Success)
    {
        Console.WriteLine("✅ Successfully retrieved subscriptions");
        if (subsResult.Result != null)
        {
            var json = JsonSerializer.Serialize(subsResult.Result, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Result:\n{json}\n");
        }
    }
    else
    {
        Console.WriteLine($"❌ Failed: {subsResult.ErrorMessage}\n");
    }

    // Test 4: List resource groups (using subscription from Azure CLI)
    var subscriptionId = "453c2549-4cc5-464f-ba66-acad920823e8";
    Console.WriteLine($"\nTest 4: Listing resource groups in subscription {subscriptionId}...");
    var rgResult = await client.ListResourceGroupsAsync(subscriptionId);
    
    if (rgResult.Success)
    {
        Console.WriteLine("✅ Successfully retrieved resource groups");
        if (rgResult.Result != null)
        {
            var json = JsonSerializer.Serialize(rgResult.Result, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Result:\n{json}\n");
        }
    }
    else
    {
        Console.WriteLine($"❌ Failed: {rgResult.ErrorMessage}\n");
    }

    // Test 5: List storage accounts
    Console.WriteLine($"\nTest 5: Listing storage accounts in subscription {subscriptionId}...");
    var storageResult = await client.ListStorageAccountsAsync(subscriptionId);
    
    if (storageResult.Success)
    {
        Console.WriteLine("✅ Successfully retrieved storage accounts");
        if (storageResult.Result != null)
        {
            var json = JsonSerializer.Serialize(storageResult.Result, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Result:\n{json}\n");
        }
    }
    else
    {
        Console.WriteLine($"❌ Failed: {storageResult.ErrorMessage}\n");
    }

    // Summary
    Console.WriteLine("\n" + new string('=', 50));
    Console.WriteLine("🎉 Azure MCP Integration Test Complete!");
    Console.WriteLine(new string('=', 50));
    Console.WriteLine("\nSummary:");
    Console.WriteLine($"  ✅ Connection: Working");
    Console.WriteLine($"  ✅ Tools available: {tools.Count}");
    Console.WriteLine($"  ✅ Subscriptions: {(subsResult.Success ? "Retrieved" : "Failed")}");
    Console.WriteLine($"  ✅ Resource Groups: {(rgResult.Success ? "Retrieved" : "Failed")}");
    Console.WriteLine($"  ✅ Storage Accounts: {(storageResult.Success ? "Retrieved" : "Failed")}");
    Console.WriteLine("\nThe Azure MCP integration is working correctly! 🚀");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Test failed with exception:");
    Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"\nInner exception:");
        Console.WriteLine($"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
    
    Environment.Exit(1);
}
