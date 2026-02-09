using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketsService _ticketsService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TicketsController(ITicketsService ticketsService, UserManager<ApplicationUser> userManager)
    {
        _ticketsService = ticketsService;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<ActionResult<TicketDetails>> Create([FromBody] CreateTicketRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();


        var ticket = await _ticketsService.AddTicketAsync(req, user.Id);

        return await GetById(ticket.Id);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<TicketListItem>>> MyTickets()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var items = await _ticketsService.GetMyTicketItemsAsync(user.Id);

        return items;
    }

    [HttpGet("my/statistics")]
    public async Task<ActionResult<TicketsStatistics>> MyStatistics()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var statistics = await _ticketsService.GetTicketsStatisticsAsync(user.Id);

        return statistics;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDetails>> GetById(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized("Please login before taking any action.");

        var isAdmin = User.IsInRole("Admin");

        var ticket = await _ticketsService.GetTicketByIdFilteredAsync(id);

        if (ticket is null)
            return NotFound();
        if (!isAdmin && ticket.CustomerId != user.Id)
            return Forbid();

        var details = new TicketDetails(
            ticket.Id,
            ticket.TicketNumber,
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

        var ticket = await _ticketsService.GetTicketByIdAsync(id);
        if (ticket is null)
            return NotFound();
        if (ticket.CustomerId != user.Id && !User.IsInRole("Admin"))
            return Forbid();

        await _ticketsService.UpdateTicketAsync(req, ticket);
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

        var ticket = await _ticketsService.GetTicketByIdAsync(id);
        if (ticket is null)
            return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && ticket.CustomerId != user.Id)
            return Forbid();

        await _ticketsService.AddCommentAsync(req, ticket, user.Id);

        return await GetById(id);
    }
}
