using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    private const string RefreshCookieName = "refresh_token";
    private const string AccessTokenCookieName = "access_token";

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokenService,
        IEmailService emailService,
        IConfiguration config
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        var user = new ApplicationUser
        {
            Email = req.Email,
            UserName = req.Email,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName)
                ? req.Email.Split('@')[0]
                : req.DisplayName,
            EmailConfirmed = false,
        };

        var create = await _userManager.CreateAsync(user, req.Password);
        if (!create.Succeeded)
            return BadRequest(create.Errors);

        await _userManager.AddToRoleAsync(user, "Customer");

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        //TODO: Move to config class and DI it.
        var baseUrl = _config["EmailConfirmation:BaseUrl"] ?? "http://localhost:5173";
        //TODO: Change to react correct link later.
        var confirmationLink = $"{baseUrl}/confirm-email?userId={user.Id}&token={encodedToken}";

        try
        {
            //TODO: Think if Email confirmation should be required (or can be deleted at all from register/login flow)
            await _emailService.SendConfirmationEmailAsync(user.Email!, confirmationLink);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Email send failed: {ex.Message}");
        }

        return Ok(
            new
            {
                message = "Registration successful. Please check your email to confirm your account.",
                emailConfirmationRequired = true,
            }
        );
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user is null)
            return BadRequest("User not found");

        var result = await _userManager.ConfirmEmailAsync(user, req.Token);
        if (!result.Succeeded)
            return BadRequest("Email confirmation failed");

        return Ok(new { message = "Email confirmed successfully. You can now login." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null)
        {
            return Ok(
                new
                {
                    message = "If an account exists with this email, a password reset link will be sent.",
                }
            );
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        //TODO: Move to config class and DI it.
        var baseUrl = _config["EmailConfirmation:BaseUrl"] ?? "http://localhost:5173";
        //TODO: Change to react correct link later.
        var resetLink = $"{baseUrl}/reset-password?userId={user.Id}&token={encodedToken}";

        try
        {
            await _emailService.SendPasswordResetEmailAsync(user.Email!, resetLink);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Password reset email send failed: {ex.Message}");
        }

        return Ok(
            new
            {
                message = "If an account exists with this email, a password reset link will be sent.",
            }
        );
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user is null)
            return BadRequest("User not found");

        var result = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = "Password reset failed", errors = result.Errors });

        return Ok(
            new
            {
                message = "Password reset successfully. You can now login with your new password.",
            }
        );
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized();

        //TODO: Think if Email confirmation should be required (or can be deleted at all from register/login flow)
        if (!user.EmailConfirmed)
        {
            return BadRequest(
                new
                {
                    message = "Please confirm your email before logging in.",
                    emailConfirmationRequired = true,
                }
            );
        }

        var res = await _signInManager.CheckPasswordSignInAsync(
            user,
            req.Password,
            lockoutOnFailure: true
        );
        if (!res.Succeeded)
            return Unauthorized();

        return await IssueTokensAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized();

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(req.AccessToken);
        }
        catch
        {
            return Unauthorized();
        }

        var userId = jwt.Subject;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var refreshHash = _tokenService.HashRefreshToken(refreshToken);
        var stored = await _tokenService.GetActiveRefreshTokenAsync(userId, refreshHash);
        if (stored is null)
            return Unauthorized();

        await _tokenService.RevokeRefreshTokenAsync(stored);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        return await IssueTokensAsync(user);
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(RefreshCookieName);
        Response.Cookies.Delete(AccessTokenCookieName);
        return Ok("Logged out succesfully");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        return new UserProfile(user.Id, user.Email ?? "", user.DisplayName ?? "", roles);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var (accessToken, expiresAt) = await _tokenService.CreateAccessTokenAsync(user);

        var (refreshToken, refreshHash, refreshExpires) = _tokenService.CreateRefreshToken();
        await _tokenService.StoreRefreshTokenAsync(user.Id, refreshHash, refreshExpires);

        Response.Cookies.Append(
            RefreshCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = refreshExpires.UtcDateTime,
            }
        );

        Response.Cookies.Append(
            AccessTokenCookieName,
            accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = expiresAt.UtcDateTime,
            }
        );

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        var profile = new UserProfile(user.Id, user.Email ?? "", user.DisplayName ?? "", roles);

        var expiresIn = (int)Math.Max(0, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds);
        return new AuthResponse(accessToken, expiresIn, profile);
    }
}
