using System.Diagnostics.Eventing.Reader;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Extensions;

namespace Ticketing.Api.Services;

public interface IAdminService
{
    public Task<List<TicketListItem>> GetTicketListItemsAsync(GetTicketsRequest request);
    public Task<Ticket?> UpdateTicketStatusAsync(Guid id, TicketStatus status);
    public Task<bool> DeleteByIdAsync(Guid id);
    public Task<int> DeleteTicketsForUserAsync(string userId);
    public Task<List<UserListItem>> GetAllUsersAsync();
    public Task<string> AssignTicketToAdminAsync(Guid id, string? adminId);
    public Task<List<UserListItem>> GetAllAdminsAsync();
    public Task<bool> UnAssignTicketToAdminAsync(Guid id);
    public Task<List<TicketDetails>> GetAssignMeTickets(string? me);
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotificationService _notificationService;

    public AdminService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminService> logger,
        IHttpContextAccessor httpContextAccessor,
        RoleManager<IdentityRole> roleManager
,
        INotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _notificationService = notificationService;
    }

    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var (adminId, adminName) = _httpContextAccessor.GetCurrentUserInfo();
        
        _logger.LogInformation(
            "Admin {AdminName} (ID: {AdminId}) attempting to delete ticket with ID: {TicketId}",
            adminName,
            adminId,
            id
        );

        try
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null)
            {
                _logger.LogWarning(
                    "Admin {AdminName} (ID: {AdminId}) attempted to delete non-existent ticket with ID: {TicketId}",
                    adminName,
                    adminId,
                    id
                );
                return false;
            }

            var customerId = ticket.CustomerId;
            var ticketTitle = ticket.Title;

            _db.Tickets.Remove(ticket);
            await _db.SaveChangesAsync();

            // Notify customer that their ticket was deleted
            await _notificationService.CreateNotificationAsync(
                customerId,
                "Ticket Deleted",
                $"Ticket #TK-{ticket.TicketNumber} was deleted by {adminName}",
                null,
                null // No ticketId since it's deleted
            );

            _logger.LogInformation(
                "Admin {AdminName} (ID: {AdminId}) successfully deleted ticket with ID: {TicketId}, notification sent to customer {CustomerId}",
                adminName,
                adminId,
                id,
                customerId
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Admin {AdminName} (ID: {AdminId}) failed to delete ticket with ID: {TicketId}. Error: {ErrorMessage}",
                adminName,
                adminId,
                id,
                ex.Message
            );
            return false;
        }
    }

    public async Task<int> DeleteTicketsForUserAsync(string userId)
    {
        // Get the user display name from database
        var user = await _userManager.FindByIdAsync(userId);
        var displayName = user?.DisplayName ?? "Unknown";
        
        _logger.LogInformation("Attempting to delete tickets for user {DisplayName} (ID: {UserId})", displayName, userId);

        try
        {
            var deleted = await _db.Tickets.Where(t => t.CustomerId == userId).ExecuteDeleteAsync();

            _logger.LogInformation(
                "Successfully deleted {DeletedCount} tickets for user {DisplayName} (ID: {UserId})",
                deleted,
                displayName,
                userId
            );
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete tickets for user {DisplayName} (ID: {UserId}). Error: {ErrorMessage}",
                displayName,
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
            var statusName = status.ToString();
            await _notificationService.CreateNotificationAsync(
                ticket.CustomerId, 
                $"Ticket {statusName}",
                $"Ticket {ticket.GetFormattedTicketNumber()} marked as {statusName}",
                null,
                ticket.Id
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

    public async Task<List<TicketListItem>> GetTicketListItemsAsync(GetTicketsRequest request)
    {
        _logger.LogInformation(
            "Retrieving tickets - UserId: {UserId}, Status: {Status}, Category: {Category}, " +
            "SortBy: {SortBy}, Ascending: {Ascending}, Page: {Page}/{Size}",
            request.UserId,
            request.Status,
            request.Category,
            request.SortBy,
            request.Ascending,
            request.PageNumber,
            request.PageSize
        );

        try
        {
            var query = _db.Tickets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                query = query.Where(t => t.CustomerId == request.UserId);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(t => t.Status == request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(t => t.Category == request.Category);
            }

            query = ApplySorting(query, request.SortBy, request.Ascending);

            var items = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(t => new TicketListItem(
                    t.Id,
                    t.TicketNumber,
                    t.Title,
                    t.Category,
                    t.Status,
                    t.Priority,
                    t.CreatedAt,
                    t.UpdatedAt
                ))
                .ToListAsync();

            _logger.LogInformation(
                "Successfully retrieved {TicketCount} tickets",
                items.Count
            );

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve tickets - Error: {ErrorMessage}",
                ex.Message
            );
            return new List<TicketListItem>();
        }    }

    public async Task<string> AssignTicketToAdminAsync(Guid id, string? adminId)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null)
        {
            return null!;
        }

        ticket.AssignedAdminId = adminId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        // Notify the newly assigned admin
        if (!string.IsNullOrEmpty(adminId))
        {
            var admin = await _userManager.FindByIdAsync(adminId);
            await _notificationService.CreateNotificationAsync(
                adminId,
                "Ticket Assigned",
                $"Ticket {ticket.GetFormattedTicketNumber()} assigned to you",
                $"{ticket.Title}",
                ticket.Id
            );

            // Notify customer about assignment
            await _notificationService.CreateNotificationAsync(
                ticket.CustomerId,
                "Ticket Assigned",
                $"Ticket {ticket.GetFormattedTicketNumber()} assigned to {admin?.DisplayName ?? "an admin"}",
                null,
                ticket.Id
            );

            _logger.LogInformation(
                "Ticket {TicketId} assigned to admin {AdminId}, notifications sent to admin and customer {CustomerId}",
                ticket.Id,
                adminId,
                ticket.CustomerId
            );
        }

        return ticket.AssignedAdminId!;
    }

    public async Task<List<UserListItem>> GetAllAdminsAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        var mappedAdmins = admins
            .Select(x => new UserListItem(x.Id, x.Email!, x.UserName!, x.DisplayName))
            .ToList();
        return mappedAdmins;
    }

    public async Task<bool> UnAssignTicketToAdminAsync(Guid id)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null)
        {
            return false;
        }

        ticket.AssignedAdminId = null;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        // Notify customer that ticket is now unassigned
        await _notificationService.CreateNotificationAsync(
            ticket.CustomerId,
            "Ticket Unassigned",
            $"Ticket {ticket.GetFormattedTicketNumber()} is now unassigned",
            "Waiting for admin to pick up",
            ticket.Id
        );

        _logger.LogInformation(
            "Ticket {TicketId} unassigned, notification sent to customer {CustomerId}",
            ticket.Id,
            ticket.CustomerId
        );

        return true;
    }

    public async Task<List<TicketDetails>> GetAssignMeTickets(string? me)
    {
        var admin = me != null ? await _userManager.FindByIdAsync(me) : null;
        var adminName = admin?.DisplayName ?? "Unknown";
        
        _logger.LogInformation("Attempting to retrieve tickets assigned to admin {AdminName} (ID: {AdminId})", adminName, me);

        try
        {
            var tickets = await _db
                .Tickets
                .Include(t => t.Comments)
                    .ThenInclude(c => c.Author)
                .Where(t => t.AssignedAdminId == me)
                .OrderByDescending(t => t.UpdatedAt)
                .Select(t => new TicketDetails(
                    t.Id,
                    t.TicketNumber,
                    t.Title,
                    t.Description,
                    t.Category,
                    t.Status,
                    t.Priority,
                    t.CustomerId,
                    t.AssignedAdminId,
                    t.CreatedAt,
                    t.UpdatedAt,
                    t.Comments
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => new TicketCommentDto(
                            c.Id,
                            c.AuthorId,
                            c.Author!.Email ?? "",
                            c.Message,
                            c.CreatedAt
                        ))
                        .ToList()
                ))
                .ToListAsync();

            _logger.LogInformation(
                "Successfully retrieved {TicketCount} tickets assigned to admin {AdminName} (ID: {AdminId})",
                tickets.Count,
                adminName,
                me
            );

            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve tickets assigned to admin {AdminName} (ID: {AdminId}). Error: {ErrorMessage}",
                adminName,
                me,
                ex.Message
            );
            return new List<TicketDetails>();
        }
    }

    private IQueryable<Ticket> ApplySorting(
    IQueryable<Ticket> query,
    string? sortBy,
    bool ascending)
    {
        HashSet<string> AllowedSortColumns =
        [
            "Title",
            "Description",
            "Category",
            "Status",
            "Priority",
            "UpdatedAt"
        ];

        var column = string.IsNullOrWhiteSpace(sortBy)
            ? "UpdatedAt"
            : sortBy;

        if (!AllowedSortColumns.Contains(column))
            column = "UpdatedAt";

        return ascending
            ? query.OrderBy(t => EF.Property<object>(t, column))
            : query.OrderByDescending(t => EF.Property<object>(t, column));
    }
}
