using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Hubs;

[Authorize]
public class SupportChatHub : Hub
{
    private readonly ILogger<SupportChatHub> _logger;

    public SupportChatHub(ILogger<SupportChatHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = Context.User?.IsInRole("Admin") ?? false;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User connected without valid userId claim");
            await base.OnConnectedAsync();
            return;
        }

        // Join user-specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        // If admin, join admins group
        if (isAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            _logger.LogInformation("Admin {UserId} connected to SupportChatHub", userId);
        }
        else
        {
            _logger.LogInformation("Customer {UserId} connected to SupportChatHub", userId);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(string conversationId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv:{conversationId}");
        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv:{conversationId}");
        _logger.LogInformation("User {UserId} left conversation {ConversationId}", userId, conversationId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} disconnected from SupportChatHub", userId);
        await base.OnDisconnectedAsync(exception);
    }

    // Server-to-client events (called by controllers/services)
    public async Task BroadcastMessageCreated(MessageDto message, string conversationId, string customerUserId)
    {
        // Send to conversation group
        await Clients.Group($"conv:{conversationId}").SendAsync("MessageCreated", message);

        // Send to all admins (for inbox updates)
        await Clients.Group("admins").SendAsync("MessageCreated", message);

        // Send to customer
        await Clients.Group($"user:{customerUserId}").SendAsync("MessageCreated", message);
    }

    public async Task BroadcastConversationUpserted(ConversationListItemDto conversation)
    {
        // Send to all admins (for inbox list updates)
        await Clients.Group("admins").SendAsync("ConversationUpserted", conversation);

        // Send to customer
        await Clients.Group($"user:{conversation.CustomerUserId}").SendAsync("ConversationUpserted", conversation);

        // Send to conversation group
        await Clients.Group($"conv:{conversation.Id}").SendAsync("ConversationUpserted", conversation);
    }
}
