using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Services;
public interface IAdminService
{
    public Task<List<TicketListItem>> GetTicketListItemsAsync(string? userId, TicketStatus? status, string? category);
    public Task<Ticket?> UpdateTicketStatusAsync(Guid id, TicketStatus status);
    public Task<bool> DeleteByIdAsync(Guid id);
    public Task<int> DeleteTicketsForUserAsync(string userId);
    public Task<List<UserListItem>> GetAllUsersAsync();
}
public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return false;
        }
        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
        return true;

    }

    public async Task<int> DeleteTicketsForUserAsync(string userId)
    {
        var deleted = await _db.Tickets
            .Where(t => t.CustomerId == userId)
            .ExecuteDeleteAsync();

        return deleted;
    }

    public async Task<List<UserListItem>> GetAllUsersAsync()
    {
        var users = await _userManager
                    .Users.OrderBy(u => u.Email)
                    .Select(u => new UserListItem(u.Id, u.Email!, u.UserName!, u.DisplayName))
                    .ToListAsync();

        return users;
    }

    public async Task<Ticket?> UpdateTicketStatusAsync(Guid id, TicketStatus status)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return null;
        }

        ticket.Status = status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return ticket;
    }

    public async Task<List<TicketListItem>> GetTicketListItemsAsync(string? userId, TicketStatus? status, string? category)
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

    
}
