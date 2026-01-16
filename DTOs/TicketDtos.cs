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

/// <summary>
/// Request parameters for filtering, sorting, and paginating tickets.
/// </summary>
public record GetTicketsRequest
{
    /// <summary>
    /// Filter by customer user ID
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Filter by ticket category
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Filter by ticket status (0=Open, 1=InProgress, 2=Resolved, 3=Closed)
    /// </summary>
    public TicketStatus? Status { get; init; }
    
    /// <summary>
    /// Sort column name (Title, Category, Status, Priority, CreatedAt, UpdatedAt)
    /// </summary>
    public string SortBy { get; init; } = "UpdatedAt";
    
    /// <summary>
    /// Sort direction (true=ascending, false=descending)
    /// </summary>
    public bool Ascending { get; init; } = false;
    
    /// <summary>
    /// Number of items per page (1-100)
    /// </summary>
    public int PageSize { get; init; } = 10;
    
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;
}
