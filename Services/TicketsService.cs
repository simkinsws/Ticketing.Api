using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Extensions;

namespace Ticketing.Api.Services;

public interface ITicketsService
{
    public Task<Ticket> AddTicketAsync(CreateTicketRequest req, string userId);
    public Task<Ticket?> GetTicketByIdFilteredAsync(Guid id);
    public Task<List<TicketListItem>> GetMyTicketItemsAsync(string userId);
    public Task<Ticket?> GetTicketByIdAsync(Guid id);
    public Task UpdateTicketAsync(UpdateTicketRequest request, Ticket ticket);
    public Task AddCommentAsync(AddCommentRequest request, Ticket ticket, string userId);
    public Task<TicketsStatistics> GetTicketsStatisticsAsync(string? userId = null);
}

public class TicketsService : ITicketsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TicketsService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TicketsService(
        AppDbContext db, 
        ILogger<TicketsService> logger, 
        IHttpContextAccessor httpContextAccessor,
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<Ticket?> GetTicketByIdAsync(Guid id)
    {
        _logger.LogInformation("Attempting to retrieve ticket with ID: {TicketId}", id);

        try
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);

            if (ticket is null)
            {
                _logger.LogWarning("Ticket with ID: {TicketId} not found", id);
                return null;
            }

            _logger.LogInformation("Successfully retrieved ticket with ID: {TicketId}", id);
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve ticket with ID: {TicketId}. Error: {ErrorMessage}",
                id,
                ex.Message
            );
            return null;
        }
    }

    public async Task<Ticket> AddTicketAsync(CreateTicketRequest req, string userId)
    {
        var displayName = _httpContextAccessor.GetCurrentUserDisplayName();
        
        _logger.LogInformation(
            "Attempting to create new ticket for user {DisplayName} (ID: {UserId}). Title: {Title}, Category: {Category}, Priority: {Priority}",
            displayName,
            userId,
            req.Title,
            req.Category,
            req.Priority
        );

        try
        {
            var ticket = new Ticket
            {
                Title = req.Title.Trim(),
                Description = req.Description.Trim(),
                Category = string.IsNullOrWhiteSpace(req.Category)
                    ? "General"
                    : req.Category.Trim(),
                Priority = req.Priority,
                Status = TicketStatus.Open,
                CustomerId = userId,
            };

            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully created ticket with ID: {TicketId} for user {DisplayName} (ID: {UserId})",
                ticket.Id,
                displayName,
                userId
            );

            // Notify all admins about new ticket (excluding ticket creator if they're an admin)
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                // Skip notifying if the ticket creator is an admin (edge case)
                if (admin.Id == userId)
                    continue;

                await _notificationService.CreateNotificationAsync(
                    admin.Id,
                    "New Ticket",
                    $"{displayName} created ticket {ticket.GetFormattedTicketNumber()}",
                    $"{ticket.Title}",
                    ticket.Id
                );
            }

            _logger.LogInformation(
                "Notified {AdminCount} admins about new ticket {TicketId}",
                admins.Count(a => a.Id != userId),
                ticket.Id
            );

            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create ticket for user {DisplayName} (ID: {UserId}). Error: {ErrorMessage}",
                displayName,
                userId,
                ex.Message
            );
            throw;
        }
    }

    public async Task<List<TicketListItem>> GetMyTicketItemsAsync(string userId)
    {
        var displayName = _httpContextAccessor.GetCurrentUserDisplayName();
        
        _logger.LogInformation("Attempting to retrieve tickets for user {DisplayName} (ID: {UserId})", displayName, userId);

        try
        {
            var items = await _db
                .Tickets.Where(t => t.CustomerId == userId)
                .OrderByDescending(t => t.UpdatedAt)
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
                "Successfully retrieved {TicketCount} tickets for user {DisplayName} (ID: {UserId})",
                items.Count,
                displayName,
                userId
            );
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve tickets for user {DisplayName} (ID: {UserId}). Error: {ErrorMessage}",
                displayName,
                userId,
                ex.Message
            );
            return new List<TicketListItem>();
        }
    }

    public async Task<TicketsStatistics> GetTicketsStatisticsAsync(string? userId = null)
    {
        var displayName = _httpContextAccessor.GetCurrentUserDisplayName();

        if (userId != null)
        {
            _logger.LogInformation("Attempting to retrieve tickets statistics for user {DisplayName} (ID: {UserId})", displayName, userId);
        }
        else
        {
            _logger.LogInformation("Attempting to retrieve tickets statistics for all users (requested by admin {DisplayName})", displayName);
        }

        try
        {
            var ticketsQuery = userId != null 
                ? _db.Tickets.Where(t => t.CustomerId == userId)
                : _db.Tickets;
            
            var statusCounts = await ticketsQuery
                .GroupBy(t => t.Status)
                .Select(g => new 
                { 
                    Status = g.Key, 
                    Count = g.Count() 
                })
                .ToListAsync();

            var totalTickets = statusCounts.Sum(x => x.Count);

            var openTickets = statusCounts.FirstOrDefault(x => x.Status == TicketStatus.Open)?.Count ?? 0;
            var inProgressTickets = statusCounts.FirstOrDefault(x => x.Status == TicketStatus.InProgress)?.Count ?? 0;
            var resolvedTickets = statusCounts.FirstOrDefault(x => x.Status == TicketStatus.Resolved)?.Count ?? 0;
            var closedTickets = statusCounts.FirstOrDefault(x => x.Status == TicketStatus.Closed)?.Count ?? 0;

            var statistics = new TicketsStatistics(
                totalTickets,
                resolvedTickets,
                inProgressTickets,
                openTickets,
                closedTickets
            );

            if (userId != null)
            {
                _logger.LogInformation(
                    "Successfully retrieved statistics for user {DisplayName} (ID: {UserId}): Total={Total}, Open={Open}, InProgress={InProgress}, Resolved={Resolved}, Closed={Closed}",
                    displayName,
                    userId,
                    totalTickets,
                    openTickets,
                    inProgressTickets,
                    resolvedTickets,
                    closedTickets
                );
            }
            else
            {
                _logger.LogInformation(
                    "Successfully retrieved statistics for all users: Total={Total}, Open={Open}, InProgress={InProgress}, Resolved={Resolved}, Closed={Closed}",
                    totalTickets,
                    openTickets,
                    inProgressTickets,
                    resolvedTickets,
                    closedTickets
                );
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve tickets statistics for {Scope}. Error: {ErrorMessage}",
                userId != null ? $"user {userId}" : "all users",
                ex.Message
            );
            throw;
        }
    }

    public async Task<Ticket?> GetTicketByIdFilteredAsync(Guid id)
    {
        _logger.LogInformation(
            "Attempting to retrieve ticket with comments for ID: {TicketId}",
            id
        );

        try
        {
            var ticket = await _db
                .Tickets.Include(t => t.Comments)
                    .ThenInclude(c => c.Author)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket is null)
            {
                _logger.LogWarning("Ticket with ID: {TicketId} not found (with comments)", id);
                return null;
            }

            _logger.LogInformation(
                "Successfully retrieved ticket with ID: {TicketId} and {CommentCount} comments",
                id,
                ticket.Comments.Count
            );
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve ticket with comments for ID: {TicketId}. Error: {ErrorMessage}",
                id,
                ex.Message
            );
            return null;
        }
    }

    public async Task UpdateTicketAsync(UpdateTicketRequest request, Ticket ticket)
    {
        var displayName = _httpContextAccessor.GetCurrentUserDisplayName();
        var userId = _httpContextAccessor.GetCurrentUserId();
        
        _logger.LogInformation(
            "Attempting to update ticket with ID: {TicketId}. New Title: {Title}, Category: {Category}",
            ticket.Id,
            request.Title,
            request.Category
        );

        try
        {
            ticket.Title = request.Title.Trim();
            ticket.Description = request.Description.Trim();
            ticket.Category = string.IsNullOrWhiteSpace(request.Category)
                ? ticket.Category
                : request.Category.Trim();
            ticket.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Successfully updated ticket with ID: {TicketId}", ticket.Id);

            // Notify assigned admin if exists, otherwise notify all admins
            if (!string.IsNullOrEmpty(ticket.AssignedAdminId) && ticket.AssignedAdminId != userId)
            {
                // Notify the assigned admin
                await _notificationService.CreateNotificationAsync(
                    ticket.AssignedAdminId,
                    "Ticket Updated",
                    $"{displayName} updated ticket {ticket.GetFormattedTicketNumber()}",
                    null,
                    ticket.Id
                );

                _logger.LogInformation(
                    "Notified assigned admin {AdminId} about ticket update {TicketId}",
                    ticket.AssignedAdminId,
                    ticket.Id
                );
            }
            else if (string.IsNullOrEmpty(ticket.AssignedAdminId))
            {
                // No assigned admin - notify all admins EXCEPT the one who updated
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in admins)
                {
                    // Skip notifying the admin who made the update
                    if (admin.Id == userId)
                        continue;

                    await _notificationService.CreateNotificationAsync(
                        admin.Id,
                        "Ticket Updated",
                        $"{displayName} updated ticket {ticket.GetFormattedTicketNumber()}",
                        null,
                        ticket.Id
                    );
                }

                _logger.LogInformation(
                    "Notified {AdminCount} admins (excluding updater) about unassigned ticket update {TicketId}",
                    admins.Count(a => a.Id != userId),
                    ticket.Id
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update ticket with ID: {TicketId}. Error: {ErrorMessage}",
                ticket.Id,
                ex.Message
            );
            throw;
        }
    }

    public async Task AddCommentAsync(AddCommentRequest request, Ticket ticket, string userId)
    {
        var displayName = _httpContextAccessor.GetCurrentUserDisplayName();
        
        _logger.LogInformation(
            "Attempting to add comment to ticket with ID: {TicketId} by user {DisplayName} (ID: {UserId})",
            ticket.Id,
            displayName,
            userId
        );

        try
        {
            _db.TicketComments.Add(
                new TicketComment
                {
                    TicketId = ticket.Id,
                    AuthorId = userId,
                    Message = request.Message.Trim(),
                }
            );
            ticket.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully added comment to ticket with ID: {TicketId} by user {DisplayName} (ID: {UserId})",
                ticket.Id,
                displayName,
                userId
            );

            // Send notification to ticket owner (if commenter is not the owner)
            if (ticket.CustomerId != userId)
            {
                var commentPreview = request.Message.Length > 50 
                    ? $"\"{request.Message.Substring(0, 50)}...\"" 
                    : $"\"{request.Message}\"";
                    
                await _notificationService.CreateNotificationAsync(
                    ticket.CustomerId,
                    "New Comment",
                    $"{displayName} commented on {ticket.GetFormattedTicketNumber()}",
                    commentPreview,
                    ticket.Id
                );

                _logger.LogInformation(
                    "Notification sent to ticket owner {OwnerId} for new comment on ticket {TicketId}",
                    ticket.CustomerId,
                    ticket.Id
                );
            }

            // If ticket is assigned to an admin and admin is not the commenter, notify the admin
            // Otherwise, if no admin is assigned, notify all admins
            if (!string.IsNullOrEmpty(ticket.AssignedAdminId) && ticket.AssignedAdminId != userId)
            {
                // Notify the assigned admin
                var commentPreview = request.Message.Length > 50 
                    ? $"\"{request.Message.Substring(0, 50)}...\"" 
                    : $"\"{request.Message}\"";
                    
                await _notificationService.CreateNotificationAsync(
                    ticket.AssignedAdminId,
                    "New Comment",
                    $"{displayName} commented on {ticket.GetFormattedTicketNumber()}",
                    commentPreview,
                    ticket.Id
                );

                _logger.LogInformation(
                    "Notification sent to assigned admin {AdminId} for new comment on ticket {TicketId}",
                    ticket.AssignedAdminId,
                    ticket.Id
                );
            }
            else if (string.IsNullOrEmpty(ticket.AssignedAdminId) && ticket.CustomerId != userId)
            {
                // No assigned admin and commenter is not the customer - notify all admins EXCEPT the commenter
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var commentPreview = request.Message.Length > 50 
                    ? $"\"{request.Message.Substring(0, 50)}...\"" 
                    : $"\"{request.Message}\"";
                    
                foreach (var admin in admins)
                {
                    // Skip notifying the admin who made the comment
                    if (admin.Id == userId)
                        continue;

                    await _notificationService.CreateNotificationAsync(
                        admin.Id,
                        "New Comment",
                        $"{displayName} commented on {ticket.GetFormattedTicketNumber()}",
                        commentPreview,
                        ticket.Id
                    );
                }

                _logger.LogInformation(
                    "Notified {AdminCount} admins (excluding commenter) about comment on unassigned ticket {TicketId}",
                    admins.Count(a => a.Id != userId),
                    ticket.Id
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to add comment to ticket with ID: {TicketId}. Error: {ErrorMessage}",
                ticket.Id,
                ex.Message
            );
            throw;
        }
    }
}
