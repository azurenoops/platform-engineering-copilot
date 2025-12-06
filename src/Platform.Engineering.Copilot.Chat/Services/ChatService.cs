using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Chat.App.Data;
using Platform.Engineering.Copilot.Chat.App.Models;
using System.Text.Json;
using System.Text;

namespace Platform.Engineering.Copilot.Chat.App.Services;

/// <summary>
/// Interface for the enhanced chat service
/// </summary>
public interface IChatService
{
    Task<Conversation> CreateConversationAsync(string title = "New Conversation", string userId = "default-user");
    Task<Conversation?> GetConversationAsync(string conversationId);
    Task<List<Conversation>> GetConversationsAsync(string userId = "default-user", int skip = 0, int take = 50);
    Task<ChatMessage> SendMessageAsync(ChatRequest request);
    Task<List<ChatMessage>> GetMessagesAsync(string conversationId, int skip = 0, int take = 50);
    Task<MessageAttachment> UploadAttachmentAsync(string messageId, IFormFile file);
    Task<ConversationContext?> GetContextAsync(string conversationId, string? type = null);
    Task<ConversationContext> StoreContextAsync(ConversationContext context);
    Task<bool> DeleteConversationAsync(string conversationId);
    Task<List<Conversation>> SearchConversationsAsync(string query, string userId = "default-user");
}

/// <summary>
/// Enhanced chat service with API integration, context awareness, and persistent history
/// </summary>
public class ChatService : IChatService
{
    private readonly ChatDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _mcpBaseUrl;
    private readonly string _uploadsPath;

