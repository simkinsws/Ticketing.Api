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

    public AdminController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("tickets")]
    public async Task<ActionResult<List<TicketListItem>>> AllTickets(
        [FromQuery] string? userId,
        [FromQuery] TicketStatus? status,
        [FromQuery] string? category
    )
    {
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
        var users = await _userManager
            .Users.OrderBy(u => u.Email)
            .Select(u => new UserListItem(u.Id, u.Email!, u.UserName!, u.DisplayName))
            .ToListAsync();

        return users;
    }
}
