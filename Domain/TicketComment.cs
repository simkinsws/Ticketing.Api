namespace Ticketing.Api.Domain;

public class TicketComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }

    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
