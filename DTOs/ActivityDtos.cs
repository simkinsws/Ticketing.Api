namespace Ticketing.Api.DTOs;

public record UserActivityDto(
    Guid Id,
    string UserId,
    string UserName,
    string UserDisplayName,
    string Category,
    string Title,
    string Description,
    string EntityType,
    string? EntityId,
    string RelativeTime,
    DateTimeOffset CreatedAt
);

public record ActivityFilterRequest(
    int Page = 1,
    int PageSize = 20,
    string? Category = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? UserId = null
);

public record ActivityPagedResponse(
    List<UserActivityDto> Activities,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
