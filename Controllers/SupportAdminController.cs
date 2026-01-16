using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class SupportAdminController : ControllerBase
{
    private readonly ISupportChatService _chatService;
    private readonly ILogger<SupportAdminController> _logger;

    public SupportAdminController(ISupportChatService chatService, ILogger<SupportAdminController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<ConversationListItemDto[]>> GetInbox()
    {
        _logger.LogInformation("Admin fetching inbox");

        var conversations = await _chatService.GetAdminInboxAsync();

        return Ok(conversations);
    }
}
