using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Enums;

namespace Ticketing.Api.Services;

public interface ISupportChatService
{
    Task<Guid> OpenConversationAsync(string customerUserId, string customerDisplayName);
    Task<ConversationListItemDto[]> GetAdminInboxAsync();
    Task<ConversationDetailDto?> GetConversationByIdAsync(Guid conversationId, string requestingUserId, bool isAdmin);
    Task<MessageDto[]> GetConversationMessagesAsync(Guid conversationId, string requestingUserId, bool isAdmin);
    Task<MessageDto> SendMessageAsync(Guid conversationId, string senderUserId, SenderType senderType, string text);
    Task MarkConversationAsReadAsync(Guid conversationId, string userId, bool isAdmin);
}

public class SupportChatService : ISupportChatService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SupportChatService> _logger;

    public SupportChatService(AppDbContext context, ILogger<SupportChatService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> OpenConversationAsync(string customerUserId, string customerDisplayName)
    {
        // Check if customer already has an open conversation
        var existing = await _context.Conversations
            .Where(c => c.CustomerUserId == customerUserId && c.IsOpen)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            _logger.LogInformation("Returning existing conversation {ConversationId} for customer {CustomerId}", existing.Id, customerUserId);
            return existing.Id;
        }

        // Create new conversation
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CustomerUserId = customerUserId,
            CustomerDisplayName = customerDisplayName,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            LastMessagePreview = "Conversation started",
            LastMessageSender = SenderType.Customer,
            UnreadForAdminCount = 0,
            UnreadForCustomerCount = 0,
            IsOpen = true
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new conversation {ConversationId} for customer {CustomerId}", conversation.Id, customerUserId);
        return conversation.Id;
    }

    public async Task<ConversationListItemDto[]> GetAdminInboxAsync()
    {
        var conversations = await _context.Conversations
            .Where(c => c.IsOpen)
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new ConversationListItemDto(
                c.Id,
                c.CustomerUserId,
                c.CustomerDisplayName,
                c.CreatedAt,
                c.LastMessageAt,
                c.LastMessagePreview,
                c.LastMessageSender,
                c.UnreadForAdminCount,
                c.UnreadForCustomerCount,
                c.IsOpen
            ))
            .ToArrayAsync();

        return conversations;
    }

    public async Task<ConversationDetailDto?> GetConversationByIdAsync(Guid conversationId, string requestingUserId, bool isAdmin)
    {
        var conversation = await _context.Conversations
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync();

        if (conversation == null)
        {
            return null;
        }

        // Authorization check
        if (!isAdmin && conversation.CustomerUserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("You don't have access to this conversation");
        }

        return new ConversationDetailDto(
            conversation.Id,
            conversation.CustomerUserId,
            conversation.CustomerDisplayName,
            conversation.CreatedAt,
            conversation.LastMessageAt,
            conversation.UnreadForAdminCount,
            conversation.UnreadForCustomerCount,
            conversation.IsOpen
        );
    }

    public async Task<MessageDto[]> GetConversationMessagesAsync(Guid conversationId, string requestingUserId, bool isAdmin)
    {
        var conversation = await _context.Conversations
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync();

        if (conversation == null)
        {
            throw new InvalidOperationException("Conversation not found");
        }

        // Authorization check
        if (!isAdmin && conversation.CustomerUserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("You don't have access to this conversation");
        }

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageDto(
                m.Id,
                m.ConversationId,
                m.SenderType,
                m.SenderUserId,
                m.Text,
                m.CreatedAt
            ))
            .ToArrayAsync();

        return messages;
    }

    public async Task<MessageDto> SendMessageAsync(Guid conversationId, string senderUserId, SenderType senderType, string text)
    {
        var conversation = await _context.Conversations
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync();

        if (conversation == null)
        {
            throw new InvalidOperationException("Conversation not found");
        }

        // Authorization check
        if (senderType == SenderType.Customer && conversation.CustomerUserId != senderUserId)
        {
            throw new UnauthorizedAccessException("You don't have access to this conversation");
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderType = senderType,
            SenderUserId = senderUserId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);

        // Update conversation
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessagePreview = text.Length > 200 ? text.Substring(0, 200) : text;
        conversation.LastMessageSender = senderType;

        // Update unread counts
        if (senderType == SenderType.Customer)
        {
            conversation.UnreadForAdminCount++;
        }
        else
        {
            conversation.UnreadForCustomerCount++;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Message {MessageId} sent in conversation {ConversationId} by {SenderType}", message.Id, conversationId, senderType);

        return new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderType,
            message.SenderUserId,
            message.Text,
            message.CreatedAt
        );
    }

    public async Task MarkConversationAsReadAsync(Guid conversationId, string userId, bool isAdmin)
    {
        var conversation = await _context.Conversations
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync();

        if (conversation == null)
        {
            throw new InvalidOperationException("Conversation not found");
        }

        // Authorization check
        if (!isAdmin && conversation.CustomerUserId != userId)
        {
            throw new UnauthorizedAccessException("You don't have access to this conversation");
        }

        if (isAdmin)
        {
            conversation.UnreadForAdminCount = 0;
        }
        else
        {
            conversation.UnreadForCustomerCount = 0;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Conversation {ConversationId} marked as read by {UserType}", conversationId, isAdmin ? "Admin" : "Customer");
    }
}
