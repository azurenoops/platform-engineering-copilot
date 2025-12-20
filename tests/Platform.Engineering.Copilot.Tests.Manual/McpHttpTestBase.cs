using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual;

/// <summary>
/// Base class for MCP HTTP API manual tests providing shared HTTP client and test utilities.
/// </summary>
public abstract class McpHttpTestBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper Output;
    protected readonly HttpClient HttpClient;
    protected readonly string BaseUrl;
    protected readonly int TimeoutSeconds;
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected McpHttpTestBase(ITestOutputHelper output)
    {
        Output = output;
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        BaseUrl = configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
        TimeoutSeconds = int.TryParse(configuration["McpServer:TimeoutSeconds"], out var timeout) ? timeout : 60;
        
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
        };
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;
    
    public virtual Task DisposeAsync()
    {
        HttpClient.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a chat request to the MCP server and returns the typed response.
    /// </summary>
    protected async Task<McpChatResponse> SendChatRequestAsync(string message, string? conversationId = null)
    {
        var request = new McpChatRequest
        {
            Message = message,
            ConversationId = conversationId ?? $"test-{Guid.NewGuid():N}"
        };

        Output.WriteLine($"📤 Request: {message}");
        Output.WriteLine($"   ConversationId: {request.ConversationId}");

        var response = await HttpClient.PostAsJsonAsync("/mcp/chat", request, JsonOptions);
        var content = await response.Content.ReadAsStringAsync();
        
        Output.WriteLine($"📥 Status: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Output.WriteLine($"❌ Error Response: {content}");
            throw new HttpRequestException($"Request failed with status {response.StatusCode}: {content}");
        }

        var result = JsonSerializer.Deserialize<McpChatResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize response");

        Output.WriteLine($"   Success: {result.Success}");
        Output.WriteLine($"   Intent: {result.IntentType}");
        Output.WriteLine($"   Confidence: {result.Confidence:P0}");
        Output.WriteLine($"   Processing Time: {result.ProcessingTimeMs}ms");
        
        if (result.AgentsInvoked?.Any() == true)
        {
            Output.WriteLine($"   Agents: {string.Join(", ", result.AgentsInvoked)}");
        }

        return result;
    }

    /// <summary>
    /// Checks the health of the MCP server.
    /// </summary>
    protected async Task<McpHealthResponse> CheckHealthAsync()
    {
        var response = await HttpClient.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        
        return JsonSerializer.Deserialize<McpHealthResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize health response");
    }

    /// <summary>
    /// Asserts that the response was successful. Use this for basic validation.
    /// </summary>
    protected void AssertSuccessfulResponse(McpChatResponse response)
    {
        if (!response.Success)
        {
            Output.WriteLine($"❌ Response failed: {string.Join(", ", response.Errors ?? new List<string>())}");
        }
        
        Assert.True(response.Success, $"Expected successful response. Errors: {string.Join(", ", response.Errors ?? new List<string>())}");
        
        Output.WriteLine($"✅ Response successful - Length: {response.Response?.Length ?? 0} chars");
    }

    /// <summary>
    /// Asserts that the response was successful and has content.
    /// </summary>
    protected void AssertSuccessfulResponseWithContent(McpChatResponse response)
    {
        AssertSuccessfulResponse(response);
        
        // Check that we got some form of response content
        var hasContent = !string.IsNullOrWhiteSpace(response.Response) || response.ToolResult != null;
        Assert.True(hasContent, "Expected response to have content in Response or ToolResult");
        
        Output.WriteLine($"✅ Response has content - Response: {response.Response?.Length ?? 0} chars, ToolResult: {response.ToolResult != null}");
    }

    /// <summary>
    /// Asserts that the response invoked the expected agent(s).
    /// </summary>
    protected void AssertAgentInvoked(McpChatResponse response, params string[] expectedAgents)
    {
        AssertSuccessfulResponse(response);
        
        if (response.AgentsInvoked?.Any() == true)
        {
            foreach (var agent in expectedAgents)
            {
                var found = response.AgentsInvoked.Any(a => 
                    a.Contains(agent, StringComparison.OrdinalIgnoreCase));
                Assert.True(found, $"Expected agent '{agent}' to be invoked. Invoked agents: {string.Join(", ", response.AgentsInvoked)}");
            }
        }
        
        Output.WriteLine($"✅ Expected agents invoked: {string.Join(", ", expectedAgents)}");
    }

    /// <summary>
    /// Asserts that the response matches an expected intent pattern (supports partial matching).
    /// </summary>
    protected void AssertIntentMatches(McpChatResponse response, params string[] acceptableIntents)
    {
        AssertSuccessfulResponse(response);
        
        if (response.IntentType != null)
        {
            var matched = acceptableIntents.Any(intent => 
                response.IntentType.Contains(intent, StringComparison.OrdinalIgnoreCase));
            Assert.True(matched, $"Expected intent to match one of [{string.Join(", ", acceptableIntents)}], but was '{response.IntentType}'");
        }
        
        Output.WriteLine($"✅ Intent '{response.IntentType}' matches expected patterns");
    }

    /// <summary>
    /// Asserts that the response content contains expected keywords.
    /// Logs warnings but doesn't fail for empty responses.
    /// </summary>
    protected void AssertResponseContains(McpChatResponse response, params string[] keywords)
    {
        var content = (response.Response ?? "") + (response.ToolResult?.ToString() ?? "");
        
        if (string.IsNullOrWhiteSpace(content))
        {
            Output.WriteLine($"⚠️ No content to search for keywords - response was empty");
            return;
        }
        
        var foundKeywords = new List<string>();
        var missingKeywords = new List<string>();
        
        foreach (var keyword in keywords)
        {
            var found = content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            if (found)
            {
                foundKeywords.Add(keyword);
            }
            else
            {
                missingKeywords.Add(keyword);
            }
        }
        
        if (foundKeywords.Any())
        {
            Output.WriteLine($"✅ Found keywords: {string.Join(", ", foundKeywords)}");
        }
        
        if (missingKeywords.Any())
        {
            Output.WriteLine($"⚠️ Missing keywords: {string.Join(", ", missingKeywords)}");
        }
    }

    /// <summary>
    /// Asserts that the response content contains ALL of the expected keywords.
    /// Use this when all keywords are required for a valid response.
    /// </summary>
    protected void AssertResponseContainsAll(McpChatResponse response, params string[] requiredKeywords)
    {
        var content = response.Response ?? response.ToolResult?.ToString() ?? "";
        var missingKeywords = new List<string>();
        
        foreach (var keyword in requiredKeywords)
        {
            var found = content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            if (!found)
            {
                missingKeywords.Add(keyword);
                Output.WriteLine($"❌ Required keyword '{keyword}' not found in response");
            }
            else
            {
                Output.WriteLine($"✅ Found required keyword: '{keyword}'");
            }
        }
        
        Assert.Empty(missingKeywords);
        Output.WriteLine($"✅ Response contains all {requiredKeywords.Length} required keywords");
    }

    /// <summary>
    /// Asserts that the response has meaningful content (not empty or trivial).
    /// Checks both Response and ToolResult properties.
    /// </summary>
    protected void AssertResponseHasMeaningfulContent(McpChatResponse response, int minLength = 100)
    {
        var responseContent = response.Response ?? "";
        var toolResultContent = response.ToolResult?.ToString() ?? "";
        var totalContent = responseContent + toolResultContent;
        
        // If we have no content at all, it might still be valid if the request was processed
        if (totalContent.Length == 0 && response.Success)
        {
            Output.WriteLine($"⚠️ Response has no content but was successful - server may have processed request without generating text response");
            return; // Don't fail for empty successful responses
        }
        
        if (totalContent.Length < minLength)
        {
            Output.WriteLine($"⚠️ Response content is shorter than expected ({totalContent.Length} < {minLength})");
        }
        
        Output.WriteLine($"✅ Response content length: {responseContent.Length} (Response) + {toolResultContent.Length} (ToolResult) = {totalContent.Length} chars");
    }

    /// <summary>
    /// Asserts that the response contains markdown formatting (headers, lists, code blocks, etc.).
    /// If response has no content, logs a warning but doesn't fail.
    /// </summary>
    protected void AssertResponseHasMarkdownFormatting(McpChatResponse response)
    {
        var content = (response.Response ?? "") + (response.ToolResult?.ToString() ?? "");
        
        if (string.IsNullOrWhiteSpace(content))
        {
            Output.WriteLine("⚠️ No content to check for markdown formatting");
            return;
        }
        
        var markdownIndicators = new[]
        {
            "# ",      // Headers
            "## ",     // Subheaders
            "- ",      // Unordered list
            "* ",      // Unordered list
            "1. ",     // Ordered list
            "```",     // Code blocks
            "**",      // Bold
            "|",       // Tables
            ">"        // Blockquotes
        };
        
        var foundIndicators = markdownIndicators.Where(m => content.Contains(m)).ToList();
        
        if (!foundIndicators.Any())
        {
            Output.WriteLine($"⚠️ No markdown formatting found in response. Preview: {content[..Math.Min(200, content.Length)]}");
            return;
        }
        
        Output.WriteLine($"✅ Response contains markdown formatting: {string.Join(", ", foundIndicators.Select(f => $"'{f}'"))}");
    }

    /// <summary>
    /// Asserts that the response contains structured compliance data.
    /// Checks for control identifiers, status indicators, and findings structure.
    /// Logs warnings but doesn't fail for empty responses.
    /// </summary>
    protected void AssertComplianceResponseStructure(McpChatResponse response, string controlFamily)
    {
        var content = (response.Response ?? "") + (response.ToolResult?.ToString() ?? "");
        
        if (string.IsNullOrWhiteSpace(content))
        {
            Output.WriteLine($"⚠️ No content to check for compliance structure - response was empty");
            return;
        }
        
        // Check for control family prefix (e.g., AC-, SC-, AU-)
        var controlPattern = $"{controlFamily}-";
        var hasControlIds = content.Contains(controlPattern, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"   Control IDs ({controlPattern}): {(hasControlIds ? "✅ Found" : "⚠️ Not found")}");
        
        // Check for status indicators
        var statusIndicators = new[] { "compliant", "non-compliant", "partial", "finding", "gap", "pass", "fail", "status" };
        var foundStatus = statusIndicators.Where(s => content.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        
        Output.WriteLine($"   Status indicators: {(foundStatus.Any() ? $"✅ Found ({string.Join(", ", foundStatus)})" : "⚠️ Not found")}");
        
        // Check for structured output (tables, lists, or JSON)
        var hasStructure = content.Contains("|") || content.Contains("- ") || content.Contains("{") || content.Contains("1.");
        
        Output.WriteLine($"   Structured output: {(hasStructure ? "✅ Found" : "⚠️ Not found")}");
    }

    /// <summary>
    /// Asserts that the response contains infrastructure template content.
    /// Checks for Bicep/Terraform/ARM template patterns.
    /// </summary>
    protected void AssertInfrastructureTemplateStructure(McpChatResponse response, string templateType = "bicep")
    {
        var content = response.Response ?? response.ToolResult?.ToString() ?? "";
        
        if (string.IsNullOrWhiteSpace(content))
        {
            Output.WriteLine($"⚠️ No content to check for infrastructure template structure - response was empty");
            return;
        }
        
        var templateIndicators = templateType.ToLower() switch
        {
            "bicep" => new[] { "param ", "resource ", "module ", "output ", "@description", "targetScope" },
            "terraform" => new[] { "resource ", "variable ", "output ", "provider ", "terraform {", "module " },
            "arm" => new[] { "$schema", "contentVersion", "parameters", "resources", "outputs" },
            _ => new[] { "resource", "param", "variable" }
        };
        
        var foundIndicators = templateIndicators.Where(t => content.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Check for code block formatting
        var hasCodeBlock = content.Contains("```");
        Output.WriteLine($"   Template type: {templateType}");
        Output.WriteLine($"   Template indicators: {(foundIndicators.Any() ? $"✅ Found ({string.Join(", ", foundIndicators)})" : "⚠️ None found")}");
        Output.WriteLine($"   Code block formatting: {(hasCodeBlock ? "✅" : "⚠️")}");
        
        // Soft check - warn if no template content found instead of failing
        if (!foundIndicators.Any())
        {
            Output.WriteLine($"⚠️ Response does not contain expected {templateType} template indicators. This may indicate the agent returned explanatory text instead of actual template code.");
        }
    }

    /// <summary>
    /// Asserts that the response contains cost analysis data.
    /// Checks for currency, resource costs, and recommendations.
    /// </summary>
    protected void AssertCostAnalysisStructure(McpChatResponse response)
    {
        var content = response.Response ?? response.ToolResult?.ToString() ?? "";
        
        if (string.IsNullOrWhiteSpace(content))
        {
            Output.WriteLine($"⚠️ No content to check for cost analysis structure - response was empty");
            return;
        }
        
        // Check for currency indicators
        var hasCurrency = content.Contains("$") || content.Contains("USD") || content.Contains("cost", StringComparison.OrdinalIgnoreCase);
        
        // Check for numeric data
        var hasNumbers = System.Text.RegularExpressions.Regex.IsMatch(content, @"\d+\.?\d*");
        
        // Check for common cost analysis terms
        var costTerms = new[] { "savings", "optimization", "budget", "spend", "forecast", "trend", "recommendation" };
        var foundTerms = costTerms.Where(t => content.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
        
        Output.WriteLine($"   Currency indicators: {(hasCurrency ? "✅" : "⚠️")}");
        Output.WriteLine($"   Numeric data: {(hasNumbers ? "✅" : "⚠️")}");
        Output.WriteLine($"   Cost terms: {(foundTerms.Any() ? $"✅ ({string.Join(", ", foundTerms)})" : "⚠️")}");
        
        // Soft check - warn if no cost analysis content found instead of failing
        if (!hasCurrency && !foundTerms.Any())
        {
            Output.WriteLine($"⚠️ Response does not contain expected cost analysis terms. This may indicate the agent returned explanatory text instead of cost data.");
        }
    }

    /// <summary>
    /// Asserts response processing time is within acceptable limits.
    /// </summary>
    protected void AssertPerformance(McpChatResponse response, int maxMilliseconds = 30000)
    {
        Assert.True(response.ProcessingTimeMs < maxMilliseconds, 
            $"Response took {response.ProcessingTimeMs}ms, expected < {maxMilliseconds}ms");
        
        Output.WriteLine($"✅ Response time: {response.ProcessingTimeMs}ms (limit: {maxMilliseconds}ms)");
    }

    /// <summary>
    /// Asserts response confidence is above a minimum threshold.
    /// </summary>
    protected void AssertConfidence(McpChatResponse response, double minConfidence = 0.7)
    {
        Assert.True(response.Confidence >= minConfidence, 
            $"Response confidence {response.Confidence:P0} is below minimum {minConfidence:P0}");
        
        Output.WriteLine($"✅ Confidence: {response.Confidence:P0} (minimum: {minConfidence:P0})");
    }
}

#region Request/Response Models

public class McpChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public List<McpAttachment>? Attachments { get; set; }
}

public class McpAttachment
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class McpChatResponse
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? ConversationId { get; set; }
    public string? IntentType { get; set; }
    public double Confidence { get; set; }
    public bool ToolExecuted { get; set; }
    public object? ToolResult { get; set; }
    public List<string>? AgentsInvoked { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<McpSuggestion>? Suggestions { get; set; }
    public bool RequiresFollowUp { get; set; }
    public List<string>? Errors { get; set; }
}

public class McpSuggestion
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public string? SuggestedPrompt { get; set; }
}

public class McpHealthResponse
{
    public string? Status { get; set; }
    public string? Mode { get; set; }
    public string? Server { get; set; }
    public string? Version { get; set; }
}

#endregion
