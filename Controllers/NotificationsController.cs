using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _service;

    public NotificationsController(AppDbContext db, INotificationService service)
    {
        _db = db;
        _service = service;
    }

    private string GetUserId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("Missing user id claim.");

    [HttpPost("me")]
    public async Task<IActionResult> CreateForMe([FromBody] CreateNotificationRequest req)
    {
        var dto = await _service.CreateNotificationAsync(GetUserId(), req.Title, req.Message);
        return Ok(dto);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = GetUserId();
        var count = await _db.Notifications.CountAsync(n =>
            n.UserId == userId && n.ReadAtUtc == null
        );
        return Ok(new { count });
    }

    [HttpGet("latest")]
    public async Task<IActionResult> Latest([FromQuery] int take = 20)
    {
        take = Math.Clamp(take, 1, 100);

        var userId = GetUserId();

        var items = await _db
            .Notifications.Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => new NotificationDto(
                n.Id,
                n.Title,
                n.Message,
                n.CreatedAtUtc.UtcDateTime,
                n.ReadAtUtc != null
            ))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = GetUserId();

        var entity = await _db.Notifications.SingleOrDefaultAsync(n =>
            n.Id == id && n.UserId == userId
        );
        if (entity is null)
            return NotFound();

        if (entity.ReadAtUtc is null)
        {
            entity.ReadAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok();
    }
}
