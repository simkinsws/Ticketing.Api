namespace Ticketing.Api.DTOs;

public record NotificationDto(
    Guid Id,
    string Title,
    string Subtitle,
    string? Message,
    DateTime CreatedAtUtc,
    bool IsRead,
    Guid? TicketId
);

public record CreateNotificationRequest(string Title, string Subtitle, string? Message);
