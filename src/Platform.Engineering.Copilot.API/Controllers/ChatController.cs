using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.API.Models;

namespace Platform.Engineering.Copilot.API.Controllers;

/// <summary>
/// REST API controller for AI-powered chat interactions and natural language processing.
/// Provides endpoints for processing chat queries, receiving AI-generated recommendations,
/// and integrating with platform tools through conversational interfaces.
/// Supports natural language platform management and intelligent tool suggestions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IIntelligentChatService _intelligentChat;

    /// <summary>
    /// Initializes a new instance of the ChatController.
    /// </summary>
    /// <param name="logger">Logger for chat API operation diagnostics</param>
    /// <param name="intelligentChat">Intelligent chat service for AI-powered intent classification</param>
    public ChatController(
        ILogger<ChatController> logger, 
        IIntelligentChatService intelligentChat)
    {
        _logger = logger;
        _intelligentChat = intelligentChat;
    }

    /// <summary>
    /// Process user message with AI-powered intent classification and tool execution
    /// This is the new intelligent chat endpoint that uses Azure OpenAI for intent classification,
    /// supports multi-step tool chaining, and provides proactive suggestions.
    /// </summary>
    /// <param name="request">Intelligent chat request with message and conversation context</param>
    /// <returns>Intelligent chat response with AI classification, tool results, and suggestions</returns>
    [HttpPost("intelligent-query")]
    public async Task<ActionResult<IntelligentChatApiResponse>> ProcessIntelligentQueryAsync(
        [FromBody] IntelligentChatRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest(new IntelligentChatApiResponse
                {
                    Success = false,
                    Error = "Message is required"
                });
            }

            if (string.IsNullOrEmpty(request.ConversationId))
            {
                return BadRequest(new IntelligentChatApiResponse
                {
                    Success = false,
                    Error = "ConversationId is required"
                });
            }

            _logger.LogInformation(
                "Processing intelligent chat query. ConversationId: {ConversationId}, Message: {Message}", 
                request.ConversationId, 
                request.Message);

            var response = await _intelligentChat.ProcessMessageAsync(
                request.Message,
                request.ConversationId,
                request.Context,
                HttpContext.RequestAborted);

            _logger.LogInformation(
                "Intelligent chat query processed successfully. Intent: {IntentType}, ToolExecuted: {ToolExecuted}", 
                response.Intent.IntentType, 
                response.ToolExecuted);

            return Ok(new IntelligentChatApiResponse
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing intelligent chat query: {Message}", request.Message);
            return StatusCode(500, new IntelligentChatApiResponse
            {
                Success = false,
                Error = "Internal server error processing intelligent chat query",
                ErrorDetails = new Dictionary<string, string>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["Message"] = ex.Message
                }
            });
        }
    }

    /// <summary>
    /// Get conversation context for a specific conversation
    /// Retrieves the current state of a conversation including message history,
    /// used tools, and mentioned resources.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier</param>
    /// <returns>Conversation context with history and state</returns>
    [HttpGet("context/{conversationId}")]
    public async Task<ActionResult<ConversationContext>> GetContextAsync(string conversationId)
    {
        try
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                return BadRequest("ConversationId is required");
            }

            _logger.LogInformation("Retrieving conversation context: {ConversationId}", conversationId);

            var context = await _intelligentChat.GetOrCreateContextAsync(
                conversationId,
                cancellationToken: HttpContext.RequestAborted);

            return Ok(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation context: {ConversationId}", conversationId);
            return StatusCode(500, new { error = "Failed to retrieve conversation context" });
        }
    }

    /// <summary>
    /// Generate proactive suggestions based on conversation context
    /// Uses AI to recommend next actions the user might want to take.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier</param>
    /// <returns>List of AI-generated proactive suggestions</returns>
    [HttpGet("suggestions/{conversationId}")]
    public async Task<ActionResult<List<ProactiveSuggestion>>> GetSuggestionsAsync(string conversationId)
    {
        try
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                return BadRequest("ConversationId is required");
            }

            _logger.LogInformation("Generating proactive suggestions: {ConversationId}", conversationId);

            var context = await _intelligentChat.GetOrCreateContextAsync(
                conversationId,
                cancellationToken: HttpContext.RequestAborted);

            var suggestions = await _intelligentChat.GenerateProactiveSuggestionsAsync(
                conversationId,
                context,
                HttpContext.RequestAborted);

            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggestions: {ConversationId}", conversationId);
            return StatusCode(500, new { error = "Failed to generate suggestions" });
        }
    }

    /// <summary>
    /// Classify user intent without executing tools
    /// Useful for testing intent classification or pre-flight analysis.
    /// </summary>
    /// <param name="request">Message and context for classification</param>
    /// <returns>Intent classification result with confidence scores</returns>
    [HttpPost("classify-intent")]
    public async Task<ActionResult<IntentClassificationResult>> ClassifyIntentAsync(
        [FromBody] IntelligentChatRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest("Message is required");
            }

            _logger.LogInformation("Classifying intent: {Message}", request.Message);

            var intent = await _intelligentChat.ClassifyIntentAsync(
                request.Message,
                request.Context,
                HttpContext.RequestAborted);

            return Ok(intent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying intent: {Message}", request.Message);
            return StatusCode(500, new { error = "Failed to classify intent" });
        }
    }
}