using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Services;

public interface IAdminService
{
    public Task<List<TicketListItem>> GetTicketListItemsAsync(
        string? userId,
        TicketStatus? status,
        string? category
    );
    public Task<Ticket?> UpdateTicketStatusAsync(Guid id, TicketStatus status);
    public Task<bool> DeleteByIdAsync(Guid id);
    public Task<int> DeleteTicketsForUserAsync(string userId);
    public Task<List<UserListItem>> GetAllUsersAsync();
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminService> logger,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var adminName =
            _httpContextAccessor.HttpContext?.User?.FindFirst("displayName")?.Value ?? "UnknownAdmin";
        _logger.LogInformation(
            "Admin {adminName} attempting to delete ticket with ID: {TicketId}",
            adminName,
            id
        );

        try
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null)
            {
                _logger.LogWarning(
                    "Admin {adminName} attempted to delete non-existent ticket with ID: {TicketId}",
                    adminName,
                    id
                );
                return false;
            }

            _db.Tickets.Remove(ticket);
            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Admin {adminName} successfully deleted ticket with ID: {TicketId}",
                adminName,
                id
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Admin {adminName} failed to delete ticket with ID: {TicketId}. Error: {ErrorMessage}",
                adminName,
                id,
                ex.Message
            );
            return false;
        }
    }

    public async Task<int> DeleteTicketsForUserAsync(string userId)
    {
        _logger.LogInformation("Attempting to delete tickets for user with ID: {UserId}", userId);

        try
        {
            var deleted = await _db.Tickets.Where(t => t.CustomerId == userId).ExecuteDeleteAsync();

            _logger.LogInformation(
                "Successfully deleted {DeletedCount} tickets for user with ID: {UserId}",
                deleted,
                userId
            );
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete tickets for user with ID: {UserId}. Error: {ErrorMessage}",
                userId,
                ex.Message
            );
            return 0;
        }
    }

    public async Task<List<UserListItem>> GetAllUsersAsync()
    {
        _logger.LogInformation("Attempting to retrieve all users");

        try
        {
            var users = await _userManager
                .Users.OrderBy(u => u.Email)
                .Select(u => new UserListItem(u.Id, u.Email!, u.UserName!, u.DisplayName))
                .ToListAsync();

            _logger.LogInformation("Successfully retrieved {UserCount} users", users.Count);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve users. Error: {ErrorMessage}", ex.Message);
            return new List<UserListItem>();
        }
    }

    public async Task<Ticket?> UpdateTicketStatusAsync(Guid id, TicketStatus status)
    {
        _logger.LogInformation(
            "Attempting to update status of ticket with ID: {TicketId} to {Status}",
            id,
            status
        );

        try
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);

            if (ticket is null)
            {
                _logger.LogWarning(
                    "Attempted to update non-existent ticket with ID: {TicketId}",
                    id
                );
                return null;
            }

            ticket.Status = status;
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Successfully updated status of ticket with ID: {TicketId} to {Status}",
                id,
                status
            );
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update status of ticket with ID: {TicketId} to {Status}. Error: {ErrorMessage}",
                id,
                status,
                ex.Message
            );
            return null;
        }
    }

    public async Task<List<TicketListItem>> GetTicketListItemsAsync(
        string? userId,
        TicketStatus? status,
        string? category
    )
    {
        _logger.LogInformation(
            "Attempting to retrieve tickets with filters - UserId: {UserId}, Status: {Status}, Category: {Category}",
            userId,
            status,
            category
        );

        try
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

            _logger.LogInformation(
                "Successfully retrieved {TicketCount} tickets with filters - UserId: {UserId}, Status: {Status}, Category: {Category}",
                items.Count,
                userId,
                status,
                category
            );
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve tickets with filters - UserId: {UserId}, Status: {Status}, Category: {Category}. Error: {ErrorMessage}",
                userId,
                status,
                category,
                ex.Message
            );
            return new List<TicketListItem>();
        }
    }
}
