using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Services;

public interface ITicketsService
{
    public Task<Ticket> AddTicketAsync(CreateTicketRequest req, string userId);
    public Task<Ticket?> GetTicketByIdFilteredAsync(Guid id);
    public Task<List<TicketListItem>> GetMyTicketItemsAsync(string userId);
    public Task<Ticket?> GetTicketByIdAsync(Guid id);
    public Task UpdateTicketAsync(UpdateTicketRequest request, Ticket ticket);
    public Task AddCommentAsync(AddCommentRequest request, Ticket ticket, string userId);
}

public class TicketsService : ITicketsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TicketsService> _logger;

    public TicketsService(AppDbContext db, ILogger<TicketsService> logger)
    {
        _db = db;
        _logger = logger;
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
        _logger.LogInformation(
            "Attempting to create new ticket for user with ID: {UserId}. Title: {Title}, Category: {Category}, Priority: {Priority}",
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
                "Successfully created ticket with ID: {TicketId} for user with ID: {UserId}",
                ticket.Id,
                userId
            );
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create ticket for user with ID: {UserId}. Error: {ErrorMessage}",
                userId,
                ex.Message
            );
            throw;
        }
    }

    public async Task<List<TicketListItem>> GetMyTicketItemsAsync(string userId)
    {
        _logger.LogInformation("Attempting to retrieve tickets for user with ID: {UserId}", userId);

        try
        {
            var items = await _db
                .Tickets.Where(t => t.CustomerId == userId)
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

            _logger.LogInformation(
                "Successfully retrieved {TicketCount} tickets for user with ID: {UserId}",
                items.Count,
                userId
            );
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve tickets for user with ID: {UserId}. Error: {ErrorMessage}",
                userId,
                ex.Message
            );
            return new List<TicketListItem>();
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
        _logger.LogInformation(
            "Attempting to add comment to ticket with ID: {TicketId} by user with ID: {UserId}",
            ticket.Id,
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
                "Successfully added comment to ticket with ID: {TicketId} by user with ID: {UserId}",
                ticket.Id,
                userId
            );
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
