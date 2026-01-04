using Ticketing.Api.Domain;

namespace Ticketing.Api.DTOs;

public record CreateTicketRequest(
    string Title,
    string Description,
    string Category,
    TicketPriority Priority
);

public record UpdateTicketRequest(
    string Title,
    string Description,
    string Category
);

public record AddCommentRequest(string Message);

public record TicketListItem(
    Guid Id,
    string Title,
    string Category,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record TicketCommentDto(
    Guid Id,
    string AuthorId,
    string AuthorEmail,
    string Message,
    DateTimeOffset CreatedAt
);

public record TicketDetails(
    Guid Id,
    string Title,
    string Description,
    string Category,
    TicketStatus Status,
    TicketPriority Priority,
    string CustomerId,
    string? AssignedAdminId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TicketCommentDto> Comments
);
