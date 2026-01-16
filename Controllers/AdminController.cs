using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
        [FromQuery] GetTicketsRequest request
    )
    {
        var items = await _adminService.GetTicketListItemsAsync(request);

        return items;
    }

    [HttpPatch("tickets/{id:guid}/status")]
    public async Task<ActionResult<TicketDetails>> UpdateStatus(
        Guid id,
        [FromBody] TicketStatus status
    )
    {
        var ticket = await _adminService.UpdateTicketStatusAsync(id, status);

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

    [HttpPatch("tickets/{id:guid}/assign")]
    public async Task<IActionResult> AssignTicketToAdmin(Guid id, [FromQuery] string? adminId)
    {
        var assignedTicket = await _adminService.AssignTicketToAdminAsync(id, adminId);
        if (assignedTicket is null)
        {
            return NotFound(new { Message = $"ticket with {id} not exists.", Success = false });
        }
        return Ok(new { Message = $"Admin {adminId} has been assigned to ticket {id}", Success = true });
    }

    [HttpPatch("tickets/{id:guid}/unassign")]
    public async Task<IActionResult> UnAssignTicketFromAdmin(Guid id)
    {
        var unAssignedTicket = await _adminService.UnAssignTicketToAdminAsync(id);
        if (!unAssignedTicket)
        {
            return NotFound(new { Message = $"ticket with {id} not exists.", Success = false });
        }
        return Ok(new { Message = $"Ticket {id} has been unassigned", Success = true });
    }

    [HttpPatch("tickets/assigned-to-me")]
    public async Task<ActionResult<List<TicketDetails>>> GetAssignedMeTickets()
    {
        var me = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var assignedMeTicket = await _adminService.GetAssignMeTickets(me);
        return Ok(assignedMeTicket);
    }

    [HttpGet("admins")]
    public async Task<ActionResult<List<UserListItem>>> GetAllAdmins()
    {
        var admins = await _adminService.GetAllAdminsAsync();
        return Ok(admins);
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<UserListItem>>> Users()
    {
        var users = await _adminService.GetAllUsersAsync();

        return Ok(users);
    }
}
