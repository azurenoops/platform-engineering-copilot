using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Services.Azure;

/// <summary>
/// Client for Microsoft's official Azure MCP Server
/// Provides 50+ tools for Azure services: Storage, KeyVault, Cosmos, SQL, AKS, Functions, etc.
/// Documentation: https://learn.microsoft.com/en-us/azure/developer/azure-mcp-server/
/// 
/// Note: This is a simplified implementation that spawns the Azure MCP Server process
/// and communicates via stdio (JSONRPC 2.0). The ModelContextProtocol package is currently
/// in preview and the API is subject to change, so we use a direct process-based approach.
/// </summary>
public class AzureMcpClient : IDisposable
{
    private readonly ILogger<AzureMcpClient> _logger;
    private readonly AzureMcpConfiguration _configuration;
    private Process? _mcpProcess;
    private StreamWriter? _processInput;
    private StreamReader? _processOutput;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private int _requestId = 0;

    public AzureMcpClient(ILogger<AzureMcpClient> logger, AzureMcpConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Initialize connection to Azure MCP Server (spawns npx process)
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            _logger.LogInformation("🔌 Initializing Azure MCP Server connection...");
            _logger.LogInformation("   Command: npx -y @azure/mcp@latest server start");
            _logger.LogInformation("   Read-only: {ReadOnly}, Namespaces: {Namespaces}", 
                _configuration.ReadOnly, 
                string.Join(", ", _configuration.Namespaces ?? new[] { "all" }));

            var arguments = new List<string> { "-y", "@azure/mcp@latest", "server", "start" };

            // Add configuration options
            if (_configuration.ReadOnly)
            {
                arguments.Add("--read-only");
            }

            if (_configuration.Namespaces?.Any() == true)
            {
                arguments.Add("--namespace");
                arguments.Add(string.Join(",", _configuration.Namespaces));
            }

            if (_configuration.Debug)
            {
                arguments.Add("--debug");
            }

            // Spawn the Azure MCP Server process
            _mcpProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npx",
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _mcpProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogDebug("Azure MCP Server stderr: {Message}", e.Data);
                }
            };

            _mcpProcess.Start();
            _mcpProcess.BeginErrorReadLine();

            _processInput = _mcpProcess.StandardInput;
            _processOutput = _mcpProcess.StandardOutput;

            // Send initialize request
            await SendRequestAsync("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "Platform Engineering Copilot",
                    version = "1.0.0"
                }
            }, cancellationToken);

            _isInitialized = true;
            _logger.LogInformation("✅ Azure MCP Server connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Azure MCP Server");
            throw new InvalidOperationException("Failed to initialize Azure MCP Server. Ensure Node.js (npx) is installed.", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Get all available tools from Azure MCP Server
    /// Returns 50+ tools for Azure services
    /// </summary>
    public async Task<List<AzureMcpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        _logger.LogInformation("📋 Listing Azure MCP tools...");
        var response = await SendRequestAsync("tools/list", new { }, cancellationToken);
        
        _logger.LogDebug("Raw tools response: {Response}", response.ToString());
        
        var toolsArray = response.GetProperty("tools");
        var tools = JsonSerializer.Deserialize<List<AzureMcpTool>>(toolsArray.GetRawText()) ?? new List<AzureMcpTool>();
        
        _logger.LogInformation("   Found {Count} Azure tools", tools.Count);
        return tools;
    }

    /// <summary>
    /// Call an Azure MCP tool
    /// </summary>
    /// <param name="toolName">Tool name (e.g., "azure_storage", "azure_keyvault", "azure_cosmos")</param>
    /// <param name="arguments">Tool arguments (subscription, resourceGroup, etc.)</param>
    public async Task<AzureMcpToolResult> CallToolAsync(
        string toolName, 
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        _logger.LogInformation("🔧 Calling Azure MCP tool: {Tool}", toolName);
        _logger.LogDebug("   Arguments: {Args}", JsonSerializer.Serialize(arguments));

        try
        {
            var response = await SendRequestAsync("tools/call", new
            {
                name = toolName,
                arguments
            }, cancellationToken);
            
            return new AzureMcpToolResult
            {
                Success = true,
                ToolName = toolName,
                Result = response,
                ExecutedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Azure MCP tool call failed: {Tool}", toolName);
            
            return new AzureMcpToolResult
            {
                Success = false,
                ToolName = toolName,
                ErrorMessage = ex.Message,
                ExecutedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// List all storage accounts in a subscription
    /// </summary>
    public async Task<AzureMcpToolResult> ListStorageAccountsAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            args["subscription"] = subscriptionId;
        }

        return await CallToolAsync("azure_storage", args, cancellationToken);
    }

    /// <summary>
    /// List KeyVault secrets (requires user confirmation/elicitation)
    /// </summary>
    public async Task<AzureMcpToolResult> ListKeyVaultSecretsAsync(
        string vaultName,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["vaultName"] = vaultName
        };
        
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            args["subscription"] = subscriptionId;
        }

        return await CallToolAsync("azure_keyvault", args, cancellationToken);
    }

    /// <summary>
    /// List Cosmos DB databases
    /// </summary>
    public async Task<AzureMcpToolResult> ListCosmosDbDatabasesAsync(
        string accountName,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["accountName"] = accountName
        };
        
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            args["subscription"] = subscriptionId;
        }

        return await CallToolAsync("azure_cosmos", args, cancellationToken);
    }

    /// <summary>
    /// List Azure Kubernetes Service clusters
    /// </summary>
    public async Task<AzureMcpToolResult> ListAksClus​tersAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            args["subscription"] = subscriptionId;
        }

        return await CallToolAsync("azure_kubernetes", args, cancellationToken);
    }

    /// <summary>
    /// List resource groups
    /// </summary>
    public async Task<AzureMcpToolResult> ListResourceGroupsAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            args["subscription"] = subscriptionId;
        }

        return await CallToolAsync("resource_groups", args, cancellationToken);
    }

    /// <summary>
    /// List Azure subscriptions
    /// </summary>
    public async Task<AzureMcpToolResult> ListSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await CallToolAsync("subscription", new Dictionary<string, object?>(), cancellationToken);
    }

    /// <summary>
    /// Comprehensive resource discovery across Azure services using Azure MCP Server
    /// Discovers subscriptions, resource groups, storage accounts, and AKS clusters
    /// </summary>
    public async Task<string> DiscoverAllResourcesAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Using Azure MCP Server for comprehensive resource discovery...");

        try
        {
            await EnsureInitializedAsync(cancellationToken);

            var discoveryResults = new List<string>();

            // 1. Discover subscriptions
            _logger.LogInformation("   📋 Discovering subscriptions...");
            var subsResult = await ListSubscriptionsAsync(cancellationToken);
            if (subsResult.Success)
            {
                discoveryResults.Add($"✅ Subscriptions: {subsResult.Result}");
            }

            // 2. Discover resource groups
            _logger.LogInformation("   📦 Discovering resource groups...");
            var rgResult = await ListResourceGroupsAsync(subscriptionId, cancellationToken);
            if (rgResult.Success)
            {
                discoveryResults.Add($"✅ Resource Groups: {rgResult.Result}");
            }

            // 3. Discover storage accounts
            _logger.LogInformation("   💾 Discovering storage accounts...");
            var storageResult = await ListStorageAccountsAsync(subscriptionId, cancellationToken);
            if (storageResult.Success)
            {
                discoveryResults.Add($"✅ Storage Accounts: {storageResult.Result}");
            }

            // 4. Discover AKS clusters
            _logger.LogInformation("   ☸️  Discovering AKS clusters...");
            var aksResult = await ListAksClustersAsync(subscriptionId, cancellationToken);
            if (aksResult.Success)
            {
                discoveryResults.Add($"✅ AKS Clusters: {aksResult.Result}");
            }

            var summary = string.Join("\n\n", discoveryResults);
            _logger.LogInformation("✅ Azure MCP discovery completed: {Count} resource types discovered", discoveryResults.Count);
            
            return $"# Azure Resource Discovery (via Azure MCP Server)\n\n{summary}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Azure MCP discovery failed");
            return $"⚠️ Azure MCP discovery encountered an error: {ex.Message}";
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private async Task<JsonElement> SendRequestAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var request = new
            {
                jsonrpc = "2.0",
                id = requestId,
                method,
                @params = parameters
            };

            var requestJson = JsonSerializer.Serialize(request);
            _logger.LogDebug("Sending request: {Request}", requestJson);
            
            await _processInput!.WriteLineAsync(requestJson);
            await _processInput.FlushAsync();

            // Read response
            var responseJson = await _processOutput!.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
            {
                throw new InvalidOperationException("Azure MCP Server returned empty response");
            }

            _logger.LogDebug("Received response: {Response}", responseJson);

            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            if (response.TryGetProperty("error", out var error))
            {
                var errorMessage = error.GetProperty("message").GetString();
                throw new InvalidOperationException($"Azure MCP Server error: {errorMessage}");
            }

            return response.GetProperty("result");
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public void Dispose()
    {
        if (_mcpProcess != null && !_mcpProcess.HasExited)
        {
            _mcpProcess.Kill();
            _mcpProcess.WaitForExit(5000);
        }
        _mcpProcess?.Dispose();
        _processInput?.Dispose();
        _processOutput?.Dispose();
    }
}

/// <summary>
/// Configuration for Azure MCP Server
/// </summary>
public class AzureMcpConfiguration
{
    /// <summary>
    /// Enable read-only mode (no write operations)
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// Azure service namespaces to expose (e.g., "storage", "keyvault", "cosmos")
    /// Leave null for all namespaces
    /// </summary>
    public string[]? Namespaces { get; set; }

    /// <summary>
    /// Enable debug logging
    /// </summary>
    public bool Debug { get; set; } = false;

    /// <summary>
    /// Disable user confirmation for sensitive operations (use with caution)
    /// </summary>
    public bool DisableUserConfirmation { get; set; } = false;
}

/// <summary>
/// Azure MCP tool information
/// </summary>
public class AzureMcpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

/// <summary>
/// Result from Azure MCP tool call
/// </summary>
public class AzureMcpToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
}