    public ChatService(
        ChatDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        // Check environment variable first, then config, then default
        _mcpBaseUrl = System.Environment.GetEnvironmentVariable("MCP_SERVER_URL") 
            ?? configuration["McpServer:BaseUrl"] 
            ?? "http://localhost:5100";
        _logger.LogInformation("MCP Server URL: {McpBaseUrl}", _mcpBaseUrl);
        _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<Conversation> CreateConversationAsync(string title = "New Conversation", string userId = "default-user")
    {
        _logger.LogInformation("Creating new conversation: {Title} for user: {UserId}", title, userId);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created conversation: {ConversationId}", conversation.Id);
        return conversation;
    }

    public async Task<Conversation?> GetConversationAsync(string conversationId)
    {
        return await _dbContext.Conversations
            .Include(c => c.Messages)
                .ThenInclude(m => m.Attachments)
            .Include(c => c.Context)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    public async Task<List<Conversation>> GetConversationsAsync(string userId = "default-user", int skip = 0, int take = 50)
    {
        return await _dbContext.Conversations
            .Where(c => c.UserId == userId && !c.IsArchived)
            .OrderByDescending(c => c.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ChatMessage> SendMessageAsync(ChatRequest request)
    {
        _logger.LogInformation("Processing message in conversation: {ConversationId}", request.ConversationId);

        // Get or create conversation
        var conversation = await GetConversationAsync(request.ConversationId);
        if (conversation == null)
        {
            conversation = await CreateConversationAsync("Chat Session", "default-user");
            request.ConversationId = conversation.Id;
        }

        // Create user message
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            ConversationId = request.ConversationId,
            Content = request.Message,
            Role = MessageRole.User,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        _dbContext.Messages.Add(userMessage);
        await _dbContext.SaveChangesAsync();

        // Process the message and get AI response
        var assistantMessage = await ProcessMessageWithApiAsync(userMessage, conversation);

        // Update conversation timestamp
        conversation.UpdatedAt = DateTime.UtcNow;
        if (conversation.Messages.Count == 1) // First message, update title
        {
            conversation.Title = GenerateConversationTitle(request.Message);
        }
        await _dbContext.SaveChangesAsync();

        return assistantMessage;
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(string conversationId, int skip = 0, int take = 50)
    {
        return await _dbContext.Messages
            .Include(m => m.Attachments)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<MessageAttachment> UploadAttachmentAsync(string messageId, IFormFile file)
    {
        _logger.LogInformation("Uploading attachment for message: {MessageId}, file: {FileName}", messageId, file.FileName);

        var fileId = Guid.NewGuid().ToString();
        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"{fileId}{fileExtension}";
        var filePath = Path.Combine(_uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new MessageAttachment
        {
            Id = fileId,
            MessageId = messageId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length,
            StoragePath = filePath,
            Type = GetAttachmentType(file.ContentType),
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Uploaded attachment: {AttachmentId} for message: {MessageId}", attachment.Id, messageId);
        return attachment;
    }

    public async Task<ConversationContext?> GetContextAsync(string conversationId, string? type = null)
    {
        var query = _dbContext.Contexts.Where(c => c.ConversationId == conversationId);

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(c => c.Type == type);
        }

        var context = await query
            .OrderByDescending(c => c.LastAccessedAt)
            .FirstOrDefaultAsync();

        if (context != null)
        {
            context.LastAccessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        return context;
    }

    public async Task<ConversationContext> StoreContextAsync(ConversationContext context)
    {
        _logger.LogInformation("Storing context: {Type} for conversation: {ConversationId}", context.Type, context.ConversationId);

        var existingContext = await _dbContext.Contexts
            .FirstOrDefaultAsync(c => c.ConversationId == context.ConversationId && c.Type == context.Type);

        if (existingContext != null)
        {
            existingContext.Data = context.Data;
            existingContext.Summary = context.Summary;
            existingContext.Tags = context.Tags;
            existingContext.LastAccessedAt = DateTime.UtcNow;
            context = existingContext;
        }
        else
        {
            _dbContext.Contexts.Add(context);
        }

        await _dbContext.SaveChangesAsync();
        return context;
    }

    public async Task<bool> DeleteConversationAsync(string conversationId)
    {
        var conversation = await _dbContext.Conversations
            .Include(c => c.Messages)
                .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) return false;

        // Delete associated files
        foreach (var attachment in conversation.Messages.SelectMany(m => m.Attachments))
        {
            if (File.Exists(attachment.StoragePath))
            {
                File.Delete(attachment.StoragePath);
            }
        }

        _dbContext.Conversations.Remove(conversation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted conversation: {ConversationId}", conversationId);
        return true;
    }

    public async Task<List<Conversation>> SearchConversationsAsync(string query, string userId = "default-user")
    {
        return await _dbContext.Conversations
            .Where(c => c.UserId == userId && !c.IsArchived &&
                       (c.Title.Contains(query) || c.Messages.Any(m => m.Content.Contains(query))))
            .OrderByDescending(c => c.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    // ============================================================================
    // INTELLIGENT CHAT API INTEGRATION (Thin Client)
    // ============================================================================

    /// <summary>
    /// Process user message using the new Intelligent Chat Service
    /// This method is now a thin client that calls the Platform API's intelligent-query endpoint
    /// All AI-powered intent classification, tool chaining, and suggestion generation happen in Core
    /// </summary>
    private async Task<ChatMessage> ProcessMessageWithApiAsync(ChatMessage userMessage, Conversation conversation)
    {
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            ConversationId = userMessage.ConversationId,
            Role = MessageRole.Assistant,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Processing,
            ParentMessageId = userMessage.Id
        };

        _dbContext.Messages.Add(assistantMessage);
        await _dbContext.SaveChangesAsync();

        try
        {
            _logger.LogInformation("Processing message with Intelligent Chat API: {ConversationId}", conversation.Id);

            // Call the new intelligent-query endpoint
            var intelligentResponse = await CallIntelligentChatApiAsync(userMessage.Content, conversation.Id);

            if (intelligentResponse != null && intelligentResponse.Success && intelligentResponse.Data != null)
            {
                var chatResponse = intelligentResponse.Data;

                // Set assistant message content from AI response
                assistantMessage.Content = chatResponse.Response;

                // Store metadata about the interaction
                assistantMessage.Metadata = new Dictionary<string, object>
                {
                    ["intentType"] = chatResponse.Intent.IntentType,
                    ["confidence"] = chatResponse.Intent.Confidence,
                    ["toolExecuted"] = chatResponse.ToolExecuted,
                    ["processingTimeMs"] = chatResponse.Metadata.ProcessingTimeMs
                };

                if (!string.IsNullOrEmpty(chatResponse.Intent.ToolName))
                {
                    assistantMessage.Metadata["toolName"] = chatResponse.Intent.ToolName;
                }

                // Store tool results if available
                if (chatResponse.ToolExecuted && chatResponse.ToolResult != null)
                {
                    assistantMessage.Metadata["toolResult"] = chatResponse.ToolResult;
                }

                // Store tool chain results if multi-step workflow was executed
                if (chatResponse.ToolChainResult != null)
                {
                    assistantMessage.Metadata["toolChain"] = new
                    {
                        chainId = chatResponse.ToolChainResult.ChainId,
                        status = chatResponse.ToolChainResult.Status,
                        totalSteps = chatResponse.ToolChainResult.TotalSteps,
                        completedSteps = chatResponse.ToolChainResult.CompletedSteps,
                        successRate = chatResponse.ToolChainResult.SuccessRate
                    };
                }

                // Store proactive suggestions for UI display
                if (chatResponse.Suggestions != null && chatResponse.Suggestions.Any())
                {
                    assistantMessage.Metadata["suggestions"] = chatResponse.Suggestions.Select(s => new
                    {
                        title = s.Title,
                        description = s.Description,
                        priority = s.Priority,
                        category = s.Category,
                        icon = s.Icon,
                        suggestedPrompt = s.SuggestedPrompt,
                        expectedOutcome = s.ExpectedOutcome
                    }).ToList();
                }

                assistantMessage.Status = MessageStatus.Completed;
                _logger.LogInformation(
                    "Successfully processed message via Intelligent Chat. Intent: {IntentType}, Tool: {ToolExecuted}", 
                    chatResponse.Intent.IntentType, 
                    chatResponse.ToolExecuted);
            }
            else
            {
                // Fallback to old API if intelligent chat fails
                _logger.LogWarning("Intelligent Chat API failed, falling back to legacy chat endpoint");
                var apiResponse = await CallPlatformApiAsync(userMessage.Content);
                assistantMessage.Content = apiResponse.Content ?? "Sorry, I couldn't process your request.";
                assistantMessage.Metadata = new Dictionary<string, object>
                {
                    ["suggestedActions"] = apiResponse.SuggestedActions ?? new List<string>(),
                    ["recommendedTools"] = apiResponse.RecommendedTools ?? new List<Models.ToolInfo>(),
                    ["fallback"] = true
                };
                assistantMessage.Status = MessageStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {MessageId}", assistantMessage.Id);
            assistantMessage.Content = "I encountered an error processing your request. Please try again.";
            assistantMessage.Status = MessageStatus.Error;
            assistantMessage.Metadata = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorType"] = ex.GetType().Name
            };
        }

        await _dbContext.SaveChangesAsync();
        return assistantMessage;
    }

    /// <summary>
    /// Call the MCP Server chat endpoint
    /// Uses AI-powered multi-agent orchestration with intent classification, tool chaining, and proactive suggestions
    /// </summary>
    private async Task<IntelligentChatApiResponse?> CallIntelligentChatApiAsync(string message, string conversationId)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_mcpBaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(180); // Extended timeout for AI processing with function calls

            var request = new
            {
                message,
                conversationId,
                context = (object?)null // Context is managed server-side
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling MCP Chat endpoint: {Endpoint}", "/mcp/chat");
            var response = await httpClient.PostAsync("/mcp/chat", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // MCP returns ChatMcpResult directly - map to our API response format
                var mcpResult = JsonSerializer.Deserialize<ChatMcpResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (mcpResult != null)
                {
                    // Convert MCP result to our expected format
                    var apiResponse = new IntelligentChatApiResponse
                    {
                        Success = mcpResult.Success,
                        Data = new IntelligentChatData
                        {
                            Response = mcpResult.Response,
                            ConversationId = mcpResult.ConversationId,
                            MessageId = Guid.NewGuid().ToString(),
                            Intent = new IntentClassification
                            {
                                IntentType = mcpResult.IntentType,
                                Confidence = mcpResult.Confidence,
                                ToolName = mcpResult.ToolExecuted ? "orchestrator" : null
                            },
                            ToolExecuted = mcpResult.ToolExecuted,
                            ToolResult = mcpResult.ToolResult,
                            Suggestions = mcpResult.Suggestions.Select(s => new ProactiveSuggestionData
                            {
                                Title = s.Title,
                                Description = s.Description,
                                Priority = s.Priority,
                                Category = s.Category,
                                SuggestedPrompt = s.SuggestedPrompt ?? string.Empty
                            }).ToList(),
                            RequiresFollowUp = mcpResult.RequiresFollowUp,
                            FollowUpPrompt = mcpResult.FollowUpPrompt,
                            Metadata = new ResponseMetadata
                            {
                                ProcessingTimeMs = (long)mcpResult.ProcessingTimeMs
                            }
                        },
                        Error = mcpResult.Errors.Any() ? string.Join("; ", mcpResult.Errors) : null
                    };

                    _logger.LogInformation("MCP Chat call successful");
                    return apiResponse;
                }

                return null;
            }
            else
            {
                _logger.LogError("MCP Chat call failed with status: {StatusCode}, content: {Content}", 
                    response.StatusCode, responseContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP Chat endpoint");
            return null;
        }
    }

    /// <summary>
    /// Legacy chat endpoint (fallback) - now routes through MCP server
    /// </summary>
    private async Task<ChatResponse> CallPlatformApiAsync(string query)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_mcpBaseUrl);

            var request = new { query = query };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("/api/chat/query", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }) ?? new ChatResponse { Success = false, Error = "Failed to deserialize response" };

                return apiResponse;
            }
            else
            {
                _logger.LogError("API call failed with status: {StatusCode}, content: {Content}", response.StatusCode, responseContent);
                return new ChatResponse
                {
                    Success = false,
                    Error = $"API call failed: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Platform API");
            return new ChatResponse
            {
                Success = false,
                Error = "Failed to communicate with Platform API"
            };
        }
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================

    private static AttachmentType GetAttachmentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            var ct when ct.StartsWith("image/") => AttachmentType.Image,
            var ct when ct.Contains("json") || ct.Contains("yaml") || ct.Contains("xml") => AttachmentType.Configuration,
            var ct when ct.Contains("log") || ct.Contains("text/plain") => AttachmentType.Log,
            var ct when ct.Contains("code") || ct.Contains("javascript") || ct.Contains("python") => AttachmentType.Code,
            _ => AttachmentType.Document
        };
    }

    private static string GenerateConversationTitle(string firstMessage)
    {
        // Generate a smart title based on the first message
        var title = firstMessage.Length > 50 ? firstMessage[..47] + "..." : firstMessage;

        // Replace common patterns with more descriptive titles
        if (firstMessage.ToLowerInvariant().Contains("ato") || firstMessage.ToLowerInvariant().Contains("compliance"))
            return "ATO Compliance Discussion";
        if (firstMessage.ToLowerInvariant().Contains("cost") || firstMessage.ToLowerInvariant().Contains("budget"))
            return "Cost Analysis Discussion";
        if (firstMessage.ToLowerInvariant().Contains("deploy") || firstMessage.ToLowerInvariant().Contains("infrastructure"))
            return "Infrastructure Discussion";

        return title;
    }
}

// ============================================================================
// MCP RESULT MODELS (from MCP Server)
// ============================================================================

internal class ChatMcpResult
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string IntentType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool ToolExecuted { get; set; }
    public object? ToolResult { get; set; }
    public List<string> AgentsInvoked { get; set; } = new();
    public string? ExecutionPattern { get; set; }
    public double ProcessingTimeMs { get; set; }
    public List<SuggestionInfo> Suggestions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool RequiresFollowUp { get; set; }
    public string? FollowUpPrompt { get; set; }
}

internal class SuggestionInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SuggestedPrompt { get; set; }
}

internal class IntelligentChatResponseData
{
    public string Response { get; set; } = string.Empty;
    public IntentData Intent { get; set; } = new();
    public bool ToolExecuted { get; set; }
    public object? ToolResult { get; set; }
    public List<SuggestionData> Suggestions { get; set; } = new();
    public ResponseMetadataData Metadata { get; set; } = new();
}

internal class IntentData
{
    public string IntentType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? ToolName { get; set; }
}

internal class SuggestionData
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SuggestedPrompt { get; set; }
}

internal class ResponseMetadataData
{
    public long ProcessingTimeMs { get; set; }
    public List<string> AgentsInvoked { get; set; } = new();
    public string ExecutionPattern { get; set; } = string.Empty;
}