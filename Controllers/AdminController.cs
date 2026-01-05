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
[Route("admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("tickets")]
    public async Task<ActionResult<List<TicketListItem>>> AllTickets(
        [FromQuery] string? userId,
        [FromQuery] TicketStatus? status,
        [FromQuery] string? category
    )
    {
        var items = await _adminService.GetTicketListItemsAsync(userId,status,category);

        return items;
    }

    [HttpPatch("tickets/{id:guid}/status")]
    public async Task<ActionResult<TicketDetails>> UpdateStatus(
        Guid id,
        [FromBody] TicketStatus status
    )
    {
        var ticket = await _adminService.GetTicketByIdAsync(id, status);

        if (ticket is null)
        {
            return NotFound($"ticket with {id} not exists.");
        }

        return Ok(ticket);
    }

    [HttpDelete("tickets/{id:guid}")]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var deletedTicket = await _adminService.DeleteByIdAsync(id);
        if (!deletedTicket)
        {
            return NotFound();
        }

        return Ok(new
        {
            StatusCode=204
        });
    }

    [HttpDelete("tickets/user/{userId}")]
    public async Task<IActionResult> DeleteTicketsForUser(string userId)
    {
        var deletedCount = await _adminService.DeleteTicketsForUserAsync(userId);

        if (deletedCount == 0)
        {
            return NotFound();
        }
        
        return Ok(new
        {
            deletedCount, StatusCode=204
        });
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<UserListItem>>> Users()
    {
        var users = await _adminService.GetAllUsersAsync();

        return Ok(users);
    }
}
