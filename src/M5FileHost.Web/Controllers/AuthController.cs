using System.ComponentModel.DataAnnotations;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace M5FileHost.Web.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext database, IConfiguration configuration, IAppEmailSender emailSender, ILogger<AuthController> logger) : ControllerBase
{
    // Issues a fresh antiforgery token bound to the current identity. The SPA
    // calls this after login/register/logout because tokens minted for the
    // anonymous user are rejected once the user is authenticated (and vice versa).
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) =>
        Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("register"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!configuration.GetValue("Features:EnableRegistration", true)) return BadRequest(new { message = "Registration is currently closed." });
        var username = request.Username.Trim();
        // The SPA register form collects no email, but Identity requires a unique
        // one, so we synthesize a non-deliverable address from the unique username.
        var email = $"{username.ToLowerInvariant()}@users.noreply.local";
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = username, DisplayName = username };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        await signIn.SignInAsync(user, false);
        return NoContent();
    }

    [HttpPost("login"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var login = request.Username.Trim();
        var user = await users.FindByEmailAsync(login) ?? await users.FindByNameAsync(login);
        if (user is null || user.IsBanned) return BadRequest(new { message = "Invalid credentials or this account is unavailable." });
        var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, true);
        if (!result.Succeeded) return BadRequest(new { message = result.IsLockedOut ? "Too many attempts. Try again later." : "Invalid credentials." });
        return Ok(await BuildUserDto(user));
    }

    [HttpPost("logout"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return NoContent(); }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await BuildUserDto(user));
    }

    [HttpPost("forgot-password"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.IsBanned)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var appUrl = configuration["AppUrl"] ?? "https://files.index.sarl";
            var resetUrl = QueryHelpers.AddQueryString($"{appUrl.TrimEnd('/')}/reset-password", new Dictionary<string, string?> { ["userId"] = user.Id.ToString(), ["token"] = token });
            try { await emailSender.SendPasswordResetAsync(user, resetUrl, HttpContext.RequestAborted); }
            catch (Exception exception) { logger.LogError(exception, "Could not send password reset email for user {UserId}", user.Id); }
        }
        return NoContent();
    }

    [HttpPost("reset-password"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsBanned) return BadRequest(new { message = "The reset link is invalid or expired." });
        var result = await users.ResetPasswordAsync(user, request.Token, request.Password);
        return result.Succeeded ? NoContent() : BadRequest(new { message = "The reset link is invalid or expired." });
    }

    private Task<UserDto> BuildUserDto(ApplicationUser user) => ApiMap.ToUserDtoAsync(database, user, HttpContext.RequestAborted);
}

public sealed record RegisterRequest([Required, RegularExpression("^[a-zA-Z0-9_]{3,32}$")] string Username, [Required, MinLength(12), MaxLength(128)] string Password);
public sealed record LoginRequest([Required, MaxLength(254)] string Username, [Required, MaxLength(128)] string Password, bool RememberMe = false);
public sealed record ForgotPasswordRequest([Required, EmailAddress, MaxLength(254)] string Email);
public sealed record ResetPasswordRequest([Required] Guid UserId, [Required] string Token, [Required, MinLength(12), MaxLength(128)] string Password);
