using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Chat.App.Models;
using Platform.Engineering.Copilot.Chat.App.Services;

namespace Platform.Engineering.Copilot.Chat.App.Controllers;

/// <summary>
/// API controller for chat conversations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(IChatService chatService, ILogger<ConversationsController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Get all conversations for a user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Conversation>>> GetConversations(
        [FromQuery] string userId = "default-user",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            var conversations = await _chatService.GetConversationsAsync(userId, skip, take);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific conversation
    /// </summary>
    [HttpGet("{conversationId}")]
    public async Task<ActionResult<Conversation>> GetConversation(string conversationId)
    {
        try
        {
            var conversation = await _chatService.GetConversationAsync(conversationId);
            if (conversation == null)
                return NotFound();

            return Ok(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new conversation
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Conversation>> CreateConversation(
        [FromBody] CreateConversationRequest request)
    {
        try
        {
            var conversation = await _chatService.CreateConversationAsync(
                request.Title ?? "New Conversation",
                request.UserId ?? "default-user");

            return CreatedAtAction(nameof(GetConversation), new { conversationId = conversation.Id }, conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversation");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    [HttpDelete("{conversationId}")]
    public async Task<ActionResult> DeleteConversation(string conversationId)
    {
        try
        {
            var success = await _chatService.DeleteConversationAsync(conversationId);
            if (!success)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Search conversations
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<Conversation>>> SearchConversations(
        [FromQuery] string query,
        [FromQuery] string userId = "default-user")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Search query is required");

            var conversations = await _chatService.SearchConversationsAsync(query, userId);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching conversations with query: {Query}", query);
            return StatusCode(500, "Internal server error");
        }
    }
}

/// <summary>
/// API controller for chat messages
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IChatService chatService, ILogger<MessagesController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Get messages for a conversation
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ChatMessage>>> GetMessages(
        [FromQuery] string conversationId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return BadRequest("ConversationId is required");

            var messages = await _chatService.GetMessagesAsync(conversationId, skip, take);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Send a message
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatMessage>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message content is required");

            var response = await _chatService.SendMessageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Upload an attachment
    /// </summary>
    [HttpPost("{messageId}/attachments")]
    public async Task<ActionResult<MessageAttachment>> UploadAttachment(
        string messageId,
        IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is required");

            if (file.Length > 10 * 1024 * 1024) // 10MB limit
                return BadRequest("File size too large (max 10MB)");

            var attachment = await _chatService.UploadAttachmentAsync(messageId, file);
            return Ok(attachment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading attachment for message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }
}

/// <summary>
/// Request model for creating conversations
/// </summary>
public class CreateConversationRequest
{
    public string? Title { get; set; }
    public string? UserId { get; set; }
}