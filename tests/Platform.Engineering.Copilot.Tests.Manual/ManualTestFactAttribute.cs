using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Manual;

/// <summary>
/// Custom Fact attribute for manual tests that automatically skips when the MCP server is not reachable.
/// Tests will run when the server is available, and skip gracefully when it's not.
/// </summary>
public class ManualTestFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> ServerAvailable = new(CheckServerAvailability);
    private static readonly Lazy<string> BaseUrl = new(GetBaseUrl);

    public ManualTestFactAttribute()
    {
        if (!ServerAvailable.Value)
        {
            Skip = $"MCP server not reachable at {BaseUrl.Value}. Start with: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http";
        }
    }

    private static string GetBaseUrl()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
        }
        catch
        {
            return "http://localhost:5100";
        }
    }

    private static bool CheckServerAvailability()
    {
        try
        {
            var uri = new Uri(BaseUrl.Value);
            using var client = new TcpClient();
            
            // Try to connect with a short timeout
            var result = client.BeginConnect(uri.Host, uri.Port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            
            if (success && client.Connected)
            {
                client.EndConnect(result);
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Custom Theory attribute for manual tests that automatically skips when the MCP server is not reachable.
/// </summary>
public class ManualTestTheoryAttribute : TheoryAttribute
{
    private static readonly Lazy<bool> ServerAvailable = new(CheckServerAvailability);
    private static readonly Lazy<string> BaseUrl = new(GetBaseUrl);

    public ManualTestTheoryAttribute()
    {
        if (!ServerAvailable.Value)
        {
            Skip = $"MCP server not reachable at {BaseUrl.Value}. Start with: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http";
        }
    }

    private static string GetBaseUrl()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
        }
        catch
        {
            return "http://localhost:5100";
        }
    }

    private static bool CheckServerAvailability()
    {
        try
        {
            var uri = new Uri(BaseUrl.Value);
            using var client = new TcpClient();
            
            var result = client.BeginConnect(uri.Host, uri.Port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            
            if (success && client.Connected)
            {
                client.EndConnect(result);
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}
