using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TicketsController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<ActionResult<TicketDetails>> Create([FromBody] CreateTicketRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var ticket = new Ticket
        {
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category.Trim(),
            Priority = req.Priority,
            Status = TicketStatus.Open,
            CustomerId = user.Id,
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return await GetById(ticket.Id);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<TicketListItem>>> MyTickets()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var items = await _db
            .Tickets.Where(t => t.CustomerId == user.Id)
            .OrderByDescending(t => t.UpdatedAt)
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDetails>> GetById(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized("Please login before taking any action.");

        var isAdmin = User.IsInRole("Admin");

        var ticket = await _db
            .Tickets.Include(t => t.Comments)
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
            return NotFound();
        if (!isAdmin && ticket.CustomerId != user.Id)
            return Forbid();

        var details = new TicketDetails(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Category,
            ticket.Status,
            ticket.Priority,
            ticket.CustomerId,
            ticket.AssignedAdminId,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket
                .Comments.OrderByDescending(c => c.CreatedAt)
                .Select(c => new TicketCommentDto(
                    c.Id,
                    c.AuthorId,
                    c.Author?.Email ?? "",
                    c.Message,
                    c.CreatedAt
                ))
                .ToList()
        );

        return details;
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketDetails>> Update(
        Guid id,
        [FromBody] UpdateTicketRequest req
    )
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
            return NotFound();
        if (ticket.CustomerId != user.Id && !User.IsInRole("Admin"))
            return Forbid();

        ticket.Title = req.Title.Trim();
        ticket.Description = req.Description.Trim();
        ticket.Category = string.IsNullOrWhiteSpace(req.Category)
            ? ticket.Category
            : req.Category.Trim();
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return await GetById(id);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<TicketDetails>> AddComment(
        Guid id,
        [FromBody] AddCommentRequest req
    )
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
            return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && ticket.CustomerId != user.Id)
            return Forbid();

        _db.TicketComments.Add(
            new TicketComment
            {
                TicketId = ticket.Id,
                AuthorId = user.Id,
                Message = req.Message.Trim(),
            }
        );

        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return await GetById(id);
    }
}
