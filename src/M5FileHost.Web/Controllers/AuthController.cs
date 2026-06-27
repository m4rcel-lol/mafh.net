using System.ComponentModel.DataAnnotations;
using M5FileHost.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace M5FileHost.Web.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IConfiguration configuration, IAppEmailSender emailSender, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromForm] RegisterRequest request)
    {
        if (!configuration.GetValue("Features:EnableRegistration", true)) return RedirectWithError("/register", "Registration is currently closed.");
        if (!ModelState.IsValid) return RedirectWithError("/register", "Check the highlighted account details.");
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = request.Email.Trim(), UserName = request.Username.Trim(), DisplayName = request.Username.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return RedirectWithError("/register", string.Join(" ", result.Errors.Select(x => x.Description)));
        await signIn.SignInAsync(user, false);
        return LocalRedirect("/dashboard");
    }

    [HttpPost("login"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Login.Trim()) ?? await users.FindByNameAsync(request.Login.Trim());
        if (user is null || user.IsBanned) return RedirectWithError("/login", "Invalid credentials or this account is unavailable.");
        var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, true);
        if (!result.Succeeded) return RedirectWithError("/login", result.IsLockedOut ? "Too many attempts. Try again later." : "Invalid credentials.");
        return LocalRedirect(IsLocalReturnUrl(request.ReturnUrl) ? request.ReturnUrl! : "/dashboard");
    }

    [HttpPost("logout"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return LocalRedirect("/"); }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(new { user.Id, user.UserName, user.Email, user.DisplayName, role = user.Role.ToString(), user.NsfwAllowed });
    }

    [HttpPost("forgot-password"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromForm, EmailAddress] string email)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null && !user.IsBanned)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var appUrl = configuration["AppUrl"] ?? "https://files.index.sarl";
            var resetUrl = QueryHelpers.AddQueryString($"{appUrl.TrimEnd('/')}/reset-password", new Dictionary<string, string?> { ["userId"] = user.Id.ToString(), ["token"] = token });
            try { await emailSender.SendPasswordResetAsync(user, resetUrl, HttpContext.RequestAborted); }
            catch (Exception exception) { logger.LogError(exception, "Could not send password reset email for user {UserId}", user.Id); }
        }
        return LocalRedirect("/forgot-password?sent=1");
    }

    [HttpPost("reset-password"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromForm] Guid userId, [FromForm, Required] string token, [FromForm, Required, MinLength(12), MaxLength(128)] string password)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.IsBanned) return RedirectWithError("/forgot-password", "The reset link is invalid or expired.");
        var result = await users.ResetPasswordAsync(user, token, password);
        return result.Succeeded ? LocalRedirect("/login?reset=1") : RedirectWithError("/forgot-password", "The reset link is invalid or expired.");
    }

    private IActionResult RedirectWithError(string path, string error) => LocalRedirect($"{path}?error={Uri.EscapeDataString(error)}");
    private bool IsLocalReturnUrl(string? url) => !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url);
}

public sealed record RegisterRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, RegularExpression("^[a-zA-Z0-9_]{3,32}$")] string Username, [Required, MinLength(12), MaxLength(128)] string Password);
public sealed record LoginRequest([Required, MaxLength(254)] string Login, [Required, MaxLength(128)] string Password, bool RememberMe = false, string? ReturnUrl = null);
