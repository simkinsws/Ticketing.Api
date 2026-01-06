namespace Ticketing.Api.DTOs;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    DateTime CreatedAtUtc,
    bool IsRead
);

public record CreateNotificationRequest(string Title, string Message);
