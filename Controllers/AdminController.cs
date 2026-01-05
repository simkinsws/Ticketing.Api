using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> log
    )
    {
        _db = db;
        _userManager = userManager;
        _logger = log;
    }

    [HttpGet("tickets")]
    public async Task<ActionResult<List<TicketListItem>>> AllTickets(
        [FromQuery] string? userId,
        [FromQuery] TicketStatus? status,
        [FromQuery] string? category
    )
    {
        _logger.LogInformation(
            "Admin requested tickets list. Filters: userId={UserId}, status={Status}, category={Category}",
            userId,
            status,
            category
        );
        var q = _db.Tickets.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
            q = q.Where(t => t.CustomerId == userId);

        if (status is not null)
            q = q.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(t => t.Category == category);

        var items = await q.OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TicketListItem(
                t.Id,
                t.Title,
                t.Category,
                t.Status,
                t.Priority,
                t.CreatedAt,
                t.UpdatedAt
            ))
            .ToListAsync();
        //TODO: Will move to Service in future.
        _logger.LogInformation("Tickets list returned {Count} items.", items.Count);

        return items;
    }

    [HttpPatch("tickets/{id:guid}/status")]
    public async Task<ActionResult<TicketDetails>> UpdateStatus(
        Guid id,
        [FromBody] TicketStatus status
    )
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
            return NotFound($"ticket with {id} not exists.");

        ticket.Status = status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ticket);
    }

    [HttpDelete("tickets/{id:guid}")]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
            return NotFound();

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
        //TODO : Return some info about deleted ticket
        return NoContent();
    }

    [HttpDelete("tickets/user/{userId}")]
    public async Task<IActionResult> DeleteTicketsForUser(string userId)
    {
        var tickets = await _db.Tickets.Where(t => t.CustomerId == userId).ToListAsync();
        _db.Tickets.RemoveRange(tickets);
        await _db.SaveChangesAsync();
        //TODO : Return number of deleted tickets or there titiles to show what was deleted
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<UserListItem>>> Users()
    {
        //TODO: Will move to Service in future.
        _logger.LogInformation("Admin requested users list.");

        var users = await _userManager
            .Users.OrderBy(u => u.Email)
            .Select(u => new UserListItem(u.Id, u.Email!, u.UserName!, u.DisplayName))
            .ToListAsync();
        //TODO: Will move to Service in future.
        _logger.LogInformation(
            "Admin fetched users list. Count={Count}, Users={Users}",
            users.Count,
            users.Select(u => new { u.Id, u.Email })
        );

        return users;
    }
}
