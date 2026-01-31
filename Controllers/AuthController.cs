using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    private readonly ILogger<AuthController> _logger;

    private const string RefreshCookieName = "refresh_token";
    private const string AccessTokenCookieName = "access_token";

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokenService,
        IEmailService emailService,
        IConfiguration config,
        ILogger<AuthController> logger
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        _logger.LogInformation(
            "Registration attempt for email: {Email} from IP: {IpAddress}",
            req.Email,
            GetClientIpAddress()
        );

        try
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
            {
                _logger.LogWarning(
                    "Registration failed for email: {Email}. Errors: {Errors}",
                    req.Email,
                    string.Join(", ", create.Errors.Select(e => e.Description))
                );
                return BadRequest(create.Errors);
            }

            await _userManager.AddToRoleAsync(user, "Customer");
            _logger.LogInformation(
                "User registered successfully. Email: {Email}, UserId: {UserId}",
                req.Email,
                user.Id
            );

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
                _logger.LogInformation("Confirmation email sent to: {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send confirmation email to: {Email}. Error: {ErrorMessage}",
                    user.Email,
                    ex.Message
                );
            }

            return Ok(
                new
                {
                    message = "Registration successful. Please check your email to confirm your account.",
                    emailConfirmationRequired = true,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during registration for email: {Email}. Error: {ErrorMessage}",
                req.Email,
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred during registration");
        }
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
    {
        _logger.LogInformation("Email confirmation attempt for UserId: {UserId}", req.UserId);

        try
        {
            var user = await _userManager.FindByIdAsync(req.UserId);
            if (user is null)
            {
                _logger.LogWarning(
                    "Email confirmation failed - User not found. UserId: {UserId}",
                    req.UserId
                );
                return BadRequest("User not found");
            }

            var result = await _userManager.ConfirmEmailAsync(user, req.Token);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Email confirmation failed for UserId: {UserId}. Errors: {Errors}",
                    req.UserId,
                    string.Join(", ", result.Errors.Select(e => e.Description))
                );
                return BadRequest("Email confirmation failed");
            }

            _logger.LogInformation(
                "Email confirmed successfully for UserId: {UserId}, Email: {Email}",
                user.Id,
                user.Email
            );
            return Ok(new { message = "Email confirmed successfully. You can now login." });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during email confirmation for UserId: {UserId}. Error: {ErrorMessage}",
                req.UserId,
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred");
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        _logger.LogInformation(
            "Password reset requested for email: {Email} from IP: {IpAddress}",
            req.Email,
            GetClientIpAddress()
        );

        try
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user is null)
            {
                _logger.LogWarning(
                    "Password reset requested for non-existent email: {Email}",
                    req.Email
                );
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
                _logger.LogInformation("Password reset email sent to: {Email}", user.Email);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Password reset email send failed: {ex.Message}"
                );
            }

            return Ok(
                new
                {
                    message = "If an account exists with this email, a password reset link will be sent.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during forgot password for email: {Email}. Error: {ErrorMessage}",
                req.Email,
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred");
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        _logger.LogInformation(
            "Password reset attempt for UserId: {UserId} from IP: {IpAddress}",
            req.UserId,
            GetClientIpAddress()
        );

        try
        {
            var user = await _userManager.FindByIdAsync(req.UserId);
            if (user is null)
            {
                _logger.LogWarning(
                    "Password reset failed - User not found. UserId: {UserId}",
                    req.UserId
                );
                return BadRequest("User not found");
            }

            var result = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Password reset failed for UserId: {UserId}, Email: {Email}. Errors: {Errors}",
                    user.Id,
                    user.Email,
                    string.Join(", ", result.Errors.Select(e => e.Description))
                );
                return BadRequest(
                    new { message = "Password reset failed", errors = result.Errors }
                );
            }

            _logger.LogInformation(
                "Password reset successfully for UserId: {UserId}, Email: {Email}",
                user.Id,
                user.Email
            );
            return Ok(
                new
                {
                    message = "Password reset successfully. You can now login with your new password.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during password reset for UserId: {UserId}. Error: {ErrorMessage}",
                req.UserId,
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred");
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var ipAddress = GetClientIpAddress();
        _logger.LogInformation(
            "Login attempt for email: {Email} from IP: {IpAddress}",
            req.Email,
            ipAddress
        );

        try
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user is null)
            {
                _logger.LogWarning(
                    "Login failed - User not found. Email: {Email} from IP: {IpAddress}",
                    req.Email,
                    ipAddress
                );
                return Unauthorized();
            }

            //TODO: Think if Email confirmation should be required (or can be deleted at all from register/login flow)
            if (!user.EmailConfirmed)
            {
                _logger.LogWarning(
                    "Login attempt with unconfirmed email. Email: {Email}, UserId: {UserId} from IP: {IpAddress}",
                    req.Email,
                    user.Id,
                    ipAddress
                );
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
            {
                if (res.IsLockedOut)
                {
                    _logger.LogCritical(
                        "Account locked due to too many failed login attempts. Email: {Email}, UserId: {UserId} from IP: {IpAddress}",
                        req.Email,
                        user.Id,
                        ipAddress
                    );
                    return StatusCode(
                        429,
                        new
                        {
                            message = "Account is locked due to too many failed login attempts. Please try again later.",
                        }
                    );
                }

                var failedAttempts = await _userManager.GetAccessFailedCountAsync(user);
                _logger.LogWarning(
                    "Failed login attempt. Email: {Email}, UserId: {UserId}, FailedAttempts: {FailedAttempts} from IP: {IpAddress}",
                    req.Email,
                    user.Id,
                    failedAttempts,
                    ipAddress
                );
                return Unauthorized();
            }

            _logger.LogInformation(
                "Login successful. Email: {Email}, UserId: {UserId} from IP: {IpAddress}",
                req.Email,
                user.Id,
                ipAddress
            );
            return await IssueTokensAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during login for email: {Email} from IP: {IpAddress}. Error: {ErrorMessage}",
                req.Email,
                ipAddress,
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred during login");
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req)
    {
        _logger.LogInformation("Token refresh attempt from IP: {IpAddress}", GetClientIpAddress());

        try
        {
            var refreshToken = Request.Cookies[RefreshCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning(
                    "Token refresh failed - No refresh token found in cookies from IP: {IpAddress}",
                    GetClientIpAddress()
                );
                return Unauthorized();
            }

            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwt;
            try
            {
                jwt = handler.ReadJwtToken(req.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Token refresh failed - Invalid access token from IP: {IpAddress}",
                    GetClientIpAddress()
                );
                return Unauthorized();
            }

            var userId = jwt.Subject;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning(
                    "Token refresh failed - No user ID in token from IP: {IpAddress}",
                    GetClientIpAddress()
                );
                return Unauthorized();
            }

            var refreshHash = _tokenService.HashRefreshToken(refreshToken);
            var stored = await _tokenService.GetActiveRefreshTokenAsync(userId, refreshHash);
            if (stored is null)
            {
                _logger.LogWarning(
                    "Token refresh failed - Invalid or expired refresh token for UserId: {UserId} from IP: {IpAddress}",
                    userId,
                    GetClientIpAddress()
                );
                return Unauthorized();
            }

            await _tokenService.RevokeRefreshTokenAsync(stored);

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                _logger.LogWarning(
                    "Token refresh failed - User not found. UserId: {UserId} from IP: {IpAddress}",
                    userId,
                    GetClientIpAddress()
                );
                return Unauthorized();
            }

            _logger.LogInformation(
                "Token refreshed successfully for UserId: {UserId} from IP: {IpAddress}",
                userId,
                GetClientIpAddress()
            );
            return await IssueTokensAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during token refresh from IP: {IpAddress}. Error: {ErrorMessage}",
                GetClientIpAddress(),
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred");
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        _logger.LogInformation(
            "Logout successful. UserId: {UserId}, Email: {Email} from IP: {IpAddress}",
            user?.Id,
            user?.Email,
            GetClientIpAddress()
        );

        Response.Cookies.Delete(RefreshCookieName);
        Response.Cookies.Delete(AccessTokenCookieName);
        return Ok("Logged out successfully");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> Me()
    {
        _logger.LogInformation("Me endpoint accessed from IP: {IpAddress}", GetClientIpAddress());

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                _logger.LogWarning("Me endpoint - User not found");
                return Unauthorized();
            }

            var roles = (await _userManager.GetRolesAsync(user)).ToArray();
            _logger.LogInformation(
                "Me endpoint - User retrieved. UserId: {UserId}, Email: {Email}, Roles: {Roles}, NameIdentifier: {NameIdentifier}",
                user.Id,
                user.Email,
                string.Join(", ", roles),
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );
            return new UserProfile(user.Id, user.Email ?? "", user.DisplayName ?? "", roles, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error in Me endpoint. Error: {ErrorMessage}",
                ex.Message
            );
            return StatusCode(500, "An unexpected error occurred");
        }
    }
    [Authorize]
    [HttpPatch("me")]
    public async Task<ActionResult<UserProfile>> UpdateUser([FromBody] UpdateUserRequest request)
    {
        _logger.LogInformation("UpdateUser endpoint accessed from IP: {IpAddress}", GetClientIpAddress());

        if (request is null)
        {
            _logger.LogWarning("UpdateUser endpoint - no user update request was found");

            return BadRequest();
        }
        try
        {
            var user = await _userManager.GetUserAsync(User);

            if (user is null) {
                _logger.LogWarning("UpdateUser endpoint - User not found");
                return Unauthorized();
            }
            // Store original values for rollback in case of validation failures
            var originalPhoneNumber = user.PhoneNumber;
            var originalEmail = user.Email;
            var originalEmailConfirmed = user.EmailConfirmed;
            var phoneNumberChanged = false;
            var emailChanged = false;
            
            try
            {
                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    user.DisplayName = request.DisplayName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    var setPhone = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber.Trim());
                    if (!setPhone.Succeeded)
                    {
                        return BadRequest(setPhone.Errors);
                    }
                    phoneNumberChanged = true;
                }

                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    var newEmail = request.Email.Trim();

                    // Only process if the email actually changed
                    if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        emailChanged = true;
                        var setEmail = await _userManager.SetEmailAsync(user, newEmail);
                        if (!setEmail.Succeeded)
                        {
                            // Rollback phone number change if it was made
                            await RollbackUserChangesAsync(user, phoneNumberChanged, false, originalPhoneNumber, originalEmail, originalEmailConfirmed);
                            return BadRequest(setEmail.Errors);
                        }

                        // Mark email as unconfirmed until the new address is verified
                        user.EmailConfirmed = false;

                        // Generate email confirmation token and send confirmation email
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var encodedToken = Uri.EscapeDataString(token);
                        var baseUrl = _config["EmailConfirmation:BaseUrl"] ?? "http://localhost:5173";
                        var confirmationLink = $"{baseUrl}/confirm-email?userId={user.Id}&token={encodedToken}";

                        try
                        {
                            await _emailService.SendConfirmationEmailAsync(newEmail, confirmationLink);
                            _logger.LogInformation("Email confirmation sent to new address: {Email}", newEmail);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Failed to send confirmation email to: {Email}. Error: {ErrorMessage}",
                                newEmail,
                                ex.Message
                            );
                            // Continue even if email sending fails - user can request a new confirmation email
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception before attempting rollback
                _logger.LogError(
                    ex,
                    "Exception occurred during user update for {UserId}. Attempting to rollback changes. Error: {ErrorMessage}",
                    user.Id,
                    ex.Message
                );

                // Rollback all changes on any exception
                await RollbackUserChangesAsync(user, phoneNumberChanged, emailChanged, originalPhoneNumber, originalEmail, originalEmailConfirmed);
                throw;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(updateResult.Errors);

            return Ok(new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.PhoneNumber
            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateUser failed");
            return StatusCode(500, "An unexpected error occurred.");
        }
    }
    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error issuing tokens for UserId: {UserId}. Error: {ErrorMessage}",
                user.Id,
                ex.Message
            );
            throw;
        }
    }

    private string GetClientIpAddress()
    {
        if (Request.Headers.ContainsKey("X-Forwarded-For"))
            return Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    private async Task RollbackUserChangesAsync(
        ApplicationUser user,
        bool phoneNumberChanged,
        bool emailChanged,
        string? originalPhoneNumber,
        string? originalEmail,
        bool originalEmailConfirmed)
    {
        if (phoneNumberChanged)
        {
            try
            {
                await _userManager.SetPhoneNumberAsync(user, originalPhoneNumber);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback phone number change for user {UserId}. Original: {OriginalPhone}",
                    user.Id,
                    originalPhoneNumber
                );
            }
        }

        if (emailChanged)
        {
            try
            {
                await _userManager.SetEmailAsync(user, originalEmail);
                user.EmailConfirmed = originalEmailConfirmed;
                await _userManager.UpdateAsync(user);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback email change for user {UserId}. Original: {OriginalEmail}",
                    user.Id,
                    originalEmail
                );
            }
        }
    }
}
