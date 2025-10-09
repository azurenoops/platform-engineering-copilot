using Microsoft.AspNetCore.SignalR;
using Platform.Engineering.Copilot.Chat.App.Models;
using Platform.Engineering.Copilot.Chat.App.Services;

namespace Platform.Engineering.Copilot.Chat.App.Hubs;

/// <summary>
/// SignalR hub for real-time chat communication
/// </summary>
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Join a conversation room for real-time updates
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        _logger.LogInformation("Connection {ConnectionId} joined conversation {ConversationId}", Context.ConnectionId, conversationId);
    }

    /// <summary>
    /// Leave a conversation room
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        _logger.LogInformation("Connection {ConnectionId} left conversation {ConversationId}", Context.ConnectionId, conversationId);
    }

    /// <summary>
    /// Send a message to a conversation
    /// </summary>
    public async Task SendMessage(ChatRequest request)
    {
        try
        {
            _logger.LogInformation("Received message for conversation {ConversationId}", request.ConversationId);

            // Notify clients that message is being processed
            await Clients.Group($"conversation-{request.ConversationId}")
                .SendAsync("MessageProcessing", new { conversationId = request.ConversationId, message = request.Message });

            // Process the message
            var response = await _chatService.SendMessageAsync(request);

            // Send the response to all clients in the conversation
            await Clients.Group($"conversation-{request.ConversationId}")
                .SendAsync("MessageReceived", response);

            _logger.LogInformation("Sent response for message in conversation {ConversationId}", request.ConversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message in conversation {ConversationId}", request.ConversationId);
            
            await Clients.Group($"conversation-{request.ConversationId}")
                .SendAsync("MessageError", new { error = "Failed to process message", conversationId = request.ConversationId });
        }
    }

    /// <summary>
    /// Notify typing status
    /// </summary>
    public async Task NotifyTyping(string conversationId, bool isTyping)
    {
        await Clients.OthersInGroup($"conversation-{conversationId}")
            .SendAsync("UserTyping", new { conversationId, isTyping, connectionId = Context.ConnectionId });
    }

    /// <summary>
    /// Handle connection events
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}