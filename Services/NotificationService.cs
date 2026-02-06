using Microsoft.AspNetCore.SignalR;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Hubs;

namespace Ticketing.Api.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(string userId, string title, string subtitle, string? message = null, Guid? ticketId = null);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<NotificationDto> CreateNotificationAsync(string userId, string title, string subtitle, string? message = null, Guid? ticketId = null)
    {
        var entity = new Notification
        {
            UserId = userId,
            Title = title,
            Subtitle = subtitle,
            Message = message,
            TicketId = ticketId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync();

        var dto = new NotificationDto(
          entity.Id,
          entity.Title,
          entity.Subtitle,
          entity.Message,
          entity.CreatedAtUtc.UtcDateTime,
          IsRead: entity.ReadAtUtc != null
      );
        await _hub.Clients.User(userId).SendAsync("notificationCreated", dto);

        return dto;
    }
}

