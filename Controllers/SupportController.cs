using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Ticketing.Api.DTOs;
using Ticketing.Api.Extensions;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/support")]
[Authorize(Roles = "Customer,Admin")]
public class SupportController : ControllerBase
{
    private readonly ISupportChatService _chatService;
    private readonly ILogger<SupportController> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SupportController(ISupportChatService chatService, ILogger<SupportController> logger, IHttpContextAccessor httpContextAccessor)
    {
        _chatService = chatService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpPost("conversation/open")]
    public async Task<ActionResult<OpenConversationResponse>> OpenConversation()
    {
        var (userId, displayName) = _httpContextAccessor.GetCurrentUserInfo();
        if (string.IsNullOrEmpty(userId) || userId == "Unknown")
        {
            return Unauthorized("User ID not found in token");
        }

        _logger.LogInformation("User {DisplayName} (ID: {UserId}) opening conversation", displayName, userId);

        var (conversationId, unreadCount) = await _chatService.OpenConversationAsync(userId, displayName);

        return Ok(new OpenConversationResponse(conversationId, unreadCount));
    }
}
