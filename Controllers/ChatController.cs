using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Ticketing.Api.DTOs;
using Ticketing.Api.Enums;
using Ticketing.Api.Hubs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ISupportChatService _chatService;
    private readonly IHubContext<SupportChatHub> _hubContext;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ISupportChatService chatService,
        IHubContext<SupportChatHub> hubContext,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet("conversations/{id}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var isAdmin = User.IsInRole("Admin");

        try
        {
            var conversation = await _chatService.GetConversationByIdAsync(id, userId, isAdmin);
            if (conversation == null)
            {
                return NotFound("Conversation not found");
            }

            return Ok(conversation);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt by user {UserId} to conversation {ConversationId}", userId, id);
            return Forbid();
        }
    }

    [HttpGet("conversations/{id}/messages")]
    public async Task<ActionResult<MessageDto[]>> GetMessages(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var isAdmin = User.IsInRole("Admin");

        try
        {
            var messages = await _chatService.GetConversationMessagesAsync(id, userId, isAdmin);
            return Ok(messages);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt by user {UserId} to conversation {ConversationId}", userId, id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
    }

    [HttpPost("messages/send")]
    public async Task<ActionResult<MessageDto>> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var isAdmin = User.IsInRole("Admin");
        var senderType = isAdmin ? SenderType.Admin : SenderType.Customer;

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Message text cannot be empty");
        }

        try
        {
            var message = await _chatService.SendMessageAsync(request.ConversationId, userId, senderType, request.Text);

            // Get updated conversation for broadcast
            var conversation = await _chatService.GetConversationByIdAsync(request.ConversationId, userId, isAdmin);
            if (conversation != null)
            {
                var conversationDto = new ConversationListItemDto(
                    conversation.Id,
                    conversation.CustomerUserId,
                    conversation.CustomerDisplayName,
                    conversation.CreatedAt,
                    conversation.LastMessageAt,
                    request.Text.Length > 200 ? request.Text.Substring(0, 200) : request.Text,
                    senderType,
                    conversation.UnreadForAdminCount,
                    conversation.UnreadForCustomerCount,
                    conversation.IsOpen
                );

                // Broadcast events via SignalR
                await _hubContext.Clients.Group($"conv:{request.ConversationId}").SendAsync("MessageCreated", message);
                await _hubContext.Clients.Group("admins").SendAsync("MessageCreated", message);
                await _hubContext.Clients.Group($"user:{conversation.CustomerUserId}").SendAsync("MessageCreated", message);

                await _hubContext.Clients.Group("admins").SendAsync("ConversationUpserted", conversationDto);
                await _hubContext.Clients.Group($"user:{conversation.CustomerUserId}").SendAsync("ConversationUpserted", conversationDto);
                await _hubContext.Clients.Group($"conv:{conversation.Id}").SendAsync("ConversationUpserted", conversationDto);
            }

            return Ok(message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt by user {UserId} to conversation {ConversationId}", userId, request.ConversationId);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
    }

    [HttpPost("conversations/{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var isAdmin = User.IsInRole("Admin");

        try
        {
            await _chatService.MarkConversationAsReadAsync(id, userId, isAdmin);

            // Get updated conversation for broadcast
            var conversation = await _chatService.GetConversationByIdAsync(id, userId, isAdmin);
            if (conversation != null)
            {
                var conversationDto = new ConversationListItemDto(
                    conversation.Id,
                    conversation.CustomerUserId,
                    conversation.CustomerDisplayName,
                    conversation.CreatedAt,
                    conversation.LastMessageAt,
                    "", // We don't have the last message text here, but it doesn't matter for unread updates
                    SenderType.Customer, // Placeholder
                    conversation.UnreadForAdminCount,
                    conversation.UnreadForCustomerCount,
                    conversation.IsOpen
                );

                // Broadcast updated unread counts
                await _hubContext.Clients.Group("admins").SendAsync("ConversationUpserted", conversationDto);
                await _hubContext.Clients.Group($"user:{conversation.CustomerUserId}").SendAsync("ConversationUpserted", conversationDto);
            }

            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt by user {UserId} to conversation {ConversationId}", userId, id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
    }
}
