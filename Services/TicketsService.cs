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

    public TicketsService(AppDbContext db)
    {
        _db = db;
    }
    public async Task<Ticket?> GetTicketByIdAsync(Guid id)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        return ticket;
    }
    public async Task<Ticket> AddTicketAsync(CreateTicketRequest req, string userId)
    {
        var ticket = new Ticket
        {
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category.Trim(),
            Priority = req.Priority,
            Status = TicketStatus.Open,
            CustomerId = userId,
        };

    
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;

    }

    public async Task<List<TicketListItem>> GetMyTicketItemsAsync(string userId)
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

        return items;
    }

    public async Task<Ticket?> GetTicketByIdFilteredAsync(Guid id)
    {
        var ticket = await _db
          .Tickets.Include(t => t.Comments)
              .ThenInclude(c => c.Author)
          .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null) return null;

        return ticket;
    }

    public async Task UpdateTicketAsync(UpdateTicketRequest request,Ticket ticket)
    {
        
        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Category = string.IsNullOrWhiteSpace(request.Category)
            ? ticket.Category
            : request.Category.Trim();
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task AddCommentAsync(AddCommentRequest request, Ticket ticket, string userId)
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

    }
}
