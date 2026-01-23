using Ticketing.Api.Enums;

namespace Ticketing.Api.DTOs;

// Request DTOs
public record SendMessageRequest(Guid ConversationId, string Text);

// Response DTOs
public record ConversationListItemDto(
    Guid Id,
    string CustomerUserId,
    string CustomerDisplayName,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    string LastMessagePreview,
    SenderType LastMessageSender,
    int UnreadForAdminCount,
    int UnreadForCustomerCount,
    bool IsOpen
);

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    SenderType SenderType,
    string SenderUserId,
    string Text,
    DateTime CreatedAt
);

public record ConversationDetailDto(
    Guid Id,
    string CustomerUserId,
    string CustomerDisplayName,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int UnreadForAdminCount,
    int UnreadForCustomerCount,
    bool IsOpen
);

public record OpenConversationResponse(Guid ConversationId);
