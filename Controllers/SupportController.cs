using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/support")]
[Authorize(Roles = "Customer,Admin")]
public class SupportController : ControllerBase
{
    private readonly ISupportChatService _chatService;
    private readonly ILogger<SupportController> _logger;

    public SupportController(ISupportChatService chatService, ILogger<SupportController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost("conversation/open")]
    public async Task<ActionResult<OpenConversationResponse>> OpenConversation()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var displayName = User.FindFirst("display_name")?.Value ?? User.Identity?.Name ?? "Customer";

        _logger.LogInformation("User {UserId} opening conversation", userId);

        var conversationId = await _chatService.OpenConversationAsync(userId, displayName);

        return Ok(new OpenConversationResponse(conversationId));
    }
}
